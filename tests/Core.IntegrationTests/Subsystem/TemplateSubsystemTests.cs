using DynamicControls.Composition;
using DynamicControls.Templates;

namespace DynamicControls.Core.IntegrationTests.Subsystem;

/// <summary>
/// Verifies the template-loading pipeline with its real internal wiring intact:
/// <see cref="TemplateLoader"/> (XML parsing) → <see cref="LayoutResolver"/> (tree + coordinate
/// resolution) → <see cref="TemplateService"/> (orchestration + caching). Covers the XML→Template
/// path that users author directly — each test stages a Layout.xml in an in-memory
/// <see cref="MockFileSystem"/> and asserts on the fully-resolved <see cref="Template"/>.
/// <see cref="ITemplateImageResolver"/> is faked so image-existence probes don't need real image
/// files and the resolved path values are predictable from the test.
/// </summary>
public class TemplateSubsystemTests
{
    private static readonly string RootDir = Path.DirectorySeparatorChar + "dc";
    private const string TemplateName = "Xbox Series X";

    // ---- factory helpers ----

    private static (TemplateService Service, FakeTemplateImageResolver Images) Build(string xml)
    {
        var dc = new MockDynamicControlsFilesystem(RootDir);
        dc.WriteLayout(TemplateName, xml);
        var images = new FakeTemplateImageResolver();
        var service = TemplateFactory.Create(RootDir, logger: new NullLogger(), fs: dc.Fs, imageResolver: images);
        return (service, images);
    }

    private static Template Load(string xml)
    {
        var (service, _) = Build(xml);
        return service.Load(TemplateName);
    }

    // ---- head / style resolution ----

    [Fact]
    public void Load_HeadStyle_DefaultsFlowThroughToResolvedLayout()
    {
        // The Head style sets template-wide visual defaults that the LayoutResolver applies when
        // individual Inputs don't override them.
        var t = Load("""
            <ControllerTemplate>
              <Head>
                <Style fontSize="22" minOpacity="0.3" inactiveBlurRadius="6" />
              </Head>
              <Body />
            </ControllerTemplate>
            """);

        t.Layout.DefaultFontSize.ShouldBe(22);
        t.Layout.DefaultMinOpacity.ShouldBe(0.3);
        t.Layout.DefaultInactiveBlurRadius.ShouldBe(6);
    }

    [Fact]
    public void Load_NoHeadStyle_FallsBackToRenderingDefaults()
    {
        var t = Load("<ControllerTemplate><Body /></ControllerTemplate>");

        t.Layout.DefaultFontSize.ShouldBe(RenderingDefaults.FontSize);
        t.Layout.DefaultMinOpacity.ShouldBe(0);
        t.Layout.DefaultInactiveBlurRadius.ShouldBe(RenderingDefaults.InactiveBlurRadius);
    }

    [Fact]
    public void Load_NamedStyleOnInput_InheritsMissingAttributesFromStyle()
    {
        // An Input with style="dim" inherits MinOpacity and ShowIf from the named style, but its
        // own explicit inactiveBlurRadius overrides the style's.
        var t = Load("""
            <ControllerTemplate>
              <Head>
                <Style name="dim" showIf="mapping" minOpacity="0.3" inactiveBlurRadius="8" />
              </Head>
              <Body>
                <Input name="ButtonA" style="dim" inactiveBlurRadius="4" x="0" y="0" width="64" height="64" />
              </Body>
            </ControllerTemplate>
            """);

        var inputA = t.Layout.Elements.OfType<InputDefinition>().Single();
        var render = inputA.InputImages.Single();
        render.ShowIf.ShouldBe(ShowIfCondition.Mapped);
        render.MinOpacity.ShouldBe(0.3);
        render.InactiveBlurRadius.ShouldBe(4.0);  // explicit Input attribute wins
    }

    [Fact]
    public void Load_LabelFontSize_InheritsFromHeadStyleWhenLabelAndInputOmitIt()
    {
        // The fontSize cascade (Label → Input → named style → Head default) must compose through
        // the whole XML pipeline, not just the resolver in isolation. With a Head default of 22
        // and neither the Input nor the Label specifying fontSize, the Label resolves to 22.
        var t = Load("""
            <ControllerTemplate>
              <Head>
                <Style fontSize="22" />
              </Head>
              <Body>
                <Input name="ButtonA" x="0" y="0">
                  <Label x="+10" y="+10" />
                </Input>
              </Body>
            </ControllerTemplate>
            """);

        var label = t.Layout.Elements.OfType<InputDefinition>().Single().Labels.Single();
        label.FontSize.ShouldBe(22);
    }

    // ---- coordinate resolution ----

    [Fact]
    public void Load_AbsoluteInputCoords_ImagePositionIsAbsolute()
    {
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Input name="ButtonA" x="100" y="200" width="64" height="64" />
              </Body>
            </ControllerTemplate>
            """);

        var image = t.Layout.Elements.OfType<InputDefinition>().Single().InputImages.Single();
        image.X.ShouldBe(100);
        image.Y.ShouldBe(200);
    }

    [Fact]
    public void Load_RelativeInputCoords_AddToAmbientOrigin()
    {
        // A top-level Input's +/- coords are relative to the canvas origin (0,0).
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Input name="ButtonA" x="+5" y="-10" width="44" height="44" />
              </Body>
            </ControllerTemplate>
            """);

        var image = t.Layout.Elements.OfType<InputDefinition>().Single().InputImages.Single();
        image.X.ShouldBe(5);
        image.Y.ShouldBe(-10);
    }

    [Fact]
    public void Load_GroupChildren_PositionedFromGroupOriginWithGap()
    {
        // A Group at (10,100) with gap=45 places its first child at y=100 and second at y=145.
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Container x="10" y="100" gap="45">
                  <Input name="ButtonA" width="34" height="34" />
                  <Input name="ButtonB" width="34" height="34" />
                </Container>
              </Body>
            </ControllerTemplate>
            """);

        var group = t.Layout.Elements.OfType<Container>().Single();
        var inputs = group.Children.OfType<InputDefinition>().ToList();

        inputs[0].InputImages.Single().Y.ShouldBe(100);
        inputs[1].InputImages.Single().Y.ShouldBe(145);
    }

    [Fact]
    public void Load_GroupVAlignBottom_DeclaredYIsTheLastChildNotTheFirst()
    {
        // A Group at (10,100) with gap=45 and vAlign="bottom": with the last of two children on
        // y=100, the first lands 45 above it at y=55.
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Container x="10" y="100" gap="45" vAlign="bottom">
                  <Input name="ButtonA" width="34" height="34" />
                  <Input name="ButtonB" width="34" height="34" />
                </Container>
              </Body>
            </ControllerTemplate>
            """);

        var group = t.Layout.Elements.OfType<Container>().Single();
        var inputs = group.Children.OfType<InputDefinition>().ToList();

        inputs[0].InputImages.Single().Y.ShouldBe(55);
        inputs[1].InputImages.Single().Y.ShouldBe(100);
    }

    [Fact]
    public void Load_NestedInput_InheritsParentOriginNotAGroupsCumulativeOffset()
    {
        // Children of an Input start a new coord context from that Input's origin — they do NOT
        // continue an enclosing Group's cumulative slot offset.
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Input name="AxisLeftStick" x="500" y="300" width="124" height="124">
                  <Input name="AxisLeftStickUp" x="+5" y="+10" width="34" height="34" />
                </Input>
              </Body>
            </ControllerTemplate>
            """);

        var parent = t.Layout.Elements.OfType<InputDefinition>().Single();
        var child = parent.Children.OfType<InputDefinition>().Single();
        child.InputImages.Single().X.ShouldBe(505);
        child.InputImages.Single().Y.ShouldBe(310);
    }

    // ---- image resolution via TemplateImageResolver ----

    [Fact]
    public void Load_Input_ImageFileIsInputNameDotPng()
    {
        // When an Input carries no useImage attribute, ImageFile is set to the Input's
        // name + ".png". Path resolution is deferred to render time (InputImageResolver)
        // so ImageFile is a plain filename, not a resolved absolute path.
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Input name="ButtonA" x="0" y="0" width="64" height="64" />
              </Body>
            </ControllerTemplate>
            """);

        t.Layout.Elements.OfType<InputDefinition>().Single()
            .InputImages.Single().ImageFile.ShouldBe("ButtonA.png");
    }

    [Fact]
    public void Load_InputWithUseImage_BothImageFileAndUseImageFileAreUseImageDotPng()
    {
        // An Input with useImage="ButtonDpadUp" borrows another input's image. Both ImageFile
        // and UseImageFile carry the borrowed filename so the renderer can distinguish a
        // "borrowing" image from an "identity" one. Path resolution still happens at
        // render time, not here.
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Input name="ButtonDpad" x="0" y="0" useImage="ButtonDpadUp" width="34" height="34" />
              </Body>
            </ControllerTemplate>
            """);

        var image = t.Layout.Elements.OfType<InputDefinition>().Single().InputImages.Single();
        image.ImageFile.ShouldBe("ButtonDpad.png");     // always the owning Input's name
        image.UseImageFile.ShouldBe("ButtonDpadUp.png"); // the borrowed asset
    }

    [Fact]
    public void Load_BaseImageFromResolver_AttachedToTemplate()
    {
        // TemplateService asks the ITemplateImageResolver for the base image and attaches it to
        // the Template. The image resolver — not the Layout.xml — owns base-image discovery.
        var (service, images) = Build("<ControllerTemplate><Body /></ControllerTemplate>");
        var baseImage = new BaseImage(@"C:\tmpl\BaseImage.png", Width: 800, Height: 400);
        images.StubBaseImage(TemplateName, baseImage);

        Template t = service.Load(TemplateName);

        t.BaseImage.ShouldBe(baseImage);
    }

    [Fact]
    public void Load_Overlay_SourceResolvedAtBuildTime()
    {
        // Overlay sources are resolved once by the TemplateImageResolver during template
        // loading, not at render time. The resolved path lands on OverlayDefinition.Source.
        var (service, images) = Build("""
            <ControllerTemplate>
              <Body>
                <Input name="ButtonA" x="0" y="0" width="64" height="64">
                  <Overlay src="Line_ButtonA.png" x="+50" y="+10" />
                </Input>
              </Body>
            </ControllerTemplate>
            """);
        images.Stub(TemplateName, "Line_ButtonA.png", generic: "/tmpl/Line_ButtonA.png");
        images.Stub(TemplateName, "ButtonA.png", generic: "/tmpl/ButtonA.png");

        var t = service.Load(TemplateName);

        t.Layout.Elements.OfType<InputDefinition>().Single()
            .Overlays.Single().Source.ShouldBe("/tmpl/Line_ButtonA.png");
    }

    // ---- structural elements ----

    [Fact]
    public void Load_Group_ChildrenCollected()
    {
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Container x="0" y="0" gap="10">
                  <Input name="ButtonA" x="0" y="0" width="44" height="44"></Input>
                  <Input name="ButtonB" x="0" y="50" width="44" height="44"></Input>
                </Container>
              </Body>
            </ControllerTemplate>
            """);

        // A body-level Group lands directly as the sole Container in Elements (no wrapper).
        var group = t.Layout.Elements.OfType<Container>().Single();
        group.Children.OfType<InputDefinition>().Select(i => i.Name).ShouldBe(["ButtonA", "ButtonB"]);
    }

    [Fact]
    public void Load_OneOf_AlternativesPreservedInOrder()
    {
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Input name="ButtonDpad" x="0" y="0" width="135" height="135">
                  <OneOf>
                    <Container>
                      <Input name="ButtonDpadUp" width="34" height="34"></Input>
                    </Container>
                    <Input name="ButtonDpad" x="0" y="0" useImage="ButtonDpadUp" width="34" height="34" />
                  </OneOf>
                </Input>
              </Body>
            </ControllerTemplate>
            """);

        var dpad = t.Layout.Elements.OfType<InputDefinition>().Single();
        var oneOf = dpad.Children.OfType<OneOf>().Single();
        oneOf.Alternatives.Count.ShouldBe(2);
        oneOf.Alternatives[0].ShouldBeOfType<Container>();
        oneOf.Alternatives[1].ShouldBeOfType<InputDefinition>();
    }

    [Fact]
    public void Load_OneOfInsideGroup_ConsumesOneSlot()
    {
        // OneOf inside a Group consumes a single slot — all alternatives share that slot's
        // origin. The Input after the OneOf advances by exactly one gap. This is the canonical
        // directional-Dpad authoring pattern (per CLAUDE.md): per-direction labels vs. a single
        // whole-input render expressed as OneOf alternatives at one group position.
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Container x="0" y="100" gap="50">
                  <OneOf>
                    <Input name="ButtonDpadUp" width="34" height="34"></Input>
                    <Input name="ButtonDpad" width="34" height="34"></Input>
                  </OneOf>
                  <Input name="ButtonStart" width="34" height="34"></Input>
                </Container>
              </Body>
            </ControllerTemplate>
            """);

        var group = t.Layout.Elements.OfType<Container>().Single();
        var oneOf = group.Children.OfType<OneOf>().Single();
        var altAbsoluteYs = oneOf.Alternatives
            .OfType<InputDefinition>()
            .Select(a => a.InputImages.Single().Y);
        altAbsoluteYs.ShouldAllBe(y => y == 100);

        var trailing = group.Children.OfType<InputDefinition>().Single(i => i.Name == "ButtonStart");
        trailing.InputImages.Single().Y.ShouldBe(150);
    }

    // ---- InputDescendants index ----

    [Fact]
    public void Load_InputNamedForAWhole_DescendantsAreItsPartNamesRegardlessOfNesting()
    {
        // InputDescendants is computed once at load time and used by the rendering pipeline to
        // fan out visibility (a whole is visible if any of its direction names has a
        // label/mapping) — sourced from WholeInputs.PartsOf by name, not from the tree's own
        // nesting. This template only nests two of the four directions under the stick; all four
        // still show up, and an unrelated Input nested the same way gets nothing.
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Input name="AxisLeftStick" x="0" y="0" width="124" height="124">
                  <Input name="AxisLeftStickUp" width="34" height="34"></Input>
                  <Input name="AxisLeftStickDown" width="34" height="34"></Input>
                </Input>
                <Input name="ButtonA" x="0" y="0" width="64" height="64">
                  <Input name="ButtonB" width="34" height="34"></Input>
                </Input>
              </Body>
            </ControllerTemplate>
            """);

        var stick = t.Layout.Elements.OfType<InputDefinition>().Single(i => i.Name == "AxisLeftStick");
        t.Layout.InputDescendants[stick].ShouldBe(
            ["AxisLeftStickUp", "AxisLeftStickDown", "AxisLeftStickLeft", "AxisLeftStickRight"]);

        var buttonA = t.Layout.Elements.OfType<InputDefinition>().Single(i => i.Name == "ButtonA");
        t.Layout.InputDescendants[buttonA].ShouldBeEmpty();
    }

    [Fact]
    public void Load_DuplicateTopLevelInput_BothInstancesResolveTheSamePartNamesByReference()
    {
        // The layout schema permits duplicate top-level <Input> entries — the directional pattern
        // uses one nested-children variant for per-direction labels and a separate "strict-self"
        // variant with no children. Both must survive as distinct InputDefinitions, and
        // InputDescendants — keyed by reference — must hold an entry for each instance, resolving
        // the same part names from the shared name regardless of which one happens to nest a
        // (now irrelevant) child.
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Input name="ButtonDpad" x="0" y="0" width="135" height="135">
                  <Input name="ButtonDpadUp" width="34" height="34"></Input>
                </Input>
                <Input name="ButtonDpad" x="0" y="0" width="135" height="135" />
              </Body>
            </ControllerTemplate>
            """);

        var dpads = t.Layout.Elements.OfType<InputDefinition>()
            .Where(i => i.Name == "ButtonDpad")
            .ToList();
        dpads.Count.ShouldBe(2);

        InputDefinition withChild = dpads.Single(d => d.Children.Count > 0);
        InputDefinition strictSelf = dpads.Single(d => d.Children.Count == 0);
        t.Layout.InputDescendants[withChild].ShouldBe(
            ["ButtonDpadUp", "ButtonDpadDown", "ButtonDpadLeft", "ButtonDpadRight"]);
        t.Layout.InputDescendants[strictSelf].ShouldBe(t.Layout.InputDescendants[withChild]);
    }

    [Fact]
    public void Load_CollapsingGroup_CollapseInfoBuiltForChildren()
    {
        // A Group with collapse="true" registers its children in CollapseInfo so the rendering
        // pipeline can vacate empty slots and shift subsequent entries up.
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Container x="0" y="0" gap="45" collapse="true">
                  <Input name="ButtonA" width="34" height="34"></Input>
                  <Input name="ButtonB" width="34" height="34"></Input>
                </Container>
              </Body>
            </ControllerTemplate>
            """);

        var group = t.Layout.Elements.OfType<Container>().Single();
        var inputs = group.Children.OfType<InputDefinition>().ToList();

        t.Layout.CollapseInfo.Keys.ShouldContain(inputs[0], ReferenceEqualityComparer.Instance);
        t.Layout.CollapseInfo.Keys.ShouldContain(inputs[1], ReferenceEqualityComparer.Instance);
        t.Layout.CollapseInfo[inputs[0]].Gap.ShouldBe(45);
    }

    [Fact]
    public void Load_CollapsingGroup_RegistersInputsNestedInAnotherGroup()
    {
        // A nested <Container> occupies one opaque slot in the outer group's slot list, but
        // CollapseGroupBuilder's SetMetadata still recurses through its children — every leaf
        // Input reachable from a slot is stamped with that slot's CollapseInfo (same gap, same
        // shared slot list), regardless of whether the slot is itself an Input, a nested Group,
        // or a OneOf's alternatives.
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Container x="0" y="0" gap="40" collapse="true">
                  <Input name="ButtonA" width="34" height="34"></Input>
                  <Container>
                    <Input name="ButtonB" width="34" height="34"></Input>
                    <Input name="ButtonC" width="34" height="34"></Input>
                  </Container>
                </Container>
              </Body>
            </ControllerTemplate>
            """);

        var group = t.Layout.Elements.OfType<Container>().Single();
        var inner = group.Children.OfType<Container>().Single();
        InputDefinition a = group.Children.OfType<InputDefinition>().Single();
        var nested = inner.Children.OfType<InputDefinition>().ToList();

        t.Layout.CollapseInfo.Keys.ShouldContain(a, ReferenceEqualityComparer.Instance);
        t.Layout.CollapseInfo.Keys.ShouldContain(nested[0], ReferenceEqualityComparer.Instance);
        t.Layout.CollapseInfo.Keys.ShouldContain(nested[1], ReferenceEqualityComparer.Instance);
        t.Layout.CollapseInfo[nested[1]].Gap.ShouldBe(40);
    }

    // ---- interaction scenarios ----
    // A handful of tests run against a single richer fixture that resembles a slice of a real
    // template. The goal is to probe behaviors that only emerge when multiple features compose
    // (named style + Group slot + collapse + OneOf + nested Inputs all at once) — interaction
    // bugs that minimal single-feature stubs miss but that E2E tests can only catch by accident.
    // Keep this list small; if a new test fits a minimal stub, prefer that.

    private const string ScenarioXml = """
        <ControllerTemplate>
          <Head>
            <Style fontSize="20" />
            <Style name="dim" showIf="mapping" minOpacity="0.4" />
          </Head>
          <Body>
            <Container x="100" y="200" gap="40" collapse="true">
              <Input name="ButtonA" style="dim" width="34" height="34">
                <Label x="+50" y="+5" />
              </Input>
              <Container>
                <Input name="ButtonB" style="dim" width="34" height="34" />
              </Container>
              <OneOf>
                <Input name="ButtonDpadUp" width="34" height="34"></Input>
                <Input name="ButtonDpad" width="34" height="34">
                  <Input name="ButtonDpadDown" width="34" height="34"></Input>
                </Input>
              </OneOf>
            </Container>
          </Body>
        </ControllerTemplate>
        """;

    [Fact]
    public void Scenario_StyleCascade_MeetsGroupSlotPositioning()
    {
        // ButtonA sits in slot 0 of the Group at (100, 200). Its render inherits minOpacity=0.4
        // and showIf=Mapped from the named "dim" style; its label inherits fontSize=20 from the
        // unnamed Head Style. All four cascades fire together.
        var t = Load(ScenarioXml);

        var group = t.Layout.Elements.OfType<Container>().Single();
        InputDefinition buttonA = group.Children.OfType<InputDefinition>().Single(i => i.Name == "ButtonA");

        InputImageDefinition render = buttonA.InputImages.Single();
        render.Y.ShouldBe(200);
        render.MinOpacity.ShouldBe(0.4);
        render.ShowIf.ShouldBe(ShowIfCondition.Mapped);

        buttonA.Labels.Single().FontSize.ShouldBe(20);
    }

    [Fact]
    public void Scenario_CollapseInfo_SpansContainerAndOneOfSlots()
    {
        // CollapseGroupBuilder must register every Input reachable as a slot leaf: ButtonA
        // (direct child), ButtonB (nested one level inside its own single-child Group, which
        // SetMetadata still recurses through), and both OneOf alternatives. ButtonDpadDown is
        // nested inside the ButtonDpad alternative — it collapses with its parent as a unit and
        // does NOT get its own CollapseInfo entry.
        var t = Load(ScenarioXml);

        var byName = AllInputs(t.Layout.Elements).ToDictionary(i => i.Name);

        t.Layout.CollapseInfo.Keys.ShouldContain(byName["ButtonA"], ReferenceEqualityComparer.Instance);
        t.Layout.CollapseInfo.Keys.ShouldContain(byName["ButtonB"], ReferenceEqualityComparer.Instance);
        t.Layout.CollapseInfo.Keys.ShouldContain(byName["ButtonDpadUp"], ReferenceEqualityComparer.Instance);
        t.Layout.CollapseInfo.Keys.ShouldContain(byName["ButtonDpad"], ReferenceEqualityComparer.Instance);
        t.Layout.CollapseInfo.ContainsKey(byName["ButtonDpadDown"]).ShouldBeFalse();
        t.Layout.CollapseInfo[byName["ButtonA"]].Gap.ShouldBe(40);
    }

    [Fact]
    public void Scenario_OneOfConsumesOneSlot_AndNestedInputInheritsThatOrigin()
    {
        // Slot accounting: ButtonA=slot0(y=200), ButtonB=slot1(y=240), OneOf=slot2(y=280). Both
        // OneOf alternatives share that slot origin. ButtonDpadDown, nested inside the ButtonDpad
        // alternative, picks up its parent's slot-derived origin.
        var t = Load(ScenarioXml);

        var byName = AllInputs(t.Layout.Elements).ToDictionary(i => i.Name);

        byName["ButtonA"].InputImages.Single().Y.ShouldBe(200);
        byName["ButtonB"].InputImages.Single().Y.ShouldBe(240);
        byName["ButtonDpadUp"].InputImages.Single().Y.ShouldBe(280);
        byName["ButtonDpad"].InputImages.Single().Y.ShouldBe(280);
        byName["ButtonDpadDown"].InputImages.Single().Y.ShouldBe(280);
    }

    private static IEnumerable<InputDefinition> AllInputs(IEnumerable<ILayoutElement> elements)
    {
        foreach (ILayoutElement element in elements)
        {
            switch (element)
            {
                case InputDefinition input:
                    yield return input;
                    foreach (InputDefinition d in AllInputs(input.Children)) yield return d;
                    break;
                case Container group:
                    foreach (InputDefinition d in AllInputs(group.Children)) yield return d;
                    break;
                case OneOf oneOf:
                    foreach (InputDefinition d in AllInputs(oneOf.Alternatives)) yield return d;
                    break;
                default:
                    break;
            }
        }
    }

    // ---- caching ----

    [Fact]
    public void Load_SameName_ReturnsCachedInstance()
    {
        // TemplateService caches Templates so repeated calls for the same name don't re-parse.
        var (service, _) = Build("<ControllerTemplate><Body /></ControllerTemplate>");

        Template first = service.Load(TemplateName);
        Template second = service.Load(TemplateName);

        second.ShouldBeSameAs(first);
    }

    [Fact]
    public void Load_NoLayoutXml_ReturnsTemplateWithEmptyLayout()
    {
        // When no Layout.xml exists the service returns a valid Template (not null), with an
        // empty element list — consumers never need to special-case a missing template.
        var service = TemplateFactory.Create(
            RootDir,
            logger: new NullLogger(),
            fs: new MockDynamicControlsFilesystem(RootDir).Fs,
            imageResolver: new FakeTemplateImageResolver());

        Template t = service.Load(TemplateName);

        t.ShouldNotBeNull();
        t.Layout.Elements.ShouldBeEmpty();
        t.BaseImage.ShouldBeNull();
    }
}

/// <summary>
/// Programmable <see cref="ITemplateImageResolver"/> that returns pre-registered paths by
/// (templateName, src) without touching disk. Unregistered lookups return src as the generic
/// path with no styled path.
/// </summary>
internal sealed class FakeTemplateImageResolver : ITemplateImageResolver
{
    private readonly Dictionary<(string Template, string Src), ResolvedImagePaths> _stubs = [];
    private readonly Dictionary<string, BaseImage> _baseImages = [];

    public void Stub(string templateName, string src, string generic, string? styled = null) =>
        _stubs[(templateName, src)] = new ResolvedImagePaths(generic, styled);

    public void StubBaseImage(string templateName, BaseImage baseImage) =>
        _baseImages[templateName] = baseImage;

    public BaseImage? FindBaseImage(string templateName) =>
        _baseImages.TryGetValue(templateName, out BaseImage? b) ? b : null;

    public ResolvedImagePaths ResolveImagePath(
        string templateName, string src, string? platform, string? controller = null) =>
        _stubs.TryGetValue((templateName, src), out ResolvedImagePaths? r) ? r : new ResolvedImagePaths(src, null);
}
