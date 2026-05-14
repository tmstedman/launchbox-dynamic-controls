using System.Diagnostics.CodeAnalysis;
using DynamicControls.Config;
using DynamicControls.InputMapping;
using DynamicControls.Labels;
using DynamicControls.Rendering;
using DynamicControls.Static;
using DynamicControls.Templates;

namespace DynamicControls.Composition;

/// <summary>
/// Composition root for Core services. Builds the shared dependencies (filesystem, logger,
/// config), delegates each subsystem's wiring to its per-topic factory, and assembles the
/// final <see cref="ControllerOverlayService"/>. Entry points (plugin, preview CLI) call this
/// and wire their own layer-specific pieces on top.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ControllerOverlayFactory
{
    public static ControllerOverlayService Create(string rootDir)
    {
        var fs = new SystemFileSystem();
        var logger = new Logger(fs, rootDir);
        var lfs = new LayeredFileSystem(rootDir, fs);

        GlobalConfig config = ConfigLoader.Load(lfs, logger);
        logger.IsDebugEnabled = config.Debug;
        // Always clear on startup so the log reflects only the current session and can't grow
        // unbounded — Info/Error write regardless of the debug flag.
        logger.ClearLog();

        InputLabelsService inputLabelsService = InputLabelsFactory.Create(lfs, logger, config);
        InputMappingService inputMappingService = InputMappingFactory.Create(lfs, logger, config);
        TemplateService templateService = TemplateFactory.Create(rootDir, logger, fs);
        InputRenderingService inputRenderingService = InputRenderingFactory.Create(logger);

        var staticImageResolver = new StaticImageResolver(logger, lfs);

        return new ControllerOverlayService(
            logger,
            inputLabelsService,
            templateService,
            staticImageResolver,
            inputMappingService,
            inputRenderingService,
            config.DefaultTemplate);
    }
}
