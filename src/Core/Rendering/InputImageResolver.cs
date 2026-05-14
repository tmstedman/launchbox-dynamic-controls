using System.Diagnostics.CodeAnalysis;
using DynamicControls.InputMapping;
using DynamicControls.Templates;

namespace DynamicControls.Rendering;

public interface IInputImageResolver
{
    string Resolve(InputImageDefinition image, InputDefinition input, ResolvedMapping mapping, Template template);
}

/// <summary>
/// Picks the rendered image path for a single render, given the input's current mapping state.
/// Image candidate paths (generic + platform-specific) are resolved via <see cref="ITemplateImageSource"/>
/// carried on the <see cref="Template"/>. Classification of the input's mapping state is split out
/// so the selection logic reads as a flat switch over three cases (Unmapped / MappedDefault /
/// Remapped). The remap decision is keyed on input.Name (logical identity), never on the borrowed
/// asset's name — useImage is an asset reference, not a mapping reference.
/// </summary>
public class InputImageResolver(ILogger logger) : IInputImageResolver
{
    private readonly ILogger _logger = logger;

    /// <summary>
    /// Resolves the image path for a single render belonging to the given input.
    /// </summary>
    /// <param name="image">Render whose image filename was stored at template build time.</param>
    /// <param name="input">The owning input — used only for logical identity in mapping/remap lookups.</param>
    /// <param name="mapping">Resolved platform mapping for the current game.</param>
    /// <param name="template">Template, used to locate image files on disk.</param>
    public string Resolve(
        InputImageDefinition image,
        InputDefinition input,
        ResolvedMapping mapping,
        Template template)
    {
        MappingState state = Classify(input.Name, mapping);

        string imageFile = image.UseImageFile ?? image.ImageFile;
        ResolvedImagePaths resolved = template.ImageSource.Resolve(imageFile, mapping.Platform, mapping.Controller);
        string generic = resolved.Generic;
        string? styled = resolved.Styled;

        string path = SelectImagePath(state, image, generic, styled, mapping, template);
        LogResolution(state, input.Name, path);
        return path;
    }

    internal string SelectImagePath(
        MappingState state,
        InputImageDefinition image,
        string generic,
        string? styled,
        ResolvedMapping mapping,
        Template template) => state switch
        {
            // Owning input isn't on this game's controller. A borrowing render references an
            // arbitrary asset, so honor any platform-specific variant of that asset; an identity
            // render falls back to the generic (platform-styled images for absent buttons mislead).
            Unmapped => image.UseImageFile != null ? styled ?? generic : generic,

            // Owning input is driven by its default physical button. Prefer the platform button
            // name as the primary candidate (e.g. "B.png" before "ButtonA.png"), then the generic.
            MappedDefault m => ResolveMappedImage(template, m.PlatformButton, mapping) ?? generic,

            // The physical button at this position normally drives a different input. Image
            // follows the physical button: look for {platformButton}.png in the platform folder
            // so the player sees the cabinet button they're actually pressing. Falls back to
            // this render's own generic if no platform-specific file exists.
            Remapped r => RemappedPlatformImage(r.PlatformButton, template, mapping) ?? generic,

            _ => throw new InvalidOperationException($"Unhandled MappingState subtype: {state.GetType().Name}")
        };

    internal string DescribeResolution(MappingState state, string inputName) => state switch
    {
        Unmapped => $"{inputName} (no platform mapping)",
        MappedDefault m => $"{inputName} <- {m.PlatformButton} (not remapped)",
        Remapped r => $"{inputName} <- {r.PlatformButton} -> {r.DefaultGenerics[0]} (remapped)",
        _ => throw new InvalidOperationException($"Unhandled MappingState subtype: {state.GetType().Name}")
    };

    /// <summary>
    /// Classifies the input's mapping state by comparing the current mapping against the
    /// selected controller's natural mapping (snapshotted on the same ResolvedMapping).
    /// </summary>
    private static MappingState Classify(string inputName, ResolvedMapping mapping)
    {
        if (!mapping.InputToButton.TryGetValue(inputName, out string? platformButton))
            return new Unmapped();

        if (!mapping.NaturalButtonToInput.TryGetValue(platformButton, out IReadOnlyList<string>? defaultGenerics)
            || defaultGenerics.Contains(inputName))
        {
            return new MappedDefault(platformButton);
        }

        return new Remapped(platformButton, defaultGenerics);
    }

    /// <summary>
    /// Resolves the image for a MappedDefault input. Prepends the physical platform button name
    /// as the highest-priority candidate (e.g. "B.png" before "ButtonA.png") so that files named
    /// after their platform button are preferred over the generic name.
    /// </summary>
    private static string? ResolveMappedImage(Template template, string platformButton, ResolvedMapping mapping) =>
        template.ImageSource.Resolve($"{platformButton}.png", mapping.Platform, mapping.Controller).Styled;

    /// <summary>
    /// For a Remapped input, returns the platform-specific image path that makes the player see
    /// the physical button they're pressing rather than the logical role it's been remapped to.
    /// Resolves {platformButton}.png from the platform folder — identical lookup to MappedDefault.
    /// Returns null if no platform-specific file exists — Resolve falls back to the owning render's generic.
    /// </summary>
    private static string? RemappedPlatformImage(string platformButton, Template template, ResolvedMapping mapping) =>
        template.ImageSource.Resolve($"{platformButton}.png", mapping.Platform, mapping.Controller).Styled;

    private void LogResolution(MappingState state, string inputName, string path)
    {
        _logger.Debug($"Button image: {DescribeResolution(state, inputName)}");
        _logger.Debug($"Button image resolved: {path}");
    }
}

/// <summary>
/// Three mutually exclusive outcomes of classifying an input against a game's mapping. Encoded
/// as records so the selection switch in InputImageResolver.Resolve can pattern-match against
/// each case directly — adding a new case (e.g. ambiguous remap) requires touching the switch
/// rather than threading another bool through the resolver.
/// </summary>
[ExcludeFromCodeCoverage]
internal abstract record MappingState;

/// <summary>No platform button drives this input in the current mapping. Either the input
/// isn't on this game's controller at all, or every binding that could drive it has been
/// remapped away.</summary>
[ExcludeFromCodeCoverage]
internal sealed record Unmapped : MappingState;

/// <summary>A platform button drives this input, and it's the same button that drives it by
/// default on this platform — no remap.</summary>
/// <param name="PlatformButton">The platform button currently mapped to this input — identical
/// to the platform default for this input. Carried for diagnostic logging only; the selection
/// logic doesn't need to look it up.</param>
[ExcludeFromCodeCoverage]
internal sealed record MappedDefault(string PlatformButton) : MappingState;

/// <summary>A platform button drives this input, but that button normally drives different
/// inputs on this platform. The image follows the physical button: the rendered image is the
/// remap-target's default platform-specific image, so the player sees the cabinet button
/// they're physically pressing rather than the logical role it's been redirected to.</summary>
/// <param name="PlatformButton">The platform button currently mapped to this input. Differs
/// from this input's default — that mismatch is what classifies the input as Remapped.</param>
/// <param name="DefaultGenerics">The generic input names this platform button drives by default
/// on this controller — i.e. the inputs the player expects to see at this physical button. The
/// first entry is the remap target whose platform-specific image is used as the rendered image.
/// Sourced from `ResolvedMapping.NaturalButtonToInput`, so it's independent of any per-game remap.</param>
[ExcludeFromCodeCoverage]
internal sealed record Remapped(string PlatformButton, IReadOnlyList<string> DefaultGenerics) : MappingState;
