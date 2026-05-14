using DynamicControls.Plugins.RetroArch;
using NSubstitute;

namespace DynamicControls.Core.Tests.Plugins.RetroArch;

/// <summary>
/// Unit tests for <see cref="RetroArchCoreLoader"/>. The loader reads
/// <c>{rootDir}/Emulators/RetroArch/{coreDisplayName}.xml</c>, validates its root and
/// <c>&lt;Controllers&gt;</c> shape, and parses each <c>&lt;Controller&gt;</c> into a
/// <see cref="RetroArchControllerConfig"/>. Filesystem is substituted so each test supplies
/// a literal XML string. Path construction is verified by the precise path the loader probes.
/// </summary>
public class RetroArchCoreLoaderTests
{
    private const string RootDir = @"C:\plugin";
    private const string Core = "Genesis Plus GX";
    private static readonly string CoreXmlPath = Path.Combine(RootDir, "Defaults", "Emulators", "RetroArch", $"{Core}.xml");

    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IFileSystem _fs = TestFs.Create();
    private readonly RetroArchCoreLoader _underTest;

    public RetroArchCoreLoaderTests()
    {
        _underTest = new RetroArchCoreLoader(_logger, new LayeredFileSystem(RootDir, _fs));
    }

    private void StubXml(string xml)
    {
        _fs.FileExists(CoreXmlPath).Returns(true);
        _fs.OpenRead(CoreXmlPath).Returns(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
    }

    // ---- file presence and shape validation ----

    [Fact]
    public void Load_FileMissing_ReturnsNullAndDoesNotParse()
    {
        // given no core config xml on disk
        _fs.FileExists(CoreXmlPath).Returns(false);

        // when the loader runs
        var result = _underTest.Load(Core);

        // then null is returned and no XML load is attempted — silent skip is fine here
        result.ShouldBeNull();
        _fs.DidNotReceive().OpenRead(Arg.Any<string>());
    }

    [Fact]
    public void Load_WrongRootElement_ReturnsNullAndLogsError()
    {
        // given an XML file with a root that isn't <RetroArchCore>
        StubXml("<NotRetroArchCore><Controllers /></NotRetroArchCore>");

        // when the loader runs
        var result = _underTest.Load(Core);

        // then null is returned and the structural mismatch is logged
        result.ShouldBeNull();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("expected <RetroArchCore> root")));
    }

    [Fact]
    public void Load_MissingControllersElement_ReturnsNullAndLogsError()
    {
        // given a valid root but no <Controllers> child
        StubXml("<RetroArchCore />");

        // when the loader runs
        var result = _underTest.Load(Core);

        // then null is returned and the missing element is logged
        result.ShouldBeNull();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("missing <Controllers>")));
    }

    [Fact]
    public void Load_NoControllers_ReturnsNullAndLogsError()
    {
        // given a <Controllers> element with no controller children (or only non-Controller children)
        StubXml("""
            <RetroArchCore>
              <Controllers />
            </RetroArchCore>
            """);

        // when the loader runs
        var result = _underTest.Load(Core);

        // then null is returned and the empty-controller-list condition is logged
        result.ShouldBeNull();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("no controllers")));
    }

    // ---- happy-path parsing ----

    [Fact]
    public void Load_ValidFile_ReturnsControllersInDocumentOrder()
    {
        // given two controller variants, each with one retropad id
        StubXml("""
            <RetroArchCore>
              <Controllers>
                <Controller name='Pad-3btn'>
                  <Retropad id='1' />
                </Controller>
                <Controller name='Pad-6btn'>
                  <Retropad id='513' />
                </Controller>
              </Controllers>
            </RetroArchCore>
            """);

        // when the loader runs
        var result = _underTest.Load(Core);

        // then both controllers are present in document order with their ids parsed as integers
        result.ShouldNotBeNull();
        result.Controllers.Select(c => c.Name).ShouldBe(["Pad-3btn", "Pad-6btn"]);
        result.Controllers[0].RetropadIds.ShouldBe([1]);
        result.Controllers[1].RetropadIds.ShouldBe([513]);
    }

    [Fact]
    public void Load_ControllerWithMultipleRetropadIds_StoresAllIds()
    {
        // given a controller that lists multiple device-type ids (e.g. several MD 6-button variants)
        StubXml("""
            <RetroArchCore>
              <Controllers>
                <Controller name='Pad-6btn'>
                  <Retropad id='513' />
                  <Retropad id='769' />
                  <Retropad id='1025' />
                </Controller>
              </Controllers>
            </RetroArchCore>
            """);

        // when the loader runs
        var result = _underTest.Load(Core);

        // then every id is captured under the same controller
        result.ShouldNotBeNull();
        result.Controllers.ShouldHaveSingleItem()
            .RetropadIds.ShouldBe([513, 769, 1025]);
    }

    // ---- per-controller robustness ----

    [Fact]
    public void Load_ControllerMissingName_IsSkippedWithError()
    {
        // given one valid controller and one missing the name attribute
        StubXml("""
            <RetroArchCore>
              <Controllers>
                <Controller>
                  <Retropad id='1' />
                </Controller>
                <Controller name='Pad'>
                  <Retropad id='2' />
                </Controller>
              </Controllers>
            </RetroArchCore>
            """);

        // when the loader runs
        var result = _underTest.Load(Core);

        // then only the named controller survives and the nameless one is logged
        result.ShouldNotBeNull();
        result.Controllers.Select(c => c.Name).ShouldBe(["Pad"]);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("missing 'name'")));
    }

    [Fact]
    public void Load_RetropadWithInvalidId_LogsErrorAndSkipsThatId()
    {
        // given a controller with one valid id and one non-numeric id
        StubXml("""
            <RetroArchCore>
              <Controllers>
                <Controller name='Pad'>
                  <Retropad id='1' />
                  <Retropad id='abc' />
                  <Retropad id='513' />
                </Controller>
              </Controllers>
            </RetroArchCore>
            """);

        // when the loader runs
        var result = _underTest.Load(Core);

        // then the controller survives with only the parseable ids; the bad id is logged
        result.ShouldNotBeNull();
        result.Controllers.ShouldHaveSingleItem().RetropadIds.ShouldBe([1, 513]);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Invalid <Retropad>") && s.Contains("abc")));
    }

    [Fact]
    public void Load_RetropadMissingIdAttribute_LogsErrorAndIsSkipped()
    {
        // given a Retropad without an id attribute at all
        StubXml("""
            <RetroArchCore>
              <Controllers>
                <Controller name='Pad'>
                  <Retropad />
                  <Retropad id='1' />
                </Controller>
              </Controllers>
            </RetroArchCore>
            """);

        // when the loader runs
        var result = _underTest.Load(Core);

        // then only the valid id is retained and the missing attribute is logged
        result.ShouldNotBeNull();
        result.Controllers.ShouldHaveSingleItem().RetropadIds.ShouldBe([1]);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Invalid <Retropad>")));
    }

    [Fact]
    public void Load_UnexpectedElementInControllers_IsLoggedAndSkipped()
    {
        // given a <Controllers> block mixing a valid Controller with a comment and an unrecognized element
        StubXml("""
            <RetroArchCore>
              <Controllers>
                <!-- ignore me -->
                <Other name='ignored' />
                <Controller name='Pad'>
                  <Retropad id='1' />
                </Controller>
              </Controllers>
            </RetroArchCore>
            """);

        // when the loader runs
        var result = _underTest.Load(Core);

        // then the comment is silently dropped (not an Element), but the unexpected element is
        // logged — users authoring this file get a signal on typos like <Cotnroller>
        result.ShouldNotBeNull();
        result.Controllers.ShouldHaveSingleItem().Name.ShouldBe("Pad");
        _logger.Received().Error(Arg.Is<string>(s =>
            s.Contains("Unexpected <Other>") && s.Contains("expected <Controller>")));
    }

    [Fact]
    public void Load_UnexpectedElementInsideController_IsLoggedAndSkipped()
    {
        // given a Controller containing a comment and an unrecognized child alongside Retropad
        StubXml("""
            <RetroArchCore>
              <Controllers>
                <Controller name='Pad'>
                  <!-- comment -->
                  <Other id='99' />
                  <Retropad id='1' />
                </Controller>
              </Controllers>
            </RetroArchCore>
            """);

        // when the loader runs
        var result = _underTest.Load(Core);

        // then only Retropad children produce ids; the comment is dropped silently and the
        // unrecognized element fires an error log so authors can spot misspelled child elements
        result.ShouldNotBeNull();
        result.Controllers.ShouldHaveSingleItem().RetropadIds.ShouldBe([1]);
        _logger.Received().Error(Arg.Is<string>(s =>
            s.Contains("Unexpected <Other>") && s.Contains("expected <Retropad>")));
    }

    [Fact]
    public void Load_XmlComments_AreSilentlyIgnoredWithoutErrorLog()
    {
        // given a file with only comments alongside valid elements — no unrecognized elements
        StubXml("""
            <RetroArchCore>
              <!-- top-level comment -->
              <Controllers>
                <!-- comment about controllers -->
                <Controller name='Pad'>
                  <!-- comment about retropad ids -->
                  <Retropad id='1' />
                </Controller>
              </Controllers>
            </RetroArchCore>
            """);

        // when the loader runs
        var result = _underTest.Load(Core);

        // then comments do not trigger any error — only unrecognized Element nodes do
        result.ShouldNotBeNull();
        result.Controllers.ShouldHaveSingleItem().RetropadIds.ShouldBe([1]);
        _logger.DidNotReceive().Error(Arg.Any<string>());
    }
}
