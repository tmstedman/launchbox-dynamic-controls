using DynamicControls.Config;
using DynamicControls.InputMapping;
using DynamicControls.Labels;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace DynamicControls.Core.Tests.Labels;

/// <summary>
/// Unit tests for <see cref="InputLabelsService"/>. The loader is a substitute so each test
/// supplies the exact <see cref="InputLabelsConfig"/> the service should see — no XML, no
/// filesystem. Covers the platform-to-generic translation via the mapping, inheritable-default
/// merging, the <c>IsGameSpecific</c> flag, the clone-of fallback in the loader loop, and
/// loader priority order.
/// </summary>
public class InputLabelsServiceTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IInputLabelsLoader _loader = Substitute.For<IInputLabelsLoader>();
    private readonly List<IInputLabelsLoader> _additionalLoaders = [];

    // Service is built fresh from the accumulated additional loaders on each access — the default
    // loader (_loader) is always the first in the chain (constructor parameter to the plugins
    // wrapper), and tests can append more via AddLoader (which stubs IsEnabled.Returns(true) so
    // the plugins wrapper keeps the substitute in the enabled set).
    private InputLabelsService BuildTestFixture =>
        new(_logger, new InputLabelsPlugins(_loader, _additionalLoaders, new GlobalConfig()));

    private IInputLabelsLoader AddLoader()
    {
        var loader = Substitute.For<IInputLabelsLoader>();
        loader.IsEnabled(Arg.Any<GlobalConfig>()).Returns(true);
        _additionalLoaders.Add(loader);
        return loader;
    }


    private static GameInfo Game(string romName) => new(
        Platform: "Sega Genesis",
        RomName: romName,
        CloneOf: null,
        EmulatorPath: null,
        RomDirectory: null,
        RetroArchCore: null);

    private static InputLabelsConfig Labels(params (string Name, string Label)[] entries) => new()
    {
        Labels = [.. entries.Select(e => new LabelEntry { Name = e.Name, Label = e.Label })],
    };

    /// <summary>
    /// Sega Genesis 3-Button mapping with the Dpad mirrored onto the left stick. Only
    /// <c>ButtonToInput</c> is read by <see cref="InputLabelsService"/>.
    /// </summary>
    #pragma warning disable format
    private static ResolvedMapping ThreeButtonMapping() => Mapping(
        platform: "Sega Genesis",
        buttonToInput: new()
        {
            ["Dpad-Left"]  = ["ButtonDpadLeft",  "AxisLeftStickLeft"],
            ["Dpad-Right"] = ["ButtonDpadRight", "AxisLeftStickRight"],
            ["Dpad-Up"]    = ["ButtonDpadUp",    "AxisLeftStickUp"],
            ["Dpad-Down"]  = ["ButtonDpadDown",  "AxisLeftStickDown"],
            ["A"]          = ["ButtonX"],
            ["B"]          = ["ButtonA"],
            ["C"]          = ["ButtonB"],
            ["Start"]      = ["ButtonStart"],
        });
    #pragma warning restore format

    /// <summary>
    /// Helper for building a <see cref="ResolvedMapping"/> from a mutable button-to-input dict;
    /// keeps test bodies focused on what they're mapping and absorbs the read-only-collection
    /// conversion in one place.
    /// </summary>
    private static ResolvedMapping Mapping(string platform, Dictionary<string, List<string>> buttonToInput)
    {
        var readOnly = buttonToInput.ToDictionary(e => e.Key, e => (IReadOnlyList<string>)e.Value);
        var empty = new Dictionary<string, string>();
        return new ResolvedMapping(
            Platform: platform,
            Controller: null,
            ButtonToInput: readOnly,
            InputToButton: empty,
            NaturalButtonToInput: readOnly,
            NaturalInputToButton: empty,
            AnalogToDigital: null);
    }

    [Fact]
    public void Load_GameLabels_TranslateThroughMapping_AndMergeDefaults()
    {
        // given a game with its own labels and a default file
        GameInfo game = Game("OutRun");

        _loader.LoadDefaultLabels("Sega Genesis").Returns(Labels(
            ("Start", "Pause"),
            ("C", "Cancel")));

        _loader.Load(game).Returns(Labels(
            ("A", "Brake"),
            ("B", "Accelerate")));

        // when the service resolves labels
        ResolvedLabels labels = BuildTestFixture.Load(game, ThreeButtonMapping());

        // then game labels land on the inputs their platform buttons drive (A->ButtonX, B->ButtonA)
        labels.LabelText["ButtonX"].ShouldBe("Brake");
        labels.LabelText["ButtonA"].ShouldBe("Accelerate");

        // and all defaults are merged in for buttons the game doesn't define
        labels.LabelText["ButtonStart"].ShouldBe("Pause");
        labels.LabelText["ButtonB"].ShouldBe("Cancel"); // C->ButtonB, merged from defaults

        // and the result is flagged game-specific
        labels.IsGameSpecific.ShouldBeTrue();
    }

    [Fact]
    public void Load_DpadMirrorEntries_PropagateLabelToBothGenericInputs()
    {
        // given a game with a single Dpad-Up label and a mapping that mirrors Dpad onto the left stick
        GameInfo game = Game("OutRun");

        _loader.Load(game).Returns(Labels(("Dpad-Up", "Low gear")));

        // when the service resolves labels
        ResolvedLabels labels = BuildTestFixture.Load(game, ThreeButtonMapping());

        // then the label lands on both the Dpad and left-stick inputs the mapping fans Dpad-Up out to
        labels.LabelText["ButtonDpadUp"].ShouldBe("Low gear");
        labels.LabelText["AxisLeftStickUp"].ShouldBe("Low gear");
    }

    [Fact]
    public void Load_NoGameLabels_FallsBackToDefaults_AndIsNotGameSpecific()
    {
        // given a loader that has no game labels but does have defaults
        GameInfo game = Game("UnknownGame");

        _loader.LoadDefaultLabels("Sega Genesis").Returns(Labels(
            ("Start", "Pause"),
            ("A", "Action")));

        _loader.Load(game).ReturnsNull();

        // when the service resolves labels
        ResolvedLabels labels = BuildTestFixture.Load(game, ThreeButtonMapping());

        // then all defaults appear
        labels.LabelText["ButtonStart"].ShouldBe("Pause");
        labels.LabelText["ButtonX"].ShouldBe("Action"); // A->ButtonX

        // and the result is not flagged game-specific — purely-inherited defaults don't count
        labels.IsGameSpecific.ShouldBeFalse();
    }

    [Fact]
    public void Load_NoGameLabelsAndNoDefaults_ReturnsEmpty()
    {
        // given a loader that has nothing at either tier
        GameInfo game = Game("UnknownGame");
        _loader.LoadDefaultLabels("Sega Genesis").ReturnsNull();
        _loader.Load(game).ReturnsNull();

        // when the service resolves labels
        ResolvedLabels labels = BuildTestFixture.Load(game, ThreeButtonMapping());

        // then the resolved labels are empty and not game-specific
        labels.LabelText.ShouldBeEmpty();
        labels.IsGameSpecific.ShouldBeFalse();
    }

    [Fact]
    public void Load_EmptyGameLabels_TreatedAsMissing_FallsThroughToDefaults()
    {
        // given a loader that returns an empty (but non-null) game labels config
        GameInfo game = Game("OutRun");
        _loader.LoadDefaultLabels("Sega Genesis").Returns(Labels(("Start", "Pause")));
        _loader.Load(game).Returns(new InputLabelsConfig());

        // when the service resolves labels
        ResolvedLabels labels = BuildTestFixture.Load(game, ThreeButtonMapping());

        // then the empty file is treated as if absent — defaults-only path, not flagged game-specific
        labels.LabelText["ButtonStart"].ShouldBe("Pause");
        labels.IsGameSpecific.ShouldBeFalse();
    }

    [Fact]
    public void Load_CloneGame_RetriesLoaderWithParentRomName()
    {
        // given a clone whose own rom name has no labels, but its parent does
        GameInfo clone = Game("OutRun (Prototype)") with { CloneOf = "OutRun" };
        GameInfo parent = clone with { RomName = "OutRun" };
        _loader.Load(clone).ReturnsNull();
        _loader.Load(parent).Returns(Labels(("A", "Brake")));

        // when the service resolves labels
        ResolvedLabels labels = BuildTestFixture.Load(clone, ThreeButtonMapping());

        // then the clone picks up the parent's labels via the CloneOf retry (the Brake assertion
        // implies parent was queried; Received() pins that the original was tried first)
        labels.LabelText["ButtonX"].ShouldBe("Brake");
        labels.IsGameSpecific.ShouldBeTrue();
        _loader.Received(1).Load(clone);
    }

    [Fact]
    public void Load_NoCloneOf_LoaderNotRetried()
    {
        // given a non-clone game with no labels
        GameInfo game = Game("UnknownGame"); // CloneOf is null
        _loader.Load(game).ReturnsNull();

        // when the service resolves labels
        BuildTestFixture.Load(game, ThreeButtonMapping());

        // then the loader is called exactly once — no clone fallback to attempt
        _loader.Received(1).Load(Arg.Any<GameInfo>());
    }

    [Fact]
    public void Load_MultipleLoaders_FirstLoaderWithDataWins()
    {
        // given two loaders registered in order, where the first has labels for this game
        var second = AddLoader();
        GameInfo game = Game("OutRun");

        _loader.Load(game).Returns(Labels(("A", "FromFirst")));
        second.Load(game).Returns(Labels(("A", "FromSecond")));

        // when the service resolves labels
        ResolvedLabels labels = BuildTestFixture.Load(game, ThreeButtonMapping());

        // then the first loader wins and the second is never consulted
        labels.LabelText["ButtonX"].ShouldBe("FromFirst");
        second.DidNotReceive().Load(Arg.Any<GameInfo>());
    }

    [Fact]
    public void Load_FirstLoaderEmpty_FallsThroughToSecondLoader()
    {
        // given two loaders where the first returns null and the second has labels
        var second = AddLoader();
        GameInfo game = Game("OutRun");

        _loader.Load(game).ReturnsNull();
        second.Load(game).Returns(Labels(("A", "FromSecond")));

        // when the service resolves labels
        ResolvedLabels labels = BuildTestFixture.Load(game, ThreeButtonMapping());

        // then the second loader's data is used
        labels.LabelText["ButtonX"].ShouldBe("FromSecond");
    }

    [Fact]
    public void Load_LabelForUnmappedPlatformButton_IsDropped()
    {
        // given a game label for a button not in the mapping
        GameInfo game = Game("OutRun");
        _loader.Load(game).Returns(Labels(
            ("A", "Brake"),
            ("Z", "Mystery")));

        // when the service resolves labels with a mapping that only knows A
        ResolvedMapping mapping = Mapping(
            platform: "Sega Genesis",
            buttonToInput: new() { ["A"] = ["ButtonX"] });
        ResolvedLabels labels = BuildTestFixture.Load(game, mapping);

        // then the unmapped Z is silently dropped
        labels.LabelText["ButtonX"].ShouldBe("Brake");
        labels.LabelText.Keys.ShouldBe(["ButtonX"]);
    }

    [Fact]
    public void Load_GameLabelOverridesDefault()
    {
        // given a game label and a default for the same platform button
        GameInfo game = Game("OutRun");
        _loader.LoadDefaultLabels("Sega Genesis").Returns(Labels(("Start", "Pause")));
        _loader.Load(game).Returns(Labels(("Start", "Restart")));

        // when the service resolves labels
        ResolvedLabels labels = BuildTestFixture.Load(game, ThreeButtonMapping());

        // then the game label wins over the default
        labels.LabelText["ButtonStart"].ShouldBe("Restart");
    }
}
