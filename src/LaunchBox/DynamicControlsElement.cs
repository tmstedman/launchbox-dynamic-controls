using System.Windows.Controls;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace DynamicControls.LaunchBox;

/// <summary>
/// Big Box theme element plugin. Exposes the DynamicControlsViewModel as the DataContext
/// so that pause theme XAML can bind to controller overlay data.
/// </summary>
public class DynamicControlsElement : UserControl, IBigBoxThemeElementPlugin
{
    public DynamicControlsElement()
    {
        DataContext = DynamicControlsViewModel.Instance;
    }

    public bool OnDown(bool held) => false;
    public bool OnEnter() => false;
    public bool OnEscape() => false;
    public bool OnLeft(bool held) => false;
    public bool OnPageDown() => false;
    public bool OnPageUp() => false;
    public bool OnRight(bool held) => false;
    public bool OnUp(bool held) => false;

    public void OnSelectionChanged(FilterType filterType, string filterValue, IPlatform platform, IPlatformCategory category, IPlaylist playlist, IGame game)
    {
    }
}
