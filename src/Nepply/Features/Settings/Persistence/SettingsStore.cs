using System.IO;
using System.Text;
using System.Text.Json;
using Nepply.Models;

namespace Nepply.Services;

public sealed class SettingsStore(string path, string? legacyPath = null)
{
    public const int CurrentSchema = 1;
    internal const int MaximumBytes = 2 * 1024 * 1024;
    private readonly object _gate = new();
    private bool _readOnly;
    public UserMessage? RecoveryNotice { get; private set; }
    private static AppSettings Read(string file)
    {
        using var stream = new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read);
        if (stream.Length > MaximumBytes) throw new InvalidDataException("Settings file exceeds size limit.");
        using var document = JsonDocument.Parse(stream);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("Settings must be an object.");
        // Inspect the envelope before decoding fields whose types may change in future versions.
        if (document.RootElement.TryGetProperty(nameof(AppSettings.SchemaVersion),out var schema))
        {
            if (schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt64(out var version) || version < 0)
                throw new JsonException("Invalid settings schema.");
            if (version > CurrentSchema) throw new NotSupportedException("Settings belong to a newer Nepply version.");
        }
        var value = document.RootElement.Deserialize<AppSettings>() ?? throw new JsonException("Empty settings.");
        if (value.SchemaVersion > CurrentSchema) throw new NotSupportedException("Settings belong to a newer Nepply version.");
        // Existing installations keep their choices without a new first-run prompt.
        if (!document.RootElement.TryGetProperty(nameof(AppSettings.HasCompletedOnboarding), out _))
            value.HasCompletedOnboarding = true;
        value.Validate(); value.SchemaVersion = CurrentSchema;
        return value;
    }
    public AppSettings Load()
    {
        lock (_gate)
        {
            RecoveryNotice = null;
            // Import only on first use. An existing Nepply file or backup always wins,
            // including a damaged or future-schema file; never resurrect stale settings.
            bool mayImport = !File.Exists(path) && !File.Exists(path + ".bak");
            try { if (File.Exists(path)) return Read(path); }
            catch (NotSupportedException)
            {
                _readOnly = true;
                RecoveryNotice = UserMessage.FromKey("RecoveryNewer");
                return new();
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException)
            {
                AppLogger.Error(ex, "Read settings");
                RecoveryNotice = UserMessage.FromKey("RecoveryDamaged");
                try { File.Move(path, path + ".broken-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N")); }
                catch (Exception move) when (move is IOException or UnauthorizedAccessException) { _readOnly = true; AppLogger.Error(move,"Preserve damaged settings"); }
            }
            try
            {
                if (File.Exists(path + ".bak"))
                {
                    var recovered = Read(path + ".bak");
                    RecoveryNotice = UserMessage.FromKey("RecoveryRestored");
                    return recovered;
                }
            }
            catch (NotSupportedException)
            {
                _readOnly = true;
                RecoveryNotice = UserMessage.FromKey("RecoveryBackupNewer");
                return new();
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException)
            { AppLogger.Error(ex,"Read backup settings"); RecoveryNotice = UserMessage.FromKey("RecoveryUnreadable"); }
            if (mayImport && !_readOnly && legacyPath != null)
                return ImportLegacy(legacyPath);
            return new();
        }
    }
    private AppSettings ImportLegacy(string source)
    {
        foreach (var candidate in new[] { source, source + ".bak" })
        {
            if (!File.Exists(candidate)) continue;
            AppSettings imported;
            try { imported = Read(candidate); }
            catch (NotSupportedException)
            {
                _readOnly = true;
                RecoveryNotice = UserMessage.FromKey("RecoveryLegacyNewer");
                return new();
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException)
            {
                AppLogger.Error(ex, "Read settings before product rename");
                RecoveryNotice = UserMessage.FromKey("RecoveryLegacyUnreadable");
                continue;
            }
            try
            {
                Save(imported);
                RecoveryNotice = UserMessage.FromKey("RecoveryLegacyImported");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                AppLogger.Error(ex, "Import settings after product rename");
                _readOnly = true;
                RecoveryNotice = UserMessage.FromKey("RecoveryLegacyUnsaved");
            }
            return imported;
        }
        return new();
    }
    public void Save(AppSettings value)
    {
        lock (_gate)
        {
            if (_readOnly) throw new IOException("Không thể ghi đè cấu hình đang được bảo vệ. Kiểm tra phiên bản và quyền truy cập.");
            value.Validate(); value.SchemaVersion = CurrentSchema;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
                if (bytes.Length > MaximumBytes) throw new InvalidDataException("Cấu hình vượt giới hạn 2 MiB; chưa ghi đè bản đang dùng.");
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary,path,path + ".bak");
                else File.Move(temporary,path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
