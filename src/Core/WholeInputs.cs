namespace DynamicControls;

/// <summary>
/// The generic inputs that are whole controls, each with the four direction inputs that make it
/// up. Fixed vocabulary: these names are the plugin's own, not platform data.
///
/// <para>The last place in Core that holds this vocabulary, and read by exactly one caller:
/// <c>InputLabelsService.CollapseWholeDirections</c>, which folds agreeing direction labels back
/// onto their whole. The mapping side now takes the relationship from the platform's own
/// <c>&lt;Alias&gt;</c> declarations (<see cref="DynamicControls.InputMapping.AliasResolver"/>)
/// and the render side from the template's <c>&lt;Whole&gt;</c> ones
/// (<c>Templates.WholeNode</c>), neither of which needs a name Core knows in advance.</para>
///
/// <para>Retiring this one is harder than it looks, and <c>&lt;Alias&gt;</c> cannot do it. An
/// alias groups <em>platform buttons</em>; what this pass needs is a relation between
/// <em>generic inputs</em> — that <c>AxisLeftStick</c> is the whole of <c>AxisLeftStickUp</c> and
/// its three siblings. Resolving an alias's members through <c>ButtonToInput</c> yields the parts
/// set but never the whole's own generic name, since the alias reaches the union of its members
/// and nothing else; recovering it would mean stripping Up/Down/Left/Right suffixes, the kind of
/// name-parsing the rest of the pipeline deliberately avoids. The generic whole is a real mapping
/// target in its own right, not a synthetic one — Sega Naomi binds <c>JOYSTICK</c> to
/// <c>AxisLeftStick</c> and Nintendo DS binds <c>MoveTouchStick</c> to <c>AxisRightStick</c>.</para>
///
/// <para>The declaration that does state it exactly is the template's <c>&lt;Whole&gt;</c>, and
/// this pass runs before a template is chosen. So retiring the table needs either the labels pass
/// to run with a template in hand, or a new platform-independent place to declare the same
/// relation — which is what this type already is. Worth leaving alone until something other than
/// tidiness asks for it.</para>
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
