namespace DynamicControls.Infrastructure;

/// <summary>Reads pixel dimensions from PNG file headers without decoding the image.</summary>
public interface IImageHeader
{
    (int Width, int Height) ReadDimensions(Stream stream);
}

/// <summary>Production implementation: parses PNG headers directly without decoding the image.</summary>
public class ImageHeader : IImageHeader
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public (int Width, int Height) ReadDimensions(Stream stream)
    {
        Span<byte> head = stackalloc byte[8];
        ReadExact(stream, head);
        if (head.SequenceEqual(PngSignature))
            return ReadPng(stream);
        throw new NotSupportedException("Image format not recognized; expected PNG.");
    }

    // After the 8-byte signature, the next 8 bytes are chunk length + "IHDR", then width and
    // height as big-endian uint32. The IHDR-first rule is mandated by the PNG spec.
    private static (int Width, int Height) ReadPng(Stream stream)
    {
        Span<byte> buf = stackalloc byte[16];
        ReadExact(stream, buf);
        return (ReadBigEndianInt32(buf.Slice(8, 4)), ReadBigEndianInt32(buf.Slice(12, 4)));
    }

    private static int ReadBigEndianInt32(ReadOnlySpan<byte> bytes) =>
        (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];

    private static void ReadExact(Stream stream, Span<byte> buffer)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int n = stream.Read(buffer[read..]);
            if (n <= 0) throw new EndOfStreamException("Unexpected end of image stream.");
            read += n;
        }
    }
}
