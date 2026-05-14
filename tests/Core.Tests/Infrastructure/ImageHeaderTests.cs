using DynamicControls.Infrastructure;

namespace DynamicControls.Core.Tests.Infrastructure;

public class ImageHeaderTests
{
    private readonly ImageHeader _underTest = new();

    [Theory]
    [InlineData("sample.png", 23, 17)]
    [InlineData("tiny.png", 1, 1)]
    [InlineData("wide.png", 640, 480)]
    public void ReadDimensions_ReturnsWidthAndHeight(string fixture, int expectedWidth, int expectedHeight)
    {
        using FileStream stream = File.OpenRead(Path.Combine("Fixtures", fixture));

        (int width, int height) = _underTest.ReadDimensions(stream);

        width.ShouldBe(expectedWidth);
        height.ShouldBe(expectedHeight);
    }

    [Fact]
    public void ReadDimensions_TruncatedStream_Throws()
    {
        // Stream ends before the 8-byte PNG signature is complete — ReadExact gets 0 bytes on
        // the second read attempt and throws EndOfStreamException.
        using var stream = new MemoryStream([0x89, 0x50, 0x4E, 0x47]);

        Should.Throw<EndOfStreamException>(() => _underTest.ReadDimensions(stream))
            .Message.ShouldContain("end of image stream");
    }

    [Fact]
    public void ReadDimensions_UnknownFormat_Throws()
    {
        using var stream = new MemoryStream([0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07]);

        Should.Throw<NotSupportedException>(() => _underTest.ReadDimensions(stream));
    }
}
