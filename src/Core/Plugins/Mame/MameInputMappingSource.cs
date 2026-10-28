using DynamicControls.Config;
using DynamicControls.InputMapping;

namespace DynamicControls.Plugins.Mame;

/// <summary>
/// Input mapping transform backed by MAME configuration files. Reads per-game cfg first,
/// then falls back to default.cfg. Translates JOYCODE values into generic input names and
/// produces a new InputMappingConfig by applying those overrides on top of the baseline
/// supplied by the source pipeline (per-game XML, platform default, ...).
/// </summary>
public class MameInputMappingSource(
    ILogger logger,
    IMameCfgLoader cfgLoader) : IInputMappingTransform
{
    private readonly ILogger _logger = logger;
    private readonly IMameCfgLoader _cfgLoader = cfgLoader;

    public bool IsEnabled(GlobalConfig config) => config.EnableMame;

    public InputMappingConfig? Transform(GameInfo game, InputMappingConfig baseline)
    {
        if (game.EmulatorPath == null) return null;
        if (!MameEmulator.IsMameExecutable(game.EmulatorPath)) return null;

        string cfgDir = Path.Combine(
            Path.GetDirectoryName(game.EmulatorPath)!, "cfg");

        IReadOnlyList<MamePortReach>? overrides = null;
        if (game.RomName != null)
            overrides = _cfgLoader.Load(Path.Combine(cfgDir, game.RomName + ".cfg"));

        if (overrides == null || overrides.Count == 0)
            overrides = _cfgLoader.Load(Path.Combine(cfgDir, "default.cfg"));

        if (overrides == null || overrides.Count == 0)
        {
            _logger.Debug("No MAME config overrides found");
            return null;
        }

        _logger.Debug($"MAME config applied: {overrides.Count} overrides");
        return Merge(baseline, overrides);
    }

    /// <summary>
    /// Produces a new InputMappingConfig that is the baseline with the given overrides applied.
    /// Entries not in <paramref name="overrides"/> are copied verbatim; matching entries are
    /// replaced with one MappingEntry per overridden generic input (a single MAME port can drive
    /// multiple generic inputs when its sequence lists multiple JOYCODEs). Overrides whose Name
    /// is not in the baseline are appended. Carries the baseline's Controller through — MAME cfg
    /// shuffles which button drives which action, it does not change which controller is in play.
    /// </summary>
    private static InputMappingConfig Merge(
        InputMappingConfig baseline,
        IReadOnlyList<MamePortReach> overrides)
    {
        // The later of two same-named buttons stands, in the earlier's place. Nothing promises the
        // loader de-duplicates, and a throw here would be swallowed into an empty overlay.
        var byName = new Dictionary<string, MamePortReach>();
        foreach (MamePortReach reach in overrides) byName[reach.Name] = reach;
        var mappings = new List<MappingEntry>();
        var expandedOverrides = new HashSet<string>();
        var baselineNames = new HashSet<string>();

        foreach (MappingEntry entry in baseline.Mappings)
        {
            baselineNames.Add(entry.Name);
            if (byName.TryGetValue(entry.Name, out MamePortReach? reach))
            {
                if (expandedOverrides.Add(entry.Name))
                {
                    mappings.AddRange(Entries(reach));
                }
            }
            else
            {
                mappings.Add(new MappingEntry { Name = entry.Name, Input = entry.Input });
            }
        }

        foreach (MamePortReach reach in byName.Values)
        {
            if (baselineNames.Contains(reach.Name)) continue;
            mappings.AddRange(Entries(reach));
        }

        var aliases = new Dictionary<string, string>();
        foreach (MamePortReach reach in byName.Values)
        {
            foreach (string alias in reach.AlsoNamed) aliases[alias] = reach.Name;
        }

        return new InputMappingConfig
        {
            Controller = baseline.Controller,
            AnalogToDigital = baseline.AnalogToDigital,
            Mappings = mappings,
            ButtonAliases = aliases,
        };
    }

    private static IEnumerable<MappingEntry> Entries(MamePortReach reach) =>
        reach.Own.Select(input => new MappingEntry { Name = reach.Name, Input = input })
            .Concat(reach.Derived.Select(input => new MappingEntry { Name = reach.Name, Input = input, Derived = true }));
}
