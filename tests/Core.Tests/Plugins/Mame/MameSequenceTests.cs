using DynamicControls.Plugins.Mame;

namespace DynamicControls.Core.Tests.Plugins.Mame;

/// <summary>
/// Unit tests for <see cref="MameSequence"/>: one cfg <c>&lt;newseq&gt;</c>, read as the
/// <c>OR</c>-separated alternatives that each trigger it, and the one question asked of two of
/// them together — which of their switches form an axis between them.
/// </summary>
public class MameSequenceTests
{
    [Theory]
    [InlineData("NONE")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Parse_NoneOrEmpty_IsUnbound(string? text) =>
        MameSequence.Parse(text).IsBound.ShouldBeFalse();

    [Fact]
    public void Parse_SplitsOnOr_KeepingAChordWhole()
    {
        // a chord -- codes pressed together, space-separated -- is one alternative, not two
        var sequence = MameSequence.Parse(" JOYCODE_1_BUTTON1 OR JOYCODE_1_BUTTON2 JOYCODE_1_BUTTON3 ");

        sequence.Alternatives.ShouldBe(["JOYCODE_1_BUTTON1", "JOYCODE_1_BUTTON2 JOYCODE_1_BUTTON3"]);
        sequence.ToString().ShouldBe("JOYCODE_1_BUTTON1 OR JOYCODE_1_BUTTON2 JOYCODE_1_BUTTON3");
    }

    [Fact]
    public void Without_DropsThoseAlternatives_AndKeepsTheRestInOrder()
    {
        var sequence = MameSequence.Parse("JOYCODE_1_BUTTON1 OR JOYCODE_1_ZAXIS_POS_SWITCH OR JOYCODE_1_BUTTON2");

        sequence.Without(MameSequence.Parse("JOYCODE_1_ZAXIS_POS_SWITCH")).Alternatives
            .ShouldBe(["JOYCODE_1_BUTTON1", "JOYCODE_1_BUTTON2"]);
    }

    [Theory]
    // downtown's stick: an end of the axis in each nudge, beside a trigger in each
    [InlineData("JOYCODE_1_ZAXIS_POS_SWITCH OR JOYCODE_1_SLIDER2_NEG_SWITCH", "JOYCODE_1_ZAXIS_NEG_SWITCH OR JOYCODE_1_SLIDER1_NEG_SWITCH",
                "JOYCODE_1_ZAXIS_POS_SWITCH", "JOYCODE_1_ZAXIS_NEG_SWITCH")]
    // which end is in which nudge doesn't matter -- it's still one axis both ways
    [InlineData("JOYCODE_1_ZAXIS_NEG_SWITCH", "JOYCODE_1_ZAXIS_POS_SWITCH",
                "JOYCODE_1_ZAXIS_NEG_SWITCH", "JOYCODE_1_ZAXIS_POS_SWITCH")]
    // MAME names the X and Y axes' ends by direction rather than sign -- the spacwalk shape,
    // and how MAME's own defaults nudge a dial, paddle or positional port
    [InlineData("JOYCODE_1_XAXIS_RIGHT_SWITCH", "JOYCODE_1_XAXIS_LEFT_SWITCH",
                "JOYCODE_1_XAXIS_RIGHT_SWITCH", "JOYCODE_1_XAXIS_LEFT_SWITCH")]
    [InlineData("JOYCODE_1_YAXIS_UP_SWITCH", "JOYCODE_1_YAXIS_DOWN_SWITCH",
                "JOYCODE_1_YAXIS_UP_SWITCH", "JOYCODE_1_YAXIS_DOWN_SWITCH")]
    public void AxisEndsSharedWith_OppositeEndsOfOneAxis_Pair(string here, string there, string expectedHere, string expectedThere)
    {
        (MameSequence pairedHere, MameSequence pairedThere) = MameSequence.Parse(here).AxisEndsSharedWith(MameSequence.Parse(there));

        pairedHere.ToString().ShouldBe(expectedHere);
        pairedThere.ToString().ShouldBe(expectedThere);
    }

    [Theory]
    // both ends of the axis in one nudge: pressing it pushes both ways at once
    [InlineData("JOYCODE_1_ZAXIS_POS_SWITCH OR JOYCODE_1_ZAXIS_NEG_SWITCH", "JOYCODE_1_ZAXIS_NEG_SWITCH")]
    // an end in each, but of two different axes
    [InlineData("JOYCODE_1_ZAXIS_POS_SWITCH", "JOYCODE_1_RZAXIS_NEG_SWITCH")]
    // the same end in both
    [InlineData("JOYCODE_1_ZAXIS_POS_SWITCH", "JOYCODE_1_ZAXIS_POS_SWITCH")]
    [InlineData("JOYCODE_1_XAXIS_LEFT_SWITCH", "JOYCODE_1_XAXIS_LEFT_SWITCH")]
    // ends of two different axes, named by direction: left is not the opposite of down
    [InlineData("JOYCODE_1_XAXIS_LEFT_SWITCH", "JOYCODE_1_YAXIS_DOWN_SWITCH")]
    // an end held only as part of a chord is not the stick on its own
    [InlineData("JOYCODE_1_ZAXIS_POS_SWITCH JOYCODE_1_BUTTON1", "JOYCODE_1_ZAXIS_NEG_SWITCH")]
    // ordinary buttons are no axis at all
    [InlineData("JOYCODE_1_BUTTON1", "JOYCODE_1_BUTTON2")]
    public void AxisEndsSharedWith_NotOneAxisBothWays_PairsNothing(string here, string there)
    {
        (MameSequence pairedHere, MameSequence pairedThere) = MameSequence.Parse(here).AxisEndsSharedWith(MameSequence.Parse(there));

        pairedHere.IsBound.ShouldBeFalse();
        pairedThere.IsBound.ShouldBeFalse();
    }

    [Theory]
    // a hat direction, and a stick axis thresholded into a switch, named by direction or by sign
    [InlineData("JOYCODE_1_HAT1UP")]
    [InlineData("JOYCODE_1_YAXIS_UP_SWITCH")]
    [InlineData("JOYCODE_1_XAXIS_LEFT_SWITCH")]
    [InlineData("JOYCODE_1_RZAXIS_NEG_SWITCH")]
    [InlineData("JOYCODE_1_RXAXIS_POS_SWITCH")]
    public void Directions_StickAndHatSwitches_AreDirections(string joycode) =>
        MameSequence.Parse(joycode).Directions().ToString().ShouldBe(joycode);

    [Theory]
    // a button, a trigger slider -- which has a NEG end but pushes no direction -- a bare axis, and
    // a direction held only as part of a chord
    [InlineData("JOYCODE_1_BUTTON1")]
    [InlineData("JOYCODE_1_SLIDER1_NEG_SWITCH")]
    [InlineData("JOYCODE_1_YAXIS")]
    [InlineData("JOYCODE_1_HAT1UP JOYCODE_1_BUTTON1")]
    public void Directions_AnythingElse_IsNoDirection(string joycode) =>
        MameSequence.Parse(joycode).Directions().IsBound.ShouldBeFalse();

    [Fact]
    public void Directions_KeepsOnlyTheDirectionsInOrder()
    {
        // gberet's up: the hat and the stick, and a button the player jumps with
        MameSequence.Parse("JOYCODE_1_HAT1UP OR JOYCODE_1_YAXIS_UP_SWITCH OR JOYCODE_1_BUTTON1").Directions().Alternatives
            .ShouldBe(["JOYCODE_1_HAT1UP", "JOYCODE_1_YAXIS_UP_SWITCH"]);
    }
}
