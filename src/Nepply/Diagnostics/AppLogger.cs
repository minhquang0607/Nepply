using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Nepply.Services;

public static class AppLogger
{
    private const long MaxFileSize = 5 * 1024 * 1024; // 5MB
    private const int MaxBackups = 3;
    internal const int MaximumEntryCharacters = 16384;
    private static long _droppedEntries;
    internal static long DroppedEntries => Interlocked.Read(ref _droppedEntries);
    internal static string LimitText(string value) => value.Length <= MaximumEntryCharacters
        ? value : value[..(MaximumEntryCharacters - 24)] + "\n[diagnostic truncated]";

    public static string LogDirectory { get; } = ResolveLogDirectory(
        AppContext.GetData(LogDirectorySwitch) as string, Environment.GetCommandLineArgs());

    /// <summary>
    /// Set only by the test project's runtimeconfig, so tests never write into the user's
    /// logs. Nepply's own runtimeconfig never sets it.
    /// </summary>
    internal const string LogDirectorySwitch = "Nepply.LogDirectory";
    internal static readonly string[] PreviewArguments = ["--preview", "--preview-island", "--smoke-test"];

    internal static string ResolveLogDirectory(string? configured, IReadOnlyList<string> args)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(configured, AppContext.BaseDirectory);
        // Preview and smoke runs show fixture data; keep their logs apart from real use.
        if (args.Any(arg => PreviewArguments.Contains(arg, StringComparer.Ordinal)))
            return Path.Combine(Path.GetTempPath(), "Nepply-preview", "Logs");
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nepply", "Logs");
    }

    private sealed record LogEntry(DateTimeOffset Timestamp, string Level, string Message, Exception? Error);
    private static readonly Channel<LogEntry> _channel;
    private static readonly Task _processTask;
    private static long _currentFileSize = 0;
    private static readonly string _mainLogFile;
    internal static string LogFile => _mainLogFile;

    static AppLogger()
    {
        var args = Environment.GetCommandLineArgs();
        var role = args.Contains("--desktop-host") ? "desktop" : args.Contains("--sensor-host") ? "sensors" : "ui";
        _mainLogFile = Path.Combine(LogDirectory, "nepply-" + role + ".log");

        // Bound retained diagnostics; formatting stack traces belongs to the consumer.
        _channel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(512)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        }, _ => Interlocked.Increment(ref _droppedEntries));

        // Initialize file size tracking
        try
        {
            Directory.CreateDirectory(LogDirectory);
            if (File.Exists(_mainLogFile))
            {
                _currentFileSize = new FileInfo(_mainLogFile).Length;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Nepply AppLogger initialization failed: {ex}");
        }

        // Start background processor
        _processTask = Task.Run(ProcessLogsAsync);
    }

    public static void Info(string message) => Enqueue("INFO", message, null);
    public static void Warn(string message) => Enqueue("WARN", message, null);
    public static void Error(Exception? exception, string message) => Enqueue("ERROR", message, exception);

    private static void Enqueue(string level, string message, Exception? exception)
    {
        try
        {
            if (!_channel.Writer.TryWrite(new(DateTimeOffset.Now, level, LimitText(message), exception)))
                Interlocked.Increment(ref _droppedEntries);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Nepply logging failed to enqueue: {ex}");
        }
    }

    private static async Task ProcessLogsAsync()
    {
        try
        {
            // Read until the channel is completed
            await foreach (var entry in _channel.Reader.ReadAllAsync())
            {
                try
                {
                    var line = $"[{entry.Timestamp:O}] {entry.Level}: {entry.Message}";
                    if (entry.Error != null) line += Environment.NewLine + entry.Error;
                    WriteToFile(LimitText(line) + Environment.NewLine + Environment.NewLine);
                }
                catch (Exception ex) { Debug.WriteLine($"Nepply log entry failed: {ex.GetType().Name}"); }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Nepply log processor crashed: {ex}");
        }
    }

    private static void WriteToFile(string line)
    {
        try
        {
            var lineBytes = Encoding.UTF8.GetByteCount(line);

            // Check if rotation is needed
            if (_currentFileSize + lineBytes > MaxFileSize)
            {
                if (!RotateLogs()) { Interlocked.Increment(ref _droppedEntries); return; }
                _currentFileSize = 0;
            }

            File.AppendAllText(_mainLogFile, line);
            _currentFileSize += lineBytes;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Nepply log write failed: {ex}");
        }
    }

    private static bool RotateLogs()
    {
        try
        {
            if (!File.Exists(_mainLogFile)) return true;

            for (int i = MaxBackups - 1; i >= 1; i--)
            {
                var current = _mainLogFile + $".{i}";
                var next = _mainLogFile + $".{i + 1}";
                if (File.Exists(current))
                {
                    File.Move(current, next, overwrite: true);
                }
            }

            File.Move(_mainLogFile, _mainLogFile + ".1", overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Nepply log rotation failed: {ex}");
            return false;
        }
    }

    /// <summary>
    /// Must be called during Application.Exit to ensure all pending logs are written.
    /// </summary>
    public static void FlushAndShutdown()
    {
        try
        {
            // Signal that no more items will be added
            _channel.Writer.TryComplete();

            // Wait for the background task to finish writing the queue
            if (!_processTask.IsCompleted)
            {
                _processTask.Wait(TimeSpan.FromSeconds(2)); // Max 2 seconds timeout
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Nepply AppLogger shutdown failed: {ex}");
        }
    }
}
