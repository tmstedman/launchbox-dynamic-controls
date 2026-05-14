using System.Diagnostics.CodeAnalysis;
using DynamicControls.Config;
using DynamicControls.InputMapping;
using DynamicControls.Plugins.Mame;
using DynamicControls.Plugins.RetroArch;

namespace DynamicControls.Composition;

/// <summary>
/// Wires the input-mapping subsystem. Builds every source and transform in priority order, hands
/// them to <see cref="InputMappingPlugins"/> for enable-filtering, and injects the plugins object
/// into the service. No logic lives here — gating is the plugin's own concern; filtering is
/// <see cref="InputMappingPlugins"/>' concern.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class InputMappingFactory
{
    /// <summary>
    /// Wires the input-mapping subsystem. <see cref="PerGameXmlMappingSource"/> is always the
    /// highest-priority source and <see cref="PlatformDefaultMappingSource"/> is always the
    /// fallback — those positions encode architectural invariants, not configuration. The
    /// <paramref name="sources"/> and <paramref name="transforms"/> parameters control
    /// what sits between (RetroArch in production; stubs in tests) and which transforms run on
    /// top (MAME in production). Both default to the production set when null; pass an empty
    /// list to opt out entirely.
    /// </summary>
    public static InputMappingService Create(
        LayeredFileSystem lfs,
        ILogger logger,
        GlobalConfig? config = null,
        IReadOnlyList<IInputMappingSource>? sources = null,
        IReadOnlyList<IInputMappingTransform>? transforms = null)
    {
        config ??= new GlobalConfig();

        var loader = new InputMappingLoader(logger, lfs);
        var resolver = new InputMappingResolver(logger);

        sources ??= [RetroArchMappingSourceFactory.Create(lfs, logger)];

        IInputMappingSource[] assembledSources =
        [
            new PerGameXmlMappingSource(logger, loader),
            .. sources,
            new PlatformDefaultMappingSource(logger),
        ];

        if (transforms == null)
        {
            var joycodes = new JoycodeMappingLoader(logger, lfs);
            var mameCfgLoader = new MameCfgLoader(logger, lfs.Fs, joycodes);
            transforms = [new MameInputMappingSource(logger, mameCfgLoader)];
        }

        var plugins = new InputMappingPlugins(logger, assembledSources, transforms, config);
        return new InputMappingService(loader, resolver, plugins);
    }
}
