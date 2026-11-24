using System.Diagnostics.CodeAnalysis;
using DynamicControls.Config;
using DynamicControls.InputMapping;
using DynamicControls.Plugins.Mame;
using DynamicControls.Plugins.RetroArch;

namespace DynamicControls.Composition;

/// <summary>
/// Wires the input-mapping subsystem. Builds every provider in priority order, hands them to
/// <see cref="InputMappingProviders"/> for enable-filtering, and injects the chain into the
/// service. No logic lives here — gating is the provider's own concern; filtering is
/// <see cref="InputMappingProviders"/>' concern.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class InputMappingFactory
{
    /// <summary>
    /// Wires the input-mapping subsystem. <see cref="PerGameMappingProvider"/> is always the
    /// highest-priority provider and <see cref="PlatformDefaultMappingProvider"/> is always the
    /// fallback — those positions encode architectural invariants, not configuration. The
    /// <paramref name="providers"/> parameter controls what sits between them (RetroArch and MAME
    /// in production; stubs in tests). It defaults to the production set when null; pass an empty
    /// list to opt out entirely.
    /// </summary>
    public static InputMappingService Create(
        LayeredFileSystem lfs,
        IFileSystem fs,
        ILogger logger,
        GlobalConfig? config = null,
        IReadOnlyList<IInputMappingProvider>? providers = null)
    {
        config ??= new GlobalConfig();

        var loader = new InputMappingLoader(logger, lfs);
        var resolver = new InputMappingResolver(logger);

        if (providers == null)
        {
            var joycodes = new JoycodeMappingLoader(logger, lfs);
            var mameCfgLoader = new MameCfgLoader(logger, fs, joycodes, new SystemDelay());
            providers =
            [
                RetroArchProviderFactory.Create(lfs, fs, logger),
                new MameMappingProvider(logger, mameCfgLoader),
            ];
        }

        IInputMappingProvider[] assembled =
        [
            new PerGameMappingProvider(logger),
            .. providers,
            new PlatformDefaultMappingProvider(logger),
        ];

        var chain = new InputMappingProviders(logger, assembled, config);
        return new InputMappingService(loader, resolver, chain, new ControlResolver(logger));
    }
}
