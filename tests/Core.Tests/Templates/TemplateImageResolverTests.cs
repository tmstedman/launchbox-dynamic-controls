using DynamicControls.Templates;
using NSubstitute;

namespace DynamicControls.Core.Tests.Templates;

/// <summary>
/// Unit tests for <see cref="TemplateImageResolver"/>. The resolver is pure path math over an
/// <see cref="IFileSystem"/> substitute: <see cref="TemplateImageResolver.FindBaseImage"/>
/// looks for BaseImage.png and reads its dimensions via <see cref="IImageHeader"/>;
/// <see cref="TemplateImageResolver.ResolveImagePath"/> prefers controller → platform for
/// styled and template-local → shared-root for generic, with the template-local path returned
/// as a placeholder when nothing exists.
/// </summary>
public class TemplateImageResolverTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IFileSystem _fs = TestFs.Create();
    private readonly IImageHeader _imageHeader = Substitute.For<IImageHeader>();
    private const string RootDir = @"C:\plugin";
    private static readonly string TemplatesDir = Path.Combine(RootDir, "Templates");
    private readonly TemplateImageResolver _underTest;

    public TemplateImageResolverTests()
    {
        _underTest = new TemplateImageResolver(_logger, _fs, _imageHeader, RootDir);
    }

    // --- FindBaseImage ---

    [Fact]
    public void FindBaseImage_PngExists_ReturnsPngWithDimensions()
    {
        // given a template folder containing BaseImage.png
        string png = Path.Combine(TemplatesDir, "x", "BaseImage.png");
        _fs.FileExists(png).Returns(true);
        _fs.OpenRead(png).Returns(new MemoryStream());
        _imageHeader.ReadDimensions(Arg.Any<Stream>()).Returns((1200, 800));

        // when looking up the base image
        BaseImage? baseImage = _underTest.FindBaseImage("x");

        // then the PNG path and read dimensions surface
        baseImage.ShouldNotBeNull();
        baseImage.Path.ShouldBe(png);
        baseImage.Width.ShouldBe(1200);
        baseImage.Height.ShouldBe(800);
    }

    [Fact]
    public void FindBaseImage_Missing_ReturnsNull()
    {
        // given no BaseImage.png exists
        _fs.FileExists(Arg.Any<string>()).Returns(false);

        // when looking up the base image
        BaseImage? baseImage = _underTest.FindBaseImage("x");

        // then null is returned and no dimension read happens
        baseImage.ShouldBeNull();
        _imageHeader.DidNotReceive().ReadDimensions(Arg.Any<Stream>());
    }

    // --- ResolveImagePath: styled path ---

    [Fact]
    public void ResolveImagePath_NoPlatform_ProducesNoStyledPath()
    {
        // given a template-local generic image and no platform
        string generic = Path.Combine(TemplatesDir, "x", "ButtonA.png");
        _fs.FileExists(generic).Returns(true);

        // when resolving
        ResolvedImagePaths paths = _underTest.ResolveImagePath("x", "ButtonA.png", platform: null);

        // then only the generic path is returned
        paths.Generic.ShouldBe(generic);
        paths.Styled.ShouldBeNull();
    }

    [Fact]
    public void ResolveImagePath_ControllerSpecificExists_PrefersControllerOverPlatform()
    {
        // given a controller-level styled file and a (would-be) platform-level styled file
        string controllerPath = Path.Combine(TemplatesDir, "x", "Sega32X", "Genesis6", "ButtonA.png");
        string platformPath = Path.Combine(TemplatesDir, "x", "Sega32X", "ButtonA.png");
        _fs.FileExists(controllerPath).Returns(true);
        _fs.FileExists(platformPath).Returns(true);

        // when resolving with both platform and controller specified
        ResolvedImagePaths paths = _underTest.ResolveImagePath("x", "ButtonA.png", "Sega32X", "Genesis6");

        // then the controller-level file wins and the platform-level path is not probed
        paths.Styled.ShouldBe(controllerPath);
        _fs.DidNotReceive().FileExists(platformPath);
    }

    [Fact]
    public void ResolveImagePath_NoController_FallsBackToPlatformStyled()
    {
        // given only a platform-level styled file exists
        string platformPath = Path.Combine(TemplatesDir, "x", "Sega32X", "ButtonA.png");
        _fs.FileExists(platformPath).Returns(true);

        // when resolving with platform but no controller
        ResolvedImagePaths paths = _underTest.ResolveImagePath("x", "ButtonA.png", "Sega32X");

        // then the platform-level file is used as the styled path
        paths.Styled.ShouldBe(platformPath);
    }

    [Fact]
    public void ResolveImagePath_PlatformWithInvalidChars_IsSanitized()
    {
        // given a platform name containing path-invalid chars
        string safePlatform = "Foo_Bar".SafeFileName();
        string platformPath = Path.Combine(TemplatesDir, "x", safePlatform, "ButtonA.png");
        _fs.FileExists(platformPath).Returns(true);

        // when resolving with the raw platform name
        ResolvedImagePaths paths = _underTest.ResolveImagePath("x", "ButtonA.png", "Foo/Bar");

        // then the styled path uses the sanitized platform folder
        paths.Styled.ShouldBe(platformPath);
    }

    // --- ResolveImagePath: generic fallback ---

    [Fact]
    public void ResolveImagePath_TemplateLocalMissing_FallsBackToSharedRoot()
    {
        // given the template-local generic is missing but a shared-root copy exists
        string templateLocal = Path.Combine(TemplatesDir, "x", "ButtonA.png");
        string shared = Path.Combine(TemplatesDir, "ButtonA.png");
        _fs.FileExists(templateLocal).Returns(false);
        _fs.FileExists(shared).Returns(true);

        // when resolving
        ResolvedImagePaths paths = _underTest.ResolveImagePath("x", "ButtonA.png", platform: null);

        // then the shared-root path is returned as generic
        paths.Generic.ShouldBe(shared);
    }

    [Fact]
    public void ResolveImagePath_NothingExists_ReturnsTemplateLocalPlaceholder()
    {
        // given neither a template-local nor shared-root copy exists
        _fs.FileExists(Arg.Any<string>()).Returns(false);

        // when resolving
        ResolvedImagePaths paths = _underTest.ResolveImagePath("x", "ButtonA.png", platform: null);

        // then the template-local path is returned anyway so callers see a deterministic path
        paths.Generic.ShouldBe(Path.Combine(TemplatesDir, "x", "ButtonA.png"));
        paths.Styled.ShouldBeNull();
    }

    // --- Caching ---

    [Fact]
    public void ResolveImagePath_Twice_CachesResultAndSkipsFileExistsChecks()
    {
        // given a first lookup that hits the filesystem
        string templateLocal = Path.Combine(TemplatesDir, "x", "ButtonA.png");
        _fs.FileExists(templateLocal).Returns(true);
        ResolvedImagePaths first = _underTest.ResolveImagePath("x", "ButtonA.png", platform: null);
        _fs.ClearReceivedCalls();

        // when resolving the same (templateName, src, platform, controller) key again
        ResolvedImagePaths second = _underTest.ResolveImagePath("x", "ButtonA.png", platform: null);

        // then the cached result is returned and no further filesystem probing happens
        second.ShouldBeSameAs(first);
        _fs.DidNotReceive().FileExists(Arg.Any<string>());
    }
}
