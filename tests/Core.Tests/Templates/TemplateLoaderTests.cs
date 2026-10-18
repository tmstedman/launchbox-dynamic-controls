using DynamicControls.Templates;
using NSubstitute;

namespace DynamicControls.Core.Tests.Templates;

/// <summary>
/// Unit tests for <see cref="TemplateLoader"/>. The loader translates a Layout.xml document
/// into raw <see cref="LayoutDocument"/> DTOs without applying any business logic. The
/// filesystem is a substitute so each test supplies a literal XML string for the parser to
/// chew on, keeping the test focus on parsing rules rather than IO.
/// </summary>
public class TemplateLoaderTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IFileSystem _fs = TestFs.Create();
    private const string RootDir = @"C:\plugin";
    private static readonly string LayoutPath = Path.Combine(RootDir, "Templates", "x", "Layout.xml");
    private readonly TemplateLoader _underTest;

    public TemplateLoaderTests()
    {
        _underTest = new TemplateLoader(_logger, _fs, RootDir);
    }

    private void StubLayoutXml(string xml)
    {
        _fs.FileExists(LayoutPath).Returns(true);
        _fs.OpenRead(LayoutPath).Returns(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
    }

    // --- File presence ---

    [Fact]
    public void LoadLayout_FileMissing_ReturnsNullAndDoesNotParse()
    {
        // given no Layout.xml exists for the template
        _fs.FileExists(LayoutPath).Returns(false);

        // when the loader is asked for it
        LayoutDocument? result = _underTest.LoadLayout("x");

        // then null is returned and the XML is not loaded
        result.ShouldBeNull();
        _fs.DidNotReceive().OpenRead(Arg.Any<string>());
    }

    [Fact]
    public void LoadLayout_EmptyRoot_ReturnsEmptyConfig()
    {
        // given a Layout.xml with no Head and no Body
        StubLayoutXml("<ControllerTemplate />");

        // when the loader runs
        LayoutDocument? result = _underTest.LoadLayout("x");

        // then a default (but non-null) config is returned
        result.ShouldNotBeNull();
        result.Head.Style.ShouldBeNull();
        result.Head.NamedStyles.ShouldBeEmpty();
        result.Elements.ShouldBeEmpty();
    }

    [Fact]
    public void LoadLayout_InvalidRootChild_IsLoggedAndSkipped()
    {
        // given a root-level element that is neither Head nor Body
        StubLayoutXml("""
            <ControllerTemplate>
              <Garbage />
              <Body><Input name='A' /></Body>
            </ControllerTemplate>
            """);

        LayoutDocument result = _underTest.LoadLayout("x")!;

        result.Elements.OfType<InputNode>().Single().Name.ShouldBe("A");
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Garbage") && s.Contains("ControllerTemplate")));
    }

    // --- Head/Style parsing ---

    [Fact]
    public void LoadLayout_UnnamedStyle_PopulatesHeadStyle()
    {
        // given a <Head><Style> with no name and a full set of attributes
        StubLayoutXml("""
            <ControllerTemplate>
              <Head>
                <Style fontSize='24' minOpacity='0.5' inactiveBlurRadius='3' />
              </Head>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutDocument result = _underTest.LoadLayout("x")!;

        // then the unnamed style surfaces as Head.Style
        result.Head.Style.ShouldNotBeNull();
        result.Head.Style.FontSize.ShouldBe(24);
        result.Head.Style.MinOpacity.ShouldBe(0.5);
        result.Head.Style.InactiveBlurRadius.ShouldBe(3);
        result.Head.NamedStyles.ShouldBeEmpty();
    }

    [Fact]
    public void LoadLayout_NamedStyle_PopulatesNamedStylesDictionary()
    {
        // given a <Style name="foo"> with a showIf and font size
        StubLayoutXml("""
            <ControllerTemplate>
              <Head>
                <Style name='foo' showIf='label' fontSize='18' />
              </Head>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutDocument result = _underTest.LoadLayout("x")!;

        // then the style lands in NamedStyles keyed by name, not on Head.Style
        result.Head.Style.ShouldBeNull();
        result.Head.NamedStyles.ShouldContainKey("foo");
        result.Head.NamedStyles["foo"].ShowIf.ShouldBe("label");
        result.Head.NamedStyles["foo"].FontSize.ShouldBe(18);
    }

    [Fact]
    public void LoadLayout_InvalidHeadChild_IsLoggedAndSkipped()
    {
        // given a <Head> with a non-Style child
        StubLayoutXml("""
            <ControllerTemplate>
              <Head><Garbage /></Head>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutDocument result = _underTest.LoadLayout("x")!;

        // then the error is logged and parsing continues
        result.Head.Style.ShouldBeNull();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Garbage") && s.Contains("Head")));
    }

    [Fact]
    public void LoadLayout_StyleWithoutFontSize_LeavesFontSizeNull()
    {
        // given a Style with no fontSize — exercises the ReadDouble false branch in ParseStyle
        StubLayoutXml("""
            <ControllerTemplate>
              <Head>
                <Style minOpacity='0.3' />
              </Head>
            </ControllerTemplate>
            """);

        LayoutDocument result = _underTest.LoadLayout("x")!;

        var style = result.Head.Style;
        style.ShouldNotBeNull();
        style.FontSize.ShouldBeNull();
        style.MinOpacity.ShouldBe(0.3);
    }

    // --- Body / Input parsing ---

    [Fact]
    public void LoadLayout_Input_PopulatesNameAndStyleAttributes()
    {
        // given an Input with style, showIf, fontSize, minOpacity, and inactiveBlurRadius set
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Input name='ButtonA' style='primary' showIf='mapped'
                       fontSize='20' minOpacity='0.25' inactiveBlurRadius='4' />
              </Body>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutDocument result = _underTest.LoadLayout("x")!;

        // then every attribute lands on the InputNode
        InputNode input = result.Elements.OfType<InputNode>().Single();
        input.Name.ShouldBe("ButtonA");
        input.Style.ShouldBe("primary");
        input.ShowIf.ShouldBe("mapped");
        input.FontSize.ShouldBe(20);
        input.MinOpacity.ShouldBe(0.25);
        input.InactiveBlurRadius.ShouldBe(4);
    }

    [Fact]
    public void LoadLayout_InputMissingName_IsSkippedAndLogged()
    {
        // given two inputs where one has no name attribute
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Input />
                <Input name='ButtonA' />
              </Body>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutDocument result = _underTest.LoadLayout("x")!;

        // then only the named input is kept and an error is logged for the nameless one
        result.Elements.ShouldHaveSingleItem();
        result.Elements.OfType<InputNode>().Single().Name.ShouldBe("ButtonA");
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("missing 'name'")));
    }

    [Fact]
    public void LoadLayout_InputWithOwnImageAttributesLabelAndOverlay_CollectsAllChildren()
    {
        // given an Input with its own image attributes plus an Overlay and Label child
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Input name='ButtonA' x='100' y='200' useImage='Stick.png' showIf='label' minOpacity='0.5' inactiveBlurRadius='2'>
                  <Overlay src='dpad.png' x='+5' y='-5' />
                  <Label x='+10' y='+20' align='CENTER' fontSize='16' />
                </Input>
              </Body>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutDocument result = _underTest.LoadLayout("x")!;

        // then the Input's own attributes and each child land with attributes parsed
        InputNode input = result.Elements.OfType<InputNode>().Single();
        input.X.ShouldBe(Coordinate.Absolute(100));
        input.Y.ShouldBe(Coordinate.Absolute(200));
        input.UseImage.ShouldBe("Stick.png");
        input.ShowIf.ShouldBe("label");
        input.MinOpacity.ShouldBe(0.5);
        input.InactiveBlurRadius.ShouldBe(2);

        OverlayNode overlay = input.Overlays.Single();
        overlay.Src.ShouldBe("dpad.png");
        overlay.X.ShouldBe(Coordinate.Relative(5));
        overlay.Y.ShouldBe(Coordinate.Relative(-5));

        LabelNode label = input.Labels.Single();
        label.X.ShouldBe(Coordinate.Relative(10));
        label.Align.ShouldBe("center");
        label.FontSize.ShouldBe(16);
    }

    [Fact]
    public void LoadLayout_OverlayMissingSrc_IsSkippedAndLogged()
    {
        // given an Input with an Overlay missing its src attribute
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Input name='ButtonA'>
                  <Overlay x='0' y='0' />
                </Input>
              </Body>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutDocument result = _underTest.LoadLayout("x")!;

        // then the overlay is dropped and an error is logged
        result.Elements.OfType<InputNode>().Single().Overlays.ShouldBeEmpty();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Overlay") && s.Contains("src")));
    }

    [Fact]
    public void LoadLayout_NestedInputs_AreCollectedAsChildren()
    {
        // given a parent Input with a nested child Input
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Input name='AxisLeftStick'>
                  <Input name='AxisLeftStickLeft' />
                </Input>
              </Body>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutDocument result = _underTest.LoadLayout("x")!;

        // then the nested input is captured under the parent's Children
        InputNode parent = result.Elements.OfType<InputNode>().Single();
        InputNode child = parent.Children.OfType<InputNode>().Single();
        child.Name.ShouldBe("AxisLeftStickLeft");
    }

    [Fact]
    public void LoadLayout_LabelWithoutOptionalAttributes_UsesDefaults()
    {
        // given a Label with no attributes — exercises the ?? "left" default and absent-coordinate branches
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Input name='A'>
                  <Label />
                </Input>
              </Body>
            </ControllerTemplate>
            """);

        LayoutDocument result = _underTest.LoadLayout("x")!;

        LabelNode label = result.Elements.OfType<InputNode>().Single().Labels.Single();
        label.Align.ShouldBe("left");
        label.X.ShouldBe(Coordinate.Relative(0));
        label.Y.ShouldBe(Coordinate.Relative(0));
        label.FontSize.ShouldBeNull();
    }

    [Fact]
    public void LoadLayout_InputWithWidthAndHeight_ParsesDimensions()
    {
        // given an Input with explicit width and height — exercises the true branches skipped by other tests
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Input name='A' width='80' height='60' />
              </Body>
            </ControllerTemplate>
            """);

        LayoutDocument result = _underTest.LoadLayout("x")!;

        InputNode input = result.Elements.OfType<InputNode>().Single();
        input.Width.ShouldBe(80);
        input.Height.ShouldBe(60);
    }

    [Fact]
    public void LoadLayout_OverlayWithAllOptionalAttributes_ParsesThem()
    {
        // given an Overlay with every optional attribute set
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Input name='A'>
                  <Overlay src='frame.png' width='120' height='80' showIf='label' minOpacity='0.3' inactiveBlurRadius='5' />
                </Input>
              </Body>
            </ControllerTemplate>
            """);

        LayoutDocument result = _underTest.LoadLayout("x")!;

        OverlayNode overlay = result.Elements.OfType<InputNode>().Single().Overlays.Single();
        overlay.Width.ShouldBe(120);
        overlay.Height.ShouldBe(80);
        overlay.ShowIf.ShouldBe("label");
        overlay.MinOpacity.ShouldBe(0.3);
        overlay.InactiveBlurRadius.ShouldBe(5);
    }

    [Fact]
    public void LoadLayout_InvalidInputChild_IsLoggedAndSkipped()
    {
        // given an unrecognised element nested directly inside an Input
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Input name='A'>
                  <Bogus />
                </Input>
              </Body>
            </ControllerTemplate>
            """);

        LayoutDocument result = _underTest.LoadLayout("x")!;

        InputNode input = result.Elements.OfType<InputNode>().Single();
        input.Children.ShouldBeEmpty();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Bogus") && s.Contains("Input")));
    }

    // --- Container / OneOf ---

    [Fact]
    public void LoadLayout_Container_ParsesPositionGapAndCollapse()
    {
        // given a Container with x, y, gap, and collapse attributes
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Container x='100' y='+50' gap='40' collapse='TRUE'>
                  <Input name='A' />
                </Container>
              </Body>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutDocument result = _underTest.LoadLayout("x")!;

        // then attributes parse: absolute x, relative y, gap, case-insensitive collapse=true
        ContainerNode group = result.Elements.OfType<ContainerNode>().Single();
        group.X.ShouldBe(Coordinate.Absolute(100));
        group.Y.ShouldBe(Coordinate.Relative(50));
        group.Gap.ShouldBe(40);
        group.Collapse.ShouldBeTrue();
        group.Children.OfType<InputNode>().Single().Name.ShouldBe("A");
    }

    [Fact]
    public void LoadLayout_ContainerWithOverlay_CollectsOverlay()
    {
        // given a Container containing an Overlay child
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Container>
                  <Input name='A' />
                  <Overlay src='lines.png' x='+5' y='+10' />
                </Container>
              </Body>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutDocument result = _underTest.LoadLayout("x")!;

        // then the overlay is collected onto the container
        ContainerNode group = result.Elements.OfType<ContainerNode>().Single();
        OverlayNode overlay = group.Overlays.Single();
        overlay.Src.ShouldBe("lines.png");
        overlay.X.ShouldBe(Coordinate.Relative(5));
        overlay.Y.ShouldBe(Coordinate.Relative(10));
    }

    [Fact]
    public void LoadLayout_Container_DirectChildOverlay_DoesNotAlsoLeakIntoChildren()
    {
        // Regression guard: now that Overlay is also a valid loose child of Container/OneOf/
        // Condition (and therefore routes through the same TryParseLayoutChild use), an Overlay
        // that's a *direct* child of its own Container must still land exclusively on that
        // Container's own Overlays list, never on the generic Children list too.
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Container>
                  <Overlay src='lines.png' />
                </Container>
              </Body>
            </ControllerTemplate>
            """);

        ContainerNode group = _underTest.LoadLayout("x")!.Elements.OfType<ContainerNode>().Single();

        group.Overlays.Count.ShouldBe(1);
        group.Children.ShouldBeEmpty();
    }

    [Fact]
    public void LoadLayout_ContainerWithInvalidChild_IsLoggedAndSkipped()
    {
        // given a Container containing an unrecognised element
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Container>
                  <Input name='A' />
                  <Bogus />
                </Container>
              </Body>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutDocument result = _underTest.LoadLayout("x")!;

        // then the container is returned without the unknown child, and an error is logged
        result.Elements.OfType<ContainerNode>().Single().Children.Count.ShouldBe(1);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Bogus") && s.Contains("Container")));
    }

    [Fact]
    public void LoadLayout_ContainerWithCollapseFalse_DoesNotCollapse()
    {
        // given a Container with collapse explicitly set to a non-"true" value — exercises the
        // branch where the attribute is present but the string comparison evaluates to false
        StubLayoutXml("""
            <ControllerTemplate>
              <Body><Container collapse='false'><Input name='A' /></Container></Body>
            </ControllerTemplate>
            """);

        LayoutDocument result = _underTest.LoadLayout("x")!;

        result.Elements.OfType<ContainerNode>().Single().Collapse.ShouldBeFalse();
    }

    [Fact]
    public void LoadLayout_ContainerWithoutCollapseAttribute_DefaultsToFalse()
    {
        // given a Container with no collapse attribute
        StubLayoutXml("""
            <ControllerTemplate>
              <Body><Container><Input name='A' /></Container></Body>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutDocument result = _underTest.LoadLayout("x")!;

        // then Collapse defaults to false
        result.Elements.OfType<ContainerNode>().Single().Collapse.ShouldBeFalse();
    }

    [Fact]
    public void LoadLayout_ContainerWithoutVAlignAttribute_DefaultsToTop()
    {
        // given a Container with no vAlign attribute
        StubLayoutXml("""
            <ControllerTemplate>
              <Body><Container><Input name='A' /></Container></Body>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutDocument result = _underTest.LoadLayout("x")!;

        // then VAlign defaults to "top"
        result.Elements.OfType<ContainerNode>().Single().VAlign.ShouldBe("top");
    }

    [Fact]
    public void LoadLayout_ContainerWithVAlign_ParsesLowerCased()
    {
        // given a Container with a mixed-case vAlign attribute
        StubLayoutXml("""
            <ControllerTemplate>
              <Body><Container vAlign='Bottom'><Input name='A' /></Container></Body>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutDocument result = _underTest.LoadLayout("x")!;

        // then VAlign is lower-cased, matching the Align precedent on Label
        result.Elements.OfType<ContainerNode>().Single().VAlign.ShouldBe("bottom");
    }

    [Fact]
    public void LoadLayout_ContainerFor_ParsedOntoNode()
    {
        // given a top-level Container naming the Input it builds on behalf of
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Container for='ButtonDpad'>
                  <Input name='A' />
                </Container>
              </Body>
            </ControllerTemplate>
            """);

        LayoutDocument result = _underTest.LoadLayout("x")!;

        result.Elements.OfType<ContainerNode>().Single().For.ShouldBe("ButtonDpad");
    }

    [Fact]
    public void LoadLayout_ContainerWithoutFor_LeavesItNull()
    {
        // given an ordinary Container with no for= attribute -- the common case, reached through an
        // enclosing Input that supplies CurrentInputName the ordinary way
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Container>
                  <Input name='A' />
                </Container>
              </Body>
            </ControllerTemplate>
            """);

        LayoutDocument result = _underTest.LoadLayout("x")!;

        result.Elements.OfType<ContainerNode>().Single().For.ShouldBeNull();
    }

    [Fact]
    public void LoadLayout_ContainerOverlayMissingSrc_IsSkippedAndLogged()
    {
        // given a Container whose Overlay is missing a src attribute
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Container>
                  <Input name='A' />
                  <Overlay />
                </Container>
              </Body>
            </ControllerTemplate>
            """);

        LayoutDocument result = _underTest.LoadLayout("x")!;

        result.Elements.OfType<ContainerNode>().Single().Overlays.ShouldBeEmpty();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Overlay") && s.Contains("src")));
    }

    [Fact]
    public void LoadLayout_OneOf_CollectsAlternativesInOrder()
    {
        // given a OneOf with two alternative Inputs
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <OneOf>
                  <Input name='Primary' />
                  <Input name='Fallback' />
                </OneOf>
              </Body>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutDocument result = _underTest.LoadLayout("x")!;

        // then alternatives are kept in document order
        OneOfNode oneOf = result.Elements.OfType<OneOfNode>().Single();
        oneOf.Alternatives.OfType<InputNode>().Select(i => i.Name)
            .ShouldBe(["Primary", "Fallback"]);
    }

    [Fact]
    public void LoadLayout_InvalidOneOfChild_IsLoggedAndSkipped()
    {
        // given a OneOf containing an unrecognised element alongside a valid alternative
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <OneOf>
                  <Input name='Primary' />
                  <Bogus />
                </OneOf>
              </Body>
            </ControllerTemplate>
            """);

        LayoutDocument result = _underTest.LoadLayout("x")!;

        result.Elements.OfType<OneOfNode>().Single().Alternatives.Count.ShouldBe(1);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Bogus") && s.Contains("OneOf")));
    }

    // --- Condition ---

    [Fact]
    public void LoadLayout_Condition_ParsesAnyAttributeAndChildren()
    {
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Condition any='AxisLeftStick' match='label'>
                  <Input name='AxisLeftStick' />
                </Condition>
              </Body>
            </ControllerTemplate>
            """);

        LayoutDocument result = _underTest.LoadLayout("x")!;

        ConditionNode condition = result.Elements.OfType<ConditionNode>().Single();
        condition.Any.ShouldBe("AxisLeftStick");
        condition.All.ShouldBeNull();
        condition.None.ShouldBeNull();
        condition.Match.ShouldBe("label");
        condition.Children.OfType<InputNode>().Single().Name.ShouldBe("AxisLeftStick");
    }

    [Fact]
    public void LoadLayout_Condition_MatchOmitted_IsNull()
    {
        // given a Condition with no match attribute — LayoutResolver defaults it to "label"
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Condition all='A B'>
                  <Input name='A' />
                </Condition>
              </Body>
            </ControllerTemplate>
            """);

        LayoutDocument result = _underTest.LoadLayout("x")!;

        result.Elements.OfType<ConditionNode>().Single().Match.ShouldBeNull();
    }

    [Fact]
    public void LoadLayout_Condition_MoreThanOneOfAnyAllNone_SkippedAndLogged()
    {
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Condition any='A' all='B'>
                  <Input name='A' />
                </Condition>
              </Body>
            </ControllerTemplate>
            """);

        LayoutDocument result = _underTest.LoadLayout("x")!;

        result.Elements.OfType<ConditionNode>().ShouldBeEmpty();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Condition") && s.Contains("any") && s.Contains("all") && s.Contains("none")));
    }

    [Fact]
    public void LoadLayout_Condition_NoneOfAnyAllNone_SkippedAndLogged()
    {
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Condition>
                  <Input name='A' />
                </Condition>
              </Body>
            </ControllerTemplate>
            """);

        LayoutDocument result = _underTest.LoadLayout("x")!;

        result.Elements.OfType<ConditionNode>().ShouldBeEmpty();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Condition") && s.Contains("any") && s.Contains("all") && s.Contains("none")));
    }

    [Fact]
    public void LoadLayout_Condition_InvalidChild_IsLoggedAndSkipped()
    {
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Condition any='A'>
                  <Input name='A' />
                  <Bogus />
                </Condition>
              </Body>
            </ControllerTemplate>
            """);

        LayoutDocument result = _underTest.LoadLayout("x")!;

        result.Elements.OfType<ConditionNode>().Single().Children.Count.ShouldBe(1);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Bogus") && s.Contains("Condition")));
    }

    [Fact]
    public void LoadLayout_Condition_BareLabel_ParsesAsLooseChild()
    {
        // given a Condition wrapping a bare Label — no enclosing Input of its own
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Condition any='A'>
                  <Label x='+3' y='+4' align='right' />
                </Condition>
              </Body>
            </ControllerTemplate>
            """);

        LayoutDocument result = _underTest.LoadLayout("x")!;

        ConditionNode condition = result.Elements.OfType<ConditionNode>().Single();
        LabelNode label = condition.Children.OfType<LabelNode>().Single();
        label.Align.ShouldBe("right");
        label.Y.ShouldBe(Coordinate.Relative(4));
    }

    [Fact]
    public void LoadLayout_Condition_BareOverlay_ParsesAsLooseChild()
    {
        // given a Condition wrapping a bare Overlay — no enclosing Input or Container of its own
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Condition any='A'>
                  <Overlay src='lines.png' x='+5' y='+10' />
                </Condition>
              </Body>
            </ControllerTemplate>
            """);

        LayoutDocument result = _underTest.LoadLayout("x")!;

        ConditionNode condition = result.Elements.OfType<ConditionNode>().Single();
        OverlayNode overlay = condition.Children.OfType<OverlayNode>().Single();
        overlay.Src.ShouldBe("lines.png");
        overlay.Y.ShouldBe(Coordinate.Relative(10));
    }

    [Fact]
    public void LoadLayout_OneOf_BareOverlay_ParsesAsAlternative()
    {
        // given a OneOf with a bare Overlay alongside an Input alternative
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <OneOf>
                  <Input name='A' />
                  <Overlay src='lines.png' />
                </OneOf>
              </Body>
            </ControllerTemplate>
            """);

        LayoutDocument result = _underTest.LoadLayout("x")!;

        OneOfNode oneOf = result.Elements.OfType<OneOfNode>().Single();
        oneOf.Alternatives.OfType<OverlayNode>().Single().Src.ShouldBe("lines.png");
    }

    [Fact]
    public void LoadLayout_Body_BareOverlay_ParsesAsTopLevelElement()
    {
        // given a bare Overlay directly under Body — no Input or Container ancestor at all
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Overlay src='background.png' />
              </Body>
            </ControllerTemplate>
            """);

        LayoutDocument result = _underTest.LoadLayout("x")!;

        result.Elements.OfType<OverlayNode>().Single().Src.ShouldBe("background.png");
    }

    [Fact]
    public void LoadLayout_Input_DirectChildLabel_DoesNotAlsoLeakIntoChildren()
    {
        // Regression guard: now that Label is also a valid loose child of Group/OneOf/Condition
        // (and therefore routes through the same TryParseLayoutChild use), a Label that's a
        // *direct* child of its own Input must still land exclusively on that Input's own Labels
        // list, never on the generic Children list too.
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Input name='ButtonA'>
                  <Label />
                </Input>
              </Body>
            </ControllerTemplate>
            """);

        InputNode input = _underTest.LoadLayout("x")!.Elements.OfType<InputNode>().Single();

        input.Labels.Count.ShouldBe(1);
        input.Children.ShouldBeEmpty();
    }

    [Fact]
    public void LoadLayout_InvalidBodyChild_IsLoggedAndSkipped()
    {
        // given a body with an unrecognized element alongside a valid one
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Garbage />
                <Input name='A' />
              </Body>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutDocument result = _underTest.LoadLayout("x")!;

        // then the unknown element is logged and the valid Input is still parsed
        result.Elements.OfType<InputNode>().Single().Name.ShouldBe("A");
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Garbage") && s.Contains("Body")));
    }

    // --- Coordinate parsing edge cases ---

    [Fact]
    public void LoadLayout_InvalidCoordinate_LogsErrorAndKeepsDefault()
    {
        // given an Input with non-numeric x
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Input name='A' x='oops' y='50' />
              </Body>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutDocument result = _underTest.LoadLayout("x")!;

        // then the bad x is logged and X stays at its default; Y still parses
        InputNode input = result.Elements.OfType<InputNode>().Single();
        input.X.ShouldBe(Coordinate.Relative(0));
        input.Y.ShouldBe(Coordinate.Absolute(50));
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("oops")));
    }

    [Fact]
    public void LoadLayout_EmptyCoordinateAttribute_LogsErrorAndKeepsDefault()
    {
        // given an Input with an empty-string x attribute — the attribute exists (so ReadCoordinate
        // calls TryParseCoordinate) but IsNullOrEmpty is true, hitting the early-return branch
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Input name='A' x='' />
              </Body>
            </ControllerTemplate>
            """);

        LayoutDocument result = _underTest.LoadLayout("x")!;

        result.Elements.OfType<InputNode>().Single().X.ShouldBe(Coordinate.Relative(0));
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("x=") && s.Contains("Input")));
    }
}
