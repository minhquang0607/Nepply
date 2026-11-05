using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Windows.Media.Imaging;
using Nepply.Models;

namespace Nepply.Services;

public static class DesktopScanProcess
{
    public sealed record Entry(string Name, string Path, bool IsSystemIcon, byte[]? Png, string[]? SourcePaths = null, string? GroupIdentity = null);
    public sealed record Reply(int Version, Entry[] Entries, string? Failure = null);
    internal sealed record Request(int Version, string[]? Paths);
    internal const int MaximumRequestBytes = 1024 * 1024;
    public static async Task<List<DesktopIcon>> ReadAsync(string executable, bool hidden, bool system, CancellationToken shutdown, bool merge = true, string[]? paths = null, bool raw = false)
    {
        shutdown.ThrowIfCancellationRequested();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        var name = "Nepply-desktop-" + Guid.NewGuid().ToString("N");
        using var pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var arg in new[] { "--desktop-host", name, hidden ? "1" : "0", system ? "1" : "0" }) start.ArgumentList.Add(arg);
        start.ArgumentList.Add(merge ? "1" : "0");
        start.ArgumentList.Add(raw ? "raw" : "grouped");
        start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var request = EncodeRequest(paths);
        timeout.Token.ThrowIfCancellationRequested();
        using var child = Process.Start(start) ?? throw new IOException("Cannot start Desktop reader.");
        try
        {
            await pipe.WaitForConnectionAsync(timeout.Token).ConfigureAwait(false);
            Helpers.HelperPipeIdentity.RequireClient(pipe, child.Id);
            var requestHeader = new byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(requestHeader, request.Length);
            await pipe.WriteAsync(requestHeader, timeout.Token).ConfigureAwait(false);
            await pipe.WriteAsync(request, timeout.Token).ConfigureAwait(false);
            var header = new byte[4]; await pipe.ReadExactlyAsync(header, timeout.Token).ConfigureAwait(false);
            int size = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(header);
            if (size is < 1 or > 32 * 1024 * 1024) throw new InvalidDataException("Desktop response exceeds limit.");
            var bytes = new byte[size]; await pipe.ReadExactlyAsync(bytes, timeout.Token).ConfigureAwait(false);
            var result = DecodeReply(bytes, timeout.Token);
            if (raw && result.Any(item => string.IsNullOrEmpty(item.GroupIdentity)))
                throw new InvalidDataException("Raw Desktop reply lacks grouping identity.");
            return result;
        }
        finally
        {
            try { if (!child.HasExited && !child.WaitForExit(200)) child.Kill(); }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            { AppLogger.Error(ex, "Stop Desktop reader"); }
        }
    }
    internal static List<DesktopIcon> DecodeReply(byte[] bytes, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        if (bytes.Length is < 1 or > 32 * 1024 * 1024) throw new InvalidDataException("Desktop response exceeds limit.");
        var reply = JsonSerializer.Deserialize<Reply>(bytes) ?? throw new InvalidDataException("Empty Desktop response.");
        if (reply.Version != 1 || reply.Entries == null || reply.Entries.Length > DesktopImageBudget.MaximumEntries
            || reply.Entries.Any(item => item == null || string.IsNullOrWhiteSpace(item.Path) || item.Path.Length > 32768 || item.Name == null || item.Name.Length > 32768))
            throw new InvalidDataException("Unsupported or incomplete Desktop response.");
        if (reply.Failure != null)
        {
            if (reply.Failure.Length is < 1 or > AppLogger.MaximumEntryCharacters || reply.Entries.Length != 0)
                throw new InvalidDataException("Invalid Desktop failure response.");
            throw new IOException("Desktop reader failed: " + reply.Failure);
        }
        var icons = new List<DesktopIcon>(reply.Entries.Length);
        long decodedBytes = 0;
        foreach (var item in reply.Entries)
        {
            cancellation.ThrowIfCancellationRequested();
            if (item.SourcePaths is { } sourcePaths && (sourcePaths.Length > 10000 || sourcePaths.Any(p => string.IsNullOrWhiteSpace(p) || p.Length > 32768)))
                throw new InvalidDataException("Invalid grouped Desktop paths.");
            BitmapImage? icon = null;
            if (item.Png is { Length: > 0 })
            {
                try
                {
                    long pixels = DesktopImageBudget.ValidatePng(item.Png);
                    if (decodedBytes > DesktopImageBudget.MaximumDecodedBytes - pixels)
                        throw new InvalidDataException("Desktop icons exceed the aggregate pixel budget.");
                    decodedBytes += pixels;
                    using var data = new MemoryStream(item.Png);
                    icon = new BitmapImage(); icon.BeginInit(); icon.CacheOption = BitmapCacheOption.OnLoad; icon.StreamSource = data; icon.EndInit(); icon.Freeze();
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException)
                {
                    // A thumbnail is decoration, not the identity of a Desktop
                    // item. Keep every item even when its image cannot decode.
                    icon = null;
                    AppLogger.Error(ex, "Decode Desktop thumbnail");
                }
            }
            if (item.SourcePaths is { } paths && (paths.Length > 10000 || paths.Any(string.IsNullOrWhiteSpace)))
                throw new InvalidDataException("Invalid grouped Desktop paths.");
            if (item.GroupIdentity is { Length: > 256 }) throw new InvalidDataException("Desktop grouping identity exceeds limit.");
            icons.Add(new DesktopIcon { Name = item.Name, Path = item.Path, IsSystemIcon = item.IsSystemIcon, Icon = icon, SourcePaths = item.SourcePaths ?? [item.Path], GroupIdentity = item.GroupIdentity });
        }
        return icons;
    }
    internal static byte[] EncodeRequest(string[]? paths)
    {
        if (paths is { Length: > Modules.Desktop.DesktopChangeBatch.MaximumPaths }
            || paths?.Any(p => !Modules.Desktop.DesktopChangeBatch.TryNormalize(p, out _)) == true
            || paths?.Sum(p => (long)p.Length) > Modules.Desktop.DesktopChangeBatch.MaximumPathCharacters)
            throw new InvalidDataException("Invalid partial Desktop request.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new Request(1, paths));
        if (bytes.Length > MaximumRequestBytes) throw new InvalidDataException("Desktop request exceeds limit.");
        return bytes;
    }
    internal static string[]? DecodeRequest(byte[] bytes)
    {
        if (bytes.Length is < 1 or > MaximumRequestBytes) throw new InvalidDataException("Desktop request exceeds limit.");
        var request = JsonSerializer.Deserialize<Request>(bytes) ?? throw new InvalidDataException("Missing Desktop request.");
        if (request.Version != 1) throw new InvalidDataException("Unsupported Desktop request.");
        _ = EncodeRequest(request.Paths);
        return request.Paths;
    }
    public static void RunHost(string name, bool hidden, bool system, bool merge, bool raw, int parentId)
    {
        using var parent = Process.GetProcessById(parentId);
        using var watchdog = new Telemetry.ParentExitSubscription(parent, () => Environment.Exit(0));
        // Bounds native Shell calls even if the parent disappears while an extension is blocked.
        using var deadline = new Timer(_ => Environment.Exit(2), null, 30000, Timeout.Infinite);
        using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
        pipe.Connect(10000);
        Helpers.HelperPipeIdentity.RequireServer(pipe, parent);
        byte[] bytes;
        var issues = new DesktopScanIssues();
        try
        {
            string[]? paths = null;
            {
                Span<byte> requestHeader = stackalloc byte[4]; pipe.ReadExactly(requestHeader);
                int length = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(requestHeader);
                if (length is < 1 or > MaximumRequestBytes) throw new InvalidDataException("Desktop request exceeds limit.");
                var request = new byte[length]; pipe.ReadExactly(request); paths = DecodeRequest(request);
            }
            if (!raw && paths != null) throw new InvalidDataException("Grouped request cannot be partial.");
            var entries = DesktopEntryReader.ReadNativeAsync(new(hidden, system, merge), paths, issues).GetAwaiter().GetResult();
            bytes = EncodeReply(raw ? entries : Modules.Desktop.DesktopRawCatalog.Group(entries, merge));
        }
        catch (Exception ex) { bytes = EncodeFailure(ex.ToString()); }
        // The parent may stop this process as soon as it has the reply; skipped entries are only in this log.
        if (issues.Count > 0) AppLogger.FlushAndShutdown();
        Span<byte> header = stackalloc byte[4]; System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        pipe.Write(header); pipe.Write(bytes); pipe.Flush();
    }
    internal static byte[] EncodeFailure(string diagnostic)
        => JsonSerializer.SerializeToUtf8Bytes(new Reply(1, [], AppLogger.LimitText(diagnostic)));
    internal static byte[] EncodeReply(IEnumerable<DesktopIcon> icons)
    {
        using var output = new LimitedMemoryStream(DesktopImageBudget.MaximumReplyBytes);
        using var writer = new Utf8JsonWriter(output);
        writer.WriteStartObject(); writer.WriteNumber("Version", 1); writer.WriteStartArray("Entries");
        long pixelBytes = 0;
        int count = 0;
        foreach (var item in icons)
        {
            if (++count > DesktopImageBudget.MaximumEntries) throw new InvalidDataException("Too many Desktop entries.");
            if (item.Name.Length > 32768 || item.Path.Length > 32768 || item.SourcePaths.Length > 10000
                || item.SourcePaths.Any(p => p.Length > 32768)) throw new InvalidDataException("Desktop metadata exceeds its limit.");
            long metadataCharacters = (long)item.Name.Length + item.Path.Length;
            foreach (var sourcePath in item.SourcePaths) metadataCharacters += sourcePath.Length;
            if (metadataCharacters > 65536) throw new InvalidDataException("Desktop entry metadata exceeds its staging budget.");
            byte[]? png = null;
            if (item.Icon is BitmapSource bitmap)
            {
                long pixels = (long)bitmap.PixelWidth * bitmap.PixelHeight * 4;
                if (bitmap.PixelWidth is > 0 and <= DesktopImageBudget.MaximumDimension
                    && bitmap.PixelHeight is > 0 and <= DesktopImageBudget.MaximumDimension
                    && pixelBytes <= DesktopImageBudget.MaximumDecodedBytes - pixels)
                {
                    try
                    {
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var image = new LimitedMemoryStream(DesktopImageBudget.MaximumPngBytes);
                        encoder.Save(image); png = image.ToArray();
                        DesktopImageBudget.ValidatePng(png); pixelBytes += pixels;
                    }
                    catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException)
                    { png = null; AppLogger.Error(ex, "Encode Desktop thumbnail"); }
                }
            }
            if (item.GroupIdentity is { Length: > 256 }) throw new InvalidDataException("Desktop grouping identity exceeds limit.");
            JsonSerializer.Serialize(writer, new Entry(item.Name, item.Path, item.IsSystemIcon, png, item.SourcePaths, item.GroupIdentity));
            writer.Flush(); // Bound writer staging to one entry rather than the whole Desktop.
        }
        writer.WriteEndArray(); writer.WriteEndObject(); writer.Flush();
        return output.ToArray();
    }
}
