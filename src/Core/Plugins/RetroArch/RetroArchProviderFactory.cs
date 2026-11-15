using System.Diagnostics.CodeAnalysis;

namespace DynamicControls.Plugins.RetroArch;

[ExcludeFromCodeCoverage]
internal static class RetroArchProviderFactory
{
    /// <summary>
    /// Builds RetroArch's mapping provider over one <see cref="IRetroArchGameOverridesResolver"/>,
    /// which is where a cache would go if reading the cfg and remap cascades ever proves costly.
    /// </summary>
    public static RetroArchMappingProvider Create(
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

        return new RetroArchMappingProvider(logger, overridesResolver, swapApplier);
    }
}
