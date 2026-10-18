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
    /// delegate to AnyRenderVisible; OneOfs pass when any alternative passes; Conditions pass
    /// on their own explicit check. A Container has no case of its own — unlike the &lt;Group&gt;
    /// it replaced, it never decides its own visibility, so it's never reached as a bare
    /// alternative to ask this of directly (see <see cref="Templates.Container"/>'s doc comment).
    /// </summary>
    bool AnyVisible(ILayoutElement element, VisibilityContext ctx);

    /// <summary>
    /// Returns the visibility flags for an input, fanning out across the names in
    /// <see cref="VisibilityContext.InputDescendants"/> (a whole control's direction names, per
    /// <see cref="DynamicControls.InputMapping.WholeInputs.PartsOf"/>). A whole is considered
    /// active when any of its direction names has a label or mapping — that's what makes a stick
    /// or Dpad light up as a unit even though the whole itself is never directly mapped.
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
    /// <see cref="ILayoutFilter"/>: Containers are transparent (all children contribute),
    /// OneOfs contribute only the first alternative whose <see cref="AnyVisible"/> check passes.
    /// </summary>
    VisibilityFlags AggregateFlags(ILayoutElement element, VisibilityContext ctx);

}

public class VisibilityEvaluator : IVisibilityEvaluator
{
    public bool AnyVisible(ILayoutElement element, VisibilityContext ctx) => element switch
    {
        InputDefinition input => AnyRenderVisible(input, ctx),
        OneOf oneOf => oneOf.Alternatives.Any(a => AnyVisible(a, ctx)),
        ConditionElement condition => AnyVisibleCondition(condition, ctx),
        _ => false
    };

    /// <summary>
    /// A <see cref="ConditionElement"/> is visible when its own check passes <b>and</b>, if any of
    /// its direct children is itself a nested <see cref="ConditionElement"/> (the compound-AND
    /// idiom — see <c>docs/layout-xml-schema.md</c>), that nested check also passes. Non-Condition
    /// children (Input/Container/Overlay) are deliberately *not* consulted here — that's
    /// <see cref="EvaluateCondition"/>'s whole point, to bypass <see cref="GetVisibilityFlags"/>'s
    /// descendant fold-in. Without this recursion, a <c>OneOf</c> choosing between alternatives
    /// would see only the outer check and could pick an alternative whose nested check then fails
    /// once rendered, stranding that slot with no fallback — the outer check alone isn't the full
    /// truth of whether this alternative has anything to show.
    /// </summary>
    private bool AnyVisibleCondition(ConditionElement condition, VisibilityContext ctx) =>
        EvaluateCondition(condition, ctx)
        && condition.Children.OfType<ConditionElement>().All(child => AnyVisibleCondition(child, ctx));

    /// <summary>
    /// Evaluates a <see cref="ConditionElement"/> directly against the named inputs in
    /// <see cref="ConditionElement.Names"/> — a raw dictionary/mapping lookup by name, never
    /// <see cref="GetVisibilityFlags"/>'s descendant fold-in. This is what lets a Condition gate
    /// on a name that isn't (or isn't only) one of its own children's names.
    /// </summary>
    private bool EvaluateCondition(ConditionElement condition, VisibilityContext ctx)
    {
        bool Matches(string name) => condition.Match switch
        {
            ConditionMatch.Label => !string.IsNullOrEmpty(ctx.LabelText.GetValueOrDefault(name)),
            ConditionMatch.Mapped => IsMapped(ctx.Mapping, name),
            _ => throw new InvalidOperationException($"Unhandled ConditionMatch: {condition.Match}")
        };

        return condition.Mode switch
        {
            ConditionMode.All => condition.Names.Count > 0 && condition.Names.All(Matches),
            ConditionMode.Any => condition.Names.Any(Matches),
            ConditionMode.None => condition.Names.All(n => !Matches(n)),
            _ => throw new InvalidOperationException($"Unhandled ConditionMode: {condition.Mode}")
        };
    }

    public VisibilityFlags GetVisibilityFlags(InputDefinition input, VisibilityContext ctx)
    {
        IEnumerable<string> names = ctx.InputDescendants[input].Prepend(input.Name);
        bool hasLabel = names.Any(n => !string.IsNullOrEmpty(ctx.LabelText.GetValueOrDefault(n)));
        bool isMapped = names.Any(n => IsMapped(ctx.Mapping, n));
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
    /// InputDefinitions reached as a OneOf alternative or Condition child.
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
                case Container c:
                    foreach (ILayoutElement child in c.Children)
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
                case ConditionElement condition:
                    if (AnyVisible(condition, ctx))
                    {
                        foreach (ILayoutElement child in condition.Children)
                            Walk(child);
                    }
                    break;
                case LabelElement:
                    // Leaf, not an InputDefinition -- contributes no flags of its own; its
                    // owning Input's flags are already counted when Walk reaches it structurally.
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
/// flag, and the whole-control fan-out names for the current template.
/// </summary>
[ExcludeFromCodeCoverage]
public record VisibilityContext(
    ResolvedMapping Mapping,
    IReadOnlyDictionary<string, string> LabelText,
    bool IsGameSpecific,
    IReadOnlyDictionary<InputDefinition, IReadOnlyList<string>> InputDescendants);
