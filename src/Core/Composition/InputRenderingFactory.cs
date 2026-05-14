using System.Diagnostics.CodeAnalysis;
using DynamicControls.Rendering;

namespace DynamicControls.Composition;

/// <summary>
/// Wires the input-rendering subsystem: assembles the label renderer, layout filter, and image
/// renderer into an <see cref="InputRenderingService"/>. Used by the root composer in production
/// and by service-level functional tests.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class InputRenderingFactory
{
    /// <summary>
    /// Builds a fully-wired <see cref="InputRenderingService"/>. Takes only a logger because the
    /// rendering subsystem has no filesystem or rootDir dependency — it operates on already-loaded
    /// templates, mappings, and labels.
    /// </summary>
    public static InputRenderingService Create(ILogger logger)
    {
        var labelRenderer = new InputLabelRenderer(logger);
        var inputImageResolver = new InputImageResolver(logger);
        var visibilityEvaluator = new VisibilityEvaluator();
        var layoutFilter = new LayoutFilter(visibilityEvaluator);
        var imageRenderer = new InputImageRenderer(inputImageResolver);

        return new InputRenderingService(layoutFilter, labelRenderer, imageRenderer);
    }
}
