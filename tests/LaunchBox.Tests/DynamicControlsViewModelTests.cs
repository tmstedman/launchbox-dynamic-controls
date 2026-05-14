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
        // then canvas dimensions match RenderingDefaults and collections are empty
        _viewModel.ControlsImagePath.ShouldBeNull();
        _viewModel.CanvasWidth.ShouldBe(RenderingDefaults.CanvasWidth);
        _viewModel.CanvasHeight.ShouldBe(RenderingDefaults.CanvasHeight);
        _viewModel.InputLabels.ShouldBeEmpty();
        _viewModel.RenderedImages.ShouldBeEmpty();
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
    public void ControlsImagePath_Set_RaisesPropertyChangedAndUpdatesValue()
    {
        // given a subscribed PropertyChanged handler
        var changes = CapturePropertyChanges(_viewModel);

        // when the property is set
        _viewModel.ControlsImagePath = @"C:\overlay.png";

        // then the value is stored and a single PropertyChanged event fired with the property name
        _viewModel.ControlsImagePath.ShouldBe(@"C:\overlay.png");
        changes.ShouldBe([nameof(DynamicControlsViewModel.ControlsImagePath)]);
    }

    [Fact]
    public void CanvasWidth_Set_RaisesPropertyChangedAndUpdatesValue()
    {
        // given a subscribed PropertyChanged handler
        var changes = CapturePropertyChanges(_viewModel);

        // when the property is set
        _viewModel.CanvasWidth = 1920;

        // then the value is stored and a single PropertyChanged event fired with the property name
        _viewModel.CanvasWidth.ShouldBe(1920);
        changes.ShouldBe([nameof(DynamicControlsViewModel.CanvasWidth)]);
    }

    [Fact]
    public void CanvasHeight_Set_RaisesPropertyChangedAndUpdatesValue()
    {
        // given a subscribed PropertyChanged handler
        var changes = CapturePropertyChanges(_viewModel);

        // when the property is set
        _viewModel.CanvasHeight = 1080;

        // then the value is stored and a single PropertyChanged event fired with the property name
        _viewModel.CanvasHeight.ShouldBe(1080);
        changes.ShouldBe([nameof(DynamicControlsViewModel.CanvasHeight)]);
    }

    [Fact]
    public void InputLabels_Set_RaisesPropertyChangedAndUpdatesValue()
    {
        // given a subscribed PropertyChanged handler and a non-empty label list
        List<RenderedLabel> labels = [];
        var changes = CapturePropertyChanges(_viewModel);

        // when the property is set
        _viewModel.InputLabels = labels;

        // then the same reference is stored and a single PropertyChanged event fired with the property name
        _viewModel.InputLabels.ShouldBeSameAs(labels);
        changes.ShouldBe([nameof(DynamicControlsViewModel.InputLabels)]);
    }

    [Fact]
    public void RenderedImages_Set_RaisesPropertyChangedAndUpdatesValue()
    {
        // given a subscribed PropertyChanged handler and a non-empty image list
        List<RenderedImage> images = [];
        var changes = CapturePropertyChanges(_viewModel);

        // when the property is set
        _viewModel.RenderedImages = images;

        // then the same reference is stored and a single PropertyChanged event fired with the property name
        _viewModel.RenderedImages.ShouldBeSameAs(images);
        changes.ShouldBe([nameof(DynamicControlsViewModel.RenderedImages)]);
    }

    [Fact]
    public void SettingSameValue_DoesNotRaisePropertyChanged()
    {
        // given a property already set to a value
        _viewModel.ControlsImagePath = @"C:\overlay.png";
        var changes = CapturePropertyChanges(_viewModel);

        // when the same value is assigned again
        _viewModel.ControlsImagePath = @"C:\overlay.png";

        // then PropertyChanged does not fire — SetProperty short-circuits on equal values
        changes.ShouldBeEmpty();
    }

    private static List<string?> CapturePropertyChanges(INotifyPropertyChanged source)
    {
        List<string?> changes = [];
        source.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        return changes;
    }
}
