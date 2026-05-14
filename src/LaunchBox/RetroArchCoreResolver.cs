using System.Text.RegularExpressions;
using Unbroken.LaunchBox.Plugins.Data;

namespace DynamicControls.LaunchBox;

public interface IRetroArchCoreResolver
{
    string? Resolve(IEmulator? emulator, string platform);
}

/// <summary>
/// Extracts the RetroArch core name a game will run under, from LaunchBox's emulator metadata.
/// The core lives in the emulator's command line as `-L "path/to/core.dll"` (or `--libretro`);
/// LaunchBox exposes both a global command line (`IEmulator.CommandLine`) and per-platform
/// command lines (`IEmulatorPlatform.CommandLine`) — the platform-specific one wins, because
/// different LaunchBox platforms configured under the same RetroArch emulator typically use
/// different cores.
/// </summary>
public class RetroArchCoreResolver : IRetroArchCoreResolver
{
    private static readonly Regex CoreFlagPattern = new(
        @"(?:-L|--libretro)\s+(?:""([^""]+)""|(\S+))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Returns the core name (DLL filename without extension, e.g. "genesis_plus_gx_libretro")
    /// for the given emulator + platform, or null when no `-L`/`--libretro` flag is present
    /// in either the platform-specific or global command line. A null result means "no per-core
    /// override directory applies" — the caller treats it as either non-RetroArch or RetroArch
    /// without core-specific config to consult.
    /// </summary>
    public string? Resolve(IEmulator? emulator, string platform)
    {
        if (emulator == null) return null;

        string? platformCommandLine = emulator.GetAllEmulatorPlatforms()
            ?.FirstOrDefault(p => p.Platform == platform)?.CommandLine;
        string? commandLine = platformCommandLine ?? emulator.CommandLine;
        if (string.IsNullOrEmpty(commandLine)) return null;

        Match match = CoreFlagPattern.Match(commandLine);
        if (!match.Success) return null;

        string dllPath = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        return Path.GetFileNameWithoutExtension(dllPath);
    }
}
