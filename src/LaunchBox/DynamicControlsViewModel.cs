using System.ComponentModel;
using DynamicControls;
using DynamicControls.Rendering;

namespace DynamicControls.LaunchBox;

public interface IDynamicControlsViewModel
{
    string? ControlsImagePath { get; set; }
    double CanvasWidth { get; set; }
    double CanvasHeight { get; set; }
    List<RenderedLabel> InputLabels { get; set; }
    List<RenderedImage> RenderedImages { get; set; }
}

/// <summary>
/// Singleton view model bound to the pause theme XAML. Exposes the controller image path,
/// canvas dimensions, and positioned input labels for WPF data binding.
/// </summary>
public class DynamicControlsViewModel : INotifyPropertyChanged, IDynamicControlsViewModel
{
    public static DynamicControlsViewModel Instance { get; } = new DynamicControlsViewModel();

    internal DynamicControlsViewModel() { }

    private string? _controlsImagePath;
    public string? ControlsImagePath
    {
        get => _controlsImagePath;
        set => SetProperty(ref _controlsImagePath, value);
    }

    private List<RenderedLabel> _inputLabels = [];
    public List<RenderedLabel> InputLabels
    {
        get => _inputLabels;
        set => SetProperty(ref _inputLabels, value);
    }

    private double _canvasWidth = RenderingDefaults.CanvasWidth;
    public double CanvasWidth
    {
        get => _canvasWidth;
        set => SetProperty(ref _canvasWidth, value);
    }

    private double _canvasHeight = RenderingDefaults.CanvasHeight;
    public double CanvasHeight
    {
        get => _canvasHeight;
        set => SetProperty(ref _canvasHeight, value);
    }

    private List<RenderedImage> _overlayImages = [];
    public List<RenderedImage> RenderedImages
    {
        get => _overlayImages;
        set => SetProperty(ref _overlayImages, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetProperty<T>(
        ref T field,
        T value,
        [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
