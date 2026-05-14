using DynamicControls.Plugins.RetroArch;
using NSubstitute;

namespace DynamicControls.Core.Tests.Plugins.RetroArch;

/// <summary>
/// Unit tests for <see cref="RetroArchRemapSwapResolver"/>. Focus: (1) btn swap entries —
/// non-canonical coreId, canonical skip, non-integer, disabled (−1), unknown slot;
/// (2) axis swap entries — non-canonical, canonical skip, unknown value, value maps to stick slot;
/// (3) unrecognized key prefixes are silently skipped; (4) null Game level returns empty.
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
        // "+0" is in AxisValueToSlot (→ "l_x_plus") but "l_x_plus" has no discrete coreId
        Resolve(new Dictionary<string, string> { ["input_player1_axis_l2"] = "+0" }).ShouldBeEmpty();
    }

    // ---- unrecognized key prefix ----

    [Fact]
    public void ResolveSwaps_UnrecognizedKeyPrefix_IsSkipped()
    {
        Resolve(new Dictionary<string, string> { ["video_smooth"] = "true" }).ShouldBeEmpty();
    }
}
