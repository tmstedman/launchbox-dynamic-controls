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
        _underTest = new LayoutResolver(_logger);
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

    // --- Named-style ShowIf/MinOpacity/InactiveBlurRadius inheritance onto an Input's own image ---

    [Fact]
    public void Resolve_InputWithoutStyle_ImageInheritsNamedStyleValues()
    {
        // given an input that references a named style but sets none of its own attributes
        TestLayout config = new TestLayout()
            .NamedStyle("s", showIf: "label", minOpacity: 0.1, inactiveBlurRadius: 6)
            .Input("ButtonA", i => i.Style("s"));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the image inherits showIf/minOpacity/inactiveBlurRadius from the named style
        InputImageDefinition image = result.FirstInput().InputImages.Single();
        image.ShowIf.ShouldBe(ShowIfCondition.Label);
        image.MinOpacity.ShouldBe(0.1);
        image.InactiveBlurRadius.ShouldBe(6);
    }

    [Fact]
    public void Resolve_InputAttribute_WinsOverNamedStyle()
    {
        // given an input that references a named style AND sets its own attributes
        TestLayout config = new TestLayout()
            .NamedStyle("s", showIf: "label", minOpacity: 0.1)
            .Input("ButtonA", i => i.Style("s").ShowIf("mapping").MinOpacity(0.7));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the input's explicit attributes win over the named-style values
        InputImageDefinition image = result.FirstInput().InputImages.Single();
        image.ShowIf.ShouldBe(ShowIfCondition.Mapped);
        image.MinOpacity.ShouldBe(0.7);
    }

    [Fact]
    public void Resolve_InputInactiveBlurRadius_WinsOverNamedStyle()
    {
        // given an input that sets its own InactiveBlurRadius alongside a named style that also has one
        TestLayout config = new TestLayout()
            .NamedStyle("s", inactiveBlurRadius: 6)
            .Input("ButtonA", i => i.Style("s").InactiveBlurRadius(12));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the input's own InactiveBlurRadius wins over the named style's value
        result.FirstInput().InputImages.Single().InactiveBlurRadius.ShouldBe(12);
    }

    [Fact]
    public void Resolve_UnknownNamedStyle_LogsError_AndDoesNotInherit()
    {
        // given an input that references a style that isn't declared in <Head>
        TestLayout config = new TestLayout()
            .Input("ButtonA", i => i.Style("missing"));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the image inherits nothing and the configurer logs an error naming the style
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
    public void Resolve_InputShowIf_ParsesValue(string showIf, ShowIfCondition expected)
    {
        // given an input with the supplied showIf attribute
        TestLayout config = new TestLayout()
            .Input("ButtonA", i => i.ShowIf(showIf));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the parser produces the expected ShowIfCondition (case- and whitespace-insensitive)
        result.FirstInput().InputImages.Single().ShowIf.ShouldBe(expected);
    }

    [Fact]
    public void Resolve_InputShowIfUnknown_LogsError_AndDefaultsToAlways()
    {
        // given an input with a showIf value the parser doesn't recognize
        TestLayout config = new TestLayout()
            .Input("ButtonA", i => i.ShowIf("bogus"));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the image defaults to Always and the configurer logs an error naming the value
        result.FirstInput().InputImages.Single().ShowIf.ShouldBe(ShowIfCondition.Always);
        _logger.Received().Error(Arg.Is<string>(m => m.Contains("bogus")));
    }

    [Fact]
    public void Resolve_InputShowIfAbsent_DefaultsToAlways()
    {
        // given an input with no showIf attribute at all
        TestLayout config = new TestLayout().Input("ButtonA");

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the image is unconditionally Always
        result.FirstInput().InputImages.Single().ShowIf.ShouldBe(ShowIfCondition.Always);
    }

    // --- Coordinate origin propagation ---

    [Fact]
    public void Resolve_InputCoords_ResolveToTheirOwnImage()
    {
        // given an input at (100,200) — its own coordinate origin doubles as its image's position
        TestLayout config = new TestLayout()
            .Input("ButtonA", i => i.At(100, 200));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the image's resolved canvas position is exactly the input's own origin
        InputImageDefinition image = result.FirstInput().InputImages.Single();
        image.X.ShouldBe(100);
        image.Y.ShouldBe(200);
    }

    [Fact]
    public void Resolve_NestedChildInput_InheritsParentOrigin()
    {
        // given a parent input at (100,200) and a child input using relative (+5,+10) coordinates
        TestLayout config = new TestLayout()
            .Input("Parent", p => p.At(100, 200)
                .Child("Child", c => c.Offset(5, 10)));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the child's own image resolves against the parent's origin
        InputDefinition child = result.FirstInput().Children.FirstInput();
        InputImageDefinition image = child.InputImages.Single();
        image.X.ShouldBe(105);
        image.Y.ShouldBe(210);
    }

    [Fact]
    public void Resolve_NestedChildInput_InheritsShowIfFromParent()
    {
        // given a parent input that sets showIf="label" and a child input that sets nothing
        // Consistent with Group's own cascade to its members: a structural child that sets
        // nothing of its own falls through to whatever's ambient, the same as a Label/Overlay
        // reaching through the same nesting always has.
        TestLayout config = new TestLayout()
            .Input("Parent", p => p.ShowIf("label")
                .Child("Child"));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the child's image inherits the parent's showIf
        InputDefinition child = result.FirstInput().Children.FirstInput();
        child.InputImages.Single().ShowIf.ShouldBe(ShowIfCondition.Label);
    }

    [Fact]
    public void Resolve_NestedChildInput_OwnExplicitShowIfWinsOverParent()
    {
        // given a parent input that sets showIf="label" and a child that sets its own showIf
        TestLayout config = new TestLayout()
            .Input("Parent", p => p.ShowIf("label")
                .Child("Child", c => c.ShowIf("mapping")));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the child's own explicit value wins over whatever's ambient from the parent
        InputDefinition child = result.FirstInput().Children.FirstInput();
        child.InputImages.Single().ShowIf.ShouldBe(ShowIfCondition.Mapped);
    }

    [Fact]
    public void Resolve_GroupWithNoOwnStyle_DoesNotPassAWrappingInputsAmbientStyleToMembers()
    {
        // given an Input with its own explicit minOpacity, wrapping a Group that sets nothing of
        // its own -- the Group must not relay the Input's ambient value to its member, or a style
        // meant to vacate fully (no minOpacity of its own, relying on the built-in default of 0)
        // would instead fade, exactly the real small-label-vacate regression this guards against
        TestLayout config = new TestLayout()
            .Input("AxisLeftStick", i => i.MinOpacity(0.3)
                .ChildContainer(g => g.Input("AxisLeftStickUp")));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the member's own MinOpacity is null -- it never saw the wrapping Input's 0.3
        InputDefinition member = result.FirstInput().Children.FirstContainer().Children.FirstInput();
        member.InputImages.Single().MinOpacity.ShouldBeNull();
    }

    [Fact]
    public void Resolve_GroupWithItsOwnStyle_StillPassesItToMembers()
    {
        // given a Group that sets its own explicit minOpacity -- cascadeAmbient: false only
        // blocks an ambient value passing *through* an empty Group; the Group's own explicit
        // value must still reach members that set nothing themselves
        TestLayout config = new TestLayout()
            .Container(g => g.MinOpacity(0.5).Input("ButtonA"));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the member inherits the Group's own explicit value
        InputDefinition member = result.FirstContainer().Children.FirstInput();
        member.InputImages.Single().MinOpacity.ShouldBe(0.5);
    }

    // --- Image filename derivation ---

    [Fact]
    public void Resolve_Input_DefaultImageFileIsInputNameDotPng()
    {
        // given an input named "ButtonStart" with no useImage
        TestLayout config = new TestLayout().Input("ButtonStart");

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the image's ImageFile is derived from the input name, UseImageFile is null
        InputImageDefinition image = result.FirstInput().InputImages.Single();
        image.ImageFile.ShouldBe("ButtonStart.png");
        image.UseImageFile.ShouldBeNull();
    }

    [Fact]
    public void Resolve_InputWithUseImage_SetsUseImageFileWithPngSuffix()
    {
        // given an input "AxisLeftStickUp" that borrows the "Stick" asset
        TestLayout config = new TestLayout()
            .Input("AxisLeftStickUp", i => i.UseImage("Stick"));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then ImageFile still comes from the input name; UseImageFile carries the borrowed asset
        InputImageDefinition image = result.FirstInput().InputImages.Single();
        image.ImageFile.ShouldBe("AxisLeftStickUp.png");
        image.UseImageFile.ShouldBe("Stick.png");
    }

    // --- Group slot positioning ---

    [Fact]
    public void Resolve_NestedGroup_ConsumesOneSlotInParent()
    {
        // given a group with gap=50 containing an input followed by a nested group
        // the nested group itself contains two inputs; it occupies one slot in the parent
        TestLayout config = new TestLayout()
            .Container(s => s.At(0, 0).Gap(50)
                .Input("A")
                .Container(inner => inner.Gap(10)
                    .Input("B")
                    .Input("C")));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then A lands at slot 0 (Y=0) and the inner group lands at slot 1 (Y=50)
        Container outer = result.FirstContainer();
        var a = (InputDefinition)outer.Children[0];
        var inner = (Container)outer.Children[1];
        a.InputImages.Single().Y.ShouldBe(0);
        // inner group's own inputs start at the slot origin (Y=50) with their own gap
        inner.Children.Cast<InputDefinition>()
            .Select(i => i.InputImages.Single().Y)
            .ShouldBe([50.0, 60.0]);
    }

    [Fact]
    public void Resolve_GroupVAlignTop_IsTheDefaultAndLeavesYUnshifted()
    {
        // given a group with vAlign explicitly "top" -- the default
        TestLayout config = new TestLayout()
            .Container(s => s.At(0, 100).Gap(50).VAlign("top")
                .Input("A")
                .Input("B")
                .Input("C"));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the declared Y is the first slot, same as if vAlign were omitted
        Container group = result.FirstContainer();
        group.Children.Cast<InputDefinition>()
            .Select(i => i.InputImages.Single().Y)
            .ShouldBe([100.0, 150.0, 200.0]);
    }

    [Fact]
    public void Resolve_GroupVAlignBottom_ShiftsOriginSoTheLastSlotLandsOnY()
    {
        // given a group with vAlign="bottom" -- the declared Y should be the LAST slot
        TestLayout config = new TestLayout()
            .Container(s => s.At(0, 100).Gap(50).VAlign("bottom")
                .Input("A")
                .Input("B")
                .Input("C"));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the origin shifts up by (slotCount-1)*gap, so slot 2 (the last) lands on Y=100
        Container group = result.FirstContainer();
        group.Children.Cast<InputDefinition>()
            .Select(i => i.InputImages.Single().Y)
            .ShouldBe([0.0, 50.0, 100.0]);
    }

    [Fact]
    public void Resolve_GroupVAlignCenter_ShiftsOriginSoTheMiddleSlotLandsOnY()
    {
        // given a group with vAlign="center" -- the declared Y should be the midpoint
        TestLayout config = new TestLayout()
            .Container(s => s.At(0, 100).Gap(50).VAlign("center")
                .Input("A")
                .Input("B")
                .Input("C"));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the origin shifts up by half of (slotCount-1)*gap, so slot 1 (the middle) lands on Y=100
        Container group = result.FirstContainer();
        group.Children.Cast<InputDefinition>()
            .Select(i => i.InputImages.Single().Y)
            .ShouldBe([50.0, 100.0, 150.0]);
    }

    [Fact]
    public void Resolve_GroupVAlignBottom_CountsANestedGroupAsOneSlot()
    {
        // given vAlign="bottom" on an outer group with 2 slots -- A, then a nested group (which
        // counts as one slot in the OUTER group regardless of its own inner slot count)
        TestLayout config = new TestLayout()
            .Container(s => s.At(0, 100).Gap(50).VAlign("bottom")
                .Input("A")
                .Container(inner => inner.Gap(10)
                    .Input("B")
                    .Input("C")));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the outer shift is (2-1)*50=50: A lands at Y=50, the nested stack's own origin
        // (its one slot) lands at Y=100 -- unaffected by vAlign, since only the OUTER declared
        // it -- and its own children stack from there with their own gap.
        Container outer = result.FirstContainer();
        var a = (InputDefinition)outer.Children[0];
        var inner = (Container)outer.Children[1];
        a.InputImages.Single().Y.ShouldBe(50);
        inner.Children.Cast<InputDefinition>()
            .Select(i => i.InputImages.Single().Y)
            .ShouldBe([100.0, 110.0]);
    }

    [Fact]
    public void Resolve_GroupVAlignUnknown_LogsError_AndDefaultsToTop()
    {
        // given a group with a vAlign value the resolver doesn't recognize
        TestLayout config = new TestLayout()
            .Container(s => s.At(0, 100).Gap(50).VAlign("bogus")
                .Input("A")
                .Input("B"));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then it behaves as "top" (no shift) and logs an error naming the value
        Container group = result.FirstContainer();
        group.Children.Cast<InputDefinition>()
            .Select(i => i.InputImages.Single().Y)
            .ShouldBe([100.0, 150.0]);
        _logger.Received().Error(Arg.Is<string>(m => m.Contains("bogus")));
    }

    [Fact]
    public void Resolve_GroupVAlignBottom_SingleSlot_IsUnaffected()
    {
        // given vAlign="bottom" on a group with only one slot -- bottom and top coincide when
        // there's nothing to distribute around
        TestLayout config = new TestLayout()
            .Container(s => s.At(0, 100).Gap(50).VAlign("bottom")
                .Input("A"));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then A still lands exactly on the declared Y
        result.FirstContainer().Children.FirstInput().InputImages.Single().Y.ShouldBe(100);
    }

    [Fact]
    public void Resolve_GroupCollapse_RecordsCollapseInfoForInputs()
    {
        // given a group with collapse="true" containing two inputs
        TestLayout config = new TestLayout()
            .Container(s => s.At(0, 0).Gap(50).Collapse()
                .Input("A")
                .Input("B"));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then each member input has an entry in ResolvedLayout.CollapseInfo with the group's gap
        Container group = result.FirstContainer();
        InputDefinition[] inputs = [.. group.Children.Cast<InputDefinition>()];
        result.CollapseInfo.Keys.ShouldBe(inputs, ignoreOrder: true);
        foreach (InputDefinition input in inputs)
            result.CollapseInfo[input].Gap.ShouldBe(50);
    }

    [Fact]
    public void Resolve_GroupCollapseWithVAlign_CarriesTheValueIntoCollapseInfo()
    {
        // given a collapsing group with vAlign="bottom" -- LayoutFilter needs this at render time
        // to correct its own shift for whatever slots collapse actually leaves visible
        TestLayout config = new TestLayout()
            .Container(s => s.At(0, 100).Gap(50).VAlign("bottom").Collapse()
                .Input("A"));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        InputDefinition input = result.FirstContainer().Children.FirstInput();
        result.CollapseInfo[input].VAlign.ShouldBe("bottom");
    }

    [Fact]
    public void Resolve_GroupCollapseWithUnknownVAlign_CollapseInfoCarriesTheNormalizedDefault()
    {
        // given a collapsing group with a vAlign value the resolver doesn't recognize -- it's
        // validated (and logged) exactly once, here; CollapseInfo must carry the normalized
        // "top", not the raw invalid string, since nothing downstream re-validates it
        TestLayout config = new TestLayout()
            .Container(s => s.At(0, 100).Gap(50).VAlign("bogus").Collapse()
                .Input("A"));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        InputDefinition input = result.FirstContainer().Children.FirstInput();
        result.CollapseInfo[input].VAlign.ShouldBe("top");
    }

    [Fact]
    public void Resolve_GroupWithoutCollapse_OmitsCollapseInfoEntry()
    {
        // given a group with collapse omitted
        TestLayout config = new TestLayout()
            .Container(s => s.At(0, 0).Gap(50).Input("A"));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then no CollapseInfo entry is recorded for the member input
        InputDefinition input = result.FirstContainer().Children.FirstInput();
        result.CollapseInfo.Keys.ShouldNotContain(input);
    }

    // --- Group + OneOf ---

    [Fact]
    public void Resolve_NestedGroup_IsReachableAsAnContainer()
    {
        // given a top-level Group whose only child is another Group containing an Input
        TestLayout config = new TestLayout()
            .Container(outer => outer
                .Container(inner => inner
                    .Input("A")));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then both Groups are resolved, the inner reachable as the outer's sole child
        var outerGroup = result.FirstContainer();
        var innerGroup = (Container)outerGroup.Children.Single();
        innerGroup.Children.FirstInput().Name.ShouldBe("A");
    }

    [Fact]
    public void Resolve_TopLevelOneOf_IsBuiltAsOneOf()
    {
        // given a top-level OneOf (not inside a Group) — exercises BuildNode's OneOfNode arm
        TestLayout config = new TestLayout()
            .OneOf(o => o
                .Input("A")
                .Input("B"));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the output contains a OneOf with both alternatives
        OneOf oneOf = result.Elements.FirstOneOf();
        oneOf.Alternatives.Cast<InputDefinition>().Select(i => i.Name).ShouldBe(["A", "B"]);
    }

    [Fact]
    public void Resolve_TopLevelGroupWithFor_CarriesForInputNameOntoTheResolvedGroup()
    {
        // given a top-level Group naming the Input it builds on behalf of, with no enclosing
        // Input of its own -- LayoutFilter needs this at render time to know which Input a loose
        // Label placed directly inside should attach to
        TestLayout config = new TestLayout()
            .Container(g => g.For("ButtonDpad").Input("A"));

        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        result.FirstContainer().ForInputName.ShouldBe("ButtonDpad");
    }

    [Fact]
    public void Resolve_LooseLabelUnderTopLevelGroupWithFor_ResolvesAgainstTheNamedInputInsteadOfLoggingAnError()
    {
        // given a top-level Group (no enclosing Input) whose for= names the Input a loose Label
        // placed directly inside it should attach to -- the mechanism that lets a whole control's
        // merged-label cluster live as a sibling of its own bare Input rather than nested inside it
        TestLayout config = new TestLayout()
            .Container(g => g.For("ButtonDpad").LooseLabel(l => l.Offset(-10, 20)));

        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        LabelElement label = result.FirstContainer().Children.FirstLabelElement();
        label.Label.X.ShouldBe(-10);
        label.Label.Y.ShouldBe(20);
    }

    [Fact]
    public void Resolve_UnknownNodeType_Throws()
    {
        // given a layout containing an ILayoutNode subtype that BuildNode doesn't handle
        var config = new TestLayout();
        config.ToConfig().Elements.Add(new UnknownNode());

        // when the resolver runs
        // then an InvalidOperationException is thrown naming the unhandled type
        Should.Throw<InvalidOperationException>(() => _underTest.Resolve(config, _imageSource));
    }

    [Fact]
    public void Resolve_UnknownNodeTypeInGroup_Throws()
    {
        // given a group containing an ILayoutNode subtype that BuildNodeInStack doesn't handle
        var config = new LayoutDocument();
        config.Elements.Add(new ContainerNode { Children = [new UnknownNode()] });

        // when the resolver runs
        // then an InvalidOperationException is thrown
        Should.Throw<InvalidOperationException>(() => _underTest.Resolve(config, _imageSource));
    }

    [Fact]
    public void Resolve_OneOfAlternatives_AllBuiltWithSharedOrigin()
    {
        // given a OneOf in a group slot with two relative-positioned alternatives
        TestLayout config = new TestLayout()
            .Container(s => s.At(100, 200).Gap(50)
                .OneOf(o => o
                    .Input("A")
                    .Input("B")));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then both alternatives resolve against the same slot origin (100,200)
        OneOf oneOf = result.FirstContainer().Children.FirstOneOf();
        var positions = oneOf.Alternatives
            .Cast<InputDefinition>()
            .Select(i => (i.InputImages.Single().X, i.InputImages.Single().Y))
            .ToList();
        positions.ShouldAllBe(p => p.X == 100 && p.Y == 200);
    }

    // --- Condition ---

    [Fact]
    public void Resolve_ConditionAll_ParsesModeNamesAndMatch()
    {
        TestLayout config = new TestLayout()
            .Condition(c => c.All("A B").Match("mapping").Input("A"));

        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        ConditionElement condition = result.Elements.FirstCondition();
        condition.Mode.ShouldBe(ConditionMode.All);
        condition.Names.ShouldBe(["A", "B"]);
        condition.Match.ShouldBe(ConditionMatch.Mapped);
        condition.Children.FirstInput().Name.ShouldBe("A");
    }

    [Fact]
    public void Resolve_ConditionAny_ParsesMode()
    {
        TestLayout config = new TestLayout().Condition(c => c.Any("A").Input("A"));

        ConditionElement condition = _underTest.Resolve(config, _imageSource).Elements.FirstCondition();

        condition.Mode.ShouldBe(ConditionMode.Any);
        condition.Names.ShouldBe(["A"]);
    }

    [Fact]
    public void Resolve_ConditionNone_ParsesMode()
    {
        TestLayout config = new TestLayout().Condition(c => c.None("A").Input("A"));

        ConditionElement condition = _underTest.Resolve(config, _imageSource).Elements.FirstCondition();

        condition.Mode.ShouldBe(ConditionMode.None);
    }

    [Fact]
    public void Resolve_ConditionMatchOmitted_DefaultsToLabel()
    {
        TestLayout config = new TestLayout().Condition(c => c.Any("A").Input("A"));

        ConditionElement condition = _underTest.Resolve(config, _imageSource).Elements.FirstCondition();

        condition.Match.ShouldBe(ConditionMatch.Label);
    }

    [Fact]
    public void Resolve_ConditionUnknownMatch_LogsErrorAndDefaultsToLabel()
    {
        TestLayout config = new TestLayout().Condition(c => c.Any("A").Match("bogus").Input("A"));

        ConditionElement condition = _underTest.Resolve(config, _imageSource).Elements.FirstCondition();

        condition.Match.ShouldBe(ConditionMatch.Label);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("bogus") && s.Contains("Condition")));
    }

    [Fact]
    public void Resolve_ConditionInGroup_IsTransparentToSlotCounting()
    {
        // given a Condition wrapping two Inputs inside a Group — exercises BuildNodeInStack's
        // ConditionNode arm; each wrapped Input should still consume its own slot
        TestLayout config = new TestLayout()
            .Container(s => s.At(100, 200).Gap(50)
                .Condition(c => c.Any("A")
                    .Input("A")
                    .Input("B")));

        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        ConditionElement condition = result.FirstContainer().Children.FirstCondition();
        var positions = condition.Children
            .Cast<InputDefinition>()
            .Select(i => (i.InputImages.Single().X, i.InputImages.Single().Y))
            .ToList();
        positions.ShouldBe([(100, 200), (100, 250)]);
    }

    // --- Loose Label under Condition (no wrapper Input of its own) ---

    [Fact]
    public void Resolve_LooseLabelInCondition_ResolvesAgainstAmbientInput()
    {
        TestLayout config = new TestLayout()
            .Input("ButtonDpad", i => i.At(300, 400)
                .ChildCondition(c => c.Any("X").LooseLabel(l => l.Offset(-10, 20).Align("right"))));

        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        LabelElement label = result.FirstInput().Children.FirstCondition().Children.FirstLabelElement();
        label.Label.X.ShouldBe(290);
        label.Label.Y.ShouldBe(420);
        label.Label.Alignment.ShouldBe("right");
    }

    [Fact]
    public void Resolve_LooseLabel_InheritsAmbientInputsFontSize()
    {
        // given the enclosing Input sets its own fontSize directly (no named style involved) — a
        // loose Label inside a nested Condition should inherit it exactly like a true direct
        // child would, unless it sets its own
        TestLayout config = new TestLayout()
            .Input("ButtonDpad", i => i.FontSize(22)
                .ChildCondition(c => c.Any("X").LooseLabel()));

        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        LabelElement label = result.FirstInput().Children.FirstCondition().Children.FirstLabelElement();
        label.Label.FontSize.ShouldBe(22);
    }

    [Fact]
    public void Resolve_LooseLabelWithNoAmbientInput_LogsErrorAndDoesNotThrow()
    {
        // given a bare Label at the top level, wrapped only in a Condition — never nested
        // inside any Input at all
        TestLayout config = new TestLayout()
            .Condition(c => c.Any("X").LooseLabel());

        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        result.Elements.FirstCondition().Children.FirstLabelElement().ShouldNotBeNull();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Label") && s.Contains("Input")));
    }

    [Fact]
    public void Resolve_NestedInputResetsAmbientInputForItsOwnLooseChildren()
    {
        // given a nested Input inside an outer one, with its own Condition-gated loose Label —
        // the loose Label must attach to the nested Input's identity, not the outer one's
        TestLayout config = new TestLayout()
            .Input("Outer", i => i.At(1, 1)
                .Child("Inner", inner => inner.At(50, 60)
                    .ChildCondition(c => c.Any("X").LooseLabel())));

        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        InputDefinition inner = result.FirstInput().Children.FirstInput();
        LabelElement label = inner.Children.FirstCondition().Children.FirstLabelElement();
        label.Label.X.ShouldBe(50);
        label.Label.Y.ShouldBe(60);
    }

    // --- Loose Label origin resolution ---
    //
    // Phase 1 (this resolver) no longer computes any Group-anchor-aware centering for a loose
    // Label -- which of a Group's slots actually survive collapse is a per-game fact this
    // build-time pass has no way to know, so centering is computed entirely by LayoutFilter at
    // render time instead (see LayoutFilterTests's "loose Label centering" section, and
    // Container's/LayoutFilter.ResolveLooseLabel's doc comments). A loose Label here just
    // resolves against whatever plain origin is ambient, the same as if it weren't inside a
    // Group at all.

    [Fact]
    public void Resolve_LooseLabelInsideGroup_ResolvesAgainstThePlainShiftedOrigin()
    {
        // given a vAlign="center" Group of 4 slots (gap=40) declared at y=300 -- the shifted
        // origin members actually render from is 300 - (3*40/2) = 240. A loose Label just
        // resolves against that plain origin, unaffected by the fact it's inside a Group.
        TestLayout config = new TestLayout()
            .Input("Whole", i => i.ChildContainer(s => s.At(100, 300).Gap(40).VAlign("center")
                .Input("A")
                .Input("B")
                .Input("C")
                .Input("D")
                .LooseLabel(l => l.Offset(0, 0))));

        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        LabelElement label = result.FirstInput().Children.FirstContainer().Children.FirstLabelElement();
        label.Label.Y.ShouldBe(240);
    }

    [Fact]
    public void Resolve_DirectChildLabelOnAGroupMember_UnaffectedByTheGroupsAnchor()
    {
        // a group member's own *direct* Label is a completely ordinary Label -- it must keep
        // resolving against that Input's own slot position, never the enclosing Group's anchor
        TestLayout config = new TestLayout()
            .Container(s => s.At(100, 300).Gap(40).VAlign("center")
                .Input("A", i => i.Label(l => l.Offset(0, 5)))
                .Input("B")
                .Input("C")
                .Input("D"));

        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        InputDefinition a = result.FirstContainer().Children.FirstInput();
        a.Labels.Single().Y.ShouldBe(a.InputImages.Single().Y + 5);
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
    public void Resolve_GroupOverlay_RelativeCoords_ResolveAgainstGroupOrigin()
    {
        // given a group-level overlay declared with relative (+5,+10) coordinates
        TestLayout config = new TestLayout()
            .Container(s => s.At(100, 200)
                .Overlay("frame.png", o => o.Offset(5, 10)));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the overlay's position is the group origin plus its offset
        OverlayDefinition overlay = result.FirstContainer().Overlays.Single();
        overlay.X.ShouldBe(105);
        overlay.Y.ShouldBe(210);
    }

    [Fact]
    public void Resolve_InputOverlay_NullSrc_IsSkipped()
    {
        // given an input with an overlay that has no src attribute in XML
        var config = new LayoutDocument();
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
        var config = new LayoutDocument();
        config.Elements.Add(new ContainerNode { Overlays = [new OverlayNode { Src = null }] });

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then no overlays appear on the resolved group
        result.FirstContainer().Overlays.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_NestedGroupOverlay_NullSrc_IsSkipped()
    {
        // given a group nested inside another group, with an overlay that has no src attribute in XML
        var config = new LayoutDocument();
        config.Elements.Add(new ContainerNode
        {
            Children = [new ContainerNode { Overlays = [new OverlayNode { Src = null }] }]
        });

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then no overlays appear on the nested group
        var nestedGroup = (Container)result.FirstContainer().Children.Single();
        nestedGroup.Overlays.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_LooseOverlay_ResolvesPositionAgainstAmbientOrigin()
    {
        // given a bare Overlay nested inside a Container, with relative (+5,+10) coordinates —
        // unlike a loose Label, position never depends on which owner (if any) is later
        // discovered at render time, so it resolves the same way a direct child would
        TestLayout config = new TestLayout()
            .Container(c => c.At(100, 200)
                .LooseOverlay("lines.png", o => o.Offset(5, 10)));

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then the overlay's position is the container origin plus its offset
        OverlayElement overlay = result.FirstContainer().Children.FirstOverlayElement();
        overlay.Overlay.X.ShouldBe(105);
        overlay.Overlay.Y.ShouldBe(210);
    }

    [Fact]
    public void Resolve_LooseOverlay_NullSrc_LogsAndReturnsPlaceholder()
    {
        // given a bare Overlay with no src attribute, nested where TryParseLayoutChild would
        // never actually produce one (the real parser already filters this at load time) --
        // exercising the resolver's own defensive fallback directly
        var config = new LayoutDocument();
        config.Elements.Add(new ContainerNode { Children = [new OverlayNode { Src = null }] });

        // when the resolver runs
        ResolvedLayout result = _underTest.Resolve(config, _imageSource);

        // then it's logged and a harmless placeholder is returned rather than throwing
        result.FirstContainer().Children.FirstOverlayElement().Overlay.Source.ShouldBe("");
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Overlay") && s.Contains("src")));
    }

    private record UnknownNode : ILayoutNode;
}
