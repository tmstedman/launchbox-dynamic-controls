using System.Diagnostics.CodeAnalysis;

namespace DynamicControls.InputMapping;

/// <summary>
/// The result of loading an input mapping. Maps platform button names (e.g. "Cross") to generic
/// input names (e.g. "ButtonA"). Built once per game launch by <see cref="InputMappingService"/>
/// and read by the rendering pipeline; never mutated after construction.
/// </summary>
/// <param name="Platform">The platform name this mapping belongs to.</param>
/// <param name="Controller">The name of the &lt;Controller&gt; selected from the platform's
/// Controllers.xml for this game. Null when no platform controllers file exists. Used as the
/// most-specific tier in the image-resolution chain
/// (<c>Templates/{template}/{platform}/{controller}/</c>) so games using a non-default controller
/// pick up controller-specific artwork.</param>
/// <param name="ButtonToInput">Maps platform button names to the generic input names they
/// trigger. A platform button can drive multiple generic inputs (e.g. when AnalogToDigital is
/// true, a Dpad button drives both the Dpad and the left stick).
/// e.g. <c>{ "Dpad-Up" -> ["ButtonDpadUp", "AxisLeftStickUp"] }</c></param>
/// <param name="InputToButton">Reverse lookup: the primary platform button that drives each
/// generic input. Built from ButtonToInput at construction time; if multiple platform buttons
/// drive the same generic, the first one encountered wins (natural mappings precede mirror
/// entries). e.g. <c>{ "ButtonA" -> "Cross" }</c></param>
/// <param name="NaturalButtonToInput">Snapshot of ButtonToInput from the selected controller's
/// natural mapping, taken before game-specific transforms run. Used by InputImageResolver to
/// detect when a platform button has been remapped to drive a different input than it does by
/// default on this controller.</param>
/// <param name="NaturalInputToButton">Snapshot of InputToButton from the selected controller's
/// natural mapping (plus any AnalogToDigital mirror), taken before game-specific transforms
/// run. Lets consumers tell whether a generic input's natural physical button still exists in
/// the current mapping, even if it has been remapped to drive a different action.</param>
/// <param name="AnalogToDigital">Which stick (if any) the Dpad is mirrored onto for this
/// game/platform. Resolved with game-XML-overrides-platform inheritance; null when no mirroring
/// applies.</param>
[ExcludeFromCodeCoverage]
public record ResolvedMapping(
    string Platform,
    string? Controller,
    IReadOnlyDictionary<string, IReadOnlyList<string>> ButtonToInput,
    IReadOnlyDictionary<string, string> InputToButton,
    IReadOnlyDictionary<string, IReadOnlyList<string>> NaturalButtonToInput,
    IReadOnlyDictionary<string, string> NaturalInputToButton,
    AnalogToDigitalMode? AnalogToDigital)
{
    /// <summary>
    /// For each platform button, the inputs in its <see cref="ButtonToInput"/> list it reaches
    /// only because something else does — an <c>analogToDigital</c> mirror, a control reaching
    /// what its members reach, a nudge reaching its half of an axis its port is bound to. Every
    /// other input in the list is the button's own binding. A button with nothing derived has
    /// no entry.
    ///
    /// <para>This is what decides between two labelled buttons reaching one input: an own
    /// binding is the stronger claim. It used to be read off each input's position in the list,
    /// on the grounds that derived inputs are appended after a button's own, but a button with
    /// two bindings of its own (both halves of an axis) then had its second look as weak as a
    /// derived one, and which half of the stick kept its label came down to declaration
    /// order.</para>
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> DerivedButtonToInput { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>
    /// Other names a label may call a platform button by, each mapped to the button in
    /// <see cref="ButtonToInput"/> it means — a MAME port's type, tag and mask where its button
    /// goes by the bare type. Read only when labels are matched to buttons; the button itself is
    /// one button, so nothing else sees a second name.
    /// </summary>
    public IReadOnlyDictionary<string, string> ButtonAliases { get; init; } = new Dictionary<string, string>();
}
