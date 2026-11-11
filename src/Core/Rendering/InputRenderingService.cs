using DynamicControls.InputMapping;
using DynamicControls.Labels;
using DynamicControls.Templates;

namespace DynamicControls.Rendering;

/// <summary>
/// Renders a fully-resolved Template against a game's mapping and labels, producing a flat
/// <see cref="RenderResult"/> of positioned labels and images ready for the view model.
/// </summary>
public interface IInputRenderingService
{
    /// <summary>
    /// Filters the template's layout for the current visibility state, renders each surviving
    /// input's images, labels, and group-level overlays, and returns the aggregated result.
    /// </summary>
    RenderResult Render(Template template, ResolvedMapping mapping, ResolvedLabels labels);
}

/// <summary>
/// Production implementation: delegates layout filtering to <see cref="ILayoutFilter"/>, label
/// rendering to <see cref="IInputLabelRenderer"/>, and image rendering to
/// <see cref="IInputImageRenderer"/>; applies the per-input collapse YOffset to labels
/// post-render (image YOffset is baked in by the image renderer itself).
/// </summary>
public class InputRenderingService(
    ILayoutFilter layoutFilter,
    IInputLabelRenderer labelRenderer,
    IInputImageRenderer imageRenderer,
    WholeLabelCollapser wholeLabelCollapser) : IInputRenderingService
{
    private readonly ILayoutFilter _layoutFilter = layoutFilter;
    private readonly IInputLabelRenderer _labelRenderer = labelRenderer;
    private readonly IInputImageRenderer _imageRenderer = imageRenderer;
    private readonly WholeLabelCollapser _wholeLabelCollapser = wholeLabelCollapser;

    public RenderResult Render(
        Template template,
        ResolvedMapping mapping,
        ResolvedLabels labels)
    {
        // Reconcile composite labels against their parts' here rather than during label
        // resolution: this is the first point a template has been chosen, and the template is
        // what declares which of its inputs are composites.
        IReadOnlyDictionary<string, string> labelText =
            _wholeLabelCollapser.Collapse(labels.LabelText, template.Layout.Wholes);

        var ctx = new VisibilityContext(
            Mapping: mapping,
            LabelText: labelText,
            IsGameSpecific: labels.IsGameSpecific,
            Wholes: template.Layout.Wholes,
            GameLabelled: GameLabelledWithParts(labels, template.Layout.Wholes));
        FilteredLayout layout = _layoutFilter.Filter(template, ctx);

        var allLabels = new List<RenderedLabel>();
        var allImages = new List<RenderedImage>();

        foreach (LayoutInput li in layout.Inputs)
        {
            string? labelValue = labelText.GetValueOrDefault(li.Input.Name);
            allImages.AddRange(_imageRenderer.Render(li, template, mapping, labels.IsGameSpecific));
            allLabels.AddRange(_labelRenderer.Render(li, labelValue)
                .Select(l => li.YOffset == 0 ? l : l with { Top = l.Top + li.YOffset }));
        }

        foreach (LayoutGroupOverlay g in layout.GroupOverlays)
        {
            bool visible = g.Flags.IsVisible(g.Overlay.ShowIf, labels.IsGameSpecific);
            double opacity = visible ? 1.0 : g.Overlay.MinOpacity ?? template.Layout.DefaultMinOpacity;
            if (opacity <= 0) continue;
            allImages.Add(new RenderedImage(
                Source: g.Overlay.Source,
                Left: g.Overlay.X,
                Top: g.Overlay.Y,
                Width: g.Overlay.Width,
                Height: g.Overlay.Height,
                Opacity: opacity,
                BlurRadius: visible ? 0.0 : g.Overlay.InactiveBlurRadius ?? template.Layout.DefaultInactiveBlurRadius));
        }

        return new RenderResult(allLabels, allImages);
    }

    /// <summary>
    /// The game-labelled inputs, plus the parts of any composite the game labelled as a whole
    /// when none of those parts has a label of its own — that is when
    /// <see cref="WholeLabelCollapser"/> copies the whole's label onto them, so they carry the
    /// game's text and a condition asking about them must see it as the game's. Where a part does
    /// have a label, nothing is copied and every part keeps whoever wrote its own.
    /// </summary>
    private static IReadOnlySet<string> GameLabelledWithParts(
        ResolvedLabels labels,
        IReadOnlyDictionary<string, IReadOnlyList<string>> wholes)
    {
        IReadOnlySet<string> gameLabelled = labels.GameLabelled;
        HashSet<string>? widened = null;
        foreach ((string whole, IReadOnlyList<string> parts) in wholes)
        {
            if (!gameLabelled.Contains(whole) || parts.Any(labels.LabelText.ContainsKey)) continue;
            widened ??= [.. gameLabelled];
            widened.UnionWith(parts);
        }
        return widened ?? gameLabelled;
    }
}
