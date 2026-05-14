using System.Diagnostics.CodeAnalysis;
using DynamicControls.Templates;

namespace DynamicControls.Composition;

/// <summary>
/// Wires the template subsystem: builds the loader, image resolver, configurer, and descendants
/// index builder into a <see cref="TemplateService"/>. Used by the root composer in production
/// and by service-level functional tests against a fixture tree.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class TemplateFactory
{
    /// <summary>
    /// Builds a fully-wired <see cref="TemplateService"/> for the given plugin root directory.
    /// Optional parameters let the root composer share already-built dependencies. No
    /// <see cref="DynamicControls.Config.GlobalConfig"/> parameter — template wiring is unaffected
    /// by feature flags today.
    /// </summary>
    public static TemplateService Create(
        string rootDir,
        ILogger? logger = null,
        IFileSystem? fs = null,
        ITemplateImageResolver? imageResolver = null)
    {
        fs ??= new SystemFileSystem();
        logger ??= new Logger(fs, rootDir);

        var templateLoader = new TemplateLoader(logger, fs, rootDir);
        imageResolver ??= new TemplateImageResolver(logger, fs, new ImageHeader(), rootDir);
        var descendantsBuilder = new InputDescendantsBuilder();
        var templateConfigurer = new LayoutResolver(logger, descendantsBuilder);

        return new TemplateService(
            templateLoader,
            imageResolver,
            templateConfigurer);
    }
}
