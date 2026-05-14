using System.Diagnostics.CodeAnalysis;

namespace DynamicControls;

/// <summary>
/// Game metadata extracted from LaunchBox, passed through the resolution pipeline.
/// </summary>
/// <param name="RomDirectory">Directory containing the ROM file. Used to locate RetroArch
/// content-directory overrides (cfg and rmp files saved alongside the ROM). Null when not
/// available.</param>
/// <param name="RetroArchCore">RetroArch core name extracted from the emulator's command line,
/// without the `.dll` extension (e.g. "genesis_plus_gx_libretro"). Null when the emulator
/// isn't RetroArch or when no `-L`/`--libretro` flag is present.</param>
[ExcludeFromCodeCoverage]
public record GameInfo(
    string Platform,
    string RomName,
    string? CloneOf,
    string? EmulatorPath,
    string? RomDirectory,
    string? RetroArchCore);
