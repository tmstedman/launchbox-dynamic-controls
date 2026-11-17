using DynamicControls.Composition;
using DynamicControls.InputMapping;
using DynamicControls.Labels;
using DynamicControls.Rendering;
using DynamicControls.Templates;
using static DynamicControls.Core.TestHelpers.Labels.LabelsFixtures;
using static DynamicControls.Core.TestHelpers.InputMapping.MappingFixtures;

namespace DynamicControls.Core.IntegrationTests.Subsystem;

/// <summary>
/// Verifies when the Xbox Series X template draws the Back and Start lines, with the real
/// template and rendering wiring intact and the shipped <c>Layout.xml</c> as the template, read
/// from the real file rather than the older copy under <c>Fixtures/</c>. Only image lookup is
/// faked, so each case is a mapping and a set of labels, not artwork. It is a subsystem test that
/// happens to read a shipped file, not a shipped-data test: those run no pipeline.
/// </summary>
public class XboxLayoutMetaButtonsSubsystemTests
{
    private const string TemplateName = "Xbox Series X";
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "Shipped");

    private static readonly Template Template =
        TemplateFactory.Create(Root, new NullLogger(), imageResolver: new FakeTemplateImageResolver()).Load(TemplateName);

    private static RenderResult Render(ResolvedLabels labels)
    {
        // a platform default maps Start and Back both, as every shipped platform with a Select does
        var buttons = new Dictionary<string, IReadOnlyList<string>> { ["Start"] = ["ButtonStart"], ["Select"] = ["ButtonBack"] };
        var inputs = new Dictionary<string, string> { ["ButtonStart"] = "Start", ["ButtonBack"] = "Select" };
        ResolvedMapping mapping = MappingOf(
            buttonToInput: buttons, inputToButton: inputs, naturalButtonToInput: buttons, naturalInputToButton: inputs);

        return InputRenderingFactory.Create(new NullLogger()).Render(Template, mapping, labels);
    }

    private static bool Draws(RenderResult result, string src) => result.Images.Any(i => i.Source == src);

    [Fact]
    public void PlatformDefaultLabellingOnlyStart_DrawsStartsLine_AndNotBacks()
    {
        // Back is mapped, so under the platform default's showIf="auto" its icon is lit; with no
        // label of its own it has nothing for a line to run to
        RenderResult result = Render(LabelsOf(isGameSpecific: false, ("ButtonStart", "Pause")));

        Draws(result, "Line_ButtonStart.png").ShouldBeTrue();
        Draws(result, "Line_ButtonBack.png").ShouldBeFalse();
        Draws(result, "Line_MetaButtons.png").ShouldBeTrue();
    }

    [Fact]
    public void PlatformDefaultLabellingOnlyBack_DrawsBacksLine_AndNotStarts()
    {
        RenderResult result = Render(LabelsOf(isGameSpecific: false, ("ButtonBack", "Map")));

        Draws(result, "Line_ButtonBack.png").ShouldBeTrue();
        Draws(result, "Line_ButtonStart.png").ShouldBeFalse();
        Draws(result, "Line_MetaButtons.png").ShouldBeTrue();
    }

    [Fact]
    public void LabellingBothButtons_DrawsBothLines()
    {
        RenderResult result = Render(LabelsOf(isGameSpecific: true, ("ButtonStart", "Pause"), ("ButtonBack", "Map")));

        Draws(result, "Line_ButtonStart.png").ShouldBeTrue();
        Draws(result, "Line_ButtonBack.png").ShouldBeTrue();
        Draws(result, "Line_MetaButtons.png").ShouldBeTrue();
    }

    [Fact]
    public void LabellingNeitherButton_DrawsNoLineForThem()
    {
        RenderResult result = Render(LabelsOf(isGameSpecific: true, ("ButtonA", "Jump")));

        Draws(result, "Line_ButtonStart.png").ShouldBeFalse();
        Draws(result, "Line_ButtonBack.png").ShouldBeFalse();
        Draws(result, "Line_MetaButtons.png").ShouldBeFalse();
    }
}
