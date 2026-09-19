namespace DynamicControls.InputMapping;

/// <summary>
/// Resolves input mappings for a game by walking a chain of registered sources (per-game XML,
/// RetroArch, ...) to pick a complete mapping, falling back to an empty mapping when no source
/// applies. Transforms (MAME cfg) are then applied on top of whichever mapping was selected;
/// when a transform applies, the resulting <see cref="ResolvedMapping"/>'s <c>Natural*</c> maps
/// reflect the pre-transform baseline so callers can still detect remaps.
/// </summary>
public interface IInputMappingService
{
    /// <summary>
    /// Loads the resolved input mapping for <paramref name="game"/> by selecting the first
    /// applicable source, then applying the first applicable transform on top.
    /// </summary>
    ResolvedMapping Load(GameInfo game);
}

/// <summary>
/// Production implementation: orchestrates the pipeline (load platform → select source → resolve
/// → select transform → re-splice naturals). Plugin iteration lives on
/// <see cref="IInputMappingPlugins"/>; raw → resolved conversion lives on
/// <see cref="IInputMappingResolver"/>; the service just sequences them and re-splices the
/// baseline's <c>Natural*</c> maps via <c>with</c> when a transform applies.
/// </summary>
public class InputMappingService(
    IInputMappingLoader loader,
    IInputMappingResolver resolver,
    IInputMappingPlugins plugins,
    WholeInputDeriver deriver) : IInputMappingService
{
    private readonly IInputMappingLoader _loader = loader;
    private readonly IInputMappingResolver _resolver = resolver;
    private readonly IInputMappingPlugins _plugins = plugins;
    private readonly WholeInputDeriver _deriver = deriver;

    /// <inheritdoc />
    public ResolvedMapping Load(GameInfo game)
    {
        PlatformControllersConfig? platform = _loader.LoadPlatformMapping(game.Platform);
        InputMappingConfig baseline = _plugins.ResolveBaseline(game, platform);
        InputMappingConfig? transformed = _plugins.ApplyTransform(game, baseline);

        if (transformed == null)
            return _resolver.Resolve(game.Platform, baseline, baseline.Controller);

        // Transforms shuffle which action a physical button drives; the source baseline is still
        // the natural state, so overwrite the snapshot with the baseline's reverse. That keeps a
        // button visible when its action has been remapped away (e.g. ButtonB after MAME swaps
        // BUTTON2 onto ButtonA).
        ResolvedMapping baselineMapping = _resolver.Resolve(
            game.Platform,
            baseline,
            baseline.Controller);
        ResolvedMapping transformedMapping = _resolver.Resolve(
            game.Platform,
            transformed,
            transformed.Controller ?? baseline.Controller);

        ResolvedMapping spliced = transformedMapping with
        {
            NaturalInputToButton = baselineMapping.InputToButton,
            NaturalButtonToInput = baselineMapping.ButtonToInput,
        };

        // A transform came from an emulator's config, which binds individual directions and has
        // no way to name a whole control. So a whole-input button (JOYSTICK, Dpad-Any) is left
        // behind wherever its directions were moved to; follow it onto them. The naturals spliced
        // in above are what the derivation reads the whole-and-parts pairing from, so this runs
        // after that and not before.
        return _deriver.Derive(spliced);
    }
}
