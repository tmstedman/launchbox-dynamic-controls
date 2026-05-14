using DynamicControls.InputMapping;
using DynamicControls.Plugins.RetroArch;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;

namespace DynamicControls.Core.IntegrationTests.Subsystem;

/// <summary>
/// Verifies the RetroArch input-mapping plugin with its real internal wiring intact:
/// <see cref="RetroArchCoreInfo"/> + <see cref="RetroArchCoreLoader"/>
/// + <see cref="RetroArchCfgLoader"/> / <see cref="RetroArchRemapLoader"/>
/// + <see cref="RetroArchVariantResolver"/>, <see cref="RetroArchCfgSwapResolver"/>
/// / <see cref="RetroArchRemapSwapResolver"/> + <see cref="RetroArchOverridesResolver"/>
/// + <see cref="RetroArchSwapApplier"/> all assembled exactly as
/// <c>RetroArchMappingSourceFactory.Create</c> assembles them, driven through
/// <see cref="RetroArchMappingSource.Load"/>. Each test stages a realistic RetroArch install in an
/// in-memory <see cref="MockFileSystem"/> (retroarch.cfg + info + per-core xml + cfg/rmp cascade
/// files) and asserts on the returned <see cref="InputMappingConfig"/>. Focuses on emergent
/// behaviours that unit tests for individual collaborators can't catch — display-name resolution
/// driving the config subfolder, cfg/rmp variant override priority, the game-level swap-detection
/// trust boundary, swap-application order, and portable-vs-APPDATA cfg root selection.
/// </summary>
public class RetroArchSubsystemTests
{
    private const string Platform = "Sega Genesis";
    private const string CoreDll = "genesis_plus_gx_libretro";
    private const string CoreDisplayName = "Genesis Plus GX";
    private const string Rom = "OutRun";

    private static readonly string RootDir = Path.DirectorySeparatorChar + "dc";
    private static readonly string RetroArchDir = Path.Combine(RootDir, "Emulators", "retroarch");
    private static readonly string RetroArchExe = Path.Combine(RetroArchDir, "retroarch.exe");
    private static readonly string RomDir = Path.Combine(RootDir, "Games", Platform);
    private static readonly string AppDataRoot = Path.Combine(RootDir, "AppData");

    private readonly MockDynamicControlsFilesystem _dc = new(RootDir);
    private readonly NullLogger _logger = new();
    private readonly FakeApplicationData _appData = new(AppDataRoot);

    // ---- factory ----

    /// <summary>Uses the production factory so the wiring under test is the wiring the rest of
    /// the system uses. The factory's optional <see cref="IApplicationData"/> parameter lets the
    /// non-portable cascade resolve into <see cref="MockFileSystem"/> instead of the host's
    /// actual APPDATA.</summary>
    private RetroArchMappingSource Build() =>
        RetroArchMappingSourceFactory.Create(_dc.Lfs, _logger, _appData);

    // ---- staging helpers ----

    private void StageCoreInfo(string coreDll, string displayName) =>
        _dc.WriteFile(
            Path.Combine(RetroArchDir, "info", $"{coreDll}.info"),
            $"corename = \"{displayName}\"\ndisplay_name = whatever");

    /// <summary>Writes the per-core XML at <c>{rootDir}/RetroArch/{display}.xml</c>. Each
    /// <paramref name="variants"/> pair becomes a <c>&lt;Controller&gt;</c> entry, so a test that
    /// selects a variant can declare the pair it exercises and keep the id→name link visible at the
    /// call site. With no pairs given, declares the two Genesis fixtures (6-Button id=513,
    /// 3-Button id=257) as backdrop for tests that don't select a variant.</summary>
    private void StageCoreXml(params (string Name, string Id)[] variants) =>
        StageCoreXml(CoreDisplayName, variants);

    private void StageCoreXml(string displayName, params (string Name, string Id)[] variants)
    {
        string controllers = string.Join("\n    ", Array.ConvertAll(
            variants, v => $"""<Controller name="{v.Name}"><Retropad id="{v.Id}" /></Controller>"""));

        _dc.WriteRetroArchCore(displayName,
            $"""
            <RetroArchCore>
              <Controllers>
                {controllers}
              </Controllers>
            </RetroArchCore>
            """);
    }

    /// <summary>Writes <c>retroarch.cfg</c> next to the exe. The file's mere presence is what
    /// switches <see cref="RetroArchCfgLoader"/> into portable mode (cfg root = exe dir, not
    /// APPDATA); its content seeds the cfg cascade's global level (only <c>input_joypad_driver</c>
    /// is read from it).</summary>
    private void StageRetroArchCfg(string body = "") =>
        _dc.WriteFile(Path.Combine(RetroArchDir, "retroarch.cfg"), body);

    private string CfgPath(string filename, string root) =>
        Path.Combine(root, "config", CoreDisplayName, filename);

    private void StageGameCfg(string body, string? root = null) =>
        _dc.WriteFile(CfgPath(Rom + ".cfg", root ?? RetroArchDir), body);

    private void StageCoreCfg(string body, string? root = null) =>
        _dc.WriteFile(CfgPath(CoreDisplayName + ".cfg", root ?? RetroArchDir), body);

    private void StageContentDirCfg(string body, string? root = null) =>
        _dc.WriteFile(CfgPath(Platform + ".cfg", root ?? RetroArchDir), body);

    /// <summary>Writes the game-level .rmp under <c>{retroArchDir}/config/remaps/{display}/{rom}.rmp</c>.
    /// Creating any file here also ensures the remaps directory exists, which
    /// <see cref="RetroArchRemapLoader"/> checks before walking layers.</summary>
    private void StageGameRmp(string body) =>
        _dc.WriteFile(
            Path.Combine(RetroArchDir, "config", "remaps", CoreDisplayName, Rom + ".rmp"),
            body);

    // ---- cfg/rmp body builders ----
    // Named so the meaningful tokens (which variant, which slot, which coreId) show at the call
    // site instead of a raw key string the reader has to parse.

    /// <summary>The <c>input_libretro_device_p1</c> line both cfg and rmp use to select a variant.</summary>
    private static string SelectDevice(string id) => $"input_libretro_device_p1 = \"{id}\"";

    /// <summary>A cfg button binding (<c>input_player1_{slot}_btn</c>). A non-canonical
    /// <paramref name="coreId"/> is what the swap resolver reads as a remap.</summary>
    private static string CfgSwap(string slot, string coreId) =>
        $"input_player1_{slot}_btn = \"{coreId}\"";

    /// <summary>An rmp button binding (<c>input_player1_btn_{slot}</c> — note the rmp key order
    /// differs from cfg's).</summary>
    private static string RmpSwap(string slot, string coreId) =>
        $"input_player1_btn_{slot} = \"{coreId}\"";

    private static PlatformControllersConfig GenesisPlatform() => PlatformConfig(
        ControllerDef(
            name: "6-Button",
            isDefault: true,
            analogToDigital: AnalogToDigitalMode.Left,
            mappings:
            [
                ("X", "ButtonLeftShoulder"), ("Y", "ButtonY"), ("Z", "ButtonRightShoulder"),
                ("A", "ButtonX"), ("B", "ButtonA"), ("C", "ButtonB"),
            ]),
        ControllerDef(
            name: "3-Button",
            analogToDigital: AnalogToDigitalMode.Left,
            mappings: [
                ("A", "ButtonX"), ("B", "ButtonA"), ("C", "ButtonB")
            ]));

    // ---- gating ----

    [Fact]
    public void Load_NoPerCoreXml_ReturnsNull_SoChainFallsThrough()
    {
        // Without {rootDir}/RetroArch/{display}.xml, the plugin has no variant catalog and
        // returns null — InputMappingService's source chain then falls through to the platform
        // default.
        StageRetroArchCfg();
        StageCoreInfo(
            coreDll: CoreDll,
            displayName: CoreDisplayName);
        StageGameCfg(SelectDevice("513"));

        InputMappingConfig? result = Build().Load(
            Game(
                emulatorPath: RetroArchExe,
                romDirectory: RomDir,
                retroArchCore: CoreDll),
            GenesisPlatform());

        result.ShouldBeNull();
    }

    [Fact]
    public void Load_NonRetroArchEmulatorPath_ReturnsNull()
    {
        // Even when every RetroArch artifact is in place, the gate trips when the emulator
        // executable isn't RetroArch — e.g. a Genesis game launched through Kega Fusion.
        StageRetroArchCfg();
        StageCoreInfo(
            coreDll: CoreDll,
            displayName: CoreDisplayName);
        StageCoreXml(
            ("6-Button", "513"),
            ("3-Button", "257"));
        StageGameCfg(SelectDevice("513"));

        InputMappingConfig? result = Build().Load(
            Game(
                emulatorPath: Path.Combine(RetroArchDir, "kega-fusion.exe"),
                romDirectory: RomDir,
                retroArchCore: CoreDll),
            GenesisPlatform());

        result.ShouldBeNull();
    }

    [Fact]
    public void Load_PerCoreXmlPresentButNoCfgOrRmp_ReturnsNull()
    {
        // No cfg files anywhere and no remaps directory: cfgResolver and remapResolver both
        // return null, so the source has nothing to contribute.
        StageRetroArchCfg();
        StageCoreInfo(
            coreDll: CoreDll,
            displayName: CoreDisplayName);
        StageCoreXml(
            ("6-Button", "513"),
            ("3-Button", "257"));

        InputMappingConfig? result = Build().Load(
            Game(
                emulatorPath: RetroArchExe,
                romDirectory: RomDir,
                retroArchCore: CoreDll),
            GenesisPlatform());

        result.ShouldBeNull();
    }

    // ---- variant resolution ----

    [Fact]
    public void Load_CfgPicksVariant_NoRmp_VariantFlowsToControllerName()
    {
        // input_libretro_device_p1 = 257 → 3-Button (the per-core xml's id=257 variant).
        // Without any rmp data, cfg's variant wins; the active mapping comes from the platform's
        // 3-Button controller, not the default 6-Button.
        StageRetroArchCfg();
        StageCoreInfo(
            coreDll: CoreDll,
            displayName: CoreDisplayName);
        StageCoreXml(("3-Button", "257"));
        StageGameCfg(SelectDevice("257"));

        InputMappingConfig? result = Build().Load(
            Game(
                emulatorPath: RetroArchExe,
                romDirectory: RomDir,
                retroArchCore: CoreDll),
            GenesisPlatform());

        result.ShouldNotBeNull();
        result.Controller.ShouldBe("3-Button");
        result.Mappings.ShouldContainEntry("A", "ButtonX");
        result.Mappings.ShouldNotContain(m => m.Name == "X"); // 6-Button-only mapping
    }

    [Fact]
    public void Load_CfgAndRmpBothPickVariants_RmpVariantWins()
    {
        // Per CLAUDE.md: rmp wins over cfg for variant selection. cfg picks 3-Button (257) but
        // rmp picks 6-Button (513); the source resolves to 6-Button.
        StageRetroArchCfg();
        StageCoreInfo(
            coreDll: CoreDll,
            displayName: CoreDisplayName);
        StageCoreXml(
            ("6-Button", "513"),
            ("3-Button", "257"));
        StageGameCfg(SelectDevice("257"));
        StageGameRmp(SelectDevice("513"));

        InputMappingConfig? result = Build().Load(
            Game(
                emulatorPath: RetroArchExe,
                romDirectory: RomDir,
                retroArchCore: CoreDll),
            GenesisPlatform());

        result.ShouldNotBeNull();
        result.Controller.ShouldBe("6-Button");
    }

    [Fact]
    public void Load_CfgVariantInCoreLevelOnly_CascadeWalkPicksItUp()
    {
        // Game-level cfg has no variant override; core-level cfg sets it. The variant resolver
        // walks Game → ContentDir → Core → Global and picks up the core-level entry.
        StageRetroArchCfg();
        StageCoreInfo(
            coreDll: CoreDll,
            displayName: CoreDisplayName);
        StageCoreXml(("3-Button", "257"));
        StageCoreCfg(SelectDevice("257"));
        // Canonical, no-op swap — just so a game-level cfg file exists in the cascade.
        StageGameCfg(CfgSwap("a", "1"));

        InputMappingConfig? result = Build().Load(
            Game(
                emulatorPath: RetroArchExe,
                romDirectory: RomDir,
                retroArchCore: CoreDll),
            GenesisPlatform());

        result.ShouldNotBeNull();
        result.Controller.ShouldBe("3-Button");
    }

    [Fact]
    public void Load_NoVariantSelected_FallsBackToPlatformDefault()
    {
        // Game-level cfg has only a swap, no variant override. With no variant chosen, the base
        // is the platform default (6-Button). Swap is still applied on top.
        StageRetroArchCfg();
        StageCoreInfo(
            coreDll: CoreDll,
            displayName: CoreDisplayName);
        StageCoreXml(
            ("6-Button", "513"),
            ("3-Button", "257"));
        // Swap: slot "a" canonical btn is 1; set it to 0 (slot "b") → b↔a swap detected.
        StageGameCfg(CfgSwap("a", "0"));

        InputMappingConfig? result = Build().Load(
            Game(
                emulatorPath: RetroArchExe,
                romDirectory: RomDir,
                retroArchCore: CoreDll),
            GenesisPlatform());

        result.ShouldNotBeNull();
        result.Controller.ShouldBe("6-Button");
    }

    // ---- swap composition and trust boundary ----

    [Fact]
    public void Load_GameLevelCfgSwap_Applied_ContentDirCfgSwap_Ignored()
    {
        // trust boundary: only the game-level cfg contributes swaps; non-game-level
        // entries are assumed already baked into Controllers.xml. Stage two different swaps and
        // verify only the game-level one shows up.
        StageRetroArchCfg();
        StageCoreInfo(
            coreDll: CoreDll,
            displayName: CoreDisplayName);
        StageCoreXml(
            ("6-Button", "513"),
            ("3-Button", "257"));
        // Game-level: slot "y" → coreId 1 (canonical for "y") then change to 9 (slot "x") so we
        // get a y↔x swap.
        StageGameCfg(CfgSwap("y", "3"));  // y's btn now 3 → slot "x"
        // ContentDir-level: would be a b↔a swap if applied.
        StageContentDirCfg(CfgSwap("b", "1"));

        InputMappingConfig? result = Build().Load(
            Game(
                emulatorPath: RetroArchExe,
                romDirectory: RomDir,
                retroArchCore: CoreDll),
            GenesisPlatform());

        result.ShouldNotBeNull();
        // game-level applied: slot y now drives slot-x's coreId. Baseline had "A" at ButtonX —
        // that entry leaves; Y's name is duplicated onto ButtonX (Y's original ButtonY survives).
        result.Mappings.ShouldNotContainEntry("A", "ButtonX");
        result.Mappings.ShouldContainEntry("Y", "ButtonX");
        result.Mappings.ShouldContainEntry("Y", "ButtonY");
        // ignored contentDir-level swap would have moved B off ButtonA; verify it didn't.
        result.Mappings.ShouldContainEntry("B", "ButtonA");
        result.Mappings.ShouldContainEntry("C", "ButtonB");
    }

    [Fact]
    public void Load_CfgSwapAndRmpSwap_Compose_RmpWinsOnConflict()
    {
        // cfg swap is applied first (it's the natural baseline rmp operates on), then rmp swap
        // overlays on top. Stage both swapping the same slot; rmp's coreId target wins.
        StageRetroArchCfg();
        StageCoreInfo(
            coreDll: CoreDll,
            displayName: CoreDisplayName);
        StageCoreXml(
            ("6-Button", "513"),
            ("3-Button", "257"));
        // cfg: y → slot "x" (coreId 9 in slot terms; physical btn 3 detected as non-canonical).
        StageGameCfg(CfgSwap("y", "3"));
        // rmp: y → slot "b" (coreId 0).
        StageGameRmp(RmpSwap("y", "0"));

        InputMappingConfig? result = Build().Load(
            Game(
                emulatorPath: RetroArchExe,
                romDirectory: RomDir,
                retroArchCore: CoreDll),
            GenesisPlatform());

        result.ShouldNotBeNull();
        // cfg applies first (swaps[y] = slot-x). State after cfg:
        //   A removed; { Name = "Y", Input = "ButtonX" } added.
        // rmp applies on top (swaps[y] = slot-b). It removes the slot-y source (ButtonX) again,
        // displacing cfg's "Y at ButtonX" addition, and re-adds with B's name (from ButtonA).
        // Final at ButtonX: B (rmp's contribution), not Y (cfg's contribution that got overridden).
        result.Mappings.ShouldContainEntry("B", "ButtonX");
        result.Mappings.ShouldNotContainEntry("Y", "ButtonX");
        result.Mappings.ShouldNotContainEntry("A", "ButtonX");
    }

    // ---- root-directory detection ----

    [Fact]
    public void Load_NonPortableMode_ResolvesCfgFromAppDataPath()
    {
        // No retroarch.cfg next to the exe → RetroArchCfgLoader treats this as a non-portable
        // install and reads from {appData}/RetroArch/. Stage the cfg there and verify the
        // variant pick still flows through. The remap loader still walks {retroArchDir}/config
        // — that boundary doesn't change.
        StageCoreInfo(
            coreDll: CoreDll,
            displayName: CoreDisplayName);
        StageCoreXml(("3-Button", "257"));
        StageGameCfg(SelectDevice("257"), root: Path.Combine(AppDataRoot, "RetroArch"));

        InputMappingConfig? result = Build().Load(
            Game(
                emulatorPath: RetroArchExe,
                romDirectory: RomDir,
                retroArchCore: CoreDll),
            GenesisPlatform());

        result.ShouldNotBeNull();
        result.Controller.ShouldBe("3-Button");
    }

    [Fact]
    public void Load_CoreInfoDisplayName_DrivesConfigSubfolderAndPerCoreXml()
    {
        // RetroArchCoreInfo reads `corename = "..."` from the info file; that display name (not
        // the DLL name) is then used both for the {rootDir}/RetroArch/{name}.xml lookup AND
        // for the config/{name}/{rom}.cfg lookup. Stage everything under "Genesis Plus GX" (not
        // "genesis_plus_gx_libretro") and verify the cfg's variant override resolves end-to-end.
        StageRetroArchCfg();
        StageCoreInfo(
            coreDll: CoreDll,
            displayName: CoreDisplayName);
        StageCoreXml(("3-Button", "257"));
        StageGameCfg(SelectDevice("257"));

        InputMappingConfig? result = Build().Load(
            Game(
                emulatorPath: RetroArchExe,
                romDirectory: RomDir,
                retroArchCore: CoreDll),
            GenesisPlatform());

        result.ShouldNotBeNull();
        result.Controller.ShouldBe("3-Button");
    }

    // ---- interaction scenario ----

    [Fact]
    public void Scenario_PortableMode_DisplayName_CfgCascadeVariant_AndGameRmpSwap()
    {
        // Multi-feature composition: portable installation, core display name resolved via .info,
        // variant override sitting at the *core* cfg level (cascade walk has to reach it),
        // game-level cfg adds a swap, game-level rmp adds another swap on top.
        StageRetroArchCfg();
        StageCoreInfo(
            coreDll: CoreDll,
            displayName: CoreDisplayName);
        StageCoreXml(("6-Button", "513"));
        // Variant override is at the core-level cfg, not game-level — cascade walk has to find it.
        StageCoreCfg(SelectDevice("513"));
        // Game-level cfg: y→slot-x swap (physical btn 3).
        StageGameCfg(CfgSwap("y", "3"));
        // Game-level rmp: b→slot-a (coreId 8).
        StageGameRmp(RmpSwap("b", "8"));

        InputMappingConfig? result = Build().Load(
            Game(
                emulatorPath: RetroArchExe,
                romDirectory: RomDir,
                retroArchCore: CoreDll),
            GenesisPlatform());

        result.ShouldNotBeNull();
        // (1) variant resolved from core-level cfg, threaded to controller name
        result.Controller.ShouldBe("6-Button");
        // (2) cfg swap effect (y → slot-x): "A" leaves ButtonX; "Y" is duplicated onto ButtonX
        result.Mappings.ShouldNotContainEntry("A", "ButtonX");
        result.Mappings.ShouldContainEntry("Y", "ButtonX");
        // (3) rmp swap effect (b → slot-a): "B" leaves ButtonA; "C" is duplicated onto ButtonA
        result.Mappings.ShouldNotContainEntry("B", "ButtonA");
        result.Mappings.ShouldContainEntry("C", "ButtonA");
    }

    // ---- user-layer override ----

    [Fact]
    public void Load_UserCoreFilePresent_WinsOverDefaultsFile()
    {
        // Defaults maps device id=513 to "6-Button"; User maps the same id to "3-Button".
        // GenesisPlatform() knows both names, so whichever file wins determines the controller.
        // (Id 1 = RETRO_DEVICE_JOYPAD default and is treated as no-override; use 513 instead.)
        StageRetroArchCfg();
        StageCoreInfo(CoreDll, CoreDisplayName);
        _dc.WriteRetroArchCore(CoreDisplayName, """
            <RetroArchCore>
              <Controllers>
                <Controller name="6-Button"><Retropad id="513" /></Controller>
              </Controllers>
            </RetroArchCore>
            """);
        _dc.WriteUserRetroArchCore(CoreDisplayName, """
            <RetroArchCore>
              <Controllers>
                <Controller name="3-Button"><Retropad id="513" /></Controller>
              </Controllers>
            </RetroArchCore>
            """);
        StageGameCfg(SelectDevice("513"));

        InputMappingConfig? result = Build().Load(
            Game(emulatorPath: RetroArchExe, romDirectory: RomDir, retroArchCore: CoreDll),
            GenesisPlatform());

        // The User core file wins — "3-Button", not the Defaults "6-Button"
        result.ShouldNotBeNull();
        result.Controller.ShouldBe("3-Button");
    }

    // ---- stubs ----

    private sealed class FakeApplicationData(string path) : IApplicationData
    {
        public string Path { get; } = path;
    }
}
