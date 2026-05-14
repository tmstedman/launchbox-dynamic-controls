using System.Diagnostics.CodeAnalysis;

namespace DynamicControls.Plugins.RetroArch;

[ExcludeFromCodeCoverage]
internal static class RetroArchMappingSourceFactory
{
    public static RetroArchMappingSource Create(
        LayeredFileSystem lfs,
        ILogger logger,
        IApplicationData? applicationData = null)
    {
        var coreInfo = new RetroArchCoreInfo(lfs.Fs, logger);
        var coreLoader = new RetroArchCoreLoader(logger, lfs);
        var reader = new RetroArchConfigFileReader(logger, lfs.Fs);
        var cfgLoader = new RetroArchCfgLoader(reader, lfs.Fs, applicationData ?? new SystemApplicationData());
        var remapLoader = new RetroArchRemapLoader(reader, lfs.Fs);
        var cfgVariantResolver = new RetroArchVariantResolver(logger, "cfg");
        var remapVariantResolver = new RetroArchVariantResolver(logger, "remap");
        var cfgSwapResolver = new RetroArchCfgSwapResolver(logger);
        var remapSwapResolver = new RetroArchRemapSwapResolver(logger);
        var cfgResolver = new RetroArchOverridesResolver(cfgLoader, cfgVariantResolver, cfgSwapResolver);
        var remapResolver = new RetroArchOverridesResolver(remapLoader, remapVariantResolver, remapSwapResolver);
        var swapApplier = new RetroArchSwapApplier(logger);
        return new RetroArchMappingSource(logger, coreInfo, coreLoader, cfgResolver, remapResolver, swapApplier);
    }
}
