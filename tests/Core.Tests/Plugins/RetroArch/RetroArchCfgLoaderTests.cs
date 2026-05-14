using DynamicControls.Plugins.RetroArch;
using NSubstitute;

namespace DynamicControls.Core.Tests.Plugins.RetroArch;

/// <summary>
/// Unit tests for <see cref="RetroArchCfgLoader"/>. Covers (1) portable vs non-portable config
/// root selection; (2) each cascade level uses the correct path independently; (3) level
/// isolation — levels don't bleed into each other; (4) input_joypad_driver filtering from
/// retroarch.cfg into the Global level only; (5) null core and missing files gate loading.
/// IRetroArchConfigFileReader and filesystem are substituted.
/// </summary>
public class RetroArchCfgLoaderTests
{
    private const string RetroArchDir = @"C:\RetroArch";
    private const string Core = "Genesis Plus GX";
    private const string Content = "Sega Genesis";
    private const string Rom = "Concord Remastered";

    private static readonly string CoreDir = Path.Combine(RetroArchDir, "config", Core);
    private static readonly string CoreCfgPath = Path.Combine(CoreDir, $"{Core}.cfg");
    private static readonly string ContentCfgPath = Path.Combine(CoreDir, $"{Content}.cfg");
    private static readonly string GameCfgPath = Path.Combine(CoreDir, $"{Rom}.cfg");
    private static readonly string RetroArchCfgPath = Path.Combine(RetroArchDir, "retroarch.cfg");

    private readonly IFileSystem _fs = TestFs.Create();
    private readonly IApplicationData _appData = Substitute.For<IApplicationData>();
    private readonly IRetroArchConfigFileReader _configReader = Substitute.For<IRetroArchConfigFileReader>();
    private readonly RetroArchCfgLoader _underTest;

    public RetroArchCfgLoaderTests()
    {
        // Default: portable mode — retroarch.cfg exists next to the exe.
        _fs.FileExists(RetroArchCfgPath).Returns(true);
        _underTest = new RetroArchCfgLoader(_configReader, _fs, _appData);
    }

    private static Dictionary<string, string> DictOf(string key, string value) => new() { [key] = value };

    private static GameInfo MakeGame(string? romDirectory = null, string romName = Rom) =>
        new("Sega Genesis", romName, null, null, romDirectory, null);

    // ---- config root: portable vs non-portable ----

    [Fact]
    public void Load_PortableMode_UsesRetroArchDirAsConfigRoot()
    {
        _fs.FileExists(RetroArchCfgPath).Returns(true);
        _configReader.LoadConfigFile(GameCfgPath).Returns(DictOf("input_libretro_device_p1", "257"));

        var result = _underTest.Load(RetroArchDir, Core, MakeGame()).ShouldNotBeNull();

        // game cfg was found under RetroArchDir — not under %APPDATA%\RetroArch
        result.Game.ShouldBeDictionaryOf(("input_libretro_device_p1", "257"));
    }

    [Fact]
    public void Load_NonPortableMode_UsesAppDataAsConfigRoot()
    {
        string appData = @"C:\Users\Trevor\AppData\Roaming";
        string nonPortableRoot = Path.Combine(appData, "RetroArch");
        string nonPortableGameCfg = Path.Combine(nonPortableRoot, "config", Core, $"{Rom}.cfg");
        _fs.FileExists(RetroArchCfgPath).Returns(false);
        _appData.Path.Returns(appData);
        _configReader.LoadConfigFile(nonPortableGameCfg).Returns(DictOf("input_libretro_device_p1", "257"));

        var result = _underTest.Load(RetroArchDir, Core, MakeGame()).ShouldNotBeNull();

        // game cfg was found under %APPDATA%\RetroArch, not under the exe directory
        result.Game.ShouldBeDictionaryOf(("input_libretro_device_p1", "257"));
    }

    // ---- Load: null / empty gating ----

    [Fact]
    public void Load_NullCore_ReturnsNullWithoutProbing()
    {
        var result = _underTest.Load(RetroArchDir, core: null, MakeGame());

        result.ShouldBeNull();
        _fs.DidNotReceive().FileExists(Arg.Any<string>());
        _configReader.DidNotReceive().LoadConfigFile(Arg.Any<string>());
    }

    [Fact]
    public void Load_NoFilesPresent_ReturnsNull()
    {
        // reader returns null for all paths by default
        _underTest.Load(RetroArchDir, Core, MakeGame()).ShouldBeNull();
    }

    // ---- Load: level population ----

    [Fact]
    public void Load_CoreCfgOnly_PopulatesCoreLevel()
    {
        _configReader.LoadConfigFile(CoreCfgPath).Returns(DictOf("input_player1_a_btn", "0"));

        var result = _underTest.Load(RetroArchDir, Core, MakeGame()).ShouldNotBeNull();

        result.Core.ShouldBeDictionaryOf(("input_player1_a_btn", "0"));
    }

    [Fact]
    public void Load_ContentDirCfg_PopulatesContentDirLevelSeparatelyFromCore()
    {
        _configReader.LoadConfigFile(CoreCfgPath).Returns(DictOf("input_player1_a_btn", "0"));
        _configReader.LoadConfigFile(ContentCfgPath).Returns(DictOf("input_player1_a_btn", "2"));

        var result = _underTest.Load(RetroArchDir, Core, MakeGame(Content)).ShouldNotBeNull();

        result.Core.ShouldBeDictionaryOf(("input_player1_a_btn", "0"));
        result.ContentDir.ShouldBeDictionaryOf(("input_player1_a_btn", "2"));
    }

    // ---- Load: level isolation ----

    [Fact]
    public void Load_GameLevel_ContainsOnlyGameCfgEntries()
    {
        _configReader.LoadConfigFile(CoreCfgPath).Returns(DictOf("core_key", "C"));
        _configReader.LoadConfigFile(ContentCfgPath).Returns(DictOf("content_key", "D"));
        _configReader.LoadConfigFile(GameCfgPath).Returns(DictOf("game_key", "G"));

        var result = _underTest.Load(RetroArchDir, Core, MakeGame(Content)).ShouldNotBeNull();

        result.Game.ShouldBeDictionaryOf(("game_key", "G"));
        result.Core.ShouldBeDictionaryOf(("core_key", "C"));
        result.ContentDir.ShouldBeDictionaryOf(("content_key", "D"));
    }

    [Fact]
    public void Load_GameLevelNull_WhenGameCfgMissing()
    {
        _configReader.LoadConfigFile(CoreCfgPath).Returns(DictOf("input_player1_a_btn", "0"));

        var result = _underTest.Load(RetroArchDir, Core, MakeGame()).ShouldNotBeNull();

        result.Game.ShouldBeNull();
    }

    // ---- Load: content directory path ----

    [Fact]
    public void Load_ContentDirectory_UsesOnlyTheLeafFolderName()
    {
        string fullContentDir = Path.Combine(@"C:\Games", "ROMs", Content);
        _configReader.LoadConfigFile(ContentCfgPath).Returns(DictOf("input_player1_a_btn", "2"));

        _underTest.Load(RetroArchDir, Core, MakeGame(fullContentDir));

        // reader was called with just the leaf folder name, not the full path
        _configReader.Received().LoadConfigFile(ContentCfgPath);
    }

    [Fact]
    public void Load_NullRomDirectory_SkipsContentDirLevel()
    {
        _configReader.LoadConfigFile(CoreCfgPath).Returns(DictOf("core_key", "C"));
        _configReader.LoadConfigFile(GameCfgPath).Returns(DictOf("game_key", "G"));

        var result = _underTest.Load(RetroArchDir, Core, MakeGame()).ShouldNotBeNull();

        result.ContentDir.ShouldBeNull();
        _configReader.DidNotReceive().LoadConfigFile(ContentCfgPath);
    }

    #pragma warning disable format
    [Theory]
    [InlineData("Genesis Plus GX", "Sega Genesis", "Sonic")]          // spaces throughout
    [InlineData("nestopia",        "NES",          "smb")]            // short, no spaces, lowercase
    [InlineData("Nestopia UE",     "Atari 2600",   "Pac-Man (1982)")] // digits, parentheses, hyphen
    #pragma warning restore format
    public void Load_BuildsExpectedPaths_ForVaryingNames(string core, string content, string rom)
    {
        string coreDir = Path.Combine(RetroArchDir, "config", core);
        string gameCfg = Path.Combine(coreDir, $"{rom}.cfg");
        _configReader.LoadConfigFile(gameCfg).Returns(DictOf("input_player1_a_btn", "5"));

        var result = _underTest.Load(RetroArchDir, core, new("Platform", rom, null, null, content, null)).ShouldNotBeNull();

        result.Game.ShouldBeDictionaryOf(("input_player1_a_btn", "5"));
    }

    // ---- Load: retroarch.cfg seeding ----

    [Fact]
    public void Load_RetroArchCfg_SeedsJoypadDriverIntoGlobalLevel()
    {
        _configReader.LoadConfigFile(RetroArchCfgPath).Returns(DictOf("input_joypad_driver", "xinput"));
        _configReader.LoadConfigFile(CoreCfgPath).Returns(DictOf("input_player1_a_btn", "0"));

        var result = _underTest.Load(RetroArchDir, Core, MakeGame()).ShouldNotBeNull();

        result.Global.ShouldBeDictionaryOf(("input_joypad_driver", "xinput"));
    }

    [Fact]
    public void Load_PerCoreCfg_OverridesJoypadDriverFromGlobal()
    {
        _configReader.LoadConfigFile(RetroArchCfgPath).Returns(DictOf("input_joypad_driver", "dinput"));
        _configReader.LoadConfigFile(CoreCfgPath).Returns(DictOf("input_joypad_driver", "sdl2"));

        var result = _underTest.Load(RetroArchDir, Core, MakeGame()).ShouldNotBeNull();

        // individual levels reflect each file faithfully — cascade resolution is the caller's job
        result.Global.ShouldBeDictionaryOf(("input_joypad_driver", "dinput"));
        result.Core.ShouldBeDictionaryOf(("input_joypad_driver", "sdl2"));
    }

    [Fact]
    public void Load_RetroArchCfg_OtherKeysAreNotIncludedInGlobal()
    {
        _configReader.LoadConfigFile(RetroArchCfgPath).Returns(new Dictionary<string, string>
        {
            ["input_joypad_driver"] = "xinput",
            ["input_player1_a_btn"] = "99",
        });
        _configReader.LoadConfigFile(CoreCfgPath).Returns(DictOf("input_player1_a_btn", "0"));

        var result = _underTest.Load(RetroArchDir, Core, MakeGame()).ShouldNotBeNull();

        // Global only contains the driver — autoconfig-derived entries are filtered out
        result.Global.ShouldBeDictionaryOf(("input_joypad_driver", "xinput"));
    }

    [Fact]
    public void Load_RetroArchCfg_JoypadDriverAbsent_GlobalIsNull()
    {
        _configReader.LoadConfigFile(RetroArchCfgPath).Returns(DictOf("video_fullscreen", "true"));
        _configReader.LoadConfigFile(CoreCfgPath).Returns(DictOf("input_player1_a_btn", "0"));

        var result = _underTest.Load(RetroArchDir, Core, MakeGame()).ShouldNotBeNull();

        result.Global.ShouldBeNull();
    }

    [Fact]
    public void Load_RetroArchCfgOnly_JoypadDriverPresent_ReturnsNonNull()
    {
        _configReader.LoadConfigFile(RetroArchCfgPath).Returns(DictOf("input_joypad_driver", "xinput"));

        var result = _underTest.Load(RetroArchDir, Core, MakeGame()).ShouldNotBeNull();

        result.Global.ShouldBeDictionaryOf(("input_joypad_driver", "xinput"));
    }
}
