using DynamicControls.Templates;

namespace DynamicControls.Core.TestHelpers.Templates;

/// <summary>
/// Fluent builder for <see cref="LayoutConfig"/> in tests. Lets a test declare a layout
/// tree (Input/Group/Stack/OneOf with Renders/Labels/Overlays) without the verbose record-init
/// syntax of the raw DTOs. Concrete-cast at the end with <see cref="ToConfig"/>, or via the
/// implicit conversion when a method already expects a <see cref="LayoutConfig"/>.
/// </summary>
internal class TestLayout
{
    private readonly LayoutConfig _config = new();

    /// <summary>Sets the template's unnamed &lt;Style&gt; — the per-template visual defaults.</summary>
    public TestLayout DefaultStyle(
        double? fontSize = null,
        double? minOpacity = null,
        double? inactiveBlurRadius = null)
    {
        _config.Head.Style = new StyleConfig
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
        _config.Head.NamedStyles[name] = new StyleConfig
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

    public TestLayout Stack(Action<StackBuilder> build)
    {
        _config.Elements.Add(BuildStack(build));
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

    public LayoutConfig ToConfig() => _config;

    public static implicit operator LayoutConfig(TestLayout l) => l._config;

    internal static InputNode BuildInput(string name, Action<InputBuilder>? build)
    {
        var b = new InputBuilder(name);
        build?.Invoke(b);
        return b.Node;
    }

    internal static StackNode BuildStack(Action<StackBuilder> build)
    {
        var b = new StackBuilder();
        build(b);
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
    #pragma warning restore format

    public InputBuilder Render(Action<RenderBuilder>? build = null)
    {
        var rb = new RenderBuilder();
        build?.Invoke(rb);
        Node.Renders.Add(rb.Node);
        return this;
    }

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

    public InputBuilder ChildStack(Action<StackBuilder> build) { Node.Children.Add(TestLayout.BuildStack(build)); return this; }
    public InputBuilder ChildGroup(Action<GroupBuilder> build) { Node.Children.Add(TestLayout.BuildGroup(build)); return this; }
    public InputBuilder ChildOneOf(Action<OneOfBuilder> build) { Node.Children.Add(TestLayout.BuildOneOf(build)); return this; }
}

internal class StackBuilder
{
    public StackNode Node { get; } = new();

    #pragma warning disable format
    public StackBuilder At(double x, double y)       { Node.X = Coordinate.Absolute(x); Node.Y = Coordinate.Absolute(y); return this; }
    public StackBuilder Offset(double dx, double dy) { Node.X = Coordinate.Relative(dx); Node.Y = Coordinate.Relative(dy); return this; }
    public StackBuilder Gap(double v)                { Node.Gap = v;       return this; }
    public StackBuilder Collapse()                   { Node.Collapse = true; return this; }

    public StackBuilder Input(string name, Action<InputBuilder>? build = null) { Node.Children.Add(TestLayout.BuildInput(name, build)); return this; }
    public StackBuilder Stack(Action<StackBuilder> build)                      { Node.Children.Add(TestLayout.BuildStack(build));       return this; }
    public StackBuilder Group(Action<GroupBuilder> build)                      { Node.Children.Add(TestLayout.BuildGroup(build));       return this; }
    public StackBuilder OneOf(Action<OneOfBuilder> build)                      { Node.Children.Add(TestLayout.BuildOneOf(build));       return this; }
    #pragma warning restore format

    public StackBuilder Overlay(string src, Action<OverlayBuilder>? build = null)
    {
        var ob = new OverlayBuilder(src);
        build?.Invoke(ob);
        Node.Overlays.Add(ob.Node);
        return this;
    }
}

internal class GroupBuilder
{
    public GroupNode Node { get; } = new();

    #pragma warning disable format
    public GroupBuilder Input(string name, Action<InputBuilder>? build = null) { Node.Children.Add(TestLayout.BuildInput(name, build)); return this; }
    public GroupBuilder Stack(Action<StackBuilder> build)                      { Node.Children.Add(TestLayout.BuildStack(build));       return this; }
    public GroupBuilder Group(Action<GroupBuilder> build)                      { Node.Children.Add(TestLayout.BuildGroup(build));       return this; }
    public GroupBuilder OneOf(Action<OneOfBuilder> build)                      { Node.Children.Add(TestLayout.BuildOneOf(build));       return this; }
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
    public OneOfBuilder Stack(Action<StackBuilder> build)                      { Node.Alternatives.Add(TestLayout.BuildStack(build));       return this; }
    public OneOfBuilder OneOf(Action<OneOfBuilder> build)                      { Node.Alternatives.Add(TestLayout.BuildOneOf(build));       return this; }
    #pragma warning restore format
}

internal class RenderBuilder
{
    public RenderNode Node { get; } = new();

    #pragma warning disable format
    public RenderBuilder At(double x, double y)        { Node.X = Coordinate.Absolute(x); Node.Y = Coordinate.Absolute(y); return this; }
    public RenderBuilder Offset(double dx, double dy)  { Node.X = Coordinate.Relative(dx); Node.Y = Coordinate.Relative(dy); return this; }
    public RenderBuilder Size(double w, double h)      { Node.Width = w; Node.Height = h; return this; }
    public RenderBuilder UseImage(string name)         { Node.UseImage = name;            return this; }
    public RenderBuilder ShowIf(string value)          { Node.ShowIf = value;             return this; }
    public RenderBuilder MinOpacity(double v)          { Node.MinOpacity = v;             return this; }
    public RenderBuilder InactiveBlurRadius(double v)  { Node.InactiveBlurRadius = v;     return this; }
    #pragma warning restore format
}

internal class OverlayBuilder(string src)
{
    public OverlayNode Node { get; } = new OverlayNode { Src = src };

    #pragma warning disable format
    public OverlayBuilder At(double x, double y)       { Node.X = Coordinate.Absolute(x); Node.Y = Coordinate.Absolute(y); return this; }
    public OverlayBuilder Offset(double dx, double dy) { Node.X = Coordinate.Relative(dx); Node.Y = Coordinate.Relative(dy); return this; }
    public OverlayBuilder Size(double w, double h)     { Node.Width = w; Node.Height = h; return this; }
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
    public LabelBuilder FontSize(double v)           { Node.FontSize = v;               return this; }
    #pragma warning restore format
}
