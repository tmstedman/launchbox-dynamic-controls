using DynamicControls.Plugins.RetroArch;

namespace DynamicControls.Core.Tests.Plugins.RetroArch;

/// <summary>
/// Unit tests for <see cref="RetroArchEmulator"/>. The helper recognises a RetroArch executable
/// by an exact, case-insensitive filename match (in contrast to <c>MameEmulator</c>, which uses
/// a prefix match). The test matrix covers extension/case variations, common path shapes, and
/// boundary non-matches — including prefixed forks like "retroarchfork.exe" that would pass a
/// StartsWith check but should not be treated as RetroArch.
/// </summary>
public class RetroArchEmulatorTests
{
    [Theory]
    [InlineData("retroarch.exe")]
    [InlineData("retroarch")]       // no extension
    [InlineData("RETROARCH.exe")]   // case-insensitive
    [InlineData("RetroArch.exe")]   // mixed case
    public void IsRetroArchExecutable_KnownRetroArchExecutables_ReturnTrue(string filename)
    {
        // bare filename
        RetroArchEmulator.IsRetroArchExecutable(filename).ShouldBeTrue();

        // same filename in a parent directory — directory portion is ignored
        RetroArchEmulator.IsRetroArchExecutable(Path.Combine("Emulators", "RetroArch", filename)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("mame.exe")]
    [InlineData("dolphin.exe")]
    [InlineData("")]                    // empty filename
    [InlineData("retro.exe")]           // not the full name
    [InlineData("retroarchfork.exe")]   // prefix match: NOT treated as RetroArch (exact-match contract)
    [InlineData("retroarch64.exe")]     // same — no -64 variant exists, exact match only
    public void IsRetroArchExecutable_NonRetroArchExecutables_ReturnFalse(string filename)
    {
        RetroArchEmulator.IsRetroArchExecutable(filename).ShouldBeFalse();
    }

    [Fact]
    public void IsRetroArchExecutable_FilenameNotDirectoryDecides()
    {
        // a non-RetroArch executable living inside a directory called "retroarch" returns false
        string nonRaInsideRaDir = Path.Combine("retroarch", "mame.exe");
        RetroArchEmulator.IsRetroArchExecutable(nonRaInsideRaDir).ShouldBeFalse();
    }
}
