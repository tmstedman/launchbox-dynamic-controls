using System.Diagnostics.CodeAnalysis;

namespace DynamicControls;

/// <summary>
/// Game metadata extracted from LaunchBox, passed through the resolution pipeline.
/// </summary>
/// <param name="Platform">LaunchBox platform name (e.g. "Sega Genesis").</param>
/// <param name="RomName">ROM filename without extension, derived from
/// <c>ApplicationPath</c>. Used as the primary filename stem when looking up per-game label
/// and input-mapping XML files (e.g. <c>Labels/{platform}/{RomName}.xml</c>).</param>
/// <param name="CloneOf">Parent ROM name for MAME clones and similar sets. When a per-game
/// lookup by <see cref="RomName"/> finds nothing, loaders retry with this value.</param>
/// <param name="EmulatorPath">Full path to the emulator executable. Used to locate emulator
/// data files (RetroArch cfg, MAME cfg). Null when not available.</param>
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
