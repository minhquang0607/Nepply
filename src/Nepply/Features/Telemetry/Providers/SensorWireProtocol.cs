using System.Buffers.Binary;
using System.IO;
using System.Text.Json;

namespace Nepply.Services.Telemetry;

internal static class SensorWireProtocol
{
    public const int RequestLimit = 8192;
    public const int ResponseLimit = 1024 * 1024;
    public static async Task WriteAsync<T>(Stream stream, T value, int limit, CancellationToken token)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(value);
        if (payload.Length > limit) throw new InvalidDataException("Sensor message exceeds size limit.");
        var header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, token).ConfigureAwait(false);
        await stream.WriteAsync(payload, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }
    public static async Task<T?> ReadAsync<T>(Stream stream, int limit, CancellationToken token) where T : class
    {
        var header = new byte[4];
        int received = await stream.ReadAsync(header.AsMemory(0, 1), token).ConfigureAwait(false);
        if (received == 0) return null; // Clean shutdown between frames.
        await stream.ReadExactlyAsync(header.AsMemory(1), token).ConfigureAwait(false);
        int size = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (size < 1 || size > limit) throw new InvalidDataException("Invalid sensor message length.");
        var payload = new byte[size];
        await stream.ReadExactlyAsync(payload, token).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(payload) ?? throw new InvalidDataException("Empty sensor message.");
    }
}
