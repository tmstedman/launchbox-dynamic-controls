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
    LayoutConfig? LoadLayout(string templateName);
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
    public LayoutConfig? LoadLayout(string templateName)
    {
        string templateDir = Path.Combine(_templatesDir, templateName);
        string layoutPath = Path.Combine(templateDir, "Layout.xml");
        _logger.Debug($"Template layout path: {layoutPath}, Exists: {_fs.FileExists(layoutPath)}");

        if (!_fs.FileExists(layoutPath)) return null;

        var result = new LayoutConfig();
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
        int groupCount = result.Elements.OfType<GroupNode>().Count();
        _logger.Debug($"Template layout: {topLevelInputCount} top-level inputs, {groupCount} top-level groups");
        return result;
    }

    /// <summary>Parses a &lt;Head&gt; element. Each &lt;Style&gt; child is either unnamed
    /// (template-wide defaults) or named (a referenceable bundle stored in NamedStyles). Other
    /// children are logged as errors.</summary>
    private HeadConfig ParseHead(XmlElement headNode)
    {
        var head = new HeadConfig();
        foreach (XmlElement child in headNode.ChildNodes.OfType<XmlElement>())
        {
            if (child.Name != "Style")
            {
                _logger.Error($"Invalid element <{child.Name}> in <Head>");
                continue;
            }

            string? name = child.Attributes["name"]?.Value;
            StyleConfig style = ParseStyle(child);
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
    private StyleConfig ParseStyle(XmlElement styleNode)
    {
        var style = new StyleConfig { ShowIf = styleNode.Attributes["showIf"]?.Value };
        if (ReadDouble(styleNode, "fontSize") is double fontSize) style.FontSize = fontSize;
        if (ReadDouble(styleNode, "minOpacity") is double minOpacity) style.MinOpacity = minOpacity;
        if (ReadDouble(styleNode, "inactiveBlurRadius") is double blur) style.InactiveBlurRadius = blur;
        return style;
    }

    /// <summary>Routes a &lt;Body&gt;'s children (Input / Group / Stack / OneOf) into the layout's
    /// Elements list.</summary>
    private void ParseBodyInto(XmlElement bodyNode, List<IConfigNode> output)
    {
        foreach (XmlElement child in bodyNode.ChildNodes.OfType<XmlElement>())
        {
            if (!TryParseLayoutChild(child, output))
                _logger.Error($"Invalid element <{child.Name}> in <Body>");
        }
    }

    /// <summary>Parses one layout-child element (Input / Group / Stack / OneOf) and appends it
    /// to <paramref name="output"/>. Returns true if the element name matched one of those four
    /// (caller is responsible for handling unknown names). An Input that fails its own validation
    /// is treated as matched but not appended.</summary>
    private bool TryParseLayoutChild(XmlElement node, List<IConfigNode> output)
    {
        switch (node.Name)
        {
            case "Input":
                InputNode? input = ParseInputNode(node);
                if (input != null) output.Add(input);
                return true;
            case "Group":
                output.Add(ParseGroupNode(node));
                return true;
            case "Stack":
                output.Add(ParseStackNode(node));
                return true;
            case "OneOf":
                output.Add(ParseOneOfNode(node));
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Parses an &lt;Input&gt; element and its Label, Render, Overlay, and nested Input/Group children.
    /// Returns null if the element is missing its required `name` attribute (whether top-level or
    /// nested — nested Inputs need explicit names too).
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
            ShowIf = inputNode.Attributes["showIf"]?.Value
        };

        if (ReadDouble(inputNode, "minOpacity") is double minOpacity) input.MinOpacity = minOpacity;
        if (ReadDouble(inputNode, "inactiveBlurRadius") is double blur) input.InactiveBlurRadius = blur;
        if (ReadDouble(inputNode, "fontSize") is double fontSize) input.FontSize = fontSize;
        if (ReadCoordinate(inputNode, "x", $"Input '{name}'") is Coordinate ix) input.X = ix;
        if (ReadCoordinate(inputNode, "y", $"Input '{name}'") is Coordinate iy) input.Y = iy;

        foreach (XmlElement child in inputNode.ChildNodes.OfType<XmlElement>())
        {
            if (TryParseLayoutChild(child, input.Children)) continue;

            switch (child.Name)
            {
                case "Label":
                    input.Labels.Add(ParseLabel(child));
                    break;
                case "Render":
                    input.Renders.Add(ParseRender(child));
                    break;
                case "Overlay":
                    OverlayNode? overlay = ParseOverlay(child);
                    if (overlay != null) input.Overlays.Add(overlay);
                    break;
                default:
                    _logger.Error($"Invalid element <{child.Name}> in <Input name=\"{name}\">");
                    break;
            }
        }

        _logger.Debug($"Input: {input.Name}, renders={input.Renders.Count}, overlays={input.Overlays.Count}, labels={input.Labels.Count}, children={input.Children.Count}");
        return input;
    }

    /// <summary>
    /// Parses a &lt;Group&gt; wrapper and its nested layout children: Input, Group, OneOf, and
    /// Overlay. Inputs missing a name are skipped but the rest of the group is still returned.
    /// </summary>
    private GroupNode ParseGroupNode(XmlElement groupNode)
    {
        var group = new GroupNode();

        foreach (XmlElement child in groupNode.ChildNodes.OfType<XmlElement>())
        {
            if (TryParseLayoutChild(child, group.Children)) continue;

            if (child.Name == "Overlay")
            {
                OverlayNode? overlay = ParseOverlay(child);
                if (overlay != null) group.Overlays.Add(overlay);
            }
            else
            {
                _logger.Error($"Invalid element <{child.Name}> in <Group>");
            }
        }

        _logger.Debug($"Group: children={group.Children.Count}, overlays={group.Overlays.Count}");
        return group;
    }

    /// <summary>
    /// Parses a &lt;Stack&gt; positioned layout container. Children are stacked vertically with
    /// positions computed from the stack origin (x, y) plus slot index times gap.
    /// </summary>
    private StackNode ParseStackNode(XmlElement stackNode)
    {
        var stack = new StackNode();

        if (ReadCoordinate(stackNode, "x", "Stack") is Coordinate sx) stack.X = sx;
        if (ReadCoordinate(stackNode, "y", "Stack") is Coordinate sy) stack.Y = sy;
        if (ReadDouble(stackNode, "gap") is double gap) stack.Gap = gap;
        if (string.Equals(stackNode.Attributes["collapse"]?.Value, "true", StringComparison.OrdinalIgnoreCase)) stack.Collapse = true;

        foreach (XmlElement child in stackNode.ChildNodes.OfType<XmlElement>())
        {
            if (TryParseLayoutChild(child, stack.Children)) continue;

            if (child.Name == "Overlay")
            {
                OverlayNode? overlay = ParseOverlay(child);
                if (overlay != null) stack.Overlays.Add(overlay);
            }
            else
            {
                _logger.Error($"Invalid element <{child.Name}> in <Stack>");
            }
        }

        _logger.Debug($"Stack: x={stack.X}, y={stack.Y}, gap={stack.Gap}, children={stack.Children.Count}, overlays={stack.Overlays.Count}");
        return stack;
    }

    /// <summary>
    /// Parses a &lt;OneOf&gt; alternatives container. Each child (Input, Group, or nested OneOf)
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
            Align = node.Attributes["align"]?.Value.ToLowerInvariant() ?? "left"
        };
        if (ReadCoordinate(node, "x", "Label") is Coordinate x) label.X = x;
        if (ReadCoordinate(node, "y", "Label") is Coordinate y) label.Y = y;
        if (ReadDouble(node, "fontSize") is double fs) label.FontSize = fs;
        return label;
    }

    /// <summary>
    /// Parses a Render XML node. Invalid coordinates are logged and replaced with the default
    /// (+0); the render is still returned so its other attributes survive.
    /// </summary>
    private RenderNode ParseRender(XmlElement node)
    {
        var render = new RenderNode();
        if (ReadCoordinate(node, "x", "Render") is Coordinate x) render.X = x;
        if (ReadCoordinate(node, "y", "Render") is Coordinate y) render.Y = y;
        if (ReadDouble(node, "width") is double w) render.Width = w;
        if (ReadDouble(node, "height") is double h) render.Height = h;
        render.UseImage = node.Attributes["useImage"]?.Value;
        render.ShowIf = node.Attributes["showIf"]?.Value;
        if (ReadDouble(node, "minOpacity") is double minOpacity) render.MinOpacity = minOpacity;
        if (ReadDouble(node, "inactiveBlurRadius") is double blur) render.InactiveBlurRadius = blur;
        return render;
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

        var overlay = new OverlayNode { Src = src };
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
