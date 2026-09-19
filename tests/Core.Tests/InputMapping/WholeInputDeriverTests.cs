using DynamicControls.InputMapping;
using NSubstitute;
using static DynamicControls.Core.TestHelpers.InputMapping.MappingFixtures;

namespace DynamicControls.Core.Tests.InputMapping;

/// <summary>
/// Covers <see cref="WholeInputDeriver.Derive"/>: a button naming a whole control follows its
/// direction siblings onto whichever controls an emulator config moved them to.
///
/// <para>The shape throughout is MAME's, because it is where the problem shows: a cfg binds each
/// <c>P1_JOYSTICK_*</c> port to a hat <c>OR</c> an axis, so all four directions drive a Dpad
/// control and a left-stick control at once, while <c>JOYSTICK</c> itself — which no cfg can
/// move, MAME having no whole-joystick port — still points at the Dpad alone.</para>
/// </summary>
public class WholeInputDeriverTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();

    private const string Up = "ButtonDpadUp";
    private const string Down = "ButtonDpadDown";
    private const string Left = "ButtonDpadLeft";
    private const string Right = "ButtonDpadRight";

    private const string StickUp = "AxisLeftStickUp";
    private const string StickDown = "AxisLeftStickDown";
    private const string StickLeft = "AxisLeftStickLeft";
    private const string StickRight = "AxisLeftStickRight";

    /// <summary>The Arcade controller's joystick vocabulary, before any cfg is applied — the
    /// natural mapping the derivation reads the whole-and-parts pairing from.</summary>
    private static Dictionary<string, IReadOnlyList<string>> Natural() => new()
    {
        ["JOYSTICK"] = ["ButtonDpad"],
        ["JOYSTICK_UP"] = [Up],
        ["JOYSTICK_DOWN"] = [Down],
        ["JOYSTICK_LEFT"] = [Left],
        ["JOYSTICK_RIGHT"] = [Right],
    };

    [Fact]
    public void Derive_EveryDirectionReachesASecondControl_TheWholeButtonDrivesBothControls()
    {
        // The 3on3dunk cfg: each direction port bound to the hat OR the corresponding axis.
        Dictionary<string, IReadOnlyList<string>> buttonToInput = new()
        {
            ["JOYSTICK"] = ["ButtonDpad"],
            ["JOYSTICK_UP"] = [Up, StickUp],
            ["JOYSTICK_DOWN"] = [Down, StickDown],
            ["JOYSTICK_LEFT"] = [Left, StickLeft],
            ["JOYSTICK_RIGHT"] = [Right, StickRight],
        };

        ResolvedMapping result = WholeInputDeriver.Derive(
            MappingOf(buttonToInput: buttonToInput, naturalButtonToInput: Natural()), _logger);

        result.ButtonToInput["JOYSTICK"].ShouldBe(["ButtonDpad", "AxisLeftStick"]);

        // Asserted rather than assumed: a derivation that happens silently is indistinguishable
        // from one that did not happen, which is precisely what makes a wrong label hard to
        // diagnose from a log.
        _logger.Received().Debug("Whole input: JOYSTICK follows its directions onto AxisLeftStick");
    }

    [Fact]
    public void Derive_DerivedControlIsAppendedAfterTheButtonsOwnBinding()
    {
        // Ordering is the contract InputLabelsService ranks claims by: a button mapped directly
        // to a control must outrank one that only reached it through this derivation. The hat is
        // still bound alongside the axes here, so the D-pad survives and both entries are present
        // to be ordered.
        Dictionary<string, IReadOnlyList<string>> buttonToInput = new()
        {
            ["JOYSTICK"] = ["ButtonDpad"],
            ["JOYSTICK_UP"] = [Up, StickUp],
            ["JOYSTICK_DOWN"] = [Down, StickDown],
            ["JOYSTICK_LEFT"] = [Left, StickLeft],
            ["JOYSTICK_RIGHT"] = [Right, StickRight],
        };

        ResolvedMapping result = WholeInputDeriver.Derive(
            MappingOf(buttonToInput: buttonToInput, naturalButtonToInput: Natural()), _logger);

        result.ButtonToInput["JOYSTICK"][0].ShouldBe("ButtonDpad");
        result.ButtonToInput["JOYSTICK"][^1].ShouldBe("AxisLeftStick");
    }

    [Fact]
    public void Derive_EveryDirectionLeavesAControl_TheWholeButtonStopsClaimingIt()
    {
        // The config binds each direction to a stick axis alone — no hat — which is what a player
        // on a gamepad rather than an arcade stick would write. Nothing drives the D-pad any
        // more, so a label left on it would tell the player a dead control moves them.
        Dictionary<string, IReadOnlyList<string>> buttonToInput = new()
        {
            ["JOYSTICK"] = ["ButtonDpad"],
            ["JOYSTICK_UP"] = [StickUp],
            ["JOYSTICK_DOWN"] = [StickDown],
            ["JOYSTICK_LEFT"] = [StickLeft],
            ["JOYSTICK_RIGHT"] = [StickRight],
        };

        ResolvedMapping result = WholeInputDeriver.Derive(
            MappingOf(buttonToInput: buttonToInput, naturalButtonToInput: Natural()), _logger);

        result.ButtonToInput["JOYSTICK"].ShouldBe(["AxisLeftStick"]);
        _logger.Received().Debug(
            "Whole input: JOYSTICK no longer drives ButtonDpad — every one of its directions has moved away");
    }

    [Fact]
    public void Derive_DroppedControlLeavesTheReverseLookupWhenNothingElseDrivesIt()
    {
        // The two views have to stay in step. VisibilityEvaluator asks the reverse lookup whether
        // an input is driven at all, so a D-pad still listed there would render as live.
        Dictionary<string, IReadOnlyList<string>> buttonToInput = new()
        {
            ["JOYSTICK"] = ["ButtonDpad"],
            ["JOYSTICK_UP"] = [StickUp],
            ["JOYSTICK_DOWN"] = [StickDown],
            ["JOYSTICK_LEFT"] = [StickLeft],
            ["JOYSTICK_RIGHT"] = [StickRight],
        };

        ResolvedMapping result = WholeInputDeriver.Derive(
            MappingOf(
                buttonToInput: buttonToInput,
                inputToButton: new Dictionary<string, string> { ["ButtonDpad"] = "JOYSTICK" },
                naturalButtonToInput: Natural()),
            _logger);

        result.InputToButton.ContainsKey("ButtonDpad").ShouldBeFalse();
        result.InputToButton["AxisLeftStick"].ShouldBe("JOYSTICK");
    }

    [Fact]
    public void Derive_OneDirectionStillReachesTheControl_TheWholeButtonKeepsIt()
    {
        // Three directions moved to the stick and Right stayed on the D-pad. Pushing right still
        // moves the player, so the D-pad is not dead and the label is still true of it. Dropping
        // needs every direction gone, not merely most of them.
        Dictionary<string, IReadOnlyList<string>> buttonToInput = new()
        {
            ["JOYSTICK"] = ["ButtonDpad"],
            ["JOYSTICK_UP"] = [StickUp],
            ["JOYSTICK_DOWN"] = [StickDown],
            ["JOYSTICK_LEFT"] = [StickLeft],
            ["JOYSTICK_RIGHT"] = [Right],
        };

        ResolvedMapping result = WholeInputDeriver.Derive(
            MappingOf(buttonToInput: buttonToInput, naturalButtonToInput: Natural()), _logger);

        result.ButtonToInput["JOYSTICK"].ShouldContain("ButtonDpad");
    }

    [Fact]
    public void Derive_TwoWayJoystick_KeepsItsControlThoughItNeverDroveAllFourDirections()
    {
        // Plenty of cabinets have a left/right-only joystick, so the D-pad is only ever driven by
        // two of its four directions. Requiring all four before keeping a claim would erase the
        // label on every one of those games.
        Dictionary<string, IReadOnlyList<string>> natural = new()
        {
            ["JOYSTICK"] = ["ButtonDpad"],
            ["JOYSTICK_LEFT"] = [Left],
            ["JOYSTICK_RIGHT"] = [Right],
        };

        ResolvedMapping result = WholeInputDeriver.Derive(
            MappingOf(buttonToInput: natural, naturalButtonToInput: natural), _logger);

        result.ButtonToInput["JOYSTICK"].ShouldBe(["ButtonDpad"]);
    }

    [Fact]
    public void Derive_ButtonOnAWholeControlWithNoDirectionsAnywhere_KeepsIt()
    {
        // A platform can map a button straight to a whole control with no per-direction buttons
        // at all. There is nothing to test the claim against, and absence of evidence must not be
        // read as evidence the control is dead.
        Dictionary<string, IReadOnlyList<string>> mapping = new()
        {
            ["JOYSTICK"] = ["ButtonDpad"],
            ["BUTTON1"] = ["ButtonA"],
        };

        ResolvedMapping result = WholeInputDeriver.Derive(
            MappingOf(buttonToInput: mapping, naturalButtonToInput: mapping), _logger);

        result.ButtonToInput["JOYSTICK"].ShouldBe(["ButtonDpad"]);
    }

    [Fact]
    public void Derive_OnlySomeDirectionsReachTheSecondControl_TheWholeButtonIsUnchanged()
    {
        // Only UP was bound to the stick. Pushing the stick up moves the player but the stick as
        // a whole does not, so claiming it would put a "Move" label on a control that mostly
        // does nothing. The Dpad keeps the whole-joystick binding by itself.
        Dictionary<string, IReadOnlyList<string>> buttonToInput = new()
        {
            ["JOYSTICK"] = ["ButtonDpad"],
            ["JOYSTICK_UP"] = [Up, StickUp],
            ["JOYSTICK_DOWN"] = [Down],
            ["JOYSTICK_LEFT"] = [Left],
            ["JOYSTICK_RIGHT"] = [Right],
        };

        ResolvedMapping result = WholeInputDeriver.Derive(
            MappingOf(buttonToInput: buttonToInput, naturalButtonToInput: Natural()), _logger);

        result.ButtonToInput["JOYSTICK"].ShouldBe(["ButtonDpad"]);
    }

    [Fact]
    public void Derive_ADirectionLostItsBindingEntirely_TheWholeButtonIsUnchanged()
    {
        // Three directions moved to the stick and the fourth is unmapped. The set is incomplete
        // for the same reason as above, and an absent direction must not read as a covered one.
        Dictionary<string, IReadOnlyList<string>> buttonToInput = new()
        {
            ["JOYSTICK"] = ["ButtonDpad"],
            ["JOYSTICK_UP"] = [Up, StickUp],
            ["JOYSTICK_DOWN"] = [Down, StickDown],
            ["JOYSTICK_LEFT"] = [Left, StickLeft],
        };

        ResolvedMapping result = WholeInputDeriver.Derive(
            MappingOf(buttonToInput: buttonToInput, naturalButtonToInput: Natural()), _logger);

        result.ButtonToInput["JOYSTICK"].ShouldBe(["ButtonDpad"]);
    }

    [Fact]
    public void Derive_DirectionsUnmoved_MappingIsUnchanged()
    {
        // No cfg applied: the directions still drive exactly what the controller file gave them.
        // The Dpad is already the whole button's binding, so nothing is added — and in
        // particular ButtonDpad is not appended to itself.
        Dictionary<string, IReadOnlyList<string>> buttonToInput = Natural();

        ResolvedMapping result = WholeInputDeriver.Derive(
            MappingOf(buttonToInput: buttonToInput, naturalButtonToInput: Natural()), _logger);

        result.ButtonToInput["JOYSTICK"].ShouldBe(["ButtonDpad"]);
        result.ButtonToInput["JOYSTICK_UP"].ShouldBe([Up]);
    }

    [Fact]
    public void Derive_WholeButtonAlreadyDrivesTheDerivedControl_ItIsNotDuplicated()
    {
        // An analogToDigital mirror has already put AxisLeftStick on JOYSTICK. The derivation
        // reaches the same conclusion and must not list it twice.
        Dictionary<string, IReadOnlyList<string>> buttonToInput = new()
        {
            ["JOYSTICK"] = ["ButtonDpad", "AxisLeftStick"],
            ["JOYSTICK_UP"] = [Up, StickUp],
            ["JOYSTICK_DOWN"] = [Down, StickDown],
            ["JOYSTICK_LEFT"] = [Left, StickLeft],
            ["JOYSTICK_RIGHT"] = [Right, StickRight],
        };

        ResolvedMapping result = WholeInputDeriver.Derive(
            MappingOf(buttonToInput: buttonToInput, naturalButtonToInput: Natural()), _logger);

        result.ButtonToInput["JOYSTICK"].ShouldBe(["ButtonDpad", "AxisLeftStick"]);
    }

    [Fact]
    public void Derive_ButtonWithNoDirectionsInTheReference_IsUnchanged()
    {
        // BUTTON1 is an ordinary button — it names no whole control and has no direction
        // siblings, so the derivation has nothing to say about it however its binding moved.
        Dictionary<string, IReadOnlyList<string>> naturalButtonToInput = new()
        {
            ["BUTTON1"] = ["ButtonA"],
        };
        Dictionary<string, IReadOnlyList<string>> buttonToInput = new()
        {
            ["BUTTON1"] = ["ButtonX"],
        };

        ResolvedMapping result = WholeInputDeriver.Derive(
            MappingOf(buttonToInput: buttonToInput, naturalButtonToInput: naturalButtonToInput), _logger);

        result.ButtonToInput["BUTTON1"].ShouldBe(["ButtonX"]);
    }

    [Fact]
    public void Derive_DualStickGame_EachWholeButtonFollowsItsOwnDirectionsOnly()
    {
        // A Robotron-shaped cabinet: two whole-stick buttons, each with its own four directions.
        // The cfg moves the left stick's directions onto the Dpad as well. Only JOYSTICKLEFT
        // follows them — JOYSTICKRIGHT's directions did not move, so it stays where it was.
        Dictionary<string, IReadOnlyList<string>> naturalButtonToInput = new()
        {
            ["JOYSTICKLEFT"] = ["AxisLeftStick"],
            ["JOYSTICKLEFT_UP"] = [StickUp],
            ["JOYSTICKLEFT_DOWN"] = [StickDown],
            ["JOYSTICKLEFT_LEFT"] = [StickLeft],
            ["JOYSTICKLEFT_RIGHT"] = [StickRight],
            ["JOYSTICKRIGHT"] = ["AxisRightStick"],
            ["JOYSTICKRIGHT_UP"] = ["AxisRightStickUp"],
            ["JOYSTICKRIGHT_DOWN"] = ["AxisRightStickDown"],
            ["JOYSTICKRIGHT_LEFT"] = ["AxisRightStickLeft"],
            ["JOYSTICKRIGHT_RIGHT"] = ["AxisRightStickRight"],
        };
        Dictionary<string, IReadOnlyList<string>> buttonToInput = new(naturalButtonToInput)
        {
            ["JOYSTICKLEFT_UP"] = [StickUp, Up],
            ["JOYSTICKLEFT_DOWN"] = [StickDown, Down],
            ["JOYSTICKLEFT_LEFT"] = [StickLeft, Left],
            ["JOYSTICKLEFT_RIGHT"] = [StickRight, Right],
        };

        ResolvedMapping result = WholeInputDeriver.Derive(
            MappingOf(buttonToInput: buttonToInput, naturalButtonToInput: naturalButtonToInput), _logger);

        result.ButtonToInput["JOYSTICKLEFT"].ShouldBe(["AxisLeftStick", "ButtonDpad"]);
        result.ButtonToInput["JOYSTICKRIGHT"].ShouldBe(["AxisRightStick"]);
    }

    [Fact]
    public void Derive_AnotherButtonAlreadyDrivesTheControl_ItKeepsItAndTheReasonIsLogged()
    {
        // JOYSTICKLEFT is mapped to the stick directly; JOYSTICK only reaches it by derivation.
        // The forward map records both, but the reverse lookup — which decides the artwork —
        // keeps the button that genuinely means "the left stick".
        Dictionary<string, IReadOnlyList<string>> naturalButtonToInput = new(Natural())
        {
            ["JOYSTICKLEFT"] = ["AxisLeftStick"],
        };
        Dictionary<string, IReadOnlyList<string>> buttonToInput = new()
        {
            ["JOYSTICK"] = ["ButtonDpad"],
            ["JOYSTICKLEFT"] = ["AxisLeftStick"],
            ["JOYSTICK_UP"] = [Up, StickUp],
            ["JOYSTICK_DOWN"] = [Down, StickDown],
            ["JOYSTICK_LEFT"] = [Left, StickLeft],
            ["JOYSTICK_RIGHT"] = [Right, StickRight],
        };

        ResolvedMapping result = WholeInputDeriver.Derive(
            MappingOf(
                buttonToInput: buttonToInput,
                inputToButton: new Dictionary<string, string> { ["AxisLeftStick"] = "JOYSTICKLEFT" },
                naturalButtonToInput: naturalButtonToInput),
            _logger);

        result.ButtonToInput["JOYSTICK"].ShouldBe(["ButtonDpad", "AxisLeftStick"]);
        result.InputToButton["AxisLeftStick"].ShouldBe("JOYSTICKLEFT");
        _logger.Received().Debug("Whole input: AxisLeftStick keeps JOYSTICKLEFT, which is mapped to it directly");
    }
}
