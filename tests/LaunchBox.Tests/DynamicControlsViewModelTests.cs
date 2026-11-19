using System.ComponentModel;
using DynamicControls.Rendering;

namespace DynamicControls.LaunchBox.Tests;

public class DynamicControlsViewModelTests
{
    private readonly DynamicControlsViewModel _viewModel = new();

    [Fact]
    public void Defaults_MatchRenderingDefaults()
    {
        // given a freshly constructed view model

        // when reading its defaults
        // then canvas dimensions match RenderingDefaults, collections are empty and no set is shown
        _viewModel.ControlsImagePath.ShouldBeNull();
        _viewModel.CanvasWidth.ShouldBe(RenderingDefaults.CanvasWidth);
        _viewModel.CanvasHeight.ShouldBe(RenderingDefaults.CanvasHeight);
        _viewModel.InputLabels.ShouldBeEmpty();
        _viewModel.RenderedImages.ShouldBeEmpty();
        _viewModel.HasControlSets.ShouldBeFalse();
        _viewModel.CanStep.ShouldBeFalse();
        _viewModel.SetName.ShouldBeNull();
        _viewModel.SetPosition.ShouldBeNull();
    }

    [Fact]
    public void Instance_ReturnsSameSingleton()
    {
        // given the singleton accessor

        // when read twice
        var first = DynamicControlsViewModel.Instance;
        var second = DynamicControlsViewModel.Instance;

        // then both references point to the same instance
        second.ShouldBeSameAs(first);
    }

    [Fact]
    public void Show_GameWithoutSets_ShowsItsOverlayAndNoSet()
    {
        // given an overlay with no control sets
        var overlay = Page(@"C:\overlay.png", width: 1920, height: 1080);

        // when it is shown
        _viewModel.Show(overlay);

        // then every bound field is the overlay's, and nothing says there are sets
        ShouldBeShowing(overlay);
        _viewModel.HasControlSets.ShouldBeFalse();
        _viewModel.SetName.ShouldBeNull();
        _viewModel.SetPosition.ShouldBeNull();
    }

    [Fact]
    public void Show_RaisesPropertyChangedForEachFieldItChanges()
    {
        // given a subscribed PropertyChanged handler
        var changes = CapturePropertyChanges(_viewModel);

        // when an overlay differing from the defaults in every overlay field is shown
        _viewModel.Show(Page(@"C:\overlay.png", width: 1920, height: 1080));

        // then each of those fields raised exactly one change, and the set fields, still empty, none
        changes.ShouldBe(
        [
            nameof(DynamicControlsViewModel.ControlsImagePath),
            nameof(DynamicControlsViewModel.CanvasWidth),
            nameof(DynamicControlsViewModel.CanvasHeight),
            nameof(DynamicControlsViewModel.InputLabels),
            nameof(DynamicControlsViewModel.RenderedImages),
        ], ignoreOrder: true);
    }

    [Fact]
    public void Show_SameValuesAgain_DoesNotRaisePropertyChanged()
    {
        // given an overlay already shown
        var overlay = Page(@"C:\overlay.png");
        _viewModel.Show(overlay);
        var changes = CapturePropertyChanges(_viewModel);

        // when it is shown again
        _viewModel.Show(overlay);

        // then PropertyChanged does not fire — SetProperty short-circuits on equal values
        changes.ShouldBeEmpty();
    }

    [Fact]
    public void Show_GameWithSets_StartsOnTheFirstSet_AndNeverShowsTheDefault()
    {
        // given an overlay with two control sets
        var lightPlane = Page(@"C:\pad.png", "Light Plane");
        var overlay = WithSets(Page(@"C:\pad.png"), lightPlane, Page(@"C:\pad.png", "Rocket Belt"));

        // when it is shown
        _viewModel.Show(overlay);

        // then the first set is on screen as the first of two, and the default's own labels are not
        ShouldBeShowing(lightPlane);
        _viewModel.InputLabels.ShouldNotBeSameAs(overlay.InputLabels);
        _viewModel.HasControlSets.ShouldBeTrue();
        _viewModel.CanStep.ShouldBeTrue();
        _viewModel.SetName.ShouldBe("Light Plane");
        _viewModel.SetPosition.ShouldBe("1 / 2");
    }

    [Fact]
    public void StepSet_Forward_ShowsEachSetInOrder_ThenWrapsToTheFirst()
    {
        // given a shown overlay with three control sets
        var lightPlane = Page(@"C:\pad.png", "Light Plane");
        var rocketBelt = Page(@"C:\pad.png", "Rocket Belt");
        var helicopter = Page(@"C:\pad.png", "Helicopter");
        _viewModel.Show(WithSets(Page(@"C:\pad.png"), lightPlane, rocketBelt, helicopter));

        // when stepped forward three times
        // then each set is shown in order with its name and position, and the third step wraps
        _viewModel.StepSet(1).ShouldBeTrue();
        ShouldBeShowing(rocketBelt);
        _viewModel.SetName.ShouldBe("Rocket Belt");
        _viewModel.SetPosition.ShouldBe("2 / 3");

        _viewModel.StepSet(1).ShouldBeTrue();
        ShouldBeShowing(helicopter);
        _viewModel.SetName.ShouldBe("Helicopter");
        _viewModel.SetPosition.ShouldBe("3 / 3");

        _viewModel.StepSet(1).ShouldBeTrue();
        ShouldBeShowing(lightPlane);
        _viewModel.SetName.ShouldBe("Light Plane");
        _viewModel.SetPosition.ShouldBe("1 / 3");
    }

    [Fact]
    public void StepSet_BackFromTheFirstSet_WrapsToTheLastSet()
    {
        // given a shown overlay with two control sets, on its first
        var rocketBelt = Page(@"C:\pad.png", "Rocket Belt");
        _viewModel.Show(WithSets(Page(@"C:\pad.png"), Page(@"C:\pad.png", "Light Plane"), rocketBelt));

        // when stepped back
        _viewModel.StepSet(-1).ShouldBeTrue();

        // then the last set is on screen
        ShouldBeShowing(rocketBelt);
        _viewModel.SetName.ShouldBe("Rocket Belt");
        _viewModel.SetPosition.ShouldBe("2 / 2");
    }

    [Fact]
    public void StepSet_ToASetWithItsOwnCanvas_AnnouncesEveryFieldItChanges()
    {
        // given a second set whose base image and canvas differ from the first's
        var set = Page(@"C:\other.png", "Helicopter", width: 640, height: 480);
        _viewModel.Show(WithSets(
            Page(@"C:\pad.png"), Page(@"C:\pad.png", "Light Plane", width: 1920, height: 1080), set));
        var changes = CapturePropertyChanges(_viewModel);

        // when stepped to it
        _viewModel.StepSet(1);

        // then every field it changes is announced, so the theme redraws it whole
        ShouldBeShowing(set);
        changes.ShouldBe(
        [
            nameof(DynamicControlsViewModel.ControlsImagePath),
            nameof(DynamicControlsViewModel.CanvasWidth),
            nameof(DynamicControlsViewModel.CanvasHeight),
            nameof(DynamicControlsViewModel.InputLabels),
            nameof(DynamicControlsViewModel.RenderedImages),
            nameof(DynamicControlsViewModel.SetName),
            nameof(DynamicControlsViewModel.SetPosition),
        ], ignoreOrder: true);
    }

    [Fact]
    public void Show_GameWithOneSet_ShowsItAndItsName_WithNothingToStepTo()
    {
        // given a game with one control set
        var lightPlane = Page(@"C:\pad.png", "Light Plane");
        _viewModel.Show(WithSets(Page(@"C:\pad.png"), lightPlane));
        var changes = CapturePropertyChanges(_viewModel);

        // when stepped either way
        // then the set and its name are on screen, with no position, and stepping changes nothing
        ShouldBeShowing(lightPlane);
        _viewModel.HasControlSets.ShouldBeTrue();
        _viewModel.SetName.ShouldBe("Light Plane");
        _viewModel.CanStep.ShouldBeFalse();
        _viewModel.SetPosition.ShouldBeNull();
        _viewModel.StepSet(1).ShouldBeFalse();
        _viewModel.StepSet(-1).ShouldBeFalse();
        changes.ShouldBeEmpty();
    }

    [Fact]
    public void StepSet_GameWithoutSets_ChangesNothing()
    {
        // given a shown overlay with no control sets
        var overlay = Page(@"C:\overlay.png");
        _viewModel.Show(overlay);
        var changes = CapturePropertyChanges(_viewModel);

        // when stepped either way
        // then there is nothing to step to: it says so and the overlay stays
        _viewModel.StepSet(1).ShouldBeFalse();
        _viewModel.StepSet(-1).ShouldBeFalse();
        ShouldBeShowing(overlay);
        changes.ShouldBeEmpty();
    }

    [Fact]
    public void Show_AfterSteppingToASet_StartsTheNextGameOnItsFirstSet()
    {
        // given a game left on its third set
        _viewModel.Show(WithSets(
            Page(@"C:\pad.png"), Page(@"C:\pad.png", "Light Plane"), Page(@"C:\pad.png", "Rocket Belt"), Page(@"C:\pad.png", "Helicopter")));
        _viewModel.StepSet(2);

        // when the next launch shows another game with sets
        var map = Page(@"C:\other.png", "Map");
        _viewModel.Show(WithSets(Page(@"C:\other.png"), map, Page(@"C:\other.png", "Radar")));

        // then that game starts on its own first set, counted against its own sets
        ShouldBeShowing(map);
        _viewModel.SetName.ShouldBe("Map");
        _viewModel.SetPosition.ShouldBe("1 / 2");
    }

    [Fact]
    public void Show_GameWithoutSets_AfterOneWithSets_ShowsNoSet()
    {
        // given a game with sets, stepped to one of them
        _viewModel.Show(WithSets(Page(@"C:\pad.png"), Page(@"C:\pad.png", "Light Plane"), Page(@"C:\pad.png", "Rocket Belt")));
        _viewModel.StepSet(1);

        // when the next launch shows a game without sets
        var next = Page(@"C:\other.png");
        _viewModel.Show(next);

        // then nothing about the earlier game's sets is left on screen
        ShouldBeShowing(next);
        _viewModel.HasControlSets.ShouldBeFalse();
        _viewModel.CanStep.ShouldBeFalse();
        _viewModel.SetName.ShouldBeNull();
        _viewModel.SetPosition.ShouldBeNull();
    }

    [Fact]
    public void Show_ASetThatDrewNothing_GetsNoPage()
    {
        // given three control sets, the second of which failed to draw and was left empty
        var lightPlane = Page(@"C:\pad.png", "Light Plane");
        var rocketBelt = Page(@"C:\pad.png", "Rocket Belt");
        _viewModel.Show(WithSets(Page(@"C:\pad.png"), lightPlane, Empty("Hang Glider"), rocketBelt));

        // when stepped forward from the first
        _viewModel.SetPosition.ShouldBe("1 / 2");
        _viewModel.StepSet(1);

        // then the next set that drew is on screen, the empty one passed over
        ShouldBeShowing(rocketBelt);
        _viewModel.SetName.ShouldBe("Rocket Belt");
        _viewModel.SetPosition.ShouldBe("2 / 2");
    }

    [Fact]
    public void Show_EverySetDrewNothing_ShowsTheDefaultAsForAGameWithoutSets()
    {
        // given a game whose default drew, but whose every control set failed to and was left empty
        var overlay = WithSets(Page(@"C:\pad.png"), Empty("Light Plane"), Empty("Rocket Belt"));

        // when it is shown
        _viewModel.Show(overlay);

        // then its default is on screen and nothing says there are sets to step through
        ShouldBeShowing(overlay);
        _viewModel.HasControlSets.ShouldBeFalse();
        _viewModel.SetName.ShouldBeNull();
        _viewModel.SetPosition.ShouldBeNull();
        _viewModel.StepSet(1).ShouldBeFalse();
    }

    private void ShouldBeShowing(ControllerOverlayModel page)
    {
        _viewModel.ControlsImagePath.ShouldBe(page.ImagePath);
        _viewModel.CanvasWidth.ShouldBe(page.CanvasWidth);
        _viewModel.CanvasHeight.ShouldBe(page.CanvasHeight);
        _viewModel.InputLabels.ShouldBeSameAs(page.InputLabels);
        _viewModel.RenderedImages.ShouldBeSameAs(page.RenderedImages);
    }

    /// <summary>An overlay with label and image lists of its own, so which page is on screen can
    /// be told by reference, and a label on it, as anything drawn has.</summary>
    private static ControllerOverlayModel Page(
        string imagePath, string? setName = null, double width = 800, double height = 600)
    {
        return new()
        {
            ImagePath = imagePath,
            CanvasWidth = width,
            CanvasHeight = height,
            InputLabels = [new RenderedLabel(Left: 0, Top: 0, Text: setName ?? "Default", Alignment: "Left", FontSize: 24)],
            RenderedImages = [],
            SetName = setName,
        };
    }

    /// <summary>A control set that failed to draw: its frame and name, with nothing on it.</summary>
    private static ControllerOverlayModel Empty(string setName) => new() { ImagePath = @"C:\pad.png", SetName = setName };

    private static ControllerOverlayModel WithSets(ControllerOverlayModel overlay, params ControllerOverlayModel[] sets) =>
        overlay with { ControlSets = [.. sets] };

    private static List<string?> CapturePropertyChanges(INotifyPropertyChanged source)
    {
        List<string?> changes = [];
        source.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        return changes;
    }
}
