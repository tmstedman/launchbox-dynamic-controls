using System.Diagnostics.CodeAnalysis;

namespace DynamicControls.Templates;

/// <summary>
/// Fully resolved and cached data for a controller template, built from Layout.xml and the
/// template image folder. Produced by TemplateService (cached per template name) and consumed
/// by InputImageResolver, InputImageRenderer, OverlayRenderer, and InputLabelRenderer during
/// rendering.
/// </summary>
/// <param name="Name">The template name, matching the folder under Templates/.</param>
/// <param name="BaseImage">The discovered base controller image (BaseImage.png/jpg) with its
/// path and pixel dimensions. Null when no base image exists for this template — consumers fall
/// back to <see cref="RenderingDefaults.CanvasWidth"/> / <see cref="RenderingDefaults.CanvasHeight"/>
/// for the canvas size in that case.</param>
/// <param name="Layout">The resolved layout tree, template-wide style defaults, and the
/// precomputed InputDescendants index — the output of <see cref="LayoutResolver"/>.</param>
/// <param name="ImageSource">Resolves image filenames to their generic and styled paths within this
/// template's directory. Set by TemplateService at build time.</param>
[ExcludeFromCodeCoverage]
public record Template(
    string Name,
    BaseImage? BaseImage,
    ResolvedLayout Layout,
    ITemplateImageSource ImageSource);
