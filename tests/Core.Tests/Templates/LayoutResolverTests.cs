using DynamicControls.Templates;
using DynamicControls.Core.TestHelpers.Templates;
using NSubstitute;

namespace DynamicControls.Core.Tests.Templates;

/// <summary>
/// Unit tests for <see cref="LayoutResolver"/>. Inputs are constructed via
/// <see cref="TestLayout"/> to keep the DTO scaffolding out of the test bodies; the
/// <see cref="ITemplateImageSource"/> is a substitute that returns the requested src verbatim
/// so overlay paths in assertions match what the test put in.
/// </summary>
public class TemplateLayoutResolverTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly ITemplateImageSource _imageSource = Substitute.For<ITemplateImageSource>();
    private readonly LayoutResolver _underTest;

    public TemplateLayoutResolverTests()
    {
        _imageSource
            .Resolve(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(ci => new ResolvedImagePaths(Generic: (string)ci[0], Styled: null));
        _underTest = new LayoutResolver(_logger, new InputDescendantsBuilder());
    }

    // --- Style defaults from <Head><Style> ---

    [Fact]
    public void Resolve_DefaultStyle_ProducesResolvedLayoutDefaults()
    {
        // given a template with explicit head style values
        TestLayout config = new TestLayout()
            .DefaultStyle(fontSize: 20, minOpacity: 0.25, inactiveBlurRadius: 4);

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then those values surface as the ResolvedLayout's defaults
        result.DefaultFontSize.ShouldBe(20);
        result.DefaultMinOpacity.ShouldBe(0.25);
        result.DefaultInactiveBlurRadius.ShouldBe(4);
    }

    [Fact]
    public void Resolve_NoHeadStyle_FallsBackToRenderingDefaults()
    {
        // given a template with no <Head><Style>
        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(new TestLayout(), _imageSource);

        // then the global RenderingDefaults are used (and MinOpacity falls to 0)
        result.DefaultFontSize.ShouldBe(RenderingDefaults.FontSize);
        result.DefaultMinOpacity.ShouldBe(0);
        result.DefaultInactiveBlurRadius.ShouldBe(RenderingDefaults.InactiveBlurRadius);
    }

    // --- Label fontSize precedence: label > input > namedStyle > template default ---

    [Fact]
    public void Resolve_LabelFontSize_UsesLabelExplicitOverEverything()
    {
        // given every tier in the fontSize precedence chain has a value
        TestLayout config = new TestLayout()
            .DefaultStyle(fontSize: 10)
            .NamedStyle("s", fontSize: 20)
            .Input("ButtonA", i => i.Style("s").FontSize(30).Label(l => l.FontSize(40)));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the label's own explicit fontSize wins over input, named style, and template default
        result.FirstInput().Labels.Single().FontSize.ShouldBe(40);
    }

    [Fact]
    public void Resolve_LabelFontSize_FallsThroughToInputFontSize()
    {
        // given the label has no fontSize but the input does
        TestLayout config = new TestLayout()
            .DefaultStyle(fontSize: 10)
            .NamedStyle("s", fontSize: 20)
            .Input("ButtonA", i => i.Style("s").FontSize(30).Label());

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the input's fontSize wins over named style and template default
        result.FirstInput().Labels.Single().FontSize.ShouldBe(30);
    }

    [Fact]
    public void Resolve_LabelFontSize_FallsThroughToNamedStyleFontSize()
    {
        // given neither the label nor the input has a fontSize but the named style does
        TestLayout config = new TestLayout()
            .DefaultStyle(fontSize: 10)
            .NamedStyle("s", fontSize: 20)
            .Input("ButtonA", i => i.Style("s").Label());

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the named style's fontSize wins over the template default
        result.FirstInput().Labels.Single().FontSize.ShouldBe(20);
    }

    [Fact]
    public void Resolve_LabelFontSize_FallsThroughToTemplateDefault()
    {
        // given no tier above the template default sets fontSize
        TestLayout config = new TestLayout()
            .DefaultStyle(fontSize: 10)
            .Input("ButtonA", i => i.Label());

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the label picks up the template default
        result.FirstInput().Labels.Single().FontSize.ShouldBe(10);
    }

    // --- Named-style ShowIf/MinOpacity/InactiveBlurRadius inheritance onto Renders ---

    [Fact]
    public void Resolve_InputWithoutStyle_RenderInheritsNamedStyleValues()
    {
        // given an input that references a named style but sets none of its own attributes
        TestLayout config = new TestLayout()
            .NamedStyle("s", showIf: "label", minOpacity: 0.1, inactiveBlurRadius: 6)
            .Input("ButtonA", i => i.Style("s").Render());

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the render inherits showIf/minOpacity/inactiveBlurRadius from the named style
        InputImageDefinition render = result.FirstInput().InputImages.Single();
        render.ShowIf.ShouldBe(ShowIfCondition.Label);
        render.MinOpacity.ShouldBe(0.1);
        render.InactiveBlurRadius.ShouldBe(6);
    }

    [Fact]
    public void Resolve_InputAttribute_WinsOverNamedStyle()
    {
        // given an input that references a named style AND sets its own attributes
        TestLayout config = new TestLayout()
            .NamedStyle("s", showIf: "label", minOpacity: 0.1)
            .Input("ButtonA", i => i.Style("s").ShowIf("mapping").MinOpacity(0.7).Render());

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the input's explicit attributes win over the named-style values
        InputImageDefinition render = result.FirstInput().InputImages.Single();
        render.ShowIf.ShouldBe(ShowIfCondition.Mapped);
        render.MinOpacity.ShouldBe(0.7);
    }

    [Fact]
    public void Resolve_InputInactiveBlurRadius_WinsOverNamedStyle()
    {
        // given an input that sets its own InactiveBlurRadius alongside a named style that also has one
        TestLayout config = new TestLayout()
            .NamedStyle("s", inactiveBlurRadius: 6)
            .Input("ButtonA", i => i.Style("s").InactiveBlurRadius(12).Render());

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the input's own InactiveBlurRadius wins over the named style's value
        result.FirstInput().InputImages.Single().InactiveBlurRadius.ShouldBe(12);
    }

    [Fact]
    public void Resolve_RenderAttribute_WinsOverInherited()
    {
        // given an input that sets attributes AND a render under it that overrides them
        TestLayout config = new TestLayout()
            .Input("ButtonA", i => i.ShowIf("label").MinOpacity(0.1)
                .Render(r => r.ShowIf("mapping").MinOpacity(0.5)));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the render's explicit attributes win over the inherited input values
        InputImageDefinition render = result.FirstInput().InputImages.Single();
        render.ShowIf.ShouldBe(ShowIfCondition.Mapped);
        render.MinOpacity.ShouldBe(0.5);
    }

    [Fact]
    public void Resolve_RenderInactiveBlurRadius_WinsOverInherited()
    {
        // given an input that sets InactiveBlurRadius AND a render that sets its own
        TestLayout config = new TestLayout()
            .Input("ButtonA", i => i.InactiveBlurRadius(10)
                .Render(r => r.InactiveBlurRadius(5)));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the render's own InactiveBlurRadius wins over the inherited input value
        result.FirstInput().InputImages.Single().InactiveBlurRadius.ShouldBe(5);
    }

    [Fact]
    public void Resolve_UnknownNamedStyle_LogsError_AndDoesNotInherit()
    {
        // given an input that references a style that isn't declared in <Head>
        TestLayout config = new TestLayout()
            .Input("ButtonA", i => i.Style("missing").Render());

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the render inherits nothing and the configurer logs an error naming the style
        result.FirstInput().InputImages.Single().MinOpacity.ShouldBeNull();
        _logger.Received().Error(Arg.Is<string>(m => m.Contains("missing")));
    }

    // --- ShowIf parsing ---

    [Theory]
    [InlineData("label", ShowIfCondition.Label)]
    [InlineData("mapping", ShowIfCondition.Mapped)]
    [InlineData("auto", ShowIfCondition.Auto)]
    [InlineData("LABEL", ShowIfCondition.Label)]
    [InlineData("  auto  ", ShowIfCondition.Auto)]
    public void Resolve_RenderShowIf_ParsesValue(string showIf, ShowIfCondition expected)
    {
        // given a render with the supplied showIf attribute
        TestLayout config = new TestLayout()
            .Input("ButtonA", i => i.Render(r => r.ShowIf(showIf)));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the parser produces the expected ShowIfCondition (case- and whitespace-insensitive)
        result.FirstInput().InputImages.Single().ShowIf.ShouldBe(expected);
    }

    [Fact]
    public void Resolve_RenderShowIfUnknown_LogsError_AndDefaultsToAlways()
    {
        // given a render with a showIf value the parser doesn't recognize
        TestLayout config = new TestLayout()
            .Input("ButtonA", i => i.Render(r => r.ShowIf("bogus")));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the render defaults to Always and the configurer logs an error naming the value
        result.FirstInput().InputImages.Single().ShowIf.ShouldBe(ShowIfCondition.Always);
        _logger.Received().Error(Arg.Is<string>(m => m.Contains("bogus")));
    }

    [Fact]
    public void Resolve_RenderShowIfAbsent_DefaultsToAlways()
    {
        // given a render with no showIf attribute at all
        TestLayout config = new TestLayout().Input("ButtonA", i => i.Render());

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the render is unconditionally Always
        result.FirstInput().InputImages.Single().ShowIf.ShouldBe(ShowIfCondition.Always);
    }

    // --- Coordinate origin propagation ---

    [Fact]
    public void Resolve_RelativeRenderCoords_AddToInputOrigin()
    {
        // given an input at (100,200) with a render using relative (+5,+10) coordinates
        TestLayout config = new TestLayout()
            .Input("ButtonA", i => i.At(100, 200).Render(r => r.Offset(5, 10)));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the render's resolved canvas position is the sum of input origin and offset
        InputImageDefinition render = result.FirstInput().InputImages.Single();
        render.X.ShouldBe(105);
        render.Y.ShouldBe(210);
    }

    [Fact]
    public void Resolve_AbsoluteRenderCoords_IgnoreInputOrigin()
    {
        // given an input at (100,200) with a render using absolute (5,10) coordinates
        TestLayout config = new TestLayout()
            .Input("ButtonA", i => i.At(100, 200).Render(r => r.At(5, 10)));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the render's position is its absolute value, unaffected by the input origin
        InputImageDefinition render = result.FirstInput().InputImages.Single();
        render.X.ShouldBe(5);
        render.Y.ShouldBe(10);
    }

    [Fact]
    public void Resolve_NestedChildInput_InheritsParentOrigin()
    {
        // given a parent input at (100,200) and a child input whose render uses (+5,+10)
        TestLayout config = new TestLayout()
            .Input("Parent", p => p.At(100, 200)
                .Child("Child", c => c.Render(r => r.Offset(5, 10))));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the child's render resolves against the parent's origin
        InputDefinition child = result.FirstInput().Children.FirstInput();
        InputImageDefinition render = child.InputImages.Single();
        render.X.ShouldBe(105);
        render.Y.ShouldBe(210);
    }

    [Fact]
    public void Resolve_NestedChildInput_DoesNotInheritShowIfFromParent()
    {
        // given a parent input that sets showIf="label" and a child input that sets nothing
        // The cascade is intentionally broken at structural boundaries — child inputs start fresh.
        TestLayout config = new TestLayout()
            .Input("Parent", p => p.ShowIf("label")
                .Child("Child", c => c.Render()));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the child's render is Always — it does not inherit the parent's showIf
        InputDefinition child = result.FirstInput().Children.FirstInput();
        child.InputImages.Single().ShowIf.ShouldBe(ShowIfCondition.Always);
    }

    // --- Render image filename derivation ---

    [Fact]
    public void Resolve_Render_DefaultImageFileIsInputNameDotPng()
    {
        // given an input named "ButtonStart" with a plain render (no useImage)
        TestLayout config = new TestLayout().Input("ButtonStart", i => i.Render());

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the render's ImageFile is derived from the input name, UseImageFile is null
        InputImageDefinition render = result.FirstInput().InputImages.Single();
        render.ImageFile.ShouldBe("ButtonStart.png");
        render.UseImageFile.ShouldBeNull();
    }

    [Fact]
    public void Resolve_RenderWithUseImage_SetsUseImageFileWithPngSuffix()
    {
        // given an input "AxisLeftStickUp" with a render that borrows the "Stick" asset
        TestLayout config = new TestLayout()
            .Input("AxisLeftStickUp", i => i.Render(r => r.UseImage("Stick")));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then ImageFile still comes from the input name; UseImageFile carries the borrowed asset
        InputImageDefinition render = result.FirstInput().InputImages.Single();
        render.ImageFile.ShouldBe("AxisLeftStickUp.png");
        render.UseImageFile.ShouldBe("Stick.png");
    }

    // --- Stack slot positioning ---

    [Fact]
    public void Resolve_StackPlainGroupChildren_AreTransparentToSlotCounting()
    {
        // given a stack with gap=50 containing inputs A, [Group(B, C)], D
        TestLayout config = new TestLayout()
            .Stack(s => s.At(0, 0).Gap(50)
                .Input("A", i => i.Render())
                .Group(g => g
                    .Input("B", i => i.Render())
                    .Input("C", i => i.Render()))
                .Input("D", i => i.Render()));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then B and C each advance the slot counter (plain group is transparent): Y = 0/50/100/150
        InputGroup stack = result.FirstInputGroup();
        var renders = stack.Children.Flatten()
            .OfType<InputDefinition>()
            .Select(i => i.InputImages.Single())
            .ToList();
        renders.Select(r => r.Y).ShouldBe([0.0, 50.0, 100.0, 150.0]);
    }

    [Fact]
    public void Resolve_NestedStack_ConsumesOneSlotInParent()
    {
        // given a stack with gap=50 containing an input followed by a nested stack
        // the nested stack itself contains two inputs; it occupies one slot in the parent
        TestLayout config = new TestLayout()
            .Stack(s => s.At(0, 0).Gap(50)
                .Input("A", i => i.Render())
                .Stack(inner => inner.Gap(10)
                    .Input("B", i => i.Render())
                    .Input("C", i => i.Render())));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then A lands at slot 0 (Y=0) and the inner stack lands at slot 1 (Y=50)
        InputGroup outer = result.FirstInputGroup();
        var a = (InputDefinition)outer.Children[0];
        var inner = (InputGroup)outer.Children[1];
        a.InputImages.Single().Y.ShouldBe(0);
        inner.AlwaysInclude.ShouldBeTrue();
        // inner stack's own inputs start at the slot origin (Y=50) with their own gap
        inner.Children.Cast<InputDefinition>()
            .Select(i => i.InputImages.Single().Y)
            .ShouldBe([50.0, 60.0]);
    }

    [Fact]
    public void Resolve_StackCollapse_RecordsCollapseInfoForInputs()
    {
        // given a stack with collapse="true" containing two inputs
        TestLayout config = new TestLayout()
            .Stack(s => s.At(0, 0).Gap(50).Collapse()
                .Input("A", i => i.Render())
                .Input("B", i => i.Render()));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then each member input has an entry in ResolvedLayout.CollapseInfo with the stack's gap
        InputGroup stack = result.FirstInputGroup();
        InputDefinition[] inputs = [.. stack.Children.Cast<InputDefinition>()];
        result.CollapseInfo.Keys.ShouldBe(inputs, ignoreOrder: true);
        foreach (InputDefinition input in inputs)
            result.CollapseInfo[input].Gap.ShouldBe(50);
    }

    [Fact]
    public void Resolve_StackWithoutCollapse_OmitsCollapseInfoEntry()
    {
        // given a stack with collapse omitted
        TestLayout config = new TestLayout()
            .Stack(s => s.At(0, 0).Gap(50).Input("A", i => i.Render()));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then no CollapseInfo entry is recorded for the member input
        InputDefinition input = result.FirstInputGroup().Children.FirstInput();
        result.CollapseInfo.Keys.ShouldNotContain(input);
    }

    // --- Group + OneOf ---

    [Fact]
    public void Resolve_Group_IsNotAlwaysInclude()
    {
        // given a top-level plain Group
        TestLayout config = new TestLayout()
            .Group(g => g.Input("A", i => i.Render()));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the resulting InputGroup is not flagged AlwaysInclude (Stacks are)
        result.FirstInputGroup().AlwaysInclude.ShouldBeFalse();
    }

    [Fact]
    public void Resolve_GroupInsideGroup_BothAreNotAlwaysInclude()
    {
        // given a top-level Group whose only child is another Group containing an Input
        TestLayout config = new TestLayout()
            .Group(outer => outer
                .Group(inner => inner
                    .Input("A", i => i.Render())));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then both Groups are resolved (neither is AlwaysInclude) and the Input is reachable
        var outerGroup = result.FirstInputGroup();
        var innerGroup = (InputGroup)outerGroup.Children.Single();
        outerGroup.AlwaysInclude.ShouldBeFalse();
        innerGroup.AlwaysInclude.ShouldBeFalse();
        innerGroup.Children.FirstInput().Name.ShouldBe("A");
    }

    [Fact]
    public void Resolve_TopLevelOneOf_IsBuiltAsOneOf()
    {
        // given a top-level OneOf (not inside a Stack) — exercises BuildNode's OneOfNode arm
        TestLayout config = new TestLayout()
            .OneOf(o => o
                .Input("A", i => i.Render())
                .Input("B", i => i.Render()));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the output contains a OneOf with both alternatives
        OneOf oneOf = result.Elements.FirstOneOf();
        oneOf.Alternatives.Cast<InputDefinition>().Select(i => i.Name).ShouldBe(["A", "B"]);
    }

    [Fact]
    public void Resolve_UnknownNodeType_Throws()
    {
        // given a layout containing an IConfigNode subtype that BuildNode doesn't handle
        var config = new TestLayout();
        config.ToConfig().Elements.Add(new UnknownNode());

        // when the resolver runs
        // then an InvalidOperationException is thrown naming the unhandled type
        Should.Throw<InvalidOperationException>(() => _underTest.Resolve(config, _imageSource));
    }

    [Fact]
    public void Resolve_UnknownNodeTypeInStack_Throws()
    {
        // given a stack containing an IConfigNode subtype that BuildNodeInStack doesn't handle
        var config = new LayoutConfig();
        config.Elements.Add(new StackNode { Children = [new UnknownNode()] });

        // when the resolver runs
        // then an InvalidOperationException is thrown
        Should.Throw<InvalidOperationException>(() => _underTest.Resolve(config, _imageSource));
    }

    [Fact]
    public void Resolve_OneOfAlternatives_AllBuiltWithSharedOrigin()
    {
        // given a OneOf in a stack slot with two relative-positioned alternatives
        TestLayout config = new TestLayout()
            .Stack(s => s.At(100, 200).Gap(50)
                .OneOf(o => o
                    .Input("A", i => i.Render(r => r.Offset(0, 0)))
                    .Input("B", i => i.Render(r => r.Offset(0, 0)))));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then both alternatives resolve against the same slot origin (100,200)
        OneOf oneOf = result.FirstInputGroup().Children.FirstOneOf();
        var positions = oneOf.Alternatives
            .Cast<InputDefinition>()
            .Select(i => (i.InputImages.Single().X, i.InputImages.Single().Y))
            .ToList();
        positions.ShouldAllBe(p => p.X == 100 && p.Y == 200);
    }

    // --- Overlay path resolution ---

    [Fact]
    public void Resolve_InputOverlay_ResolvesSrcViaImageSource()
    {
        // given an input-level overlay and an ImageSource that returns a known resolved path
        _imageSource.Resolve("dpad.png", Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(new ResolvedImagePaths(Generic: "Templates/x/dpad.png", Styled: null));
        TestLayout config = new TestLayout()
            .Input("ButtonA", i => i.Overlay("dpad.png", o => o.At(10, 20)));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the overlay carries the resolved path and its declared canvas position
        OverlayDefinition overlay = result.FirstInput().Overlays.Single();
        overlay.Source.ShouldBe("Templates/x/dpad.png");
        overlay.X.ShouldBe(10);
        overlay.Y.ShouldBe(20);
    }

    [Fact]
    public void Resolve_InputOverlay_OwnMinOpacityAndBlurRadius_WinOverInherited()
    {
        // given an input-level overlay that sets its own MinOpacity and InactiveBlurRadius,
        // alongside an input that also sets those values — overlay's own values must win
        TestLayout config = new TestLayout()
            .Input("ButtonA", i => i
                .MinOpacity(0.9).InactiveBlurRadius(99)
                .Overlay("dpad.png", o => o.At(0, 0).MinOpacity(0.3).InactiveBlurRadius(5)));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the overlay's own values flow through unchanged
        OverlayDefinition overlay = result.FirstInput().Overlays.Single();
        overlay.MinOpacity.ShouldBe(0.3);
        overlay.InactiveBlurRadius.ShouldBe(5);
    }

    [Fact]
    public void Resolve_GroupOverlay_RelativeCoords_ResolveAgainstStackOrigin()
    {
        // given a stack-level overlay declared with relative (+5,+10) coordinates
        TestLayout config = new TestLayout()
            .Stack(s => s.At(100, 200)
                .Overlay("frame.png", o => o.Offset(5, 10)));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the overlay's position is the stack origin plus its offset
        OverlayDefinition overlay = result.FirstInputGroup().Overlays.Single();
        overlay.X.ShouldBe(105);
        overlay.Y.ShouldBe(210);
    }

    [Fact]
    public void Resolve_InputOverlay_NullSrc_IsSkipped()
    {
        // given an input with an overlay that has no src attribute in XML
        var config = new LayoutConfig();
        config.Elements.Add(new InputNode { Name = "A", Overlays = [new OverlayNode { Src = null }] });

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then no overlays appear on the resolved input
        result.FirstInput().Overlays.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_GroupOverlay_NullSrc_IsSkipped()
    {
        // given a group with an overlay that has no src attribute in XML
        var config = new LayoutConfig();
        config.Elements.Add(new GroupNode { Overlays = [new OverlayNode { Src = null }] });

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then no overlays appear on the resolved group
        result.FirstInputGroup().Overlays.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_StackOverlay_NullSrc_IsSkipped()
    {
        // given a stack with an overlay that has no src attribute in XML
        var config = new LayoutConfig();
        config.Elements.Add(new StackNode { Overlays = [new OverlayNode { Src = null }] });

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then no overlays appear on the resolved stack
        result.FirstInputGroup().Overlays.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_GroupInStack_Overlay_NullSrc_IsSkipped()
    {
        // given a plain group nested inside a stack, with an overlay that has no src attribute in XML
        var config = new LayoutConfig();
        config.Elements.Add(new StackNode
        {
            Children = [new GroupNode { Overlays = [new OverlayNode { Src = null }] }]
        });

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then no overlays appear on the nested group
        var nestedGroup = (InputGroup)result.FirstInputGroup().Children.Single();
        nestedGroup.Overlays.ShouldBeEmpty();
    }

    private record UnknownNode : IConfigNode;
}
