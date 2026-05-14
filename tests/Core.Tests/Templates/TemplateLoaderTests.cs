using DynamicControls.Templates;
using NSubstitute;

namespace DynamicControls.Core.Tests.Templates;

/// <summary>
/// Unit tests for <see cref="TemplateLoader"/>. The loader translates a Layout.xml document
/// into raw <see cref="LayoutConfig"/> DTOs without applying any business logic. The
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
        LayoutConfig? result = _underTest.LoadLayout("x");

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
        LayoutConfig? result = _underTest.LoadLayout("x");

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

        LayoutConfig result = _underTest.LoadLayout("x")!;

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
        LayoutConfig result = _underTest.LoadLayout("x")!;

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
        LayoutConfig result = _underTest.LoadLayout("x")!;

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
        LayoutConfig result = _underTest.LoadLayout("x")!;

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

        LayoutConfig result = _underTest.LoadLayout("x")!;

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
        LayoutConfig result = _underTest.LoadLayout("x")!;

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
        LayoutConfig result = _underTest.LoadLayout("x")!;

        // then only the named input is kept and an error is logged for the nameless one
        result.Elements.ShouldHaveSingleItem();
        result.Elements.OfType<InputNode>().Single().Name.ShouldBe("ButtonA");
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("missing 'name'")));
    }

    [Fact]
    public void LoadLayout_InputWithLabelRenderOverlay_CollectsAllChildren()
    {
        // given an Input with one of each leaf type as a child
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Input name='ButtonA' x='100' y='200'>
                  <Render x='+0' y='+0' useImage='Stick.png' showIf='label' minOpacity='0.5' inactiveBlurRadius='2' />
                  <Overlay src='dpad.png' x='+5' y='-5' />
                  <Label x='+10' y='+20' align='CENTER' fontSize='16' />
                </Input>
              </Body>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutConfig result = _underTest.LoadLayout("x")!;

        // then each child lands in its respective list with attributes parsed
        InputNode input = result.Elements.OfType<InputNode>().Single();
        input.X.ShouldBe(Coordinate.Absolute(100));
        input.Y.ShouldBe(Coordinate.Absolute(200));

        RenderNode render = input.Renders.Single();
        render.X.ShouldBe(Coordinate.Relative(0));
        render.UseImage.ShouldBe("Stick.png");
        render.ShowIf.ShouldBe("label");
        render.MinOpacity.ShouldBe(0.5);
        render.InactiveBlurRadius.ShouldBe(2);

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
        LayoutConfig result = _underTest.LoadLayout("x")!;

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
        LayoutConfig result = _underTest.LoadLayout("x")!;

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

        LayoutConfig result = _underTest.LoadLayout("x")!;

        LabelNode label = result.Elements.OfType<InputNode>().Single().Labels.Single();
        label.Align.ShouldBe("left");
        label.X.ShouldBe(Coordinate.Relative(0));
        label.Y.ShouldBe(Coordinate.Relative(0));
        label.FontSize.ShouldBeNull();
    }

    [Fact]
    public void LoadLayout_RenderWithWidthAndHeight_ParsesDimensions()
    {
        // given a Render with explicit width and height — exercises the true branches skipped by other render tests
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Input name='A'>
                  <Render width='80' height='60' />
                </Input>
              </Body>
            </ControllerTemplate>
            """);

        LayoutConfig result = _underTest.LoadLayout("x")!;

        RenderNode render = result.Elements.OfType<InputNode>().Single().Renders.Single();
        render.Width.ShouldBe(80);
        render.Height.ShouldBe(60);
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

        LayoutConfig result = _underTest.LoadLayout("x")!;

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

        LayoutConfig result = _underTest.LoadLayout("x")!;

        InputNode input = result.Elements.OfType<InputNode>().Single();
        input.Children.ShouldBeEmpty();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Bogus") && s.Contains("Input")));
    }

    // --- Group / Stack / OneOf ---

    [Fact]
    public void LoadLayout_Group_CollectsInputsAndOverlays()
    {
        // given a Group containing two Inputs and an Overlay
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Group>
                  <Input name='A' />
                  <Input name='B' />
                  <Overlay src='frame.png' />
                </Group>
              </Body>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutConfig result = _underTest.LoadLayout("x")!;

        // then both inputs and the overlay land on the GroupNode
        GroupNode group = result.Elements.OfType<GroupNode>().Single();
        group.Children.OfType<InputNode>().Select(i => i.Name).ShouldBe(["A", "B"]);
        group.Overlays.Single().Src.ShouldBe("frame.png");
    }

    [Fact]
    public void LoadLayout_GroupOverlayMissingSrc_IsSkippedAndLogged()
    {
        // given a Group whose Overlay is missing a src attribute
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Group>
                  <Input name='A' />
                  <Overlay />
                </Group>
              </Body>
            </ControllerTemplate>
            """);

        LayoutConfig result = _underTest.LoadLayout("x")!;

        result.Elements.OfType<GroupNode>().Single().Overlays.ShouldBeEmpty();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Overlay") && s.Contains("src")));
    }

    [Fact]
    public void LoadLayout_InvalidGroupChild_IsLoggedAndSkipped()
    {
        // given a Group containing an element that is neither a layout child nor an Overlay
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Group>
                  <Input name='A' />
                  <Bogus />
                </Group>
              </Body>
            </ControllerTemplate>
            """);

        LayoutConfig result = _underTest.LoadLayout("x")!;

        result.Elements.OfType<GroupNode>().Single().Children.Count.ShouldBe(1);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Bogus") && s.Contains("Group")));
    }

    [Fact]
    public void LoadLayout_Stack_ParsesPositionGapAndCollapse()
    {
        // given a Stack with x, y, gap, and collapse attributes
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Stack x='100' y='+50' gap='40' collapse='TRUE'>
                  <Input name='A' />
                </Stack>
              </Body>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutConfig result = _underTest.LoadLayout("x")!;

        // then attributes parse: absolute x, relative y, gap, case-insensitive collapse=true
        StackNode stack = result.Elements.OfType<StackNode>().Single();
        stack.X.ShouldBe(Coordinate.Absolute(100));
        stack.Y.ShouldBe(Coordinate.Relative(50));
        stack.Gap.ShouldBe(40);
        stack.Collapse.ShouldBeTrue();
        stack.Children.OfType<InputNode>().Single().Name.ShouldBe("A");
    }

    [Fact]
    public void LoadLayout_StackWithOverlay_CollectsOverlay()
    {
        // given a Stack containing an Overlay child
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Stack>
                  <Input name='A' />
                  <Overlay src='lines.png' x='+5' y='+10' />
                </Stack>
              </Body>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutConfig result = _underTest.LoadLayout("x")!;

        // then the overlay is collected onto the stack
        StackNode stack = result.Elements.OfType<StackNode>().Single();
        OverlayNode overlay = stack.Overlays.Single();
        overlay.Src.ShouldBe("lines.png");
        overlay.X.ShouldBe(Coordinate.Relative(5));
        overlay.Y.ShouldBe(Coordinate.Relative(10));
    }

    [Fact]
    public void LoadLayout_StackWithInvalidChild_IsLoggedAndSkipped()
    {
        // given a Stack containing an unrecognised element
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Stack>
                  <Input name='A' />
                  <Bogus />
                </Stack>
              </Body>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutConfig result = _underTest.LoadLayout("x")!;

        // then the stack is returned without the unknown child, and an error is logged
        result.Elements.OfType<StackNode>().Single().Children.Count.ShouldBe(1);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Bogus") && s.Contains("Stack")));
    }

    [Fact]
    public void LoadLayout_StackWithCollapseFalse_DoesNotCollapse()
    {
        // given a Stack with collapse explicitly set to a non-"true" value — exercises the
        // branch where the attribute is present but the string comparison evaluates to false
        StubLayoutXml("""
            <ControllerTemplate>
              <Body><Stack collapse='false'><Input name='A' /></Stack></Body>
            </ControllerTemplate>
            """);

        LayoutConfig result = _underTest.LoadLayout("x")!;

        result.Elements.OfType<StackNode>().Single().Collapse.ShouldBeFalse();
    }

    [Fact]
    public void LoadLayout_StackWithoutCollapseAttribute_DefaultsToFalse()
    {
        // given a Stack with no collapse attribute
        StubLayoutXml("""
            <ControllerTemplate>
              <Body><Stack><Input name='A' /></Stack></Body>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutConfig result = _underTest.LoadLayout("x")!;

        // then Collapse defaults to false
        result.Elements.OfType<StackNode>().Single().Collapse.ShouldBeFalse();
    }

    [Fact]
    public void LoadLayout_StackOverlayMissingSrc_IsSkippedAndLogged()
    {
        // given a Stack whose Overlay is missing a src attribute
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Stack>
                  <Input name='A' />
                  <Overlay />
                </Stack>
              </Body>
            </ControllerTemplate>
            """);

        LayoutConfig result = _underTest.LoadLayout("x")!;

        result.Elements.OfType<StackNode>().Single().Overlays.ShouldBeEmpty();
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
        LayoutConfig result = _underTest.LoadLayout("x")!;

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

        LayoutConfig result = _underTest.LoadLayout("x")!;

        result.Elements.OfType<OneOfNode>().Single().Alternatives.Count.ShouldBe(1);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Bogus") && s.Contains("OneOf")));
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
        LayoutConfig result = _underTest.LoadLayout("x")!;

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
        LayoutConfig result = _underTest.LoadLayout("x")!;

        // then the bad x is logged and X stays at its default; Y still parses
        InputNode input = result.Elements.OfType<InputNode>().Single();
        input.X.ShouldBe(Coordinate.Relative(0));
        input.Y.ShouldBe(Coordinate.Absolute(50));
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("oops")));
    }

    [Fact]
    public void LoadLayout_RenderWithInvalidCoordinate_KeepsRenderWithDefaultAndLogs()
    {
        // given a Render whose y attribute can't be parsed
        StubLayoutXml("""
            <ControllerTemplate>
              <Body>
                <Input name='A'>
                  <Render x='0' y='bad' />
                </Input>
              </Body>
            </ControllerTemplate>
            """);

        // when the loader runs
        LayoutConfig result = _underTest.LoadLayout("x")!;

        // then the render is kept with the default y, valid x preserved, and the error is logged
        RenderNode render = result.Elements.OfType<InputNode>().Single().Renders.Single();
        render.X.ShouldBe(Coordinate.Absolute(0));
        render.Y.ShouldBe(Coordinate.Relative(0));
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Render") && s.Contains("bad")));
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

        LayoutConfig result = _underTest.LoadLayout("x")!;

        result.Elements.OfType<InputNode>().Single().X.ShouldBe(Coordinate.Relative(0));
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("x=") && s.Contains("Input")));
    }
}
