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
                <Input name="ButtonA" style="dim" inactiveBlurRadius="4" x="0" y="0">
                  <Render width="64" height="64" />
                </Input>
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
    public void Load_AbsoluteInputCoords_RenderPositionIsAbsolute()
    {
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Input name="ButtonA" x="100" y="200">
                  <Render x="+10" y="+20" width="64" height="64" />
                </Input>
              </Body>
            </ControllerTemplate>
            """);

        var render = t.Layout.Elements.OfType<InputDefinition>().Single().InputImages.Single();
        render.X.ShouldBe(110);
        render.Y.ShouldBe(220);
    }

    [Fact]
    public void Load_RelativeRenderCoords_AddedToInputOrigin()
    {
        // Renders declared with +/- coords are relative to their Input's origin.
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Input name="ButtonA" x="50" y="60">
                  <Render x="+5" y="-10" width="44" height="44" />
                </Input>
              </Body>
            </ControllerTemplate>
            """);

        var render = t.Layout.Elements.OfType<InputDefinition>().Single().InputImages.Single();
        render.X.ShouldBe(55);
        render.Y.ShouldBe(50);
    }

    [Fact]
    public void Load_StackChildren_PositionedFromStackOriginWithGap()
    {
        // A Stack at (10,100) with gap=45 places its first child at y=100 and second at y=145.
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Group>
                  <Stack x="10" y="100" gap="45">
                    <Input name="ButtonA">
                      <Render width="34" height="34" />
                    </Input>
                    <Input name="ButtonB">
                      <Render width="34" height="34" />
                    </Input>
                  </Stack>
                </Group>
              </Body>
            </ControllerTemplate>
            """);

        var group = t.Layout.Elements.OfType<InputGroup>().Single();
        var stack = group.Children.OfType<InputGroup>().Single();
        var inputs = stack.Children.OfType<InputDefinition>().ToList();

        inputs[0].InputImages.Single().Y.ShouldBe(100);
        inputs[1].InputImages.Single().Y.ShouldBe(145);
    }

    [Fact]
    public void Load_NestedInput_InheritsParentOriginNotStackOrigin()
    {
        // Children of an Input start a new coord context from that Input's origin — they do NOT
        // continue the Stack's cumulative offset.
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Input name="AxisLeftStick" x="500" y="300">
                  <Render width="124" height="124" />
                  <Input name="AxisLeftStickUp">
                    <Render x="+5" y="+10" width="34" height="34" />
                  </Input>
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
    public void Load_Render_ImageFileIsInputNameDotPng()
    {
        // When a <Render> carries no useImage attribute, ImageFile is set to the Input's
        // name + ".png". Path resolution is deferred to render time (InputImageResolver)
        // so ImageFile is a plain filename, not a resolved absolute path.
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Input name="ButtonA" x="0" y="0">
                  <Render width="64" height="64" />
                </Input>
              </Body>
            </ControllerTemplate>
            """);

        t.Layout.Elements.OfType<InputDefinition>().Single()
            .InputImages.Single().ImageFile.ShouldBe("ButtonA.png");
    }

    [Fact]
    public void Load_RenderWithUseImage_BothImageFileAndUseImageFileAreUseImageDotPng()
    {
        // A <Render useImage="ButtonDpadUp"> borrows another input's image. Both ImageFile
        // and UseImageFile carry the borrowed filename so the renderer can distinguish a
        // "borrowing" render from an "identity" render. Path resolution still happens at
        // render time, not here.
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Input name="ButtonDpad" x="0" y="0">
                  <Render useImage="ButtonDpadUp" width="34" height="34" />
                </Input>
              </Body>
            </ControllerTemplate>
            """);

        var render = t.Layout.Elements.OfType<InputDefinition>().Single().InputImages.Single();
        render.ImageFile.ShouldBe("ButtonDpad.png");     // always the owning Input's name
        render.UseImageFile.ShouldBe("ButtonDpadUp.png"); // the borrowed asset
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
                <Input name="ButtonA" x="0" y="0">
                  <Overlay src="LineA.png" x="+50" y="+10" />
                  <Render width="64" height="64" />
                </Input>
              </Body>
            </ControllerTemplate>
            """);
        images.Stub(TemplateName, "LineA.png", generic: "/tmpl/LineA.png");
        images.Stub(TemplateName, "ButtonA.png", generic: "/tmpl/ButtonA.png");

        var t = service.Load(TemplateName);

        t.Layout.Elements.OfType<InputDefinition>().Single()
            .Overlays.Single().Source.ShouldBe("/tmpl/LineA.png");
    }

    // ---- structural elements ----

    [Fact]
    public void Load_Group_AlwaysIncludeFalse_ChildrenCollected()
    {
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Group>
                  <Input name="ButtonA" x="0" y="0"><Render width="44" height="44" /></Input>
                  <Input name="ButtonB" x="0" y="50"><Render width="44" height="44" /></Input>
                </Group>
              </Body>
            </ControllerTemplate>
            """);

        var group = t.Layout.Elements.OfType<InputGroup>().Single();
        group.AlwaysInclude.ShouldBeFalse();
        group.Children.OfType<InputDefinition>().Select(i => i.Name).ShouldBe(["ButtonA", "ButtonB"]);
    }

    [Fact]
    public void Load_Stack_AlwaysIncludeTrue()
    {
        // A <Stack> resolves to an InputGroup with AlwaysInclude=true.
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Stack x="0" y="0" gap="10">
                  <Input name="ButtonA"><Render width="34" height="34" /></Input>
                </Stack>
              </Body>
            </ControllerTemplate>
            """);

        // A body-level Stack lands directly as the sole InputGroup in Elements (no wrapper).
        var stack = t.Layout.Elements.OfType<InputGroup>().Single();
        stack.AlwaysInclude.ShouldBeTrue();
    }

    [Fact]
    public void Load_OneOf_AlternativesPreservedInOrder()
    {
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Input name="ButtonDpad" x="0" y="0">
                  <Render width="135" height="135" />
                  <OneOf>
                    <Group>
                      <Input name="ButtonDpadUp"><Render width="34" height="34" /></Input>
                    </Group>
                    <Input name="ButtonDpad" x="0" y="0">
                      <Render useImage="ButtonDpadUp" width="34" height="34" />
                    </Input>
                  </OneOf>
                </Input>
              </Body>
            </ControllerTemplate>
            """);

        var dpad = t.Layout.Elements.OfType<InputDefinition>().Single();
        var oneOf = dpad.Children.OfType<OneOf>().Single();
        oneOf.Alternatives.Count.ShouldBe(2);
        oneOf.Alternatives[0].ShouldBeOfType<InputGroup>();
        oneOf.Alternatives[1].ShouldBeOfType<InputDefinition>();
    }

    [Fact]
    public void Load_OneOfInsideStack_ConsumesOneSlot()
    {
        // OneOf inside a Stack consumes a single slot — all alternatives share that slot's
        // origin. The Input after the OneOf advances by exactly one gap. This is the canonical
        // directional-Dpad authoring pattern (per CLAUDE.md): per-direction labels vs. a single
        // whole-input render expressed as OneOf alternatives at one stack position.
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Stack x="0" y="100" gap="50">
                  <OneOf>
                    <Input name="ButtonDpadUp"><Render width="34" height="34" /></Input>
                    <Input name="ButtonDpad"><Render width="34" height="34" /></Input>
                  </OneOf>
                  <Input name="ButtonStart"><Render width="34" height="34" /></Input>
                </Stack>
              </Body>
            </ControllerTemplate>
            """);

        var stack = t.Layout.Elements.OfType<InputGroup>().Single();
        var oneOf = stack.Children.OfType<OneOf>().Single();
        var altAbsoluteYs = oneOf.Alternatives
            .OfType<InputDefinition>()
            .Select(a => a.InputImages.Single().Y);
        altAbsoluteYs.ShouldAllBe(y => y == 100);

        var trailing = stack.Children.OfType<InputDefinition>().Single(i => i.Name == "ButtonStart");
        trailing.InputImages.Single().Y.ShouldBe(150);
    }

    // ---- InputDescendants index ----

    [Fact]
    public void Load_InputDescendants_ContainsDirectAndTransitiveChildren()
    {
        // InputDescendants is computed once at load time and used by the rendering pipeline to
        // fan out visibility (an ancestor is visible if any descendant has a label/mapping).
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Input name="AxisLeftStick" x="0" y="0">
                  <Render width="124" height="124" />
                  <Input name="AxisLeftStickUp"><Render width="34" height="34" /></Input>
                  <Input name="AxisLeftStickDown"><Render width="34" height="34" /></Input>
                </Input>
              </Body>
            </ControllerTemplate>
            """);

        var stick = t.Layout.Elements.OfType<InputDefinition>().Single();
        var descendants = t.Layout.InputDescendants[stick];
        descendants.Select(d => d.Name).ShouldBe(["AxisLeftStickUp", "AxisLeftStickDown"], ignoreOrder: true);
    }

    [Fact]
    public void Load_DuplicateTopLevelInput_KeyedByReferenceInInputDescendants()
    {
        // The layout schema permits duplicate top-level <Input> entries — the directional pattern
        // uses one nested-children variant for per-direction labels and a separate "strict-self"
        // variant with no children. Both must survive as distinct InputDefinitions, and
        // InputDescendants — keyed by reference — must hold an entry for each instance.
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Input name="ButtonDpad" x="0" y="0">
                  <Render width="135" height="135" />
                  <Input name="ButtonDpadUp"><Render width="34" height="34" /></Input>
                </Input>
                <Input name="ButtonDpad" x="0" y="0">
                  <Render width="135" height="135" />
                </Input>
              </Body>
            </ControllerTemplate>
            """);

        var dpads = t.Layout.Elements.OfType<InputDefinition>()
            .Where(i => i.Name == "ButtonDpad")
            .ToList();
        dpads.Count.ShouldBe(2);

        InputDefinition withChild = dpads.Single(d => d.Children.Count > 0);
        InputDefinition strictSelf = dpads.Single(d => d.Children.Count == 0);
        t.Layout.InputDescendants[withChild].Select(d => d.Name).ShouldBe(["ButtonDpadUp"]);
        t.Layout.InputDescendants[strictSelf].ShouldBeEmpty();
    }

    [Fact]
    public void Load_CollapsingStack_CollapseInfoBuiltForChildren()
    {
        // A Stack with collapse="true" registers its children in CollapseInfo so the rendering
        // pipeline can vacate empty slots and shift subsequent entries up.
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Group>
                  <Stack x="0" y="0" gap="45" collapse="true">
                    <Input name="ButtonA"><Render width="34" height="34" /></Input>
                    <Input name="ButtonB"><Render width="34" height="34" /></Input>
                  </Stack>
                </Group>
              </Body>
            </ControllerTemplate>
            """);

        var stack = t.Layout.Elements.OfType<InputGroup>().Single()
            .Children.OfType<InputGroup>().Single();
        var inputs = stack.Children.OfType<InputDefinition>().ToList();

        t.Layout.CollapseInfo.Keys.ShouldContain(inputs[0], ReferenceEqualityComparer.Instance);
        t.Layout.CollapseInfo.Keys.ShouldContain(inputs[1], ReferenceEqualityComparer.Instance);
        t.Layout.CollapseInfo[inputs[0]].Gap.ShouldBe(45);
    }

    [Fact]
    public void Load_CollapsingStack_RegistersInputsNestedInPlainGroup()
    {
        // CollapseGroupBuilder treats plain Groups as transparent — Inputs reached through a
        // nested <Group> get their own slot and land in CollapseInfo. Verifies the wiring
        // between LayoutResolver (which marks the inner Group AlwaysInclude=false) and
        // CollapseGroupBuilder's slot walk.
        var t = Load("""
            <ControllerTemplate>
              <Body>
                <Stack x="0" y="0" gap="40" collapse="true">
                  <Input name="ButtonA"><Render width="34" height="34" /></Input>
                  <Group>
                    <Input name="ButtonB"><Render width="34" height="34" /></Input>
                    <Input name="ButtonC"><Render width="34" height="34" /></Input>
                  </Group>
                </Stack>
              </Body>
            </ControllerTemplate>
            """);

        var stack = t.Layout.Elements.OfType<InputGroup>().Single();
        var inner = stack.Children.OfType<InputGroup>().Single();
        InputDefinition a = stack.Children.OfType<InputDefinition>().Single();
        var nested = inner.Children.OfType<InputDefinition>().ToList();

        t.Layout.CollapseInfo.Keys.ShouldContain(a, ReferenceEqualityComparer.Instance);
        t.Layout.CollapseInfo.Keys.ShouldContain(nested[0], ReferenceEqualityComparer.Instance);
        t.Layout.CollapseInfo.Keys.ShouldContain(nested[1], ReferenceEqualityComparer.Instance);
        t.Layout.CollapseInfo[nested[1]].Gap.ShouldBe(40);
    }

    // ---- interaction scenarios ----
    // A handful of tests run against a single richer fixture that resembles a slice of a real
    // template. The goal is to probe behaviors that only emerge when multiple features compose
    // (named style + Stack slot + collapse + OneOf + nested Inputs all at once) — interaction
    // bugs that minimal single-feature stubs miss but that E2E tests can only catch by accident.
    // Keep this list small; if a new test fits a minimal stub, prefer that.

    private const string ScenarioXml = """
        <ControllerTemplate>
          <Head>
            <Style fontSize="20" />
            <Style name="dim" showIf="mapping" minOpacity="0.4" />
          </Head>
          <Body>
            <Stack x="100" y="200" gap="40" collapse="true">
              <Input name="ButtonA" style="dim">
                <Render width="34" height="34" />
                <Label x="+50" y="+5" />
              </Input>
              <Group>
                <Input name="ButtonB" style="dim">
                  <Render width="34" height="34" />
                </Input>
              </Group>
              <OneOf>
                <Input name="ButtonDpadUp"><Render width="34" height="34" /></Input>
                <Input name="ButtonDpad">
                  <Render width="34" height="34" />
                  <Input name="ButtonDpadDown"><Render width="34" height="34" /></Input>
                </Input>
              </OneOf>
            </Stack>
          </Body>
        </ControllerTemplate>
        """;

    [Fact]
    public void Scenario_StyleCascade_MeetsStackSlotPositioning()
    {
        // ButtonA sits in slot 0 of the Stack at (100, 200). Its render inherits minOpacity=0.4
        // and showIf=Mapped from the named "dim" style; its label inherits fontSize=20 from the
        // unnamed Head Style. All four cascades fire together.
        var t = Load(ScenarioXml);

        var stack = t.Layout.Elements.OfType<InputGroup>().Single();
        InputDefinition buttonA = stack.Children.OfType<InputDefinition>().Single(i => i.Name == "ButtonA");

        InputImageDefinition render = buttonA.InputImages.Single();
        render.Y.ShouldBe(200);
        render.MinOpacity.ShouldBe(0.4);
        render.ShowIf.ShouldBe(ShowIfCondition.Mapped);

        buttonA.Labels.Single().FontSize.ShouldBe(20);
    }

    [Fact]
    public void Scenario_CollapseInfo_SpansInputGroupAndOneOfSlots()
    {
        // CollapseGroupBuilder must register every Input reachable as a slot leaf: ButtonA
        // (direct child), ButtonB (through the transparent Group), and both OneOf alternatives.
        // ButtonDpadDown is nested inside the ButtonDpad alternative — it collapses with its
        // parent as a unit and does NOT get its own CollapseInfo entry.
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
                case InputGroup group:
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
