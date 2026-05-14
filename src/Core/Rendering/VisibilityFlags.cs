using DynamicControls.Templates;

namespace DynamicControls.Rendering;

/// <summary>
/// Aggregate visibility state for an input or group: whether any reachable input has a label,
/// and whether any is mapped to a physical button. OR-reduced across structural descendants.
/// </summary>
public record VisibilityFlags(bool HasLabel, bool IsMapped)
{
    public static readonly VisibilityFlags None = new(false, false);

    /// <summary>
    /// Returns true when these flags satisfy the given <paramref name="showIf"/> condition.
    /// <list type="bullet">
    ///   <item><term>Always</term><description>unconditionally visible</description></item>
    ///   <item><term>Label</term><description>visible when any reachable input has a label</description></item>
    ///   <item><term>Mapped</term><description>visible when any reachable input is mapped to a physical button</description></item>
    ///   <item><term>Auto</term>
    ///     <description>when game-specific label data is available, behaves like Label
    ///     (show inputs the game uses); otherwise behaves like Mapped
    ///     (show inputs the controller physically has)</description></item>
    /// </list>
    /// </summary>
    public bool IsVisible(ShowIfCondition showIf, bool isGameSpecific) => showIf switch
    {
        ShowIfCondition.Always => true,
        ShowIfCondition.Label => HasLabel,
        ShowIfCondition.Mapped => IsMapped,
        ShowIfCondition.Auto => isGameSpecific ? HasLabel : IsMapped,
        _ => throw new ArgumentOutOfRangeException(nameof(showIf), showIf, "Unknown ShowIfCondition")
    };

    /// <summary>
    /// OR-reduces two flag sets: the result has <see cref="HasLabel"/> if either operand does,
    /// and <see cref="IsMapped"/> if either operand does. Used to fan out visibility across
    /// structural descendants — a parent is considered labelled or mapped if any descendant is.
    /// </summary>
    public static VisibilityFlags operator |(VisibilityFlags a, VisibilityFlags b) =>
        new(a.HasLabel || b.HasLabel, a.IsMapped || b.IsMapped);
}
