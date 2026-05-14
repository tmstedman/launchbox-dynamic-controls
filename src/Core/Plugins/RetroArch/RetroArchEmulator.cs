namespace DynamicControls.Plugins.RetroArch;

/// <summary>
/// Helpers for identifying a RetroArch emulator installation. Used to guard RetroArch-specific
/// data sources (remap files, cfg overrides) so they are only applied when the emulator is
/// actually RetroArch — not when a non-RetroArch emulator runs a compatible ROM.
/// </summary>
internal static class RetroArchEmulator
{
    /// <summary>
    /// True when the emulator executable is RetroArch.
    /// </summary>
    internal static bool IsRetroArchExecutable(string emulatorPath) =>
        Path.GetFileNameWithoutExtension(emulatorPath)
            .Equals("retroarch", StringComparison.OrdinalIgnoreCase);
}
