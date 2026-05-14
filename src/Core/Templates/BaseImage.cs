using System.Diagnostics.CodeAnalysis;

namespace DynamicControls.Templates;

/// <summary>
/// The resolved base controller image for a template: its path on disk and its pixel dimensions.
/// Produced by <see cref="TemplateImageResolver.FindBaseImage"/>; null when no BaseImage.png
/// exists in the template folder.
/// </summary>
[ExcludeFromCodeCoverage]
public record BaseImage(string Path, double Width, double Height);
