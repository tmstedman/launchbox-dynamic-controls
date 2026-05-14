using System.Diagnostics.CodeAnalysis;

namespace DynamicControls.Plugins.RetroArch;

/// <summary>
/// Outcome of parsing a RetroArch input override source (cfg cascade or .rmp file): an optional
/// variant override (libretro device type → core-declared variant) and the set of slot→coreId
/// swaps. Either field may be empty independently. Both <see cref="RetroArchConfigFileReader"/> and
/// <see cref="RetroArchOverridesResolver"/> return this so the consumer can treat both sources
/// symmetrically and apply them in priority order (cfg first, rmp on top).
/// </summary>
[ExcludeFromCodeCoverage]
public record RetroArchInputOverrides(
    RetroArchControllerConfig? Variant,
    Dictionary<string, int> Swaps);
