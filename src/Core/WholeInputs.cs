namespace DynamicControls;

/// <summary>
/// The generic inputs that are whole controls, each with the four direction inputs that make it
/// up. Fixed vocabulary: these names are the plugin's own, not platform data. Shared across
/// subsystems — <see cref="DynamicControls.InputMapping.WholeInputDeriver"/> (which extends a
/// whole-naming platform button onto whichever individual directions its siblings currently
/// reach), <c>InputLabelsService</c>'s final collapse pass (which folds four agreeing direction
/// labels back onto their whole), and <c>VisibilityEvaluator</c>'s render-time fan-out — so it
/// lives at the Core root rather than under any one subsystem's folder, read by whichever end of
/// the pipeline needs it.
/// </summary>
public static class WholeInputs
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> PartsOf =
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["ButtonDpad"] = ["ButtonDpadUp", "ButtonDpadDown", "ButtonDpadLeft", "ButtonDpadRight"],
            ["AxisLeftStick"] = ["AxisLeftStickUp", "AxisLeftStickDown", "AxisLeftStickLeft", "AxisLeftStickRight"],
            ["AxisRightStick"] = ["AxisRightStickUp", "AxisRightStickDown", "AxisRightStickLeft", "AxisRightStickRight"],
        };
}
