using DynamicControls.Plugins.RetroArch;
using NSubstitute;

namespace DynamicControls.Core.Tests.Plugins.RetroArch;

/// <summary>
/// Unit tests for <see cref="RetroArchRemapSwapResolver"/>. Focus: (1) btn swap entries —
/// non-canonical coreId, canonical skip, non-integer, disabled (−1), unknown slot;
/// (2) stk entries — a stick direction by RetroArch's own name, and the other end of its axis
/// left pressing nothing; (3) axis swap entries — non-canonical, canonical skip, unknown value,
/// value maps to stick slot; (4) unrecognized key prefixes are silently skipped; (5) null Game
/// level returns empty.
/// </summary>
public class RetroArchRemapSwapResolverTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly RetroArchRemapSwapResolver _underTest;

    public RetroArchRemapSwapResolverTests()
    {
        _underTest = new RetroArchRemapSwapResolver(_logger);
    }

    private Dictionary<string, int> Resolve(Dictionary<string, string>? gameLevel) =>
        _underTest.ResolveSwaps(new RetroArchGameData(null, null, null, gameLevel));

    // ---- game-level gating ----

    [Fact]
    public void ResolveSwaps_GameLevelNull_ReturnsEmpty()
    {
        Resolve(gameLevel: null).ShouldBeEmpty();
    }

    // ---- btn entries (input_player1_btn_{slot} = {coreId}) ----

    #pragma warning disable format
    [Theory]
    [InlineData("a",  "0",  "a",  0)]  // east face  (canonical 8)  → coreId 0 (slot "b")
    [InlineData("up", "5",  "up", 5)]  // d-pad up   (canonical 4)  → down  (5)
    [InlineData("l",  "11", "l",  11)] // left shoulder (canonical 10) → right (11)
    #pragma warning restore format
    public void ResolveSwaps_BtnEntry_NonCanonicalCoreId_ProducesSwap(
        string slot, string value, string expectedSlot, int expectedId)
    {
        Resolve(new Dictionary<string, string> { [$"input_player1_btn_{slot}"] = value })
            .ShouldBeDictionaryOf((expectedSlot, expectedId));
    }

    [Fact]
    public void ResolveSwaps_BtnEntry_CanonicalCoreId_IsSkipped()
    {
        Resolve(new Dictionary<string, string> { ["input_player1_btn_a"] = "8" }).ShouldBeEmpty();
    }

    [Fact]
    public void ResolveSwaps_BtnEntry_NonIntegerValue_IsSkipped()
    {
        Resolve(new Dictionary<string, string> { ["input_player1_btn_a"] = "nul" }).ShouldBeEmpty();
    }

    [Fact]
    public void ResolveSwaps_BtnEntry_UnknownSlot_IsSkipped()
    {
        Resolve(new Dictionary<string, string> { ["input_player1_btn_zzz"] = "0" }).ShouldBeEmpty();
    }

    [Fact]
    public void ResolveSwaps_BtnEntry_DisabledSlotMinusOne_ProducesSwap()
    {
        Resolve(new Dictionary<string, string> { ["input_player1_btn_a"] = "-1" })
            .ShouldBeDictionaryOf(("a", -1));
    }

    // ---- stk entries (input_player1_stk_{RetroArch's stick direction} = {coreId}) ----

    #pragma warning disable format
    [Theory]
    [InlineData("r_y+", "23", "r_y_plus",  23, "r_y_minus")] // right stick down → up
    [InlineData("l_x+", "0",  "l_x_plus",  0,  "l_x_minus")] // left stick right → the south face button
    [InlineData("r_x-", "-1", "r_x_minus", -1, "r_x_plus")]  // right stick left → nothing
    #pragma warning restore format
    public void ResolveSwaps_StkEntry_NonCanonicalCoreId_ProducesSwap_AndTheAxisOtherEndPressesNothing(
        string suffix, string value, string expectedSlot, int expectedId, string otherEnd)
    {
        // RetroArch takes the axis from the core once either end is remapped, and gives back only
        // the ends that are
        Resolve(new Dictionary<string, string> { [$"input_player1_stk_{suffix}"] = value })
            .ShouldBeDictionaryOf((expectedSlot, expectedId), (otherEnd, -1));
    }

    [Fact]
    public void ResolveSwaps_BothEndsOfAStickAxisRemapped_NeitherPressesNothing()
    {
        // an inverted axis: each end gives the other back
        Resolve(new Dictionary<string, string>
        {
            ["input_player1_stk_r_y+"] = "23",
            ["input_player1_stk_r_y-"] = "22",
        }).ShouldBeDictionaryOf(("r_y_plus", 23), ("r_y_minus", 22));
    }

    [Fact]
    public void ResolveSwaps_OtherEndWrittenAsItself_StillPressesNothing()
    {
        // an entry naming its own id is no remap, so it doesn't give the axis back
        Resolve(new Dictionary<string, string>
        {
            ["input_player1_stk_r_x+"] = "0",
            ["input_player1_stk_r_x-"] = "21",
        }).ShouldBeDictionaryOf(("r_x_plus", 0), ("r_x_minus", -1));
    }

    [Fact]
    public void ResolveSwaps_StkEntry_CanonicalCoreId_IsSkipped()
    {
        Resolve(new Dictionary<string, string> { ["input_player1_stk_r_y+"] = "22" }).ShouldBeEmpty();
    }

    [Fact]
    public void ResolveSwaps_StkEntry_UnknownDirection_IsSkipped()
    {
        Resolve(new Dictionary<string, string> { ["input_player1_stk_z_q+"] = "0" }).ShouldBeEmpty();
    }

    // ---- axis entries (input_player1_axis_{slot} = {axisValue}) ----

    #pragma warning disable format
    [Theory]
    [InlineData("l2", "+5", "l2", 13)] // l2 (canonical "+2") → r2: AxisValueToSlot["+5"]="r2", SlotToId["r2"]=13
    [InlineData("r2", "+2", "r2", 12)] // r2 (canonical "+5") → l2: AxisValueToSlot["+2"]="l2", SlotToId["l2"]=12
    #pragma warning restore format
    public void ResolveSwaps_AxisEntry_NonCanonicalAxisValue_ProducesSwap(
        string slot, string value, string expectedSlot, int expectedId)
    {
        Resolve(new Dictionary<string, string> { [$"input_player1_axis_{slot}"] = value })
            .ShouldBeDictionaryOf((expectedSlot, expectedId));
    }

    [Fact]
    public void ResolveSwaps_AxisEntry_CanonicalAxisValue_IsSkipped()
    {
        Resolve(new Dictionary<string, string> { ["input_player1_axis_l2"] = "+2" }).ShouldBeEmpty();
    }

    [Fact]
    public void ResolveSwaps_AxisEntry_UnknownAxisValue_IsSkipped()
    {
        Resolve(new Dictionary<string, string> { ["input_player1_axis_l2"] = "bogus" }).ShouldBeEmpty();
    }

    [Fact]
    public void ResolveSwaps_AxisEntry_ValueMapsToStickSlot_IsSkipped()
    {
        // "+0" is in AxisValueToSlot (→ "l_x_plus"), a stick direction, and an axis entry's target
        // is looked up among the sixteen buttons only
        Resolve(new Dictionary<string, string> { ["input_player1_axis_l2"] = "+0" }).ShouldBeEmpty();
    }

    // ---- unrecognized key prefix ----

    [Fact]
    public void ResolveSwaps_UnrecognizedKeyPrefix_IsSkipped()
    {
        Resolve(new Dictionary<string, string> { ["video_smooth"] = "true" }).ShouldBeEmpty();
    }
}
