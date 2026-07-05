using DynamicControls.Plugins.RetroArch;
using NSubstitute;

namespace DynamicControls.Core.Tests.Plugins.RetroArch;

/// <summary>
/// Unit tests for <see cref="RetroArchRemapLoader"/>. Covers (1) gating — null core and missing
/// remapsDir return null without probing; (2) level independence — each remap file (game,
/// contentDir, core, common) populates its own level independently; (3) null handling — missing
/// files leave levels null; (4) path construction with varying names. IRetroArchConfigFileReader
/// and filesystem are substituted.
/// </summary>
public class RetroArchRemapLoaderTests
{
    private const string RetroArchDir = @"C:\RetroArch";
    private static readonly string RemapsDir = Path.Combine(RetroArchDir, "config", "remaps");
    private const string Core = "Genesis Plus GX";
    private const string Rom = "Sonic";

    private static readonly string ContentDir = Path.Combine(@"C:\Games", "Sega Genesis");

    private static readonly string CoreDir = Path.Combine(RemapsDir, Core);
    private static readonly string GameRmpPath = Path.Combine(CoreDir, $"{Rom}.rmp");
    private static readonly string ContentRmpPath = Path.Combine(CoreDir, $"{Path.GetFileName(ContentDir)}.rmp");
    private static readonly string CoreRmpPath = Path.Combine(CoreDir, $"{Core}.rmp");
    private static readonly string CommonRmpPath = Path.Combine(RemapsDir, "common.rmp");

    private readonly IFileSystem _fs = TestFs.Create();
    private readonly IRetroArchConfigFileReader _configReader = Substitute.For<IRetroArchConfigFileReader>();
    private readonly RetroArchRemapLoader _underTest;

    public RetroArchRemapLoaderTests()
    {
        _fs.DirectoryExists(Arg.Any<string>()).Returns(false);
        _underTest = new RetroArchRemapLoader(_configReader, _fs);
    }

    private static Dictionary<string, string> DictOf(string key, string value) => new() { [key] = value };

    private void StubDirExists(string dir) => _fs.DirectoryExists(dir).Returns(true);

    private void StubGameRmp(string key, string value)
    {
        StubDirExists(RemapsDir);
        StubDirExists(CoreDir);
        _configReader.LoadConfigFile(GameRmpPath).Returns(DictOf(key, value));
    }

    private void StubContentRmp(string key, string value)
    {
        StubDirExists(RemapsDir);
        StubDirExists(CoreDir);
        _configReader.LoadConfigFile(ContentRmpPath).Returns(DictOf(key, value));
    }

    private void StubCoreRmp(string key, string value)
    {
        StubDirExists(RemapsDir);
        StubDirExists(CoreDir);
        _configReader.LoadConfigFile(CoreRmpPath).Returns(DictOf(key, value));
    }

    private void StubCommonRmp(string key, string value)
    {
        StubDirExists(RemapsDir);
        _configReader.LoadConfigFile(CommonRmpPath).Returns(DictOf(key, value));
    }

    private GameInfo MakeGame(string romName = Rom) =>
        new("Platform", romName, null, null, null, ContentDir, null);

    // ---- gating ----

    [Fact]
    public void Load_NullCore_ReturnsNullWithoutProbing()
    {
        _underTest.Load(RetroArchDir, core: null, MakeGame()).ShouldBeNull();
        _fs.DidNotReceive().DirectoryExists(Arg.Any<string>());
        _configReader.DidNotReceive().LoadConfigFile(Arg.Any<string>());
    }

    [Fact]
    public void Load_RemapsDirMissing_ReturnsNullWithoutProbing()
    {
        _underTest.Load(RetroArchDir, Core, MakeGame()).ShouldBeNull();
        _configReader.DidNotReceive().LoadConfigFile(Arg.Any<string>());
    }

    [Fact]
    public void Load_NoRmpFilesFound_ReturnsNull()
    {
        StubDirExists(RemapsDir);
        StubDirExists(CoreDir);
        // reader returns null for all paths by default

        _underTest.Load(RetroArchDir, Core, MakeGame()).ShouldBeNull();
    }

    // ---- level independence ----

    [Fact]
    public void Load_AllLevelsPresent_PopulatesEachLevelIndependently()
    {
        StubDirExists(RemapsDir);
        StubDirExists(CoreDir);
        _configReader.LoadConfigFile(GameRmpPath).Returns(DictOf("game_key", "G"));
        _configReader.LoadConfigFile(ContentRmpPath).Returns(DictOf("content_key", "C"));
        _configReader.LoadConfigFile(CoreRmpPath).Returns(DictOf("core_key", "K"));
        _configReader.LoadConfigFile(CommonRmpPath).Returns(DictOf("common_key", "D"));

        var result = _underTest.Load(RetroArchDir, Core, MakeGame()).ShouldNotBeNull();

        result.Game.ShouldBeDictionaryOf(("game_key", "G"));
        result.ContentDir.ShouldBeDictionaryOf(("content_key", "C"));
        result.Core.ShouldBeDictionaryOf(("core_key", "K"));
        result.Global.ShouldBeDictionaryOf(("common_key", "D"));
    }

    [Fact]
    public void Load_GameRmpOnly_OtherLevelsAreNull()
    {
        StubGameRmp("game_key", "G");

        var result = _underTest.Load(RetroArchDir, Core, MakeGame()).ShouldNotBeNull();

        result.Game.ShouldBeDictionaryOf(("game_key", "G"));
        result.ContentDir.ShouldBeNull();
        result.Core.ShouldBeNull();
        result.Global.ShouldBeNull();
    }

    [Fact]
    public void Load_CoreRmpOnly_OtherLevelsAreNull()
    {
        StubCoreRmp("core_key", "K");

        var result = _underTest.Load(RetroArchDir, Core, MakeGame()).ShouldNotBeNull();

        result.Core.ShouldBeDictionaryOf(("core_key", "K"));
        result.Game.ShouldBeNull();
        result.ContentDir.ShouldBeNull();
        result.Global.ShouldBeNull();
    }

    [Fact]
    public void Load_CommonRmpOnly_PopulatesGlobal()
    {
        StubCommonRmp("common_key", "D");

        var result = _underTest.Load(RetroArchDir, Core, MakeGame()).ShouldNotBeNull();

        result.Global.ShouldBeDictionaryOf(("common_key", "D"));
        result.Game.ShouldBeNull();
        result.ContentDir.ShouldBeNull();
        result.Core.ShouldBeNull();
    }

    [Fact]
    public void Load_GameRmpMissing_StillLoadsOtherLevels()
    {
        StubCoreRmp("core_key", "K");
        StubCommonRmp("common_key", "D");

        var result = _underTest.Load(RetroArchDir, Core, MakeGame()).ShouldNotBeNull();

        result.Game.ShouldBeNull();
        result.Core.ShouldBeDictionaryOf(("core_key", "K"));
        result.Global.ShouldBeDictionaryOf(("common_key", "D"));
    }

    // ---- null romDirectory skips content-dir level ----

    [Fact]
    public void Load_NullRomDirectory_SkipsContentDirLevel()
    {
        StubDirExists(RemapsDir);
        StubDirExists(CoreDir);
        _configReader.LoadConfigFile(GameRmpPath).Returns(DictOf("game_key", "G"));

        var result = _underTest.Load(RetroArchDir, Core, new("Platform", Rom, null, null, null, null, null)).ShouldNotBeNull();

        result.ContentDir.ShouldBeNull();
        _configReader.DidNotReceive().LoadConfigFile(ContentRmpPath);
    }

    [Fact]
    public void Load_RomDirIsFullPath_UsesOnlyLeafForContentRmpName()
    {
        StubContentRmp("content_key", "C");

        _underTest.Load(RetroArchDir, Core, MakeGame());

        // reader was called with just the leaf folder name, not the full romDir path
        _configReader.Received().LoadConfigFile(ContentRmpPath);
    }

    // ---- coreDirMissing falls through to common ----

    [Fact]
    public void Load_CoreDirMissing_StillLoadsCommon()
    {
        StubCommonRmp("common_key", "D");

        var result = _underTest.Load(RetroArchDir, Core, MakeGame()).ShouldNotBeNull();

        result.Global.ShouldBeDictionaryOf(("common_key", "D"));
        result.Game.ShouldBeNull();
        result.Core.ShouldBeNull();
        _configReader.DidNotReceive().LoadConfigFile(GameRmpPath);
    }

    // ---- path construction ----

    #pragma warning disable format
    [Theory]
    [InlineData("Genesis Plus GX", "Sega Genesis", "Sonic")]          // spaces throughout
    [InlineData("nestopia",        "NES",          "smb")]            // short, no spaces, lowercase
    [InlineData("Nestopia UE",     "Atari 2600",   "Pac-Man (1982)")] // digits, parentheses, hyphen
    #pragma warning restore format
    public void Load_BuildsExpectedPaths_ForVaryingNames(string core, string content, string rom)
    {
        string coreDir = Path.Combine(RemapsDir, core);
        string romDir = Path.Combine(@"C:\Games", content);
        string gameRmp = Path.Combine(coreDir, $"{rom}.rmp");
        StubDirExists(RemapsDir);
        StubDirExists(coreDir);
        _configReader.LoadConfigFile(gameRmp).Returns(DictOf("game_key", "G"));

        var result = _underTest.Load(RetroArchDir, core, new("Platform", rom, null, null, null, romDir, null)).ShouldNotBeNull();

        result.Game.ShouldBeDictionaryOf(("game_key", "G"));
    }
}
