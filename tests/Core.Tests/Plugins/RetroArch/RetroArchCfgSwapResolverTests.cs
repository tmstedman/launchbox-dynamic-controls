using DynamicControls.Plugins.RetroArch;
using NSubstitute;

namespace DynamicControls.Core.Tests.Plugins.RetroArch;

/// <summary>
/// Unit tests for <see cref="RetroArchCfgSwapResolver"/>. Focus: (1) dinput detection — global
/// key skips swap detection (case-insensitive); (2) game-level gating — null Game returns empty
/// swaps; (3) btn entries — physBtn resolved via BtnNumberToSlot → SlotToId, with separate paths
/// for disabled (−1), unknown physBtn, unknown slot, and non-int/non-hat values; (4) hat entries
/// — d-pad via hat notation, canonical and non-canonical; (5) axis entries — trigger swaps,
/// canonical skip, unknown value and stick-slot paths with debug logs.
/// </summary>
public class RetroArchCfgSwapResolverTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly RetroArchCfgSwapResolver _underTest;

    public RetroArchCfgSwapResolverTests()
    {
        _underTest = new RetroArchCfgSwapResolver(_logger);
    }

    private Dictionary<string, int> Resolve(
        Dictionary<string, string>? gameLevel = null,
        Dictionary<string, string>? globalLevel = null) =>
        _underTest.ResolveSwaps(new RetroArchGameData(globalLevel, null, null, gameLevel));

    // ---- dinput detection ----

    [Theory]
    [InlineData("dinput")]
    [InlineData("DINPUT")] // OrdinalIgnoreCase
    public void ResolveSwaps_DinputInGlobal_ReturnsEmptyAndLogsInfo(string driverName)
    {
        // given a game level that would produce swaps, but dinput is the active driver
        var result = Resolve(
            gameLevel: new Dictionary<string, string> { ["input_player1_a_btn"] = "0" },
            globalLevel: new Dictionary<string, string> { ["input_joypad_driver"] = driverName });

        result.ShouldBeEmpty();
        _logger.Received().Info(Arg.Is<string>(s => s.Contains("dinput")));
    }

    // ---- game-level gating ----

    [Fact]
    public void ResolveSwaps_GameLevelNull_ReturnsEmpty()
    {
        // given no per-game cfg file (Game level is null)
        var result = Resolve(gameLevel: null);

        result.ShouldBeEmpty();
    }

    // ---- btn entries (input_player1_{slot}_btn = {physBtn}) ----

    #pragma warning disable format
    [Theory]
    [InlineData("a", "0",  "a",  0)]  // east face  (canonical physBtn 1) → physBtn 0 = slot "b" → coreId 0
    [InlineData("b", "1",  "b",  8)]  // south face (canonical physBtn 0) → physBtn 1 = slot "a" → coreId 8
    [InlineData("l", "5",  "l",  11)] // left shoulder (canonical 4)      → physBtn 5 = slot "r" → coreId 11
    #pragma warning restore format
    public void ResolveSwaps_BtnEntry_NonCanonicalPhysBtn_ProducesSwap(
        string slot, string physBtnStr, string expectedSlot, int expectedCoreId)
    {
        var result = Resolve(gameLevel: new Dictionary<string, string> { [$"input_player1_{slot}_btn"] = physBtnStr });

        result.ShouldBeDictionaryOf((expectedSlot, expectedCoreId));
    }

    [Fact]
    public void ResolveSwaps_BtnEntry_CanonicalPhysBtn_IsSkipped()
    {
        // given slot "a" recorded as its canonical physBtn 1 — no actual remap
        Resolve(gameLevel: new Dictionary<string, string> { ["input_player1_a_btn"] = "1" }).ShouldBeEmpty();
    }

    [Fact]
    public void ResolveSwaps_BtnEntry_DisabledMinusOne_ProducesSwap()
    {
        // given physBtn -1 — RetroArch's unbind sentinel; passed through as coreId -1
        Resolve(gameLevel: new Dictionary<string, string> { ["input_player1_a_btn"] = "-1" })
            .ShouldBeDictionaryOf(("a", -1));
    }

    [Fact]
    public void ResolveSwaps_BtnEntry_PhysBtnNotInBtnNumberToSlot_IsSkipped()
    {
        // given physBtn 99 — not assigned to any slot in the canonical SDL mapping
        Resolve(gameLevel: new Dictionary<string, string> { ["input_player1_a_btn"] = "99" }).ShouldBeEmpty();
    }

    [Fact]
    public void ResolveSwaps_BtnEntry_UnknownSlot_IsSkipped()
    {
        Resolve(gameLevel: new Dictionary<string, string> { ["input_player1_zzz_btn"] = "0" }).ShouldBeEmpty();
    }

    [Fact]
    public void ResolveSwaps_BtnEntry_NonIntegerNonHatValue_IsSkipped()
    {
        // given a value that is neither an integer nor a hat string
        Resolve(gameLevel: new Dictionary<string, string> { ["input_player1_a_btn"] = "nul" }).ShouldBeEmpty();
    }

    // ---- hat entries (d-pad via input_player1_{slot}_btn = h{...}) ----

    #pragma warning disable format
    [Theory]
    [InlineData("up",   "h0down",  "up",   5)] // up   (canonical "h0up")   → "h0down" = down  → coreId 5
    [InlineData("left", "h0right", "left", 7)] // left (canonical "h0left") → "h0right" = right → coreId 7
    #pragma warning restore format
    public void ResolveSwaps_HatEntry_NonCanonicalHatValue_ProducesSwap(
        string slot, string hatValue, string expectedSlot, int expectedCoreId)
    {
        Resolve(gameLevel: new Dictionary<string, string> { [$"input_player1_{slot}_btn"] = hatValue })
            .ShouldBeDictionaryOf((expectedSlot, expectedCoreId));
    }

    [Fact]
    public void ResolveSwaps_HatEntry_CanonicalHatValue_IsSkipped()
    {
        Resolve(gameLevel: new Dictionary<string, string> { ["input_player1_up_btn"] = "h0up" }).ShouldBeEmpty();
    }

    [Fact]
    public void ResolveSwaps_HatEntry_HatValueNotInMapping_IsSkipped()
    {
        // "h1up" — valid hat syntax but not in HatValueToSlot
        Resolve(gameLevel: new Dictionary<string, string> { ["input_player1_up_btn"] = "h1up" }).ShouldBeEmpty();
    }

    [Fact]
    public void ResolveSwaps_HatEntry_UnknownSlot_IsSkipped()
    {
        Resolve(gameLevel: new Dictionary<string, string> { ["input_player1_zzz_btn"] = "h0up" }).ShouldBeEmpty();
    }

    // ---- axis entries (input_player1_{slot}_axis = {axisValue}) ----

    #pragma warning disable format
    [Theory]
    [InlineData("l2", "+5", "l2", 13)] // l2 (canonical "+2") → "+5" = r2 → coreId 13
    [InlineData("r2", "+2", "r2", 12)] // r2 (canonical "+5") → "+2" = l2 → coreId 12
    #pragma warning restore format
    public void ResolveSwaps_AxisEntry_NonCanonicalAxisValue_ProducesSwap(
        string slot, string axisValue, string expectedSlot, int expectedCoreId)
    {
        Resolve(gameLevel: new Dictionary<string, string> { [$"input_player1_{slot}_axis"] = axisValue })
            .ShouldBeDictionaryOf((expectedSlot, expectedCoreId));
    }

    [Fact]
    public void ResolveSwaps_AxisEntry_CanonicalAxisValue_IsSkipped()
    {
        Resolve(gameLevel: new Dictionary<string, string> { ["input_player1_l2_axis"] = "+2" }).ShouldBeEmpty();
    }

    [Fact]
    public void ResolveSwaps_AxisEntry_UnknownSlot_IsSkipped()
    {
        Resolve(gameLevel: new Dictionary<string, string> { ["input_player1_zzz_axis"] = "+5" }).ShouldBeEmpty();
    }

    [Fact]
    public void ResolveSwaps_AxisEntry_UnknownAxisValue_LogsAndIsSkipped()
    {
        Resolve(gameLevel: new Dictionary<string, string> { ["input_player1_l2_axis"] = "bogus" }).ShouldBeEmpty();
        _logger.Received().Debug(Arg.Is<string>(s => s.Contains("not in canonical baseline")));
    }

    [Fact]
    public void ResolveSwaps_AxisEntry_ValueMapsToStickSlot_LogsAndIsSkipped()
    {
        // "+0" is in AxisValueToSlot (→ "l_x_plus") but "l_x_plus" has no discrete coreId
        Resolve(gameLevel: new Dictionary<string, string> { ["input_player1_l2_axis"] = "+0" }).ShouldBeEmpty();
        _logger.Received().Debug(Arg.Is<string>(s => s.Contains("stick-axis swaps not supported")));
    }
}
