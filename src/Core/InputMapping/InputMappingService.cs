namespace DynamicControls.InputMapping;

/// <summary>
/// Resolves input mappings for a game by walking a chain of providers (per-game XML, RetroArch,
/// MAME, the platform default) and taking what the first one to answer supplies: the controller in
/// play and any remap of it. The resulting <see cref="ResolvedMapping"/>'s <c>Natural*</c> maps
/// reflect the controller before the remap, so callers can still detect one.
/// </summary>
public interface IInputMappingService
{
    /// <summary>
    /// Loads the resolved input mapping for <paramref name="game"/> from the first provider that
    /// answers.
    /// </summary>
    ResolvedMapping Load(GameInfo game);
}

/// <summary>
/// Production implementation: orchestrates the pipeline (load platform → resolve providers →
/// resolve natural and current → re-splice naturals). Provider iteration lives on
/// <see cref="IInputMappingProviders"/>; raw → resolved conversion lives on
/// <see cref="IInputMappingResolver"/>; the service just sequences them and re-splices the
/// natural state's <c>Natural*</c> maps via <c>with</c> when the provider reports a remap.
/// </summary>
public class InputMappingService(
    IInputMappingLoader loader,
    IInputMappingResolver resolver,
    IInputMappingProviders providers,
    ControlResolver controlResolver) : IInputMappingService
{
    private readonly IInputMappingLoader _loader = loader;
    private readonly IInputMappingResolver _resolver = resolver;
    private readonly IInputMappingProviders _providers = providers;
    private readonly ControlResolver _controlResolver = controlResolver;

    /// <inheritdoc />
    public ResolvedMapping Load(GameInfo game)
    {
        PlatformControllersConfig? platform = _loader.LoadPlatformMapping(game.Platform);
        ProvidedMapping provided = _providers.Resolve(game, platform);
        // Both sides name the same controller (IInputMappingProvider.Load), so either will do.
        string? controller = provided.Current.Controller;

        ResolvedMapping mapping = _resolver.Resolve(game.Platform, provided.Current, controller);
        if (!ReferenceEquals(provided.Natural, provided.Current))
        {
            // A remap shuffles which action a physical button drives; the natural state is still
            // the controller as declared, so overwrite the snapshot with its reverse. That is what
            // InputImageResolver compares against to tell a button the remap moved from one in its
            // usual place.
            ResolvedMapping natural = _resolver.Resolve(game.Platform, provided.Natural, controller);
            mapping = mapping with
            {
                NaturalInputToButton = natural.InputToButton,
                NaturalButtonToInput = natural.ButtonToInput,
            };
        }

        // A remap comes from an emulator's config (or a file standing in for one), which binds
        // individual directions and has no way to name a control as a unit. So a control — a
        // button that does name one — is read last, against wherever its members actually ended up.
        return ResolveControls(mapping, platform, controller);
    }

    /// <summary>Applies the selected controller's own controls, if it declares any.</summary>
    private ResolvedMapping ResolveControls(
        ResolvedMapping mapping, PlatformControllersConfig? platform, string? controllerName)
    {
        ControllerConfig? controller = platform?.Resolve(controllerName);
        return _controlResolver.Apply(mapping, controller?.Controls ?? []);
    }
}
