using System.Buffers.Binary;
using System.IO;

namespace Nepply.Services;

internal static class DesktopImageBudget
{
    internal const int MaximumDimension = 512;
    internal const int MaximumEntries = 10000;
    internal const int MaximumPngBytes = 512 * 1024;
    internal const long MaximumDecodedBytes = 32 * 1024 * 1024;
    internal const int MaximumReplyBytes = 32 * 1024 * 1024;

    // Reject before WIC materializes pixels. Our producer emits single-frame, <=8-bit PNG icons.
    internal static long ValidatePng(ReadOnlySpan<byte> png)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (png.Length < 33 || png.Length > MaximumPngBytes || !png[..8].SequenceEqual(signature)
            || BinaryPrimitives.ReadUInt32BigEndian(png[8..12]) != 13 || !png[12..16].SequenceEqual("IHDR"u8))
            throw new InvalidDataException("Desktop icon has an invalid PNG header or exceeds its byte budget.");
        uint width = BinaryPrimitives.ReadUInt32BigEndian(png[16..20]);
        uint height = BinaryPrimitives.ReadUInt32BigEndian(png[20..24]);
        if (width is 0 or > MaximumDimension || height is 0 or > MaximumDimension || png[24] is 0 or > 8)
            throw new InvalidDataException("Desktop icon exceeds its decoded pixel budget.");
        return (long)width * height * 4;
    }
}

// Enforce producer limits while writing, not after serializing an unbounded reply.
internal sealed class LimitedMemoryStream(int limit) : MemoryStream(Math.Min(limit, 4096))
{
    private void Check(int count)
    {
        if (count < 0 || Position > limit - count) throw new InvalidDataException("Desktop payload exceeds its byte budget.");
    }
    public override void Write(byte[] buffer, int offset, int count) { Check(count); base.Write(buffer, offset, count); }
    public override void Write(ReadOnlySpan<byte> buffer) { Check(buffer.Length); base.Write(buffer); }
    public override void WriteByte(byte value) { Check(1); base.WriteByte(value); }
    public override void SetLength(long value)
    {
        if (value > limit) throw new InvalidDataException("Desktop payload exceeds its byte budget.");
        base.SetLength(value);
    }
}
