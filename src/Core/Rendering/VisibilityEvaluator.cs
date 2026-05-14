using System.Diagnostics.CodeAnalysis;
using DynamicControls.InputMapping;
using DynamicControls.Templates;

namespace DynamicControls.Rendering;

/// <summary>
/// Evaluates visibility conditions for template nodes against a VisibilityContext.
/// </summary>
public interface IVisibilityEvaluator
{
    /// <summary>
    /// Dispatches to the appropriate visibility check for any node type. InputDefinitions
    /// delegate to AnyRenderVisible; InputGroups pass when any child passes; OneOfs pass
    /// when any alternative passes.
    /// </summary>
    bool AnyVisible(ILayoutElement element, VisibilityContext ctx);

    /// <summary>
    /// Returns the visibility flags for an input, fanning out across its structural descendants.
    /// A parent input is considered active when any descendant has a label or mapping — that's
    /// what makes a complex input (e.g. a stick with directional children) light up as a unit.
    /// </summary>
    VisibilityFlags GetVisibilityFlags(InputDefinition input, VisibilityContext ctx);

    /// <summary>
    /// True when all of an input's renders have effective opacity of zero in the given context.
    /// Used to determine whether a collapsing stack slot should be vacated.
    /// </summary>
    bool AllImagesZeroOpacity(InputDefinition input, double defaultMinOpacity, VisibilityContext ctx);

    /// <summary>
    /// OR-reduces <see cref="VisibilityFlags"/> across every <see cref="InputDefinition"/>
    /// reachable through <paramref name="element"/>, following the same selection rules as
    /// <see cref="ILayoutFilter"/>: InputGroups are transparent (all children contribute),
    /// OneOfs contribute only the first alternative whose <see cref="AnyVisible"/> check passes.
    /// </summary>
    VisibilityFlags AggregateFlags(ILayoutElement element, VisibilityContext ctx);

}

public class VisibilityEvaluator : IVisibilityEvaluator
{
    public bool AnyVisible(ILayoutElement element, VisibilityContext ctx) => element switch
    {
        InputDefinition input => AnyRenderVisible(input, ctx),
        InputGroup group => group.Children.Any(c => AnyVisible(c, ctx)),
        OneOf oneOf => oneOf.Alternatives.Any(a => AnyVisible(a, ctx)),
        _ => false
    };

    public VisibilityFlags GetVisibilityFlags(InputDefinition input, VisibilityContext ctx)
    {
        IEnumerable<InputDefinition> all = ctx.InputDescendants[input].Prepend(input);
        bool hasLabel = all.Any(c => !string.IsNullOrEmpty(ctx.LabelText.GetValueOrDefault(c.Name)));
        bool isMapped = all.Any(c => IsMapped(ctx.Mapping, c.Name));
        return new VisibilityFlags(hasLabel, isMapped);
    }

    public bool AllImagesZeroOpacity(InputDefinition input, double defaultMinOpacity, VisibilityContext ctx)
    {
        VisibilityFlags flags = GetVisibilityFlags(input, ctx);
        return input.InputImages.Count == 0
            || input.InputImages.All(image =>
            {
                bool visible = flags.IsVisible(image.ShowIf, ctx.IsGameSpecific);
                double opacity = visible ? 1.0 : image.MinOpacity ?? defaultMinOpacity;
                return opacity <= 0;
            });
    }

    /// <summary>
    /// True if any of the input's renders is visible under its ShowIf in the given context,
    /// or if any structural descendant has a visible render. Used by AnyVisible to resolve
    /// InputDefinitions in the group pre-pass.
    /// </summary>
    private bool AnyRenderVisible(InputDefinition input, VisibilityContext ctx)
    {
        VisibilityFlags flags = GetVisibilityFlags(input, ctx);
        return input.InputImages.Any(image => flags.IsVisible(image.ShowIf, ctx.IsGameSpecific))
            || input.Children.Any(c => AnyVisible(c, ctx));
    }

    public VisibilityFlags AggregateFlags(ILayoutElement element, VisibilityContext ctx)
    {
        VisibilityFlags result = VisibilityFlags.None;
        Walk(element);
        return result;

        void Walk(ILayoutElement node)
        {
            switch (node)
            {
                case InputDefinition input:
                    result |= GetVisibilityFlags(input, ctx);
                    foreach (ILayoutElement child in input.Children)
                        Walk(child);
                    break;
                case InputGroup g:
                    foreach (ILayoutElement child in g.Children)
                        Walk(child);
                    break;
                case OneOf o:
                    foreach (ILayoutElement alt in o.Alternatives)
                    {
                        if (AnyVisible(alt, ctx))
                        {
                            Walk(alt);
                            break;
                        }
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled ILayoutElement subtype: {node.GetType().Name}");
            }
        }
    }

    /// <summary>
    /// True if the given generic input name is mapped: either a platform button drives it now,
    /// or its natural physical button is still present in the mapping (so the on-screen position
    /// still belongs to a real button even after a MAME/RetroArch remap).
    /// </summary>
    private static bool IsMapped(ResolvedMapping mapping, string name) =>
        mapping.InputToButton.ContainsKey(name)
        || (mapping.NaturalInputToButton.TryGetValue(name, out string? natural)
            && mapping.ButtonToInput.ContainsKey(natural));
}

/// <summary>
/// Pipeline input assembled once per render pass and threaded through LayoutFilter,
/// InputImageRenderer, and VisibilityEvaluator. Holds the mapping, label text, game-specific
/// flag, and pre-computed fan-out index for the current game.
/// </summary>
[ExcludeFromCodeCoverage]
public record VisibilityContext(
    ResolvedMapping Mapping,
    IReadOnlyDictionary<string, string> LabelText,
    bool IsGameSpecific,
    IReadOnlyDictionary<InputDefinition, IReadOnlyList<InputDefinition>> InputDescendants);
