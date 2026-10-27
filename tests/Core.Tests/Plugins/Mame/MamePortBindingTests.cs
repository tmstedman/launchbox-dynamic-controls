using DynamicControls.Plugins.Mame;

namespace DynamicControls.Core.Tests.Plugins.Mame;

/// <summary>
/// Unit tests for <see cref="MamePortBinding"/>: what one MAME port reaches, given the sequences
/// it declares. Each sequence becomes its own platform button -- the port's own name for
/// <c>standard</c>, <c>.inc</c>/<c>.dec</c> for the nudges -- and each button's reach is split
/// into what it binds and what it only derives from the port's axis. No XML and no filesystem:
/// the sequences are given directly, against one fixed joycode vocabulary.
/// </summary>
public class MamePortBindingTests
{
    private const string Port = "P1_DIAL";

    private static readonly JoycodeMapping Joycodes = new(new Dictionary<string, List<JoycodeTarget>>
    {
        ["JOYCODE_1_BUTTON1"] = [new("ButtonA", null)],
        ["JOYCODE_1_BUTTON2"] = [new("ButtonB", null)],
        ["JOYCODE_1_BUTTON3"] = [new("ButtonC", null)],
        ["JOYCODE_1_ZAXIS"] = [new("AxisRightStickLeft", "dec"), new("AxisRightStickRight", "inc")],
        ["JOYCODE_1_ZAXIS_NEG_SWITCH"] = [new("AxisRightStickLeft", null)],
        ["JOYCODE_1_ZAXIS_POS_SWITCH"] = [new("AxisRightStickRight", null)],
        ["JOYCODE_1_XAXIS_LEFT_SWITCH"] = [new("AxisLeftStickLeft", null)],
        ["JOYCODE_1_XAXIS_RIGHT_SWITCH"] = [new("AxisLeftStickRight", null)],
        ["JOYCODE_1_SLIDER1_NEG_SWITCH"] = [new("AxisTriggerLeft", null)],
        ["JOYCODE_1_SLIDER2_NEG_SWITCH"] = [new("AxisTriggerRight", null)],
        ["JOYCODE_1_HAT1UP"] = [new("ButtonDpadUp", null)],
        ["JOYCODE_1_YAXIS_UP_SWITCH"] = [new("AxisLeftStickUp", null)],
    });

    /// <summary>
    /// Resolves <see cref="Port"/> with the given sequences. A null sequence is one the port
    /// doesn't declare at all; <c>"NONE"</c> is one it declares and leaves unbound.
    /// </summary>
    private static IReadOnlyList<MamePortReach> Resolve(
        string? standard = null, string? increment = null, string? decrement = null, string port = Port)
    {
        var declared = new Dictionary<string, MameSequence>();
        if (standard != null) declared["standard"] = MameSequence.Parse(standard);
        if (increment != null) declared["increment"] = MameSequence.Parse(increment);
        if (decrement != null) declared["decrement"] = MameSequence.Parse(decrement);
        return MamePortBinding.Resolve(port, declared, Joycodes);
    }

    private static Dictionary<string, IReadOnlyList<string>> Inputs(IReadOnlyList<MamePortReach> reach) =>
        reach.ToDictionary(r => r.Name, r => r.Inputs);

    private static Dictionary<string, IReadOnlyList<string>> Derived(IReadOnlyList<MamePortReach> reach) =>
        reach.Where(r => r.Derived.Count > 0).ToDictionary(r => r.Name, r => r.Derived);

    // ---- one button per sequence ----

    [Fact]
    public void StandardOnly_ReachesUnderThePortsOwnName()
    {
        // an ordinary button declares no nudges, so it never grows a .inc/.dec name
        Inputs(Resolve(standard: "JOYCODE_1_BUTTON1")).ShouldBeDictionaryOf((Port, ["ButtonA"]));
    }

    [Fact]
    public void OrChainedJoycodes_ReachEveryInputInSourceOrder()
    {
        // MAME's alternative-binding syntax: every physical button is marked
        Inputs(Resolve(standard: "JOYCODE_1_BUTTON2 OR JOYCODE_1_BUTTON1"))
            .ShouldBeDictionaryOf((Port, ["ButtonB", "ButtonA"]));
    }

    [Fact]
    public void StandardNone_IncrementAndDecrementBound_UsesBoth()
    {
        // the adillor/spacwalk shape: a true analogue control with no analogue hardware assigned,
        // driven instead by two ordinary buttons nudging it each way. The port's own name is
        // stated empty -- it is bound, but not to anything analogue, so Controllers.xml's baseline
        // guess at the axis must not survive the merge
        Inputs(Resolve(standard: "NONE", increment: "JOYCODE_1_BUTTON2", decrement: "JOYCODE_1_BUTTON3"))
            .ShouldBeDictionaryOf(
                (Port, []),
                (Port + ".inc", ["ButtonB"]),
                (Port + ".dec", ["ButtonC"]));
    }

    [Fact]
    public void StandardAndIncrementBothBound_EachKeepsItsOwnName()
    {
        // real analogue hardware AND a digital fallback for one direction; the undeclared
        // decrement gets no name at all
        Inputs(Resolve(standard: "JOYCODE_1_BUTTON1", increment: "JOYCODE_1_BUTTON2"))
            .ShouldBeDictionaryOf(
                (Port, ["ButtonA"]),
                (Port + ".inc", ["ButtonB"]));
    }

    [Fact]
    public void SameInputFromStandardAndIncrement_BothRecordIt()
    {
        // separate buttons now, so each records it -- the duplicate that mattered was within one
        // button's list, and a <Control> over the pair dedups what the port reaches
        Inputs(Resolve(standard: "JOYCODE_1_BUTTON1", increment: "JOYCODE_1_BUTTON1"))
            .ShouldBeDictionaryOf(
                (Port, ["ButtonA"]),
                (Port + ".inc", ["ButtonA"]));
    }

    [Fact]
    public void AllThreeSequencesBound_SplitIntoThreeButtons()
    {
        // the airco22b shape: a throttle read from an analogue axis, nudged by two separate
        // controls -- the triggers, not the axis. Each carries its own label in the artwork
        Inputs(Resolve(standard: "JOYCODE_1_BUTTON1", increment: "JOYCODE_1_BUTTON2", decrement: "JOYCODE_1_BUTTON3"))
            .ShouldBeDictionaryOf(
                (Port, ["ButtonA"]),
                (Port + ".inc", ["ButtonB"]),
                (Port + ".dec", ["ButtonC"]));
    }

    [Fact]
    public void NothingTranslates_ReachesNothingAtAll()
    {
        // every sequence is bound but none translates -- the port must not even state its name
        // empty, which would drop a baseline for no reason
        Resolve(increment: "JOYCODE_1_UNKNOWN", decrement: "JOYCODE_1_ALSOUNKNOWN").ShouldBeEmpty();
    }

    // ---- a stick nudging both ways is the port's axis ----

    [Fact]
    public void NudgesHoldOppositeEndsOfOneAxis_ThePairIsThePortsOwn()
    {
        // downtown's shape: the stick turns the rotary by its two switches rather than its axis,
        // so it keeps turning while held, and the triggers nudge it too. Opposite ends of one
        // axis, one in each nudge, are a stick standing in for the port's analogue binding --
        // what tdfever states directly by binding the axis to standard -- so they are the port's
        // own reach. The nudges still reach their half of it, so a game labelling only the halves
        // keeps its stick labels, but only as a derived claim the port's own label outranks.
        IReadOnlyList<MamePortReach> reach = Resolve(
            standard: "NONE",
            increment: "JOYCODE_1_ZAXIS_POS_SWITCH OR JOYCODE_1_SLIDER2_NEG_SWITCH",
            decrement: "JOYCODE_1_ZAXIS_NEG_SWITCH OR JOYCODE_1_SLIDER1_NEG_SWITCH");

        Inputs(reach).ShouldBeDictionaryOf(
            (Port, ["AxisRightStickRight", "AxisRightStickLeft"]),
            (Port + ".inc", ["AxisTriggerRight", "AxisRightStickRight"]),
            (Port + ".dec", ["AxisTriggerLeft", "AxisRightStickLeft"]));
        Derived(reach).ShouldBeDictionaryOf(
            (Port + ".inc", ["AxisRightStickRight"]),
            (Port + ".dec", ["AxisRightStickLeft"]));
    }

    [Fact]
    public void NudgesHoldOnlyTheAxisPair_ThePortReachesItAndTheNudgesOnlyDerive()
    {
        // the same stick with no triggers alongside: the nudges bind nothing of their own
        IReadOnlyList<MamePortReach> reach = Resolve(
            standard: "NONE", increment: "JOYCODE_1_ZAXIS_POS_SWITCH", decrement: "JOYCODE_1_ZAXIS_NEG_SWITCH");

        Inputs(reach).ShouldBeDictionaryOf(
            (Port, ["AxisRightStickRight", "AxisRightStickLeft"]),
            (Port + ".inc", ["AxisRightStickRight"]),
            (Port + ".dec", ["AxisRightStickLeft"]));
        Derived(reach).ShouldBeDictionaryOf(
            (Port + ".inc", ["AxisRightStickRight"]),
            (Port + ".dec", ["AxisRightStickLeft"]));
    }

    [Fact]
    public void NudgesHoldTheLeftAndRightEndsOfTheXAxis_ThePairIsThePortsOwn()
    {
        // spacwalk's shape, and MAME's default for a nudged dial or paddle: the X axis names its
        // ends LEFT and RIGHT rather than NEG and POS, and is the same stick standing in
        IReadOnlyList<MamePortReach> reach = Resolve(
            standard: "NONE", increment: "JOYCODE_1_XAXIS_RIGHT_SWITCH", decrement: "JOYCODE_1_XAXIS_LEFT_SWITCH");

        Inputs(reach).ShouldBeDictionaryOf(
            (Port, ["AxisLeftStickRight", "AxisLeftStickLeft"]),
            (Port + ".inc", ["AxisLeftStickRight"]),
            (Port + ".dec", ["AxisLeftStickLeft"]));
        Derived(reach).ShouldBeDictionaryOf(
            (Port + ".inc", ["AxisLeftStickRight"]),
            (Port + ".dec", ["AxisLeftStickLeft"]));
    }

    [Fact]
    public void NudgeSwitchesNotOneAxis_StayTheNudgesOwn()
    {
        // two triggers, each a different axis's end: no pair, so nothing moves to the port
        // (which switches pair is MameSequenceTests' concern)
        IReadOnlyList<MamePortReach> reach = Resolve(
            standard: "NONE", increment: "JOYCODE_1_SLIDER2_NEG_SWITCH", decrement: "JOYCODE_1_SLIDER1_NEG_SWITCH");

        Inputs(reach).ShouldBeDictionaryOf(
            (Port, []),
            (Port + ".inc", ["AxisTriggerRight"]),
            (Port + ".dec", ["AxisTriggerLeft"]));
        Derived(reach).ShouldBeEmpty();
    }

    // ---- an unbound nudge still names its half of a bound axis ----

    [Fact]
    public void UnboundNudges_ReadTheirHalfOffTheAxis_AsDerived()
    {
        // bm1stmix's shape: real analogue hardware and no nudge buttons, so each nudge reaches its
        // end of the axis only because the port's own binding does
        IReadOnlyList<MamePortReach> reach = Resolve(standard: "JOYCODE_1_ZAXIS", increment: "NONE", decrement: "NONE");

        Inputs(reach).ShouldBeDictionaryOf(
            (Port, ["AxisRightStickLeft", "AxisRightStickRight"]),
            (Port + ".inc", ["AxisRightStickRight"]),
            (Port + ".dec", ["AxisRightStickLeft"]));
        Derived(reach).ShouldBeDictionaryOf(
            (Port + ".inc", ["AxisRightStickRight"]),
            (Port + ".dec", ["AxisRightStickLeft"]));
    }

    [Fact]
    public void UnboundNudges_AxisDeclaresNoHalves_StatedEmpty()
    {
        // nothing says which end is which, but the names are still stated, so a baseline for them
        // is dropped rather than left pointing at an axis this cfg never bound
        Inputs(Resolve(standard: "JOYCODE_1_BUTTON1", increment: "NONE", decrement: ""))
            .ShouldBeDictionaryOf(
                (Port, ["ButtonA"]),
                (Port + ".inc", []),
                (Port + ".dec", []));
    }

    // ---- a joystick direction carrying a button is the joystick's direction, and a button ----

    [Theory]
    [InlineData("P1_JOYSTICK_UP")]
    [InlineData("P1_JOYSTICKLEFT_UP")]
    public void DirectionPortAlsoCarryingAButton_ReachesTheDirectionForTheJoystick(string port)
    {
        // gberet's shape: up on the hat or the stick, and a button the player jumps with, all one
        // port. The hat and stick are the joystick's direction -- the port reaches them because
        // the joystick does -- while the button is the port's alone. So a label of the whole
        // joystick's is the direction's, and the port's own label keeps to the button.
        IReadOnlyList<MamePortReach> reach = Resolve(
            standard: "JOYCODE_1_HAT1UP OR JOYCODE_1_YAXIS_UP_SWITCH OR JOYCODE_1_BUTTON1", port: port);

        Inputs(reach).ShouldBeDictionaryOf((port, ["ButtonA", "ButtonDpadUp", "AxisLeftStickUp"]));
        Derived(reach).ShouldBeDictionaryOf((port, ["ButtonDpadUp", "AxisLeftStickUp"]));
    }

    [Fact]
    public void DirectionPortWithOnlyDirections_IsItsOwn()
    {
        // nothing else shares the port, so it is simply that direction -- whether MAME happened to
        // write the port into the cfg must not decide who labels it
        Derived(Resolve(standard: "JOYCODE_1_HAT1UP OR JOYCODE_1_YAXIS_UP_SWITCH", port: "P1_JOYSTICK_UP"))
            .ShouldBeEmpty();
    }

    [Fact]
    public void DirectionPortsButtonTranslatesToNothing_IsItsOwn()
    {
        // another player's button reaches nothing here, so there is no second control to split off
        Derived(Resolve(standard: "JOYCODE_1_HAT1UP OR JOYCODE_2_BUTTON1", port: "P1_JOYSTICK_UP"))
            .ShouldBeEmpty();
    }

    [Fact]
    public void ButtonPortAlsoCarryingAHat_IsItsOwn()
    {
        // only a joystick's direction belongs to a joystick; a button bound to the hat is that button
        Derived(Resolve(standard: "JOYCODE_1_BUTTON1 OR JOYCODE_1_HAT1UP", port: "P1_BUTTON1"))
            .ShouldBeEmpty();
    }
}
