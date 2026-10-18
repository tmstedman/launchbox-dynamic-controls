using System.Globalization;
using System.Xml;

namespace DynamicControls.Templates;

/// <summary>
/// Parses controller template <c>Layout.xml</c> files into thin DTOs. No coordinate resolution,
/// style inheritance, or image lookup happens at this layer — callers receive the raw element
/// tree as authored in XML.
/// </summary>
public interface ITemplateLoader
{
    /// <summary>
    /// Loads and parses the <c>Layout.xml</c> for the given template. Returns null when the
    /// layout file does not exist.
    /// </summary>
    /// <param name="templateName">Template folder name under <c>Templates/</c>.</param>
    LayoutDocument? LoadLayout(string templateName);
}

/// <summary>
/// Production implementation: filesystem and XML parsing run lazily on each call (no caching);
/// invalid attributes and missing required fields are logged as errors but never throw.
/// </summary>
public class TemplateLoader(ILogger logger, IFileSystem fs, string rootDir) : ITemplateLoader
{
    private readonly ILogger _logger = logger;
    private readonly IFileSystem _fs = fs;
    private readonly string _templatesDir = Path.Combine(rootDir, "Templates");

    /// <inheritdoc />
    public LayoutDocument? LoadLayout(string templateName)
    {
        string templateDir = Path.Combine(_templatesDir, templateName);
        string layoutPath = Path.Combine(templateDir, "Layout.xml");
        _logger.Debug($"Template layout path: {layoutPath}, Exists: {_fs.FileExists(layoutPath)}");

        if (!_fs.FileExists(layoutPath)) return null;

        var result = new LayoutDocument();
        using Stream stream = _fs.OpenRead(layoutPath);
        var doc = new XmlDocument();
        doc.Load(stream);
        XmlElement root = doc.DocumentElement!;

        foreach (XmlElement node in root.ChildNodes.OfType<XmlElement>())
        {
            switch (node.Name)
            {
                case "Head":
                    result.Head = ParseHead(node);
                    break;
                case "Body":
                    ParseBodyInto(node, result.Elements);
                    break;
                default:
                    _logger.Error($"Invalid element <{node.Name}> in <ControllerTemplate>");
                    break;
            }
        }

        int topLevelInputCount = result.Elements.OfType<InputNode>().Count();
        int containerCount = result.Elements.OfType<ContainerNode>().Count();
        _logger.Debug($"Template layout: {topLevelInputCount} top-level inputs, {containerCount} top-level containers");
        return result;
    }

    /// <summary>Parses a &lt;Head&gt; element. Each &lt;Style&gt; child is either unnamed
    /// (template-wide defaults) or named (a referenceable bundle stored in NamedStyles). Other
    /// children are logged as errors.</summary>
    private HeadNode ParseHead(XmlElement headNode)
    {
        var head = new HeadNode();
        foreach (XmlElement child in headNode.ChildNodes.OfType<XmlElement>())
        {
            if (child.Name != "Style")
            {
                _logger.Error($"Invalid element <{child.Name}> in <Head>");
                continue;
            }

            string? name = child.Attributes["name"]?.Value;
            StyleNode style = ParseStyle(child);
            if (string.IsNullOrEmpty(name))
                head.Style = style;
            else
                head.NamedStyles[name] = style;
        }
        return head;
    }

    /// <summary>Parses a &lt;Style&gt; element's attributes. Each is nullable — absence means
    /// "fall through to the next layer" in the resolution chain (Input's explicit value, then
    /// the referenced style, then per-element attribute, then the built-in default).</summary>
    private StyleNode ParseStyle(XmlElement styleNode)
    {
        var style = new StyleNode { ShowIf = styleNode.Attributes["showIf"]?.Value };
        if (ReadDouble(styleNode, "fontSize") is double fontSize) style.FontSize = fontSize;
        if (ReadDouble(styleNode, "minOpacity") is double minOpacity) style.MinOpacity = minOpacity;
        if (ReadDouble(styleNode, "inactiveBlurRadius") is double blur) style.InactiveBlurRadius = blur;
        return style;
    }

    /// <summary>Routes a &lt;Body&gt;'s children (Input / Container / OneOf) into the layout's
    /// Elements list.</summary>
    private void ParseBodyInto(XmlElement bodyNode, List<ILayoutNode> output)
    {
        foreach (XmlElement child in bodyNode.ChildNodes.OfType<XmlElement>())
        {
            if (!TryParseLayoutChild(child, output))
                _logger.Error($"Invalid element <{child.Name}> in <Body>");
        }
    }

    /// <summary>Parses one layout-child element (Input / Container / OneOf / Condition / Label) and
    /// appends it to <paramref name="output"/>. Returns true if the element name matched one of
    /// those (caller is responsible for handling unknown names). An Input or Condition that
    /// fails its own validation is treated as matched but not appended.
    /// <para>A loose Label parsed here (i.e. one that isn't a direct child of its own Input —
    /// <see cref="ParseInputNode"/> intercepts that case before ever calling this method) has no
    /// Input of its own to attach to; it resolves against whichever Input is ambient at this
    /// point in the tree, at build time (see <c>LayoutResolver.BuildContext.CurrentInputName</c>).</para>
    /// </summary>
    private bool TryParseLayoutChild(XmlElement node, List<ILayoutNode> output)
    {
        switch (node.Name)
        {
            case "Input":
                InputNode? input = ParseInputNode(node);
                if (input != null) output.Add(input);
                return true;
            case "Container":
                output.Add(ParseContainerNode(node));
                return true;
            case "OneOf":
                output.Add(ParseOneOfNode(node));
                return true;
            case "Condition":
                ConditionNode? condition = ParseConditionNode(node);
                if (condition != null) output.Add(condition);
                return true;
            case "Label":
                output.Add(ParseLabel(node));
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Parses an &lt;Input&gt; element — its own image attributes, its Label/Overlay children,
    /// and any nested Container/OneOf/Condition children. Returns null if the element is missing
    /// its required `name` attribute (whether top-level or nested — nested Inputs need explicit
    /// names too).
    /// </summary>
    private InputNode? ParseInputNode(XmlElement inputNode)
    {
        string? name = inputNode.Attributes["name"]?.Value;
        if (string.IsNullOrEmpty(name))
        {
            _logger.Error("Skipping <Input>: missing 'name' attribute");
            return null;
        }

        var input = new InputNode
        {
            Name = name,
            Style = inputNode.Attributes["style"]?.Value,
            ShowIf = inputNode.Attributes["showIf"]?.Value,
            UseImage = inputNode.Attributes["useImage"]?.Value
        };

        if (ReadDouble(inputNode, "minOpacity") is double minOpacity) input.MinOpacity = minOpacity;
        if (ReadDouble(inputNode, "inactiveBlurRadius") is double blur) input.InactiveBlurRadius = blur;
        if (ReadDouble(inputNode, "fontSize") is double fontSize) input.FontSize = fontSize;
        if (ReadCoordinate(inputNode, "x", $"Input '{name}'") is Coordinate ix) input.X = ix;
        if (ReadCoordinate(inputNode, "y", $"Input '{name}'") is Coordinate iy) input.Y = iy;
        if (ReadDouble(inputNode, "width") is double width) input.Width = width;
        if (ReadDouble(inputNode, "height") is double height) input.Height = height;

        foreach (XmlElement child in inputNode.ChildNodes.OfType<XmlElement>())
        {
            // Checked before TryParseLayoutChild: Label is also a valid loose child of
            // Container/OneOf/Condition (see TryParseLayoutChild), but a Label that's a *direct*
            // child of its own Input always belongs on that Input's own Labels list, never the
            // generic Children list.
            switch (child.Name)
            {
                case "Label":
                    input.Labels.Add(ParseLabel(child));
                    continue;
                case "Overlay":
                    OverlayNode? overlay = ParseOverlay(child);
                    if (overlay != null) input.Overlays.Add(overlay);
                    continue;
                default:
                    break;
            }

            if (TryParseLayoutChild(child, input.Children)) continue;

            _logger.Error($"Invalid element <{child.Name}> in <Input name=\"{name}\">");
        }

        _logger.Debug($"Input: {input.Name}, overlays={input.Overlays.Count}, labels={input.Labels.Count}, children={input.Children.Count}");
        return input;
    }

    /// <summary>
    /// Parses a &lt;Container&gt; positioned layout container. Children are stacked vertically
    /// with positions computed from the container's own origin (x, y) plus slot index times gap.
    /// <c>vAlign</c> (top/bottom/center, default top) is validated against the origin's slot
    /// count by the resolver, not here — an invalid value just flows through as an arbitrary
    /// string. Unlike the old &lt;Group&gt; it replaced, a Container never decides its own
    /// inclusion — it's always rendered once reached; wrap it in an explicit &lt;Condition&gt;
    /// when gating is wanted. Its style attributes (style/showIf/minOpacity/inactiveBlurRadius/
    /// fontSize) are the same shape as an Input's own — see <see cref="IStyledNode"/> — and
    /// cascade to its member Inputs and Overlay children the same way an Input's own attributes
    /// cascade to its Labels and Overlays. <c>for</c> is unrelated to any of that — see
    /// <see cref="ContainerNode.For"/>.
    /// </summary>
    private ContainerNode ParseContainerNode(XmlElement containerNode)
    {
        var container = new ContainerNode
        {
            Style = containerNode.Attributes["style"]?.Value,
            ShowIf = containerNode.Attributes["showIf"]?.Value,
            For = containerNode.GetAttribute("for") is { Length: > 0 } forName ? forName : null,
        };

        if (ReadCoordinate(containerNode, "x", "Container") is Coordinate cx) container.X = cx;
        if (ReadCoordinate(containerNode, "y", "Container") is Coordinate cy) container.Y = cy;
        if (ReadDouble(containerNode, "gap") is double gap) container.Gap = gap;
        container.VAlign = containerNode.Attributes["vAlign"]?.Value.ToLowerInvariant() ?? "top";
        if (string.Equals(containerNode.Attributes["collapse"]?.Value, "true", StringComparison.OrdinalIgnoreCase)) container.Collapse = true;
        if (ReadDouble(containerNode, "minOpacity") is double minOpacity) container.MinOpacity = minOpacity;
        if (ReadDouble(containerNode, "inactiveBlurRadius") is double blur) container.InactiveBlurRadius = blur;
        if (ReadDouble(containerNode, "fontSize") is double fontSize) container.FontSize = fontSize;

        foreach (XmlElement child in containerNode.ChildNodes.OfType<XmlElement>())
        {
            if (TryParseLayoutChild(child, container.Children)) continue;

            if (child.Name == "Overlay")
            {
                OverlayNode? overlay = ParseOverlay(child);
                if (overlay != null) container.Overlays.Add(overlay);
            }
            else
            {
                _logger.Error($"Invalid element <{child.Name}> in <Container>");
            }
        }

        _logger.Debug($"Container: x={container.X}, y={container.Y}, gap={container.Gap}, vAlign={container.VAlign}, for={container.For}, children={container.Children.Count}, overlays={container.Overlays.Count}");
        return container;
    }

    /// <summary>
    /// Parses a &lt;OneOf&gt; alternatives container. Each child (Input, Condition, or nested OneOf)
    /// is an alternative branch evaluated in document order; the first whose visibility passes
    /// is rendered.
    /// </summary>
    private OneOfNode ParseOneOfNode(XmlElement oneOfNode)
    {
        var oneOf = new OneOfNode();

        foreach (XmlElement child in oneOfNode.ChildNodes.OfType<XmlElement>())
        {
            if (!TryParseLayoutChild(child, oneOf.Alternatives))
                _logger.Error($"Invalid element <{child.Name}> in <OneOf>");
        }

        _logger.Debug($"OneOf: alternatives={oneOf.Alternatives.Count}");
        return oneOf;
    }

    /// <summary>
    /// Parses a &lt;Condition&gt; element. Exactly one of `any`/`all`/`none` must be present;
    /// missing or having more than one is logged and the element is skipped entirely (rather than
    /// guessing which was meant). `match` defaults to "label" when absent.
    /// </summary>
    private ConditionNode? ParseConditionNode(XmlElement node)
    {
        string? any = node.Attributes["any"]?.Value;
        string? all = node.Attributes["all"]?.Value;
        string? none = node.Attributes["none"]?.Value;

        int specified = new[] { any, all, none }.Count(v => !string.IsNullOrEmpty(v));
        if (specified != 1)
        {
            _logger.Error($"Skipping <Condition>: expected exactly one of 'any', 'all', 'none', found {specified}");
            return null;
        }

        var condition = new ConditionNode { Any = any, All = all, None = none, Match = node.Attributes["match"]?.Value };

        foreach (XmlElement child in node.ChildNodes.OfType<XmlElement>())
        {
            if (TryParseLayoutChild(child, condition.Children)) continue;
            _logger.Error($"Invalid element <{child.Name}> in <Condition>");
        }

        _logger.Debug($"Condition: any={any}, all={all}, none={none}, match={condition.Match}, children={condition.Children.Count}");
        return condition;
    }

    /// <summary>Parses a string as a culture-invariant double.</summary>
    private static bool TryParseDouble(string? value, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    /// <summary>Parses a coordinate string. A leading + indicates a relative positive offset;
    /// a leading - indicates a relative negative offset; no sign prefix means absolute.</summary>
    private static bool TryParseCoordinate(string? value, out Coordinate result)
    {
        if (string.IsNullOrEmpty(value)) { result = default; return false; }
        bool isRelative = value[0] is '+' or '-';
        string numStr = value[0] == '+' ? value[1..] : value;
        if (!double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
        {
            result = default;
            return false;
        }
        result = isRelative ? Coordinate.Relative(d) : Coordinate.Absolute(d);
        return true;
    }

    /// <summary>Returns the parsed value of a numeric attribute, or null if the attribute is
    /// absent or not a valid number. Invalid values are silently ignored — callers retain the
    /// field's default.</summary>
    private static double? ReadDouble(XmlElement node, string attr) =>
        TryParseDouble(node.Attributes[attr]?.Value, out double v) ? v : null;

    /// <summary>Returns the parsed value of a coordinate attribute, or null if the attribute is
    /// absent. If the attribute is present but not a valid coordinate, logs an error and returns
    /// null so the caller retains the field's default (+0).</summary>
    private Coordinate? ReadCoordinate(XmlElement node, string attr, string context)
    {
        if (node.Attributes[attr]?.Value is not string s) return null;
        if (TryParseCoordinate(s, out Coordinate c)) return c;
        _logger.Error($"{context}: could not parse {attr}=\"{s}\" as a number, using +0");
        return null;
    }

    /// <summary>
    /// Parses a Label XML node. Invalid coordinates are logged and replaced with the default
    /// (+0); the label is still returned so its other attributes survive.
    /// </summary>
    private LabelNode ParseLabel(XmlElement node)
    {
        var label = new LabelNode
        {
            Align = node.Attributes["align"]?.Value.ToLowerInvariant() ?? "left",
            Style = node.Attributes["style"]?.Value
        };
        if (ReadCoordinate(node, "x", "Label") is Coordinate x) label.X = x;
        if (ReadCoordinate(node, "y", "Label") is Coordinate y) label.Y = y;
        if (ReadDouble(node, "fontSize") is double fs) label.FontSize = fs;
        return label;
    }

    /// <summary>
    /// Parses an Overlay XML node. Returns null and logs a warning if required attributes are missing or invalid.
    /// </summary>
    private OverlayNode? ParseOverlay(XmlElement node)
    {
        string? src = node.Attributes["src"]?.Value;
        if (src == null)
        {
            _logger.Error("Skipping <Overlay>: missing 'src' attribute");
            return null;
        }

        var overlay = new OverlayNode { Src = src, Style = node.Attributes["style"]?.Value };
        if (ReadCoordinate(node, "x", $"Overlay src=\"{src}\"") is Coordinate x) overlay.X = x;
        if (ReadCoordinate(node, "y", $"Overlay src=\"{src}\"") is Coordinate y) overlay.Y = y;
        if (ReadDouble(node, "width") is double w) overlay.Width = w;
        if (ReadDouble(node, "height") is double h) overlay.Height = h;
        overlay.ShowIf = node.Attributes["showIf"]?.Value;
        if (ReadDouble(node, "minOpacity") is double minOpacity) overlay.MinOpacity = minOpacity;
        if (ReadDouble(node, "inactiveBlurRadius") is double blur) overlay.InactiveBlurRadius = blur;
        return overlay;
    }
}
