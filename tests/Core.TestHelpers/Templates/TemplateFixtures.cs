using DynamicControls.Templates;

namespace DynamicControls.Core.TestHelpers.Templates;

/// <summary>
/// Factory helpers for <see cref="Template"/>. Use via
/// <c>using static DynamicControls.Core.TestHelpers.Templates.TemplateFixtures;</c>. Defaults
/// to an empty <see cref="ResolvedLayout"/> and a no-op <see cref="ITemplateImageSource"/>;
/// tests that need to assert on resolver calls should pass their own implementation.
/// </summary>
public static class TemplateFixtures
{
    public static Template TemplateOf(
        double defaultMinOpacity = 0,
        double defaultInactiveBlurRadius = 0,
        double defaultFontSize = 12,
        ITemplateImageSource? imageSource = null,
        IReadOnlyList<ILayoutElement>? elements = null,
        IReadOnlyDictionary<InputDefinition, CollapseInfo>? collapseInfo = null,
        IReadOnlyDictionary<InputDefinition, IReadOnlyList<InputDefinition>>? inputDescendants = null)
    {
        IReadOnlyList<ILayoutElement> elementList = elements ?? [];
        return new(
            Name: "test",
            BaseImage: null,
            Layout: new ResolvedLayout(
                Elements: elementList,
                InputDescendants: inputDescendants ?? new InputDescendantsBuilder().Build(elementList),
                CollapseInfo: collapseInfo ?? new Dictionary<InputDefinition, CollapseInfo>(ReferenceEqualityComparer.Instance),
                DefaultFontSize: defaultFontSize,
                DefaultMinOpacity: defaultMinOpacity,
                DefaultInactiveBlurRadius: defaultInactiveBlurRadius),
            ImageSource: imageSource ?? new NoOpImageSource());
    }

    private sealed class NoOpImageSource : ITemplateImageSource
    {
        public ResolvedImagePaths Resolve(string src, string? platform, string? controller = null) =>
            new(src, null);
    }
}
