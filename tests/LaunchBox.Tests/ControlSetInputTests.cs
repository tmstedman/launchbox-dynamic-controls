using DynamicControls.Infrastructure;
using DynamicControls.Rendering;
using NSubstitute;

namespace DynamicControls.LaunchBox.Tests;

public class ControlSetInputTests
{
    private readonly DynamicControlsViewModel _viewModel = new();
    private readonly ILogger _logger = Substitute.For<ILogger>();

    [Theory]
    [InlineData(0x25, 1)] // Left
    [InlineData(0x21, 1)] // Page Up
    [InlineData(0x27, 2)] // Right
    [InlineData(0x22, 2)] // Page Down
    [InlineData(0x26, 0)] // Up
    [InlineData(0x28, 0)] // Down
    [InlineData(0x0D, 0)] // Enter
    public void FromKeys_TurnsBackOnLeftAndPageUp_ForwardOnRightAndPageDown_AndNotOnTheMenuKeys(int key, int press)
    {
        // given one key held

        // when asked what is pressed
        // then only the keys the vertical menu leaves alone, and the page keys, step
        ControlSetPresses.FromKeys(vk => vk == key).ShouldBe((ControlSetPress)press);
    }

    [Theory]
    [InlineData(0x0004, 1)] // D-pad Left
    [InlineData(0x0100, 1)] // Left shoulder
    [InlineData(0x0008, 2)] // D-pad Right
    [InlineData(0x0200, 2)] // Right shoulder
    [InlineData(0x0001, 0)] // D-pad Up
    [InlineData(0x0002, 0)] // D-pad Down
    [InlineData(0x1000, 0)] // A
    [InlineData(0x0000, 0)]
    public void FromGamepad_TurnsBackOnTheLeftSide_ForwardOnTheRight_AndNotOnTheMenuButtons(int buttons, int press)
    {
        // given the buttons an XInput pad reports held

        // when asked what is pressed
        // then the D-pad's sides and the shoulders step, and the rest are left to the menu
        ControlSetPresses.FromGamepad((ushort)buttons).ShouldBe((ControlSetPress)press);
    }

    [Fact]
    public void FromGamepad_BothSidesAtOnce_PressesBoth()
    {
        // given Left and Right held together
        // when asked what is pressed
        // then both are, for the poll to cancel out
        ControlSetPresses.FromGamepad(0x0004 | 0x0008).ShouldBe(ControlSetPress.Back | ControlSetPress.Forward);
    }

    [Fact]
    public void LogTheme_RecordsWhatHappenedAndTheStateItLeft()
    {
        // given the plugin's input handler
        var input = new ControlSetInput(_viewModel, _logger, []);

        // when the theme reports it has been hidden
        input.LogTheme("visibility changed to False", isLoaded: true, isVisible: false, opacity: 0.5, width: 1920, height: 1080);

        // then the log says what happened and whether it is loaded, visible, faded or sized
        _logger.Received().Info("Pause theme visibility changed to False (loaded: True, visible: False, opacity: 0.5, size: 1920x1080)");
    }

    [Fact]
    public void Begin_WithThePluginsOwnSources_OnAGameWithoutSets_PollsAndLogsNothing()
    {
        // given the handler the plugin builds, which reads the real keyboard and pads
        _viewModel.Show(new ControllerOverlayModel());
        var input = new ControlSetInput(_viewModel, _logger);

        // when the pause screen opens on a game without sets
        bool polls = input.Begin();

        // then nothing is polled, and nothing is logged about the sources
        polls.ShouldBeFalse();
        _logger.DidNotReceive().Info(Arg.Any<string>());
    }

    [Fact]
    public void Begin_OnAGameWithoutSets_PollsNothing()
    {
        // given a game without control sets
        _viewModel.Show(new ControllerOverlayModel());
        var pad = new FakeSource("gamepad");
        var input = new ControlSetInput(_viewModel, _logger, [pad]);

        // when the pause screen opens
        bool polls = input.Begin();

        // then nothing is polled, so such a game's pause screen is as it always was
        polls.ShouldBeFalse();
        pad.Begun.ShouldBeFalse();
    }

    [Fact]
    public void Begin_OnAGameWithOneSet_PollsNothing()
    {
        // given a game with one control set, so nothing to step to
        _viewModel.Show(WithSets("Light Plane"));
        var pad = new FakeSource("gamepad");
        var input = new ControlSetInput(_viewModel, _logger, [pad]);

        // when the pause screen opens
        bool polls = input.Begin();

        // then nothing is polled
        polls.ShouldBeFalse();
        pad.Begun.ShouldBeFalse();
    }

    [Fact]
    public void Begin_OnAGameWithSets_PollsAndLogsWhatEachSourceFound()
    {
        // given a game with two control sets and two sources
        _viewModel.Show(WithSets("Light Plane", "Rocket Belt"));
        var input = new ControlSetInput(_viewModel, _logger, [new FakeSource("keyboard"), new FakeSource("gamepad", found: "gamepad (no XInput pad connected)")]);

        // when the pause screen opens
        bool polls = input.Begin();

        // then it polls, and the log says what each source found, so a pad that isn't there shows
        polls.ShouldBeTrue();
        _logger.Received(1).Info("Pause screen steps control sets from: keyboard; gamepad (no XInput pad connected)");
    }

    [Fact]
    public void Poll_NewPressForward_StepsOnePage()
    {
        // given a game with two sets, on its first, and a pad
        _viewModel.Show(WithSets("Light Plane", "Rocket Belt"));
        var pad = new FakeSource("gamepad");
        var input = Begun(pad);

        // when Right is pressed
        pad.Down = ControlSetPress.Forward;
        input.Poll();

        // then the second set is on screen
        _viewModel.SetName.ShouldBe("Rocket Belt");
    }

    [Fact]
    public void Poll_NewPressBack_StepsBackAndWrapsToTheLastPage()
    {
        // given a game with two sets, on its first
        _viewModel.Show(WithSets("Light Plane", "Rocket Belt"));
        var pad = new FakeSource("gamepad");
        var input = Begun(pad);

        // when Left is pressed
        pad.Down = ControlSetPress.Back;
        input.Poll();

        // then the last set is on screen
        _viewModel.SetName.ShouldBe("Rocket Belt");
    }

    [Fact]
    public void Poll_HeldButton_StepsOnceUntilItIsReleasedAndPressedAgain()
    {
        // given a game with three sets, on its first
        _viewModel.Show(WithSets("Light Plane", "Rocket Belt", "Helicopter"));
        var pad = new FakeSource("gamepad");
        var input = Begun(pad);

        // when Right is held through three reads
        pad.Down = ControlSetPress.Forward;
        input.Poll();
        input.Poll();
        input.Poll();

        // then it turned one page
        _viewModel.SetName.ShouldBe("Rocket Belt");

        // and when it is released and pressed again
        pad.Down = ControlSetPress.None;
        input.Poll();
        pad.Down = ControlSetPress.Forward;
        input.Poll();

        // then it turns another
        _viewModel.SetName.ShouldBe("Helicopter");
    }

    [Fact]
    public void Poll_ButtonAlreadyHeldWhenTheScreenOpens_DoesNotStep()
    {
        // given a game with two sets, and a shoulder still held as the pause screen opens
        _viewModel.Show(WithSets("Light Plane", "Rocket Belt"));
        var pad = new FakeSource("gamepad") { Down = ControlSetPress.Forward };
        var input = Begun(pad);

        // when it is still held on the next read
        input.Poll();

        // then the first set stays
        _viewModel.SetName.ShouldBe("Light Plane");
    }

    [Fact]
    public void Poll_BackAndForwardTogether_CancelOut()
    {
        // given a game with two sets, a keyboard pressing Left and a pad pressing Right
        _viewModel.Show(WithSets("Light Plane", "Rocket Belt"));
        var keyboard = new FakeSource("keyboard");
        var pad = new FakeSource("gamepad");
        var input = Begun(keyboard, pad);

        // when both are pressed in the same read
        keyboard.Down = ControlSetPress.Back;
        pad.Down = ControlSetPress.Forward;
        input.Poll();

        // then the page stays
        _viewModel.SetName.ShouldBe("Light Plane");
    }

    [Fact]
    public void Poll_EitherSourceSteps()
    {
        // given a game with three sets, a keyboard and a pad
        _viewModel.Show(WithSets("Light Plane", "Rocket Belt", "Helicopter"));
        var keyboard = new FakeSource("keyboard");
        var pad = new FakeSource("gamepad");
        var input = Begun(keyboard, pad);

        // when the keyboard steps forward and lets go, and then the pad steps forward
        keyboard.Down = ControlSetPress.Forward;
        input.Poll();
        keyboard.Down = ControlSetPress.None;
        input.Poll();
        pad.Down = ControlSetPress.Forward;
        input.Poll();

        // then both turned a page
        _viewModel.SetName.ShouldBe("Helicopter");
    }

    [Fact]
    public void Poll_OnePressTwoSourcesReportTogether_StepsOnce()
    {
        // given a game with three sets, and a pad whose D-pad a mapper also sends as arrow keys
        _viewModel.Show(WithSets("Light Plane", "Rocket Belt", "Helicopter"));
        var keyboard = new FakeSource("keyboard");
        var pad = new FakeSource("gamepad");
        var input = Begun(keyboard, pad);

        // when Right is pressed and both report it in the same read
        keyboard.Down = ControlSetPress.Forward;
        pad.Down = ControlSetPress.Forward;
        input.Poll();

        // then it turned one page
        _viewModel.SetName.ShouldBe("Rocket Belt");
    }

    [Fact]
    public void Poll_OnePressASecondSourceReportsALaterRead_StepsOnce()
    {
        // given a game with three sets, and a pad whose D-pad a mapper also sends as arrow keys
        _viewModel.Show(WithSets("Light Plane", "Rocket Belt", "Helicopter"));
        var keyboard = new FakeSource("keyboard");
        var pad = new FakeSource("gamepad");
        var input = Begun(keyboard, pad);

        // when Right is pressed, the pad reporting it a read before the mapper's key
        pad.Down = ControlSetPress.Forward;
        input.Poll();
        keyboard.Down = ControlSetPress.Forward;
        input.Poll();

        // then it turned one page
        _viewModel.SetName.ShouldBe("Rocket Belt");
    }

    [Fact]
    public void Poll_OffScreen_DoesNotStep_NorWhenThePressIsStillHeldAsItShows()
    {
        // given a game with three sets, its theme hidden while the game plays
        _viewModel.Show(WithSets("Light Plane", "Rocket Belt", "Helicopter"));
        var keyboard = new FakeSource("keyboard");
        var input = Begun(keyboard);

        // when Right is pressed in the game, and is still held as the theme shows again
        keyboard.Down = ControlSetPress.Forward;
        input.Poll(onScreen: false);
        input.Poll();

        // then no page turned
        _viewModel.SetName.ShouldBe("Light Plane");

        // and when it is released and pressed on screen
        keyboard.Down = ControlSetPress.None;
        input.Poll();
        keyboard.Down = ControlSetPress.Forward;
        input.Poll();

        // then it turns a page
        _viewModel.SetName.ShouldBe("Rocket Belt");
    }

    [Fact]
    public void Poll_LogsTheFirstPressFromEachSource_AndWhichWay()
    {
        // given a game with two sets, a keyboard and a pad
        _viewModel.Show(WithSets("Light Plane", "Rocket Belt"));
        var keyboard = new FakeSource("keyboard");
        var pad = new FakeSource("gamepad");
        var input = Begun(keyboard, pad);
        _logger.ClearReceivedCalls();

        // when the pad presses Right twice and the keyboard Left once
        pad.Down = ControlSetPress.Forward;
        input.Poll();
        pad.Down = ControlSetPress.None;
        input.Poll();
        pad.Down = ControlSetPress.Forward;
        input.Poll();
        pad.Down = ControlSetPress.None;
        keyboard.Down = ControlSetPress.Back;
        input.Poll();

        // then each source's first press each way is logged once, so a test shows what is read
        _logger.Received(2).Info(Arg.Any<string>());
        _logger.Received(1).Info("Pause screen read a press to step forward from the gamepad");
        _logger.Received(1).Info("Pause screen read a press to step back from the keyboard");
    }

    [Fact]
    public void ThemeLoaded_OnAGameWithSetsToStepTo_PollsOnItsTimer()
    {
        // given a game with two sets and a pad
        _viewModel.Show(WithSets("Light Plane", "Rocket Belt"));
        var pad = new FakeSource("gamepad");
        var input = new ControlSetInput(_viewModel, _logger, [pad]);
        var timer = new FakeTimer();

        // when the theme loads and Right is pressed before the next tick
        input.ThemeLoaded(new object(), () => true, timer);
        pad.Down = ControlSetPress.Forward;
        timer.Tick();

        // then the timer runs the poll, and the press turned a page
        timer.Running.ShouldBeTrue();
        _viewModel.SetName.ShouldBe("Rocket Belt");
    }

    [Fact]
    public void ThemeLoaded_OnAGameWithNothingToStepTo_StartsNoTimer()
    {
        // given a game with one set
        _viewModel.Show(WithSets("Light Plane"));
        var input = new ControlSetInput(_viewModel, _logger, [new FakeSource("gamepad")]);
        var timer = new FakeTimer();

        // when the theme loads
        input.ThemeLoaded(new object(), () => true, timer);

        // then nothing polls
        timer.Running.ShouldBeFalse();
    }

    [Fact]
    public void ThemeUnloaded_StopsThePoll()
    {
        // given a theme polling a game with two sets
        _viewModel.Show(WithSets("Light Plane", "Rocket Belt"));
        var input = new ControlSetInput(_viewModel, _logger, [new FakeSource("gamepad")]);
        var theme = new object();
        var timer = new FakeTimer();
        input.ThemeLoaded(theme, () => true, timer);

        // when it unloads
        input.ThemeUnloaded(theme);

        // then its timer stops
        timer.Running.ShouldBeFalse();
    }

    [Fact]
    public void ThemeLoaded_WhileAnotherPolls_TakesOver_AndTheEarlierThemesUnloadLeavesItPolling()
    {
        // given a theme polling a game with three sets, and a pad
        _viewModel.Show(WithSets("Light Plane", "Rocket Belt", "Helicopter"));
        var pad = new FakeSource("gamepad");
        var input = new ControlSetInput(_viewModel, _logger, [pad]);
        var earlier = new object();
        var earlierTimer = new FakeTimer();
        input.ThemeLoaded(earlier, () => false, earlierTimer);

        // when a second theme loads before the first unloads, and then the first unloads
        var later = new object();
        var laterTimer = new FakeTimer();
        input.ThemeLoaded(later, () => true, laterTimer);
        input.ThemeUnloaded(earlier);

        // then only the second polls, so the hidden first can't take its presses as already held
        earlierTimer.Running.ShouldBeFalse();
        laterTimer.Running.ShouldBeTrue();
        pad.Down = ControlSetPress.Forward;
        laterTimer.Tick();
        _viewModel.SetName.ShouldBe("Rocket Belt");
    }

    [Fact]
    public void Tick_WhileTheThemeIsHidden_DoesNotStep()
    {
        // given a theme polling a game with two sets, then hidden
        _viewModel.Show(WithSets("Light Plane", "Rocket Belt"));
        var pad = new FakeSource("gamepad");
        var input = new ControlSetInput(_viewModel, _logger, [pad]);
        bool visible = true;
        var timer = new FakeTimer();
        input.ThemeLoaded(new object(), () => visible, timer);
        visible = false;

        // when Right is pressed in the game
        pad.Down = ControlSetPress.Forward;
        timer.Tick();

        // then no page turned
        _viewModel.SetName.ShouldBe("Light Plane");
    }

    [Fact]
    public void Tick_SourceThrows_IsLoggedAndStopsThePoll_WithoutReachingBigBox()
    {
        // given a theme polling a game with two sets, from a source that fails when read again
        _viewModel.Show(WithSets("Light Plane", "Rocket Belt"));
        var broken = new FakeSource("gamepad");
        var input = new ControlSetInput(_viewModel, _logger, [broken]);
        var timer = new FakeTimer();
        input.ThemeLoaded(new object(), () => true, timer);
        broken.Fails = true;

        // when the timer ticks
        Should.NotThrow(timer.Tick);

        // then the error is logged and the poll stops
        timer.Running.ShouldBeFalse();
        _logger.Received(1).Error(Arg.Is<string>(m => m.Contains("while polling") && m.Contains("device gone")));
    }

    [Fact]
    public void Guarded_WhenTheLogItselfFails_NothingReachesBigBox()
    {
        // given a log that fails on every write
        _logger.When(l => l.Error(Arg.Any<string>())).Do(_ => throw new IOException("log locked"));
        var input = new ControlSetInput(_viewModel, _logger, []);

        // when a theme handler fails
        // then the error goes no further
        Should.NotThrow(() => input.Guarded("loading", () => throw new InvalidOperationException("boom")));
    }

    private ControlSetInput Begun(params FakeSource[] sources)
    {
        var input = new ControlSetInput(_viewModel, _logger, [.. sources]);
        input.Begin();
        return input;
    }

    private static ControllerOverlayModel WithSets(params string[] names) => new()
    {
        ControlSets =
        [
            .. names.Select(name => new ControllerOverlayModel
            {
                SetName = name,
                InputLabels = [new RenderedLabel(Left: 0, Top: 0, Text: name, Alignment: "Left", FontSize: 24)],
            }),
        ],
    };

    private sealed class FakeSource(string name, string? found = null) : IPressSource
    {
        public ControlSetPress Down { get; set; }
        public bool Begun { get; private set; }

        public string Name => name;

        public string Begin()
        {
            Begun = true;
            return found ?? name;
        }

        /// <summary>Makes every read after this one fail, as a pad unplugged mid-read might.</summary>
        public bool Fails { get; set; }

        public ControlSetPress Read() => Fails ? throw new InvalidOperationException("device gone") : Down;
    }

    private sealed class FakeTimer : IPollTimer
    {
        private Action? _tick;

        public bool Running { get; private set; }

        public void Start(Action tick)
        {
            _tick = tick;
            Running = true;
        }

        public void Stop() => Running = false;

        /// <summary>One tick, as the dispatcher would run it; nothing while stopped.</summary>
        public void Tick()
        {
            if (Running) _tick?.Invoke();
        }
    }
}
