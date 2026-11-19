using System.ComponentModel;
using DynamicControls;
using DynamicControls.Rendering;

namespace DynamicControls.LaunchBox;

public interface IDynamicControlsViewModel
{
    /// <summary>Shows a launched game's overlay: its first control set if it has any, else the overlay itself.</summary>
    void Show(ControllerOverlayModel overlay);
}

/// <summary>
/// Singleton view model bound to the pause theme XAML. Exposes the controller image path,
/// canvas dimensions, and positioned input labels for WPF data binding.
/// </summary>
/// <remarks>
/// A game with control sets is a series of pages, one per set in the order its labels declare
/// them, and its default overlay is not shown: the sets are what it says about the game.
/// <see cref="StepSet"/> turns them, wrapping past either end, and every bound field follows the
/// page on screen. A game without sets is one page, its overlay, and stepping does nothing. A set
/// that drew nothing (one that failed to draw is left empty) gets no page, and a game none of
/// whose sets drew anything shows its overlay as if it had none. One set is one page: it shows
/// its name, but there is nothing to step to.
/// </remarks>
public class DynamicControlsViewModel : INotifyPropertyChanged, IDynamicControlsViewModel
{
    public static DynamicControlsViewModel Instance { get; } = new DynamicControlsViewModel();

    internal DynamicControlsViewModel() { }

    private List<ControllerOverlayModel> _pages = [];
    private int _page;

    private string? _controlsImagePath;
    public string? ControlsImagePath
    {
        get => _controlsImagePath;
        private set => SetProperty(ref _controlsImagePath, value);
    }

    private List<RenderedLabel> _inputLabels = [];
    public List<RenderedLabel> InputLabels
    {
        get => _inputLabels;
        private set => SetProperty(ref _inputLabels, value);
    }

    private double _canvasWidth = RenderingDefaults.CanvasWidth;
    public double CanvasWidth
    {
        get => _canvasWidth;
        private set => SetProperty(ref _canvasWidth, value);
    }

    private double _canvasHeight = RenderingDefaults.CanvasHeight;
    public double CanvasHeight
    {
        get => _canvasHeight;
        private set => SetProperty(ref _canvasHeight, value);
    }

    private List<RenderedImage> _overlayImages = [];
    public List<RenderedImage> RenderedImages
    {
        get => _overlayImages;
        private set => SetProperty(ref _overlayImages, value);
    }

    private bool _hasControlSets;
    /// <summary>Whether the game has a control set on screen, which is when the theme shows
    /// <see cref="SetName"/>.</summary>
    public bool HasControlSets
    {
        get => _hasControlSets;
        private set => SetProperty(ref _hasControlSets, value);
    }

    private bool _canStep;
    /// <summary>Whether there is another set to step to, which is when the theme shows
    /// <see cref="SetPosition"/> and the arrows, and when the pause screen reads input at all; a
    /// game with one set has nowhere to turn.</summary>
    public bool CanStep
    {
        get => _canStep;
        private set => SetProperty(ref _canStep, value);
    }

    private string? _setName;
    /// <summary>The name of the control set on screen, or null for a game without sets.</summary>
    public string? SetName
    {
        get => _setName;
        private set => SetProperty(ref _setName, value);
    }

    private string? _setPosition;
    /// <summary>Which control set is on screen ("3 / 5"), or null unless <see cref="CanStep"/>.</summary>
    public string? SetPosition
    {
        get => _setPosition;
        private set => SetProperty(ref _setPosition, value);
    }

    public void Show(ControllerOverlayModel overlay)
    {
        List<ControllerOverlayModel> sets = [.. overlay.DrawnControlSets];
        _pages = sets.Count > 0 ? sets : [overlay];
        _page = 0;
        HasControlSets = sets.Count > 0;
        CanStep = sets.Count > 1;
        ShowPage();
    }

    /// <summary>
    /// Turns <paramref name="by"/> pages, forward when positive, wrapping past either end.
    /// Returns false, changing nothing, when there is no other set to step to.
    /// </summary>
    public bool StepSet(int by)
    {
        if (!CanStep) return false;
        _page = (((_page + by) % _pages.Count) + _pages.Count) % _pages.Count;
        ShowPage();
        return true;
    }

    private void ShowPage()
    {
        ControllerOverlayModel page = _pages[_page];
        ControlsImagePath = page.ImagePath;
        CanvasWidth = page.CanvasWidth;
        CanvasHeight = page.CanvasHeight;
        InputLabels = page.InputLabels;
        RenderedImages = page.RenderedImages;
        SetName = page.SetName;
        SetPosition = CanStep ? $"{_page + 1} / {_pages.Count}" : null;
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
