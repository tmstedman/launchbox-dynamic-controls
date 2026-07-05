namespace DynamicControls.Core.Tests.Infrastructure;

public class RomNameUtilsTests
{
    [Theory]
    #pragma warning disable format
    [InlineData("Sonic the Hedgehog (USA)",           "sonic the hedgehog")]
    [InlineData("OutRun (USA, Europe)",               "outrun")]
    [InlineData("Super Mario 64 (USA)",               "super mario 64")]
    [InlineData("Game (Rev 1) (USA)",                 "game")]
    [InlineData("Game [!]",                           "game")]
    [InlineData("Game (USA) [!]",                     "game")]
    [InlineData("sf2ce",                              "sf2ce")]
    [InlineData("Street Fighter II",                  "street fighter ii")]
    [InlineData("(USA)",                              "")]
    [InlineData("Game",                               "game")]
    // mismatched brackets are left untouched
    [InlineData("Game (USA]",                         "game (usa]")]
    [InlineData("Game [USA)",                         "game [usa)")]
    [InlineData("Game (Unclosed",                     "game (unclosed")]
    #pragma warning restore format
    public void NormalizeRomName_ProducesExpectedNormalizedForm(string input, string expected)
    {
        input.NormalizeRomName().ShouldBe(expected);
    }

    [Fact]
    public void NormalizeRomName_IsIdempotent()
    {
        string input = "OutRun (USA, Europe) [!]";
        string once = input.NormalizeRomName();
        string twice = once.NormalizeRomName();
        once.ShouldBe(twice);
    }
}
