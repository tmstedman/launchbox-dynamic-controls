using DynamicControls.Templates;

namespace DynamicControls.Core.TestHelpers.Templates;

/// <summary>
/// Fluent builder for <see cref="LayoutDocument"/> in tests. Lets a test declare a layout
/// tree (Input/Group/OneOf with Labels/Overlays) without the verbose record-init
/// syntax of the raw DTOs. Concrete-cast at the end with <see cref="ToConfig"/>, or via the
/// implicit conversion when a method already expects a <see cref="LayoutDocument"/>.
/// </summary>
internal class TestLayout
{
    private readonly LayoutDocument _config = new();

    /// <summary>Sets the template's unnamed &lt;Style&gt; — the per-template visual defaults.</summary>
    public TestLayout DefaultStyle(
        double? fontSize = null,
        double? minOpacity = null,
        double? inactiveBlurRadius = null)
    {
        _config.Head.Style = new StyleNode
        {
            FontSize = fontSize,
            MinOpacity = minOpacity,
            InactiveBlurRadius = inactiveBlurRadius,
        };
        return this;
    }

    /// <summary>Adds a named &lt;Style&gt; referenced from Inputs via the `style` attribute.</summary>
    public TestLayout NamedStyle(
        string name,
        string? showIf = null,
        double? fontSize = null,
        double? minOpacity = null,
        double? inactiveBlurRadius = null)
    {
        _config.Head.NamedStyles[name] = new StyleNode
        {
            ShowIf = showIf,
            FontSize = fontSize,
            MinOpacity = minOpacity,
            InactiveBlurRadius = inactiveBlurRadius,
        };
        return this;
    }

    public TestLayout Input(string name, Action<InputBuilder>? build = null)
    {
        _config.Elements.Add(BuildInput(name, build));
        return this;
    }

    public TestLayout Group(Action<GroupBuilder> build)
    {
        _config.Elements.Add(BuildGroup(build));
        return this;
    }

    public TestLayout OneOf(Action<OneOfBuilder> build)
    {
        _config.Elements.Add(BuildOneOf(build));
        return this;
    }

    public TestLayout Condition(Action<ConditionBuilder> build)
    {
        _config.Elements.Add(BuildCondition(build));
        return this;
    }

    public LayoutDocument ToConfig() => _config;

    public static implicit operator LayoutDocument(TestLayout l) => l._config;

    internal static InputNode BuildInput(string name, Action<InputBuilder>? build)
    {
        var b = new InputBuilder(name);
        build?.Invoke(b);
        return b.Node;
    }

    internal static GroupNode BuildGroup(Action<GroupBuilder> build)
    {
        var b = new GroupBuilder();
        build(b);
        return b.Node;
    }

    internal static OneOfNode BuildOneOf(Action<OneOfBuilder> build)
    {
        var b = new OneOfBuilder();
        build(b);
        return b.Node;
    }

    internal static ConditionNode BuildCondition(Action<ConditionBuilder> build)
    {
        var b = new ConditionBuilder();
        build(b);
        return b.Node;
    }

    internal static LabelNode BuildLabel(Action<LabelBuilder>? build)
    {
        var b = new LabelBuilder();
        build?.Invoke(b);
        return b.Node;
    }
}

internal class InputBuilder(string name)
{
    public InputNode Node { get; } = new InputNode { Name = name };

    #pragma warning disable format
    public InputBuilder Style(string name)                  { Node.Style = name;             return this; }
    public InputBuilder ShowIf(string value)                { Node.ShowIf = value;           return this; }
    public InputBuilder FontSize(double v)                  { Node.FontSize = v;             return this; }
    public InputBuilder MinOpacity(double v)                { Node.MinOpacity = v;           return this; }
    public InputBuilder InactiveBlurRadius(double v)        { Node.InactiveBlurRadius = v;   return this; }
    public InputBuilder At(double x, double y)              { Node.X = Coordinate.Absolute(x); Node.Y = Coordinate.Absolute(y); return this; }
    public InputBuilder Offset(double dx, double dy)        { Node.X = Coordinate.Relative(dx); Node.Y = Coordinate.Relative(dy); return this; }
    public InputBuilder Size(double w, double h)            { Node.Width = w; Node.Height = h; return this; }
    public InputBuilder UseImage(string name)               { Node.UseImage = name;          return this; }
    #pragma warning restore format

    public InputBuilder Label(Action<LabelBuilder>? build = null)
    {
        var lb = new LabelBuilder();
        build?.Invoke(lb);
        Node.Labels.Add(lb.Node);
        return this;
    }

    public InputBuilder Overlay(string src, Action<OverlayBuilder>? build = null)
    {
        var ob = new OverlayBuilder(src);
        build?.Invoke(ob);
        Node.Overlays.Add(ob.Node);
        return this;
    }

    public InputBuilder Child(string name, Action<InputBuilder>? build = null)
    {
        Node.Children.Add(TestLayout.BuildInput(name, build));
        return this;
    }

    public InputBuilder ChildGroup(Action<GroupBuilder> build) { Node.Children.Add(TestLayout.BuildGroup(build)); return this; }
    public InputBuilder ChildOneOf(Action<OneOfBuilder> build) { Node.Children.Add(TestLayout.BuildOneOf(build)); return this; }
    public InputBuilder ChildCondition(Action<ConditionBuilder> build) { Node.Children.Add(TestLayout.BuildCondition(build)); return this; }
}

internal class GroupBuilder
{
    public GroupNode Node { get; } = new();

    #pragma warning disable format
    public GroupBuilder At(double x, double y)       { Node.X = Coordinate.Absolute(x); Node.Y = Coordinate.Absolute(y); return this; }
    public GroupBuilder Offset(double dx, double dy) { Node.X = Coordinate.Relative(dx); Node.Y = Coordinate.Relative(dy); return this; }
    public GroupBuilder Gap(double v)                { Node.Gap = v;       return this; }
    public GroupBuilder Collapse()                   { Node.Collapse = true; return this; }
    public GroupBuilder VAlign(string v)             { Node.VAlign = v;    return this; }
    public GroupBuilder Style(string name)                  { Node.Style = name;             return this; }
    public GroupBuilder ShowIf(string value)                { Node.ShowIf = value;           return this; }
    public GroupBuilder FontSize(double v)                  { Node.FontSize = v;             return this; }
    public GroupBuilder MinOpacity(double v)                { Node.MinOpacity = v;           return this; }
    public GroupBuilder InactiveBlurRadius(double v)        { Node.InactiveBlurRadius = v;   return this; }

    public GroupBuilder Input(string name, Action<InputBuilder>? build = null) { Node.Children.Add(TestLayout.BuildInput(name, build)); return this; }
    public GroupBuilder Group(Action<GroupBuilder> build)                     { Node.Children.Add(TestLayout.BuildGroup(build));       return this; }
    public GroupBuilder OneOf(Action<OneOfBuilder> build)                     { Node.Children.Add(TestLayout.BuildOneOf(build));       return this; }
    public GroupBuilder Condition(Action<ConditionBuilder> build)             { Node.Children.Add(TestLayout.BuildCondition(build));   return this; }
    public GroupBuilder LooseLabel(Action<LabelBuilder>? build = null)        { Node.Children.Add(TestLayout.BuildLabel(build));       return this; }
    #pragma warning restore format

    public GroupBuilder Overlay(string src, Action<OverlayBuilder>? build = null)
    {
        var ob = new OverlayBuilder(src);
        build?.Invoke(ob);
        Node.Overlays.Add(ob.Node);
        return this;
    }
}

internal class OneOfBuilder
{
    public OneOfNode Node { get; } = new();

    #pragma warning disable format
    public OneOfBuilder Input(string name, Action<InputBuilder>? build = null) { Node.Alternatives.Add(TestLayout.BuildInput(name, build)); return this; }
    public OneOfBuilder Group(Action<GroupBuilder> build)                      { Node.Alternatives.Add(TestLayout.BuildGroup(build));       return this; }
    public OneOfBuilder OneOf(Action<OneOfBuilder> build)                      { Node.Alternatives.Add(TestLayout.BuildOneOf(build));       return this; }
    public OneOfBuilder Condition(Action<ConditionBuilder> build)              { Node.Alternatives.Add(TestLayout.BuildCondition(build));   return this; }
    public OneOfBuilder LooseLabel(Action<LabelBuilder>? build = null)        { Node.Alternatives.Add(TestLayout.BuildLabel(build));        return this; }
    #pragma warning restore format
}

internal class ConditionBuilder
{
    public ConditionNode Node { get; } = new();

    #pragma warning disable format
    public ConditionBuilder Any(string names)   { Node.Any = names;  return this; }
    public ConditionBuilder All(string names)   { Node.All = names;  return this; }
    public ConditionBuilder None(string names)  { Node.None = names; return this; }
    public ConditionBuilder Match(string value) { Node.Match = value; return this; }

    public ConditionBuilder Input(string name, Action<InputBuilder>? build = null) { Node.Children.Add(TestLayout.BuildInput(name, build)); return this; }
    public ConditionBuilder Group(Action<GroupBuilder> build)                      { Node.Children.Add(TestLayout.BuildGroup(build));       return this; }
    public ConditionBuilder OneOf(Action<OneOfBuilder> build)                      { Node.Children.Add(TestLayout.BuildOneOf(build));       return this; }
    public ConditionBuilder Condition(Action<ConditionBuilder> build)              { Node.Children.Add(TestLayout.BuildCondition(build));   return this; }
    public ConditionBuilder LooseLabel(Action<LabelBuilder>? build = null)        { Node.Children.Add(TestLayout.BuildLabel(build));       return this; }
    #pragma warning restore format
}

internal class OverlayBuilder(string src)
{
    public OverlayNode Node { get; } = new OverlayNode { Src = src };

    #pragma warning disable format
    public OverlayBuilder At(double x, double y)       { Node.X = Coordinate.Absolute(x); Node.Y = Coordinate.Absolute(y); return this; }
    public OverlayBuilder Offset(double dx, double dy) { Node.X = Coordinate.Relative(dx); Node.Y = Coordinate.Relative(dy); return this; }
    public OverlayBuilder Size(double w, double h)     { Node.Width = w; Node.Height = h; return this; }
    public OverlayBuilder Style(string name)           { Node.Style = name;               return this; }
    public OverlayBuilder ShowIf(string value)         { Node.ShowIf = value;             return this; }
    public OverlayBuilder MinOpacity(double v)         { Node.MinOpacity = v;             return this; }
    public OverlayBuilder InactiveBlurRadius(double v) { Node.InactiveBlurRadius = v;     return this; }
    #pragma warning restore format
}

internal class LabelBuilder
{
    public LabelNode Node { get; } = new();

    #pragma warning disable format
    public LabelBuilder At(double x, double y)       { Node.X = Coordinate.Absolute(x); Node.Y = Coordinate.Absolute(y); return this; }
    public LabelBuilder Offset(double dx, double dy) { Node.X = Coordinate.Relative(dx); Node.Y = Coordinate.Relative(dy); return this; }
    public LabelBuilder Align(string align)          { Node.Align = align;              return this; }
    public LabelBuilder Style(string name)           { Node.Style = name;               return this; }
    public LabelBuilder FontSize(double v)           { Node.FontSize = v;               return this; }
    #pragma warning restore format
}
