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
        IReadOnlyList<string> parts = template.Layout.Wholes.GetValueOrDefault(input.Name, []);
        MappingState state = Classify(input.Name, mapping, parts);

        string imageFile = image.UseImageFile ?? image.ImageFile;
        ResolvedImagePaths resolved = template.ImageSource.Resolve(imageFile, mapping.Platform, mapping.Controller);
        string generic = resolved.Generic;
        string? styled = resolved.Styled;

        (string Buttons, string Path)? combined =
            mapping.InputToButton.ContainsKey(input.Name) ? null : CombinedImage(parts, mapping, template);
        string path = combined?.Path ?? SelectImagePath(state, image, generic, styled, mapping, template);
        if (combined != null) LogCombined(input.Name, combined.Value.Buttons, path);
        else LogResolution(state, input.Name, path);
        // Only the generic can be a placeholder: a styled path is found by existing. Probes for a
        // platform file that isn't there come back with a missing generic too, which is why the
        // check lives here, on the path actually chosen, rather than in the resolver.
        if (path == generic && !resolved.GenericExists)
            _logger.Error($"Image not found: {path}");
        return path;
    }

    /// <summary>
    /// The platform's picture of the buttons that drive <paramref name="parts"/> together, found
    /// as <c>{buttons, space-joined}.png</c> in the platform folder — Nintendo 64's
    /// <c>C-Left C-Right.png</c> for the two horizontal directions of the C cluster. Null
    /// when the parts are driven by fewer than two buttons (one button is the cover's business,
    /// below), when any part is driven by none, or when no such file exists. Carries the button
    /// names with the path, since the classification the other log line reports is not what chose it.
    ///
    /// <para>The name is a fact about the set of buttons, not about which one currently drives
    /// which part, so the buttons are sorted by name. Swapping C-Left and C-Right so each drives
    /// the other's direction leaves the same two buttons and so the same picture. The platform's
    /// own mapping can't supply an order that survives that: a per-game or RetroArch remap
    /// <em>is</em> the mapping's natural state, so ordering by where each button naturally sits
    /// would follow the swap.</para>
    ///
    /// <para>Tried before the covering button because that one accepts a wider control: C-Any
    /// reaches all four C directions, so it covers the two horizontal ones too and would draw its
    /// four-circle picture for a pair.</para>
    /// </summary>
    private static (string Buttons, string Path)? CombinedImage(
        IReadOnlyList<string> parts, ResolvedMapping mapping, Template template)
    {
        List<string> buttons = [];
        foreach (string part in parts)
        {
            if (!mapping.InputToButton.TryGetValue(part, out string? button)) return null;
            if (!buttons.Contains(button)) buttons.Add(button);
        }
        if (buttons.Count < 2) return null;

        string name = string.Join(' ', buttons.OrderBy(b => b, StringComparer.Ordinal).Select(b => b.SafeFileName()));
        string? path = template.ImageSource.Resolve($"{name}.png", mapping.Platform, mapping.Controller).Styled;
        return path == null ? null : (name, path);
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
    ///
    /// <para>A declared composite (<paramref name="parts"/> non-empty) that no button drives
    /// directly falls back to a button whose own reach covers every one of its parts. Nintendo
    /// 64 is why: its <c>C-Any</c> is the cluster the four C buttons make up, and the stick it
    /// drives draws <c>C-Any.png</c> rather than a generic one. A composite reached only through
    /// its parts would otherwise classify as Unmapped, and an identity render of an unmapped
    /// input falls through to the generic image — silently, since no label moves.</para>
    ///
    /// <para>Every part must be covered by the same button. A remap that split them across
    /// several leaves no single button standing for the whole, which is the honest answer: there
    /// is no longer one physical control there to picture.</para>
    /// </summary>
    private static MappingState Classify(string inputName, ResolvedMapping mapping, IReadOnlyList<string> parts)
    {
        if (!mapping.InputToButton.TryGetValue(inputName, out string? platformButton))
        {
            platformButton = parts.Count > 0 ? ButtonCovering(parts, mapping) : null;
            if (platformButton == null) return new Unmapped();
        }

        // The parts check is guarded on there being parts at all: All() over an empty list is
        // true, which would make every ordinary input a MappedDefault and strand Remapped as dead
        // code.
        if (!mapping.NaturalButtonToInput.TryGetValue(platformButton, out IReadOnlyList<string>? defaultGenerics)
            || defaultGenerics.Contains(inputName)
            || (parts.Count > 0 && parts.All(defaultGenerics.Contains)))
        {
            return new MappedDefault(platformButton);
        }

        return new Remapped(platformButton, defaultGenerics);
    }

    /// <summary>The single platform button whose current reach includes every one of
    /// <paramref name="parts"/>, or null when none does or several do.</summary>
    private static string? ButtonCovering(IReadOnlyList<string> parts, ResolvedMapping mapping)
    {
        string? found = null;
        foreach ((string button, IReadOnlyList<string> reaches) in mapping.ButtonToInput)
        {
            if (!parts.All(reaches.Contains)) continue;
            if (found != null) return null;
            found = button;
        }
        return found;
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

    private void LogCombined(string inputName, string buttons, string path)
    {
        _logger.Debug($"Button image: {inputName} <- {buttons} (the buttons driving its parts)");
        _logger.Debug($"Button image resolved: {path}");
    }

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
