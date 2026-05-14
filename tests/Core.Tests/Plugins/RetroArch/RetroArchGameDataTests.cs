using DynamicControls.Plugins.RetroArch;

namespace DynamicControls.Core.Tests.Plugins.RetroArch;

/// <summary>
/// Unit tests for <see cref="RetroArchGameData.Merged"/>. Covers (1) cascade priority — Game
/// overrides all; ContentDir overrides Core and Global; Core overrides Global; (2) non-conflicting
/// keys from all levels are included; (3) null levels are skipped; (4) all-null produces an empty
/// merged view.
/// </summary>
public class RetroArchGameDataTests
{
    // ---- cascade priority ----

    public static IEnumerable<object[]> CascadePriorityCases =>
    [
        ["Game",       new RetroArchGameData(new(){ ["k"]="G" }, new(){ ["k"]="C" }, new(){ ["k"]="D" }, new(){ ["k"]="M" })],
        ["ContentDir", new RetroArchGameData(new(){ ["k"]="G" }, new(){ ["k"]="C" }, new(){ ["k"]="D" }, null)],
        ["Core",       new RetroArchGameData(new(){ ["k"]="G" }, new(){ ["k"]="C" }, null,               null)],
        ["Global",     new RetroArchGameData(new(){ ["k"]="G" }, null,               null,               null)],
    ];

    [Theory]
    [MemberData(nameof(CascadePriorityCases))]
    public void Merged_MostSpecificLevelWinsOnConflict(string winnerLabel, RetroArchGameData data)
    {
        #pragma warning disable format
        string expected = winnerLabel switch
        {
            "Game"       => "M",
            "ContentDir" => "D",
            "Core"       => "C",
            "Global"     => "G",
            _            => throw new ArgumentException(winnerLabel)
        };
        #pragma warning restore format

        data.Merged.ShouldBeDictionaryOf(("k", expected));
    }

    // ---- non-conflicting keys from all levels ----

    [Fact]
    public void Merged_NonConflictingKeys_AllLevelsContribute()
    {
        #pragma warning disable format
        var data = new RetroArchGameData(
            Global:     new() { ["global_key"]     = "G" },
            Core:       new() { ["core_key"]       = "C" },
            ContentDir: new() { ["contentdir_key"] = "D" },
            Game:       new() { ["game_key"]       = "M" });

        data.Merged.ShouldBeDictionaryOf(
            ("global_key",     "G"),
            ("core_key",       "C"),
            ("contentdir_key", "D"),
            ("game_key",       "M"));
        #pragma warning restore format
    }

    // ---- null levels ----

    [Fact]
    public void Merged_NullLevels_AreSkipped()
    {
        var data = new RetroArchGameData(null, new() { ["k"] = "C" }, null, null);

        data.Merged.ShouldBeDictionaryOf(("k", "C"));
    }

    [Fact]
    public void Merged_AllNull_ReturnsEmpty()
    {
        new RetroArchGameData(null, null, null, null).Merged.ShouldBeEmpty();
    }
}
