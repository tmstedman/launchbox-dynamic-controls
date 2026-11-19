using DynamicControls.Rendering;

namespace DynamicControls.Core.Tests;

/// <summary>
/// Unit tests for <see cref="ControllerOverlayModel"/>: which of a game's control sets count as
/// drawn, the one definition the pause screen's pages and Preview's files both follow.
/// </summary>
public class ControllerOverlayModelTests
{
    private static ControllerOverlayModel WithLabel(string setName) => new()
    {
        SetName = setName,
        InputLabels = [new RenderedLabel(Left: 0, Top: 0, Text: "Jump", Alignment: "Left", FontSize: 24)],
    };

    private static ControllerOverlayModel WithImage(string setName) => new()
    {
        SetName = setName,
        RenderedImages = [new RenderedImage(Source: "A.png")],
    };

    [Fact]
    public void IsBlank_WithNeitherALabelNorAnImage_IsTrue_AndWithEitherIsFalse()
    {
        // given an overlay with nothing on it, one with a label and one with an image
        // when asked whether each is blank
        // then only the first is: either a label or an image counts as drawn
        new ControllerOverlayModel().IsBlank.ShouldBeTrue();
        WithLabel("A").IsBlank.ShouldBeFalse();
        WithImage("B").IsBlank.ShouldBeFalse();
    }

    [Fact]
    public void DrawnControlSets_LeavesOutASetThatDrewNothing_KeepingTheRestInOrder()
    {
        // given a game whose second set failed to draw and was left blank under its name
        ControllerOverlayModel first = WithLabel("Light Plane");
        ControllerOverlayModel failed = new() { SetName = "Sky Diving" };
        ControllerOverlayModel third = WithImage("Rocket Belt");
        var overlay = new ControllerOverlayModel { ControlSets = [first, failed, third] };

        // when asked which sets drew
        IReadOnlyList<ControllerOverlayModel> drawn = overlay.DrawnControlSets;

        // then the blank one is not among them, and the others keep their order
        drawn.ShouldBe([first, third]);
    }

    [Fact]
    public void DrawnControlSets_OfAGameWithoutSets_IsEmpty()
    {
        // given a game without sets
        // when asked which sets drew
        // then there are none
        new ControllerOverlayModel().DrawnControlSets.ShouldBeEmpty();
    }
}
