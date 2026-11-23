using DynamicControls.InputMapping;

namespace DynamicControls.Core.TestHelpers.InputMapping;

/// <summary>
/// Factory helpers for input-mapping test data. Pass only what the test cares about; defaults
/// are intentionally empty/neutral. Use via
/// <c>using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;</c>.
/// </summary>
public static class InputMappingFixtures
{
    public const string Platform = "Sega Genesis";

    public static GameInfo Game(
        string platform = Platform,
        string romName = "OutRun",
        string? cloneOf = null,
        int? launchBoxId = null,
        string? emulatorPath = null,
        string? romDirectory = null,
        string? retroArchCore = null) =>
        new(Platform: platform, RomName: romName, CloneOf: cloneOf, LaunchBoxId: launchBoxId,
            EmulatorPath: emulatorPath, RomDirectory: romDirectory, RetroArchCore: retroArchCore);

    public static InputMappingConfig MappingConfig(
        string? controller = null,
        AnalogToDigitalMode? analogToDigital = null,
        (string Name, string Input)[]? mappings = null,
        string[]? unmaps = null) => new()
        {
            Controller = controller,
            AnalogToDigital = analogToDigital,
            Mappings = [.. (mappings ?? []).Select(m => new MappingEntry { Name = m.Name, Input = m.Input })],
            Unmaps = [.. unmaps ?? []],
        };

    public static PlatformControllersConfig PlatformConfig(params ControllerConfig[] controllers) => new()
    {
        Controllers = [.. controllers],
    };

    /// <summary>The platform with <paramref name="games"/> as its &lt;Game&gt; entries.</summary>
    public static PlatformControllersConfig WithGames(this PlatformControllersConfig platform, params GameMappingConfig[] games) =>
        platform with { Games = [.. games] };

    /// <summary>A &lt;Game&gt; entry, keyed by <see cref="Game"/>'s default ROM name unless told
    /// otherwise. Add it to a platform with <see cref="WithGames"/>.</summary>
    public static GameMappingConfig GameEntry(
        string? romName = "OutRun",
        int? launchBoxId = null,
        string? inheritFrom = null,
        AnalogToDigitalMode? analogToDigital = null,
        (string Name, string Input)[]? mappings = null,
        string[]? unmaps = null) => new()
        {
            RomName = romName,
            LaunchBoxId = launchBoxId,
            InheritFrom = inheritFrom,
            AnalogToDigital = analogToDigital,
            Mappings = [.. (mappings ?? []).Select(m => new MappingEntry { Name = m.Name, Input = m.Input })],
            Unmaps = [.. unmaps ?? []],
        };

    public static ControllerConfig ControllerDef(
        string name,
        bool isDefault = false,
        AnalogToDigitalMode? analogToDigital = null,
        (string Name, string Input)[]? mappings = null) => new()
        {
            Name = name,
            IsDefault = isDefault,
            AnalogToDigital = analogToDigital,
            Mappings = [.. (mappings ?? []).Select(m => new MappingEntry { Name = m.Name, Input = m.Input })],
        };
}
