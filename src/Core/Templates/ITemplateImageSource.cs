using System.Diagnostics.CodeAnalysis;

namespace DynamicControls.Templates;

/// <summary>
/// Resolves image filenames to their generic and platform-styled paths within a specific
/// template's directory. Carried on <see cref="Template"/> so the rendering layer can
/// resolve platform-button images at render time without depending on TemplateImageResolver.
/// </summary>
public interface ITemplateImageSource
{
    /// <summary>
    /// Resolves <paramref name="src"/> to its generic path and the most-specific styled path
    /// (Controller → Platform priority). The styled path is null when no platform-specific
    /// file exists. Results are cached — file existence checks run at most once per unique
    /// (src, platform, controller) combination within this template.
    /// </summary>
    ResolvedImagePaths Resolve(string src, string? platform, string? controller = null);
}

/// <summary>
/// Resolved generic and platform-styled paths for a template image. The styled path is the
/// most-specific existing file (Controller → Platform priority); null when no platform-specific
/// variant exists. The generic path is always set — either the template-local file, the
/// shared-root fallback, or a placeholder template-local path when neither exists on disk.
/// </summary>
[ExcludeFromCodeCoverage]
public record ResolvedImagePaths(string Generic, string? Styled);
