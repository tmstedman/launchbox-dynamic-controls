using DynamicControls.Templates;

namespace DynamicControls.Rendering;

/// <summary>
/// Renders an input's label definitions into positioned <see cref="RenderedLabel"/>s with
/// alignment offsets applied. Returns an empty list when the input has no label value, so
/// callers don't need to special-case missing labels.
/// </summary>
public interface IInputLabelRenderer
{
    /// <summary>
    /// Renders all label slots on <paramref name="input"/> with text
    /// <paramref name="labelValue"/>. Each slot's alignment shifts the x-position so the
    /// renderer downstream can position by left edge consistently.
    /// </summary>
    List<RenderedLabel> Render(InputDefinition input, string? labelValue);
}

/// <summary>
/// Production implementation: alignment computation uses <see cref="RenderingDefaults.LabelWidth"/>
/// as the assumed label-box width for centering/right-alignment offsets.
/// </summary>
public class InputLabelRenderer(ILogger logger) : IInputLabelRenderer
{
    private const double LabelWidth = RenderingDefaults.LabelWidth;

    private readonly ILogger _logger = logger;

    /// <inheritdoc />
    public List<RenderedLabel> Render(InputDefinition input, string? labelValue)
    {
        if (string.IsNullOrEmpty(labelValue)) return [];

        return [.. input.Labels.Select(label =>
        {
            (double left, string alignment) = label.Alignment switch
            {
                "right" => (label.X - LabelWidth, "Right"),
                "center" => (label.X - (LabelWidth / 2), "Center"),
                _ => (label.X, "Left")
            };
            _logger.Debug($"Label: {labelValue} at ({label.X},{label.Y}) align={label.Alignment} fontSize={label.FontSize}");
            return new RenderedLabel(
                Left: left,
                Top: label.Y - (label.FontSize * 0.75),
                Text: labelValue,
                Alignment: alignment,
                FontSize: label.FontSize,
                InputName: input.Name);
        })];
    }
}
