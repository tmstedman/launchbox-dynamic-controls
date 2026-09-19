using System.Diagnostics.CodeAnalysis;

namespace DynamicControls.Plugins.RetroArch;

/// <summary>The two plugins RetroArch contributes, built over one shared overrides resolver.</summary>
public record RetroArchPlugins(
    RetroArchMappingSource Source,
    RetroArchSwapTransform Transform);

[ExcludeFromCodeCoverage]
internal static class RetroArchPluginsFactory
{
    /// <summary>
    /// Builds RetroArch's source and transform. Both are returned together and share one
    /// <see cref="IRetroArchGameOverridesResolver"/> instance, which is where a cache would go if
    /// reading the cfg and remap cascades twice per launch ever proves costly.
    /// </summary>
    public static RetroArchPlugins Create(
        LayeredFileSystem lfs,
        IFileSystem fs,
        ILogger logger,
        IApplicationData? applicationData = null)
    {
        var coreInfo = new RetroArchCoreInfo(fs, logger);
        var coreLoader = new RetroArchCoreLoader(logger, lfs);
        var reader = new RetroArchConfigFileReader(logger, fs);
        var cfgLoader = new RetroArchCfgLoader(reader, fs, applicationData ?? new SystemApplicationData());
        var remapLoader = new RetroArchRemapLoader(reader, fs);
        var cfgVariantResolver = new RetroArchVariantResolver(logger, "cfg");
        var remapVariantResolver = new RetroArchVariantResolver(logger, "remap");
        var cfgSwapResolver = new RetroArchCfgSwapResolver(logger);
        var remapSwapResolver = new RetroArchRemapSwapResolver(logger);
        var cfgResolver = new RetroArchOverridesResolver(cfgLoader, cfgVariantResolver, cfgSwapResolver);
        var remapResolver = new RetroArchOverridesResolver(remapLoader, remapVariantResolver, remapSwapResolver);
        var swapApplier = new RetroArchSwapApplier(logger);

        var overridesResolver = new RetroArchGameOverridesResolver(
            logger, coreInfo, coreLoader, cfgResolver, remapResolver);

        return new RetroArchPlugins(
            new RetroArchMappingSource(logger, overridesResolver),
            new RetroArchSwapTransform(logger, overridesResolver, swapApplier));
    }
}
