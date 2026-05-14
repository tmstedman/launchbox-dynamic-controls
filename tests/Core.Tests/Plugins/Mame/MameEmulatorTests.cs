using DynamicControls.Plugins.Mame;

namespace DynamicControls.Core.Tests.Plugins.Mame;

/// <summary>
/// Unit tests for <see cref="MameEmulator"/>. The helper recognises a MAME executable purely
/// by filename prefix (case-insensitive), so the test matrix covers the documented variants
/// (mame / mame32 / mame64), case folding, common path shapes, and obvious non-matches.
/// </summary>
public class MameEmulatorTests
{
    [Theory]
    [InlineData("mame.exe")]
    [InlineData("mame64.exe")]
    [InlineData("mame32.exe")]
    [InlineData("mame")]            // no extension
    [InlineData("MAME.exe")]        // case-insensitive
    [InlineData("Mame64.exe")]      // mixed case
    [InlineData("mameui.exe")]      // fork prefixed with "mame" (StartsWith, not equals)
    public void IsMameExecutable_KnownMameExecutables_ReturnTrue(string filename)
    {
        // bare filename
        MameEmulator.IsMameExecutable(filename).ShouldBeTrue();

        // same filename in a parent directory — directory portion is ignored
        MameEmulator.IsMameExecutable(Path.Combine("Emulators", "MAME", filename)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("retroarch.exe")]
    [InlineData("notmame.exe")]     // does not start with "mame"
    [InlineData("dolphin.exe")]
    [InlineData("")]                // empty filename
    [InlineData("ame.exe")]         // missing leading 'm'
    public void IsMameExecutable_NonMameExecutables_ReturnFalse(string filename)
    {
        MameEmulator.IsMameExecutable(filename).ShouldBeFalse();
    }

    [Fact]
    public void IsMameExecutable_FilenameNotDirectoryDecides()
    {
        // "mame" appearing as a directory name does not make the executable a MAME variant
        string nonMameInsideMameDir = Path.Combine("mame", "retroarch.exe");
        MameEmulator.IsMameExecutable(nonMameInsideMameDir).ShouldBeFalse();
    }
}
