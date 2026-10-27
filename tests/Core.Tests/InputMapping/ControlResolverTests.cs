using DynamicControls.InputMapping;
using NSubstitute;
using static DynamicControls.Core.TestHelpers.InputMapping.MappingFixtures;

namespace DynamicControls.Core.Tests.InputMapping;

/// <summary>
/// Unit tests for <see cref="ControlResolver"/>: a platform button standing for several others
/// reaches whatever those others currently reach.
///
/// The rule that matters most here is <b>appended, never displacing</b>. A button that drives an
/// input directly outranks one that merely stands for buttons reaching it, so the direct binding
/// keeps the reverse lookup — which is what decides the image the player is shown. The natural
/// snapshot is resolved the same way against its own members, since <c>IsMapped</c> reads it and
/// a button reached only through one would otherwise look unmapped however plainly the controller declares it.
/// </summary>
public class ControlResolverTests
{
    private readonly ControlResolver _underTest = new(Substitute.For<ILogger>());

    private static ControlEntry Control(string name, params string[] members) =>
        new() { Name = name, Mappings = [.. members] };

    private static Dictionary<string, IReadOnlyList<string>> Buttons(
        params (string Button, string[] Inputs)[] entries) =>
        entries.ToDictionary(e => e.Button, e => (IReadOnlyList<string>)e.Inputs);

    // --- the basic reach ---

    [Fact]
    public void ControlReachesTheUnionOfItsMembersTargets()
    {
        ResolvedMapping mapping = MappingOf(
            buttonToInput: Buttons(
                ("Dpad-Up", ["ButtonDpadUp"]),
                ("Dpad-Down", ["ButtonDpadDown"])),
            inputToButton: new Dictionary<string, string>
            {
                ["ButtonDpadUp"] = "Dpad-Up",
                ["ButtonDpadDown"] = "Dpad-Down",
            });

        ResolvedMapping result = _underTest.Apply(mapping, [Control("Dpad-Any", "Dpad-Up", "Dpad-Down")]);

        result.ButtonToInput["Dpad-Any"].ShouldBe(["ButtonDpadUp", "ButtonDpadDown"]);
    }

    [Fact]
    public void ControlFollowsItsMembersWhereverTheyNowPoint()
    {
        // The whole point of resolving after a transform: a cfg has moved the directions onto the
        // stick, and the button that names the control as a unit has to follow them there.
        ResolvedMapping mapping = MappingOf(
            buttonToInput: Buttons(
                ("Dpad-Up", ["AxisLeftStickUp"]),
                ("Dpad-Down", ["AxisLeftStickDown"])));

        ResolvedMapping result = _underTest.Apply(mapping, [Control("Dpad-Any", "Dpad-Up", "Dpad-Down")]);

        result.ButtonToInput["Dpad-Any"].ShouldBe(["AxisLeftStickUp", "AxisLeftStickDown"]);
    }

    [Fact]
    public void MembersReachingTheSameInput_CountOnceInMemberOrder()
    {
        // Two members on one input is still one target, and the order is the author's rather
        // than a dictionary's.
        ResolvedMapping mapping = MappingOf(
            buttonToInput: Buttons(
                ("X", ["ButtonA", "ButtonB"]),
                ("Y", ["ButtonB", "ButtonC"])));

        ResolvedMapping result = _underTest.Apply(mapping, [Control("X-Any", "X", "Y")]);

        result.ButtonToInput["X-Any"].ShouldBe(["ButtonA", "ButtonB", "ButtonC"]);
    }

    // --- appended, never displacing ---

    [Fact]
    public void DirectBindingKeepsTheReverseLookup_ControlDoesNotStealIt()
    {
        // Dpad-Up drives ButtonDpadUp outright; Dpad-Any only stands for buttons that reach it.
        // InputToButton decides which image is drawn, so the direct binding must keep it.
        ResolvedMapping mapping = MappingOf(
            buttonToInput: Buttons(("Dpad-Up", ["ButtonDpadUp"])),
            inputToButton: new Dictionary<string, string> { ["ButtonDpadUp"] = "Dpad-Up" });

        ResolvedMapping result = _underTest.Apply(mapping, [Control("Dpad-Any", "Dpad-Up")]);

        result.InputToButton["ButtonDpadUp"].ShouldBe("Dpad-Up");
    }

    [Fact]
    public void InputNothingClaimedDirectly_FallsToTheControl()
    {
        ResolvedMapping mapping = MappingOf(
            buttonToInput: Buttons(("Dpad-Up", ["ButtonDpadUp"])));

        ResolvedMapping result = _underTest.Apply(mapping, [Control("Dpad-Any", "Dpad-Up")]);

        result.InputToButton["ButtonDpadUp"].ShouldBe("Dpad-Any");
    }

    [Fact]
    public void ControlWithItsOwnDirectMapping_KeepsItAndAppendsWhatItStandsFor()
    {
        ResolvedMapping mapping = MappingOf(
            buttonToInput: Buttons(
                ("Dpad-Any", ["ButtonDpad"]),
                ("Dpad-Up", ["ButtonDpadUp"])));

        ResolvedMapping result = _underTest.Apply(mapping, [Control("Dpad-Any", "Dpad-Up")]);

        result.ButtonToInput["Dpad-Any"].ShouldBe(["ButtonDpad", "ButtonDpadUp"]);
    }

    // --- what a control appends is derived ---

    [Fact]
    public void WhatAControlReaches_IsDerived()
    {
        // the control reaches these only because its members do
        ResolvedMapping mapping = MappingOf(
            buttonToInput: Buttons(
                ("Dpad-Up", ["ButtonDpadUp"]),
                ("Dpad-Down", ["ButtonDpadDown"])));

        ResolvedMapping result = _underTest.Apply(mapping, [Control("Dpad-Any", "Dpad-Up", "Dpad-Down")]);

        result.DerivedButtonToInput.ShouldBeDictionaryOf(("Dpad-Any", ["ButtonDpadUp", "ButtonDpadDown"]));
    }

    [Fact]
    public void ControlWithItsOwnDirectMapping_OnlyWhatItAppendsIsDerived()
    {
        // downtown's rotary: the port reaches the stick by its own binding and the triggers only
        // through its nudges. The stick stays its own claim, so its label outranks the nudges'
        // there, while on the triggers the nudges' own claims outrank the port's. What the
        // nudges themselves had derived is carried through untouched.
        ResolvedMapping mapping = MappingOf(
            buttonToInput: Buttons(
                ("P1_POSITIONAL", ["AxisRightStickRight", "AxisRightStickLeft"]),
                ("P1_POSITIONAL.inc", ["AxisTriggerRight", "AxisRightStickRight"]),
                ("P1_POSITIONAL.dec", ["AxisTriggerLeft", "AxisRightStickLeft"])),
            derivedButtonToInput: Buttons(
                ("P1_POSITIONAL.inc", ["AxisRightStickRight"]),
                ("P1_POSITIONAL.dec", ["AxisRightStickLeft"])));

        ResolvedMapping result = _underTest.Apply(mapping, [Control("P1_POSITIONAL", "P1_POSITIONAL.dec", "P1_POSITIONAL.inc")]);

        result.ButtonToInput["P1_POSITIONAL"].ShouldBe(
            ["AxisRightStickRight", "AxisRightStickLeft", "AxisTriggerLeft", "AxisTriggerRight"]);
        result.DerivedButtonToInput.ShouldBeDictionaryOf(
            ("P1_POSITIONAL", ["AxisTriggerLeft", "AxisTriggerRight"]),
            ("P1_POSITIONAL.inc", ["AxisRightStickRight"]),
            ("P1_POSITIONAL.dec", ["AxisRightStickLeft"]));
    }

    // --- the natural snapshot ---

    [Fact]
    public void NaturalMapsAreResolvedAgainstTheirOwnMembers()
    {
        // IsMapped reads the natural snapshot, so a button reached only through a control would look
        // unmapped however plainly the controller declares it.
        ResolvedMapping mapping = MappingOf(
            buttonToInput: Buttons(("Dpad-Up", ["AxisLeftStickUp"])),
            naturalButtonToInput: Buttons(("Dpad-Up", ["ButtonDpadUp"])));

        ResolvedMapping result = _underTest.Apply(mapping, [Control("Dpad-Any", "Dpad-Up")]);

        result.ButtonToInput["Dpad-Any"].ShouldBe(["AxisLeftStickUp"]);
        result.NaturalButtonToInput["Dpad-Any"].ShouldBe(["ButtonDpadUp"]);
        result.NaturalInputToButton["ButtonDpadUp"].ShouldBe("Dpad-Any");
    }

    // --- composition is not supported ---

    [Fact]
    public void ControlNamingAnotherControl_SeesOnlyWhatThatNameIsBoundToDirectly()
    {
        // Characterisation, not a requirement: Expand reads every member's targets from the
        // mapping it was handed while writing into a copy, so one control never picks up
        // another's expansion and the result does not depend on declaration order. Nothing
        // shipped does this. #28 decides whether to compose or to refuse it outright; until
        // then this pins what actually happens so a change cannot be silent.
        ResolvedMapping mapping = MappingOf(
            buttonToInput: Buttons(("Dpad-Up", ["ButtonDpadUp"]), ("Dpad-Down", ["ButtonDpadDown"])));

        ResolvedMapping result = _underTest.Apply(mapping,
        [
            Control("Dpad-Any", "Dpad-Up", "Dpad-Down"),
            Control("Everything", "Dpad-Any", "Dpad-Up"),
        ]);

        // Dpad-Any expanded, but Everything saw Dpad-Any as unbound and reached only Dpad-Up's target.
        result.ButtonToInput["Dpad-Any"].ShouldBe(["ButtonDpadUp", "ButtonDpadDown"]);
        result.ButtonToInput["Everything"].ShouldBe(["ButtonDpadUp"]);
    }

    // --- nothing to do ---

    [Fact]
    public void ControlWhoseMembersAreAllUnbound_ReachesNothingAndIsSkipped()
    {
        ResolvedMapping mapping = MappingOf(buttonToInput: Buttons(("A", ["ButtonA"])));

        ResolvedMapping result = _underTest.Apply(mapping, [Control("Dpad-Any", "Dpad-Up", "Dpad-Down")]);

        result.ButtonToInput.ContainsKey("Dpad-Any").ShouldBeFalse();
    }

    [Fact]
    public void NoControls_ReturnsTheMappingUnchanged()
    {
        ResolvedMapping mapping = MappingOf(buttonToInput: Buttons(("A", ["ButtonA"])));

        _underTest.Apply(mapping, []).ShouldBeSameAs(mapping);
    }

    [Fact]
    public void PartiallyBoundControl_ReachesOnlyTheMembersThatAreBound()
    {
        ResolvedMapping mapping = MappingOf(buttonToInput: Buttons(("Dpad-Up", ["ButtonDpadUp"])));

        ResolvedMapping result = _underTest.Apply(
            mapping, [Control("Dpad-Any", "Dpad-Up", "Dpad-Down", "Dpad-Left", "Dpad-Right")]);

        result.ButtonToInput["Dpad-Any"].ShouldBe(["ButtonDpadUp"]);
    }

    [Fact]
    public void UnrelatedBindingsAreLeftAlone()
    {
        ResolvedMapping mapping = MappingOf(
            buttonToInput: Buttons(("A", ["ButtonA"]), ("Dpad-Up", ["ButtonDpadUp"])),
            inputToButton: new Dictionary<string, string> { ["ButtonA"] = "A" });

        ResolvedMapping result = _underTest.Apply(mapping, [Control("Dpad-Any", "Dpad-Up")]);

        result.ButtonToInput["A"].ShouldBe(["ButtonA"]);
        result.InputToButton["ButtonA"].ShouldBe("A");
    }
}
