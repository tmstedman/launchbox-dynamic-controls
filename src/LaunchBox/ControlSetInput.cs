using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Threading;
using DynamicControls.Infrastructure;

namespace DynamicControls.LaunchBox;

/// <summary>
/// Steps the pause screen through a game's control sets from the keyboard (Left and Right, Page
/// Up and Page Down) and from a gamepad (D-pad Left and Right, the shoulder buttons). The pause
/// theme has no code-behind, so it switches this on with the attached property
/// <c>dc:ControlSetInput.Enabled="True"</c> on its root, and the plugin supplies the instance that
/// answers (<see cref="Current"/>).
/// </summary>
/// <remarks>
/// The inputs are polled, not received: Big Box reads its own controller and hands the pause
/// theme no key events, so a key handler on the theme never fires. LaunchBox builds the theme
/// afresh for each pause and tears it down after, so the poll runs from the root's
/// <c>Loaded</c> to its <c>Unloaded</c> and only when there is another set to step to; any other
/// game is never polled and the menu behaves as it always has. One theme is polled at a time: a
/// theme that loads while another is polling takes over, and the earlier one's unload doesn't
/// stop it. Nothing is kept from the menu, so a press that steps a page also reaches it, which for
/// these inputs does nothing — the menu is vertical. A button already held when the pause screen
/// opens doesn't step, and a held one steps once. The sources are read together, so a press two
/// of them report (a pad whose D-pad a mapper also sends as arrow keys) turns one page. While the
/// theme is hidden nothing steps, so a key pressed in the game can't turn a page; a theme that is
/// shown but never unloaded would otherwise poll through the game. The first press from each
/// source is logged, so one test on a real install shows which inputs work. An error in any of
/// this is logged and stops the poll for that pause rather than reaching Big Box.
/// </remarks>
public sealed class ControlSetInput
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly ControlSetPress[] Directions = [ControlSetPress.Forward, ControlSetPress.Back];

    private readonly DynamicControlsViewModel _viewModel;
    private readonly ILogger _logger;
    private readonly IPressSource[] _sources;
    private readonly ControlSetPress[] _held;
    private ControlSetPress _down;
    private readonly HashSet<(string Source, ControlSetPress Press)> _seen = [];
    private object? _polledTheme;
    private IPollTimer? _timer;

    public ControlSetInput(DynamicControlsViewModel viewModel, ILogger logger)
        : this(viewModel, logger, [new KeyboardPressSource(), new GamepadPressSource()]) { }

    internal ControlSetInput(DynamicControlsViewModel viewModel, ILogger logger, IPressSource[] sources)
    {
        _viewModel = viewModel;
        _logger = logger;
        _sources = sources;
        _held = new ControlSetPress[sources.Length];
    }

    /// <summary>The instance a theme's inputs go to; null until the plugin has loaded.</summary>
    public static ControlSetInput? Current { get; set; }

    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(ControlSetInput), new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

    public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);

    // The WPF side only forwards to the instance, where what each event does is decided and
    // tested; it can't be exercised outside a running WPF dispatcher.
    private static void OnEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not FrameworkElement root) return;
        bool enabled = (bool)e.NewValue;
        root.Loaded -= OnThemeLoaded;
        root.Unloaded -= OnThemeUnloaded;
        root.IsVisibleChanged -= OnThemeVisibleChanged;
        if (enabled)
        {
            root.Loaded += OnThemeLoaded;
            root.Unloaded += OnThemeUnloaded;
            root.IsVisibleChanged += OnThemeVisibleChanged;
        }

        if (Current is not { } input) return;
        input.Guarded(enabled ? "attaching to" : "detaching from", () =>
        {
            if (enabled) LogTheme(input, root, "attached");
            else input.ThemeUnloaded(root);
        });
    }

    private static readonly RoutedEventHandler OnThemeLoaded = (sender, _) =>
    {
        if (Current is not { } input) return;
        var root = (FrameworkElement)sender;
        input.Guarded("loading", () =>
        {
            LogTheme(input, root, "loaded");
            input.ThemeLoaded(root, () => root.IsVisible, new DispatcherPollTimer(root.Dispatcher, PollInterval));
        });
    };

    private static readonly RoutedEventHandler OnThemeUnloaded = (sender, _) =>
    {
        if (Current is not { } input) return;
        var root = (FrameworkElement)sender;
        input.Guarded("unloading", () =>
        {
            LogTheme(input, root, "unloaded");
            input.ThemeUnloaded(root);
        });
    };

    private static readonly DependencyPropertyChangedEventHandler OnThemeVisibleChanged = (sender, e) =>
    {
        if (Current is not { } input) return;
        var root = (FrameworkElement)sender;
        input.Guarded("showing or hiding", () => LogTheme(input, root, $"visibility changed to {e.NewValue}"));
    };

    private static void LogTheme(ControlSetInput input, FrameworkElement root, string what) =>
        input.LogTheme(what, root.IsLoaded, root.IsVisible, root.Opacity, root.ActualWidth, root.ActualHeight);

    /// <summary>
    /// Starts polling for a theme that has loaded, on <paramref name="timer"/>, if there is another
    /// set to step to. Whatever was polling before stops first, so only this theme is read; its
    /// presses step only while <paramref name="isVisible"/> says it is on screen.
    /// </summary>
    internal void ThemeLoaded(object theme, Func<bool> isVisible, IPollTimer timer)
    {
        StopPolling();
        if (!Begin()) return;

        _polledTheme = theme;
        _timer = timer;
        timer.Start(() => Guarded("polling", () => Poll(onScreen: isVisible())));
    }

    /// <summary>Stops polling if <paramref name="theme"/> is the one being polled; a theme
    /// another has since taken over from leaves that one polling.</summary>
    internal void ThemeUnloaded(object theme)
    {
        if (ReferenceEquals(theme, _polledTheme)) StopPolling();
    }

    private void StopPolling()
    {
        IPollTimer? timer = _timer;
        _timer = null;
        _polledTheme = null;
        timer?.Stop();
    }

    /// <summary>
    /// Runs one of the theme's handlers, which WPF calls on Big Box's dispatcher, so that an error
    /// is logged and stops the poll instead of reaching Big Box. If the log is what failed, the
    /// error is dropped: there is nowhere left to report it.
    /// </summary>
    internal void Guarded(string what, Action handler)
    {
        try
        {
            handler();
        }
        catch (Exception ex)
        {
            try
            {
                StopPolling();
                _logger.Error($"Stepping control sets stopped after an error while {what} the pause theme: {ex}");
            }
            catch (Exception)
            {
                // The log is what failed.
            }
        }
    }

    /// <summary>
    /// Logs what happened to the pause theme's root and the state it left it in. Nothing in
    /// LaunchBox's documentation or SDK says when a pause theme is on screen, so this records
    /// what WPF reports at each pause: a theme built afresh each time shows as loaded then
    /// unloaded, one kept and hidden shows visibility changes, and one hidden by opacity or
    /// position shows neither, which the opacity and size make plain.
    /// </summary>
    internal void LogTheme(string what, bool isLoaded, bool isVisible, double opacity, double width, double height) =>
        _logger.Info(FormattableString.Invariant(
            $"Pause theme {what} (loaded: {isLoaded}, visible: {isVisible}, opacity: {opacity:0.##}, size: {width:0}x{height:0})"));

    /// <summary>
    /// Readies the sources for a pause screen and says whether there is anything to poll for: only
    /// a game with another set to step to is. What each source finds is logged, since whether a
    /// gamepad is there to read is the one thing a test press can't tell apart from a gamepad
    /// that isn't read.
    /// </summary>
    internal bool Begin()
    {
        if (!_viewModel.CanStep) return false;

        string[] found = [.. _sources.Select(source => source.Begin())];
        _logger.Info($"Pause screen steps control sets from: {string.Join("; ", found)}");

        // A button already down as the screen opens (the one that paused, a shoulder still held)
        // isn't a press to step on.
        _down = ControlSetPress.None;
        for (int i = 0; i < _sources.Length; i++)
        {
            _held[i] = _sources[i].Read();
            _down |= _held[i];
        }
        return true;
    }

    /// <summary>
    /// Reads every source and, while <paramref name="onScreen"/>, turns a page for a press newly
    /// down on any of them since the last read. The sources are read as one, so a press already
    /// down on one source doesn't step again when another reports it. Off screen, presses are
    /// only noted as held, so one made while hidden doesn't step when the theme shows again.
    /// </summary>
    internal void Poll(bool onScreen = true)
    {
        ControlSetPress down = ControlSetPress.None;
        for (int i = 0; i < _sources.Length; i++)
        {
            ControlSetPress read = _sources[i].Read();
            if (onScreen) LogFirstPresses(_sources[i].Name, read & ~_held[i]);
            _held[i] = read;
            down |= read;
        }

        ControlSetPress pressed = down & ~_down;
        _down = down;
        if (!onScreen) return;

        int step = (pressed.HasFlag(ControlSetPress.Forward) ? 1 : 0) - (pressed.HasFlag(ControlSetPress.Back) ? 1 : 0);
        if (step != 0) _viewModel.StepSet(step);
    }

    private void LogFirstPresses(string source, ControlSetPress pressed)
    {
        foreach (ControlSetPress which in Directions)
        {
            if (pressed.HasFlag(which) && _seen.Add((source, which)))
                _logger.Info($"Pause screen read a press to step {(which == ControlSetPress.Forward ? "forward" : "back")} from the {source}");
        }
    }
}

/// <summary>A repeating timer the poll runs on, so what each pause-theme event does can be tested
/// without a WPF dispatcher.</summary>
internal interface IPollTimer
{
    /// <summary>Starts calling <paramref name="tick"/> until <see cref="Stop"/>.</summary>
    void Start(Action tick);

    void Stop();
}

/// <summary>The timer on a real install: Big Box's dispatcher, at input priority.</summary>
[ExcludeFromCodeCoverage(Justification = "WPF timer shim; what it ticks is tested through IPollTimer")]
internal sealed class DispatcherPollTimer(Dispatcher dispatcher, TimeSpan interval) : IPollTimer
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Input, dispatcher) { Interval = interval };

    public void Start(Action tick)
    {
        _timer.Tick += (_, _) => tick();
        _timer.Start();
    }

    public void Stop() => _timer.Stop();
}
