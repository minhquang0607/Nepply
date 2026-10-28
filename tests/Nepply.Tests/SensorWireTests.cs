using System.IO;
using System.Buffers.Binary;
using Nepply.Services.Telemetry;

internal static class SensorWireTests
{
    public static void Run(Action<string, bool> check)
    {
        using var stream = new MemoryStream();
        SensorWireProtocol.WriteAsync(stream, new SensorProcessProvider.Request(1, "GPU · thử nghiệm"), 8192, default).GetAwaiter().GetResult();
        SensorWireProtocol.WriteAsync(stream, new SensorProcessProvider.Request(1, "second"), 8192, default).GetAwaiter().GetResult();
        stream.Position = 0;
        var first = SensorWireProtocol.ReadAsync<SensorProcessProvider.Request>(stream, 8192, default).GetAwaiter().GetResult();
        var second = SensorWireProtocol.ReadAsync<SensorProcessProvider.Request>(stream, 8192, default).GetAwaiter().GetResult();
        check("Sensor frames preserve UTF8 and message boundaries", first?.GpuIdentifier == "GPU · thử nghiệm" && second?.GpuIdentifier == "second");
        using var fragmented = new FragmentedStream(stream.ToArray());
        check("Sensor frame handles single-byte partial reads", SensorWireProtocol.ReadAsync<SensorProcessProvider.Request>(fragmented, 8192, default).GetAwaiter().GetResult()?.GpuIdentifier == "GPU · thử nghiệm");
        check("Sensor clean EOF is normal shutdown", SensorWireProtocol.ReadAsync<SensorProcessProvider.Request>(stream, 8192, default).GetAwaiter().GetResult() == null);
        foreach (int length in new[] { -1, 0, 8193, int.MaxValue })
        {
            var bytes = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(bytes, length);
            using var bad = new MemoryStream(bytes); bool rejected = false;
            try { SensorWireProtocol.ReadAsync<SensorProcessProvider.Request>(bad, 8192, default).GetAwaiter().GetResult(); }
            catch (InvalidDataException) { rejected = true; }
            check("Sensor rejects invalid length before payload allocation: " + length, rejected);
        }
        foreach (var bytes in new[] { new byte[] { 1, 0 }, new byte[] { 5, 0, 0, 0, 123 } })
        {
            using var truncated = new MemoryStream(bytes); bool rejected = false;
            try { SensorWireProtocol.ReadAsync<SensorProcessProvider.Request>(truncated, 8192, default).GetAwaiter().GetResult(); }
            catch (EndOfStreamException) { rejected = true; }
            check("Sensor rejects truncated frame: " + bytes.Length, rejected);
        }
        using var output = new MemoryStream(); bool tooLarge = false;
        try { SensorWireProtocol.WriteAsync(output, new SensorProcessProvider.Request(1, new string('x', 8192)), 8192, default).GetAwaiter().GetResult(); }
        catch (InvalidDataException) { tooLarge = true; }
        check("Oversized request writes no partial frame", tooLarge && output.Length == 0);
        using var canceled = new CancellationTokenSource(); canceled.Cancel(); bool stopped = false;
        stream.Position = 0;
        try { SensorWireProtocol.ReadAsync<SensorProcessProvider.Request>(stream, 8192, canceled.Token).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { stopped = true; }
        check("Sensor frame read honors cancellation", stopped);
    }
    private sealed class FragmentedStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
    }
}
