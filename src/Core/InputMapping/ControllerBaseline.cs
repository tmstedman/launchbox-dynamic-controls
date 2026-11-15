namespace DynamicControls.InputMapping;

/// <summary>
/// A platform controller as an <see cref="InputMappingConfig"/>: what a provider starts from before
/// it applies anything of its own. Copies the mapping list so a provider can edit its own without
/// reaching the platform's.
/// </summary>
public static class ControllerBaseline
{
    public static InputMappingConfig Of(ControllerConfig controller) => new()
    {
        Controller = controller.Name,
        AnalogToDigital = controller.AnalogToDigital,
        Mappings = [.. controller.Mappings],
    };

    /// <summary>The platform's default controller, or an empty mapping when the platform declares
    /// none — the floor an emulator's own bindings are then laid onto.</summary>
    public static InputMappingConfig Default(PlatformControllersConfig? platform) =>
        platform?.Resolve(null) is { } controller ? Of(controller) : new InputMappingConfig();
}
