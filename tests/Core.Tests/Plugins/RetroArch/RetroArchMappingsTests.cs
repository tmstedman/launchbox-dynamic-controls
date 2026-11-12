using DynamicControls.Plugins.RetroArch;

namespace DynamicControls.Core.Tests.Plugins.RetroArch;

/// <summary>
/// Sanity tests for <see cref="RetroArchMappings"/>. The class is a pure data table — these tests
/// guard against (1) accidental duplicate values in a forward dict (which would silently break the
/// inverse), (2) a broken inversion lambda, and (3) drift from libretro's well-known RetroPad
/// conventions (e.g. slot "b" is the south face button, mapped to coreId 0 and physical btn 0).
/// Parser tests exercise the lookups in their natural use but don't necessarily touch every slot,
/// so these direct tests close the gap.
/// </summary>
public class RetroArchMappingsTests
{
    // ---- inverse round-trip ----

    [Fact]
    public void IdToSlot_RoundTripsSlotToId() =>
        RoundTrip(RetroArchMappings.SlotToId, RetroArchMappings.IdToSlot);

    [Fact]
    public void AnalogSlotToId_RoundTripsAnalogIdToSlot() =>
        RoundTrip(RetroArchMappings.AnalogIdToSlot, RetroArchMappings.AnalogSlotToId);

    [Fact]
    public void StickAxisOtherEnd_IsItsOwnInverse() =>
        RoundTrip(RetroArchMappings.StickAxisOtherEnd, RetroArchMappings.StickAxisOtherEnd);

    [Theory]
    [InlineData("l_x_plus", "l_x_minus")]
    [InlineData("r_y_minus", "r_y_plus")]
    public void StickAxisOtherEnd_PairsTheTwoEndsOfOneAxis(string end, string otherEnd)
    {
        RetroArchMappings.StickAxisOtherEnd[end].ShouldBe(otherEnd);
    }

    [Fact]
    public void BtnNumberToSlot_RoundTripsSlotToBtnNumber() =>
        RoundTrip(RetroArchMappings.SlotToBtnNumber, RetroArchMappings.BtnNumberToSlot);

    [Fact]
    public void AxisValueToSlot_RoundTripsSlotToAxisValue() =>
        RoundTrip(RetroArchMappings.SlotToAxisValue, RetroArchMappings.AxisValueToSlot);

    [Fact]
    public void HatValueToSlot_RoundTripsSlotToHatValue() =>
        RoundTrip(RetroArchMappings.SlotToHatValue, RetroArchMappings.HatValueToSlot);

    private static void RoundTrip<TFwd, TBack>(
        IReadOnlyDictionary<TFwd, TBack> forward,
        IReadOnlyDictionary<TBack, TFwd> inverse)
        where TFwd : notnull where TBack : notnull
    {
        // Count match catches duplicate values in the forward dict (which would silently collapse
        // entries during inversion). Per-entry round-trip catches a broken inversion lambda.
        inverse.Count.ShouldBe(forward.Count);
        foreach ((TFwd k, TBack v) in forward)
            inverse[v].ShouldBe(k);
    }

    // ---- libretro coreId anchors (RETRO_DEVICE_ID_JOYPAD_*) ----

    [Theory]
    [InlineData("b", 0)]      // south face — RETRO_DEVICE_ID_JOYPAD_B
    [InlineData("y", 1)]      // west face  — RETRO_DEVICE_ID_JOYPAD_Y
    [InlineData("select", 2)]
    [InlineData("start", 3)]
    [InlineData("up", 4)]
    [InlineData("down", 5)]
    [InlineData("left", 6)]
    [InlineData("right", 7)]
    [InlineData("a", 8)]      // east face — RETRO_DEVICE_ID_JOYPAD_A
    [InlineData("x", 9)]      // north face — RETRO_DEVICE_ID_JOYPAD_X
    public void SlotToId_MatchesLibretroConvention(string slot, int expectedId)
    {
        RetroArchMappings.SlotToId[slot].ShouldBe(expectedId);
    }

    // ---- the analog directions a remap can send a button to (RetroArch's RARCH_ANALOG_* ids) ----

    [Theory]
    [InlineData(16, "AxisLeftStickRight")]
    [InlineData(17, "AxisLeftStickLeft")]
    [InlineData(18, "AxisLeftStickDown")]   // +y is down
    [InlineData(19, "AxisLeftStickUp")]
    [InlineData(20, "AxisRightStickRight")]
    [InlineData(21, "AxisRightStickLeft")]
    [InlineData(22, "AxisRightStickDown")]
    [InlineData(23, "AxisRightStickUp")]
    public void AnalogIdToSlot_NamesTheStickDirectionRetroArchMeans(int coreId, string expectedGeneric)
    {
        RetroArchMappings.RetroPadToGeneric[RetroArchMappings.AnalogIdToSlot[coreId]].ShouldBe(expectedGeneric);
    }

    [Theory]
    [InlineData(0, "b")]
    [InlineData(15, "r3")]
    [InlineData(22, "r_y_plus")]
    [InlineData(-1, null)]   // disabled
    [InlineData(24, null)]   // past the last stick direction
    public void TargetSlotOf_ReachesButtonsAndStickDirections(int coreId, string? expectedSlot)
    {
        RetroArchMappings.TargetSlotOf(coreId).ShouldBe(expectedSlot);
    }

    [Theory]
    [InlineData("l_x+", "AxisLeftStickRight")]
    [InlineData("l_x-", "AxisLeftStickLeft")]
    [InlineData("l_y+", "AxisLeftStickDown")]
    [InlineData("l_y-", "AxisLeftStickUp")]
    [InlineData("r_x+", "AxisRightStickRight")]
    [InlineData("r_x-", "AxisRightStickLeft")]
    [InlineData("r_y+", "AxisRightStickDown")]
    [InlineData("r_y-", "AxisRightStickUp")]
    public void StickRemapSuffixToSlot_NamesTheStickDirectionRetroArchMeans(string suffix, string expectedGeneric)
    {
        RetroArchMappings.RetroPadToGeneric[RetroArchMappings.StickRemapSuffixToSlot[suffix]].ShouldBe(expectedGeneric);
    }

    [Theory]
    [InlineData("b", 0)]
    [InlineData("r_y_plus", 22)]
    [InlineData("zzz", null)]
    public void IdOfSlot_KnowsButtonsAndStickDirections(string slot, int? expectedId)
    {
        RetroArchMappings.IdOfSlot(slot).ShouldBe(expectedId);
    }

    // ---- SDL/XInput physical button anchors ----

    [Theory]
    [InlineData("b", 0)]      // south face
    [InlineData("a", 1)]      // east face
    [InlineData("y", 2)]      // west face
    [InlineData("x", 3)]      // north face
    [InlineData("select", 6)]
    [InlineData("start", 7)]
    public void SlotToBtnNumber_MatchesSdlConvention(string slot, int expectedBtn)
    {
        RetroArchMappings.SlotToBtnNumber[slot].ShouldBe(expectedBtn);
    }

    // ---- RetroPad → generic-input anchors (face buttons are the famously counterintuitive bit) ----

    [Theory]
    [InlineData("b", "ButtonA")]   // RetroPad's "b" slot drives the South face button (ButtonA)
    [InlineData("a", "ButtonB")]   // "a" → East (ButtonB)
    [InlineData("y", "ButtonX")]   // "y" → West (ButtonX)
    [InlineData("x", "ButtonY")]   // "x" → North (ButtonY)
    [InlineData("start", "ButtonStart")]
    [InlineData("select", "ButtonBack")]
    public void RetroPadToGeneric_MapsFaceButtonsToVisualPositions(string slot, string expectedGeneric)
    {
        RetroArchMappings.RetroPadToGeneric[slot].ShouldBe(expectedGeneric);
    }
}
