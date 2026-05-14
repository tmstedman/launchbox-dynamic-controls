namespace DynamicControls.Plugins.RetroArch;

/// <summary>
/// Loads per-level key/value data from a RetroArch installation directory for a given core and
/// game. Returns null when no applicable file exists at any level. Implemented by
/// <see cref="RetroArchCfgLoader"/> (per-core cfg cascade) and
/// <see cref="RetroArchRemapLoader"/> (remap cascade).
/// </summary>
public interface IRetroArchGameLoader
{
    RetroArchGameData? Load(string retroArchDir, string? core, GameInfo game);
}
