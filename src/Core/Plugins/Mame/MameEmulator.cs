namespace DynamicControls.Plugins.Mame;

/// <summary>
/// Helpers for identifying a MAME emulator installation. Used to guard MAME-specific
/// data sources (cfg remaps, controls.dat labels) so they are only applied when the
/// emulator is actually MAME — not when a non-MAME emulator runs a MAME ROM.
/// </summary>
internal static class MameEmulator
{
    /// <summary>
    /// True when the emulator executable is a MAME variant (mame.exe, mame64.exe, mame32.exe, etc.).
    /// </summary>
    internal static bool IsMameExecutable(string emulatorPath) =>
        Path.GetFileNameWithoutExtension(emulatorPath)
            .StartsWith("mame", StringComparison.OrdinalIgnoreCase);
}
