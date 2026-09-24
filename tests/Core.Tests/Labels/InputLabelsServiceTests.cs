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
        LaunchBoxId: null,
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

    // --- button combinations ---

    /// <summary>
    /// The 3countb shape: a per-game MAME cfg binds RB to both BUTTON1 and BUTTON2, so pressing it
    /// performs the game's combined move. Only <c>ButtonToInput</c> is read here.
    /// </summary>
    #pragma warning disable format
    private static ResolvedMapping SharedShoulderMapping() => Mapping(
        platform: "Arcade",
        buttonToInput: new()
        {
            ["BUTTON1"] = ["ButtonX", "ButtonRightShoulder"],
            ["BUTTON2"] = ["ButtonA", "ButtonRightShoulder"],
        });
    #pragma warning restore format

    [Fact]
    public void Load_Combination_LabelsTheSharedInput_IndividualsKeepTheirOwn()
    {
        _loader.Load(Arg.Any<GameInfo>()).Returns(Labels(
            ("BUTTON1", "Punch"),
            ("BUTTON2", "Kick"),
            ("BUTTON1 BUTTON2", "Power Move")));

        ResolvedLabels labels = BuildTestFixture.Load(Game("3countb"), SharedShoulderMapping());

        labels.LabelText.ShouldBeDictionaryOf(
            ("ButtonRightShoulder", "Power Move"),
            ("ButtonX", "Punch"),
            ("ButtonA", "Kick"));
    }

    [Fact]
    public void Load_Combination_NameOrderDoesNotMatter()
    {
        _loader.Load(Arg.Any<GameInfo>()).Returns(Labels(("BUTTON2 BUTTON1", "Power Move")));

        ResolvedLabels labels = BuildTestFixture.Load(Game("3countb"), SharedShoulderMapping());

        labels.LabelText["ButtonRightShoulder"].ShouldBe("Power Move");
    }

    [Fact]
    public void Load_Combination_NoSharedBinding_RendersNothing()
    {
        // this player's configuration gives the two buttons no control in common
        ResolvedMapping mapping = Mapping(platform: "Arcade", buttonToInput: new()
        {
            ["BUTTON1"] = ["ButtonX"],
            ["BUTTON2"] = ["ButtonA"],
        });
        _loader.Load(Arg.Any<GameInfo>()).Returns(Labels(
            ("BUTTON1", "Punch"),
            ("BUTTON1 BUTTON2", "Power Move")));

        ResolvedLabels labels = BuildTestFixture.Load(Game("3countb"), mapping);

        labels.LabelText.ShouldBeDictionaryOf(("ButtonX", "Punch"));
        _logger.DidNotReceive().Error(Arg.Any<string>());
    }

    [Fact]
    public void Load_Combination_NamingAnUnmappedButton_RendersNothing()
    {
        _loader.Load(Arg.Any<GameInfo>()).Returns(Labels(("BUTTON1 BUTTON9", "Power Move")));

        ResolvedLabels labels = BuildTestFixture.Load(Game("3countb"), SharedShoulderMapping());

        labels.LabelText.ContainsKey("ButtonRightShoulder").ShouldBeFalse();
        _logger.DidNotReceive().Error(Arg.Any<string>());
    }

    [Fact]
    public void Load_ButtonsShareAnInputEquallyWithNoCombination_LogsAndLeavesItUnlabelled()
    {
        _loader.Load(Arg.Any<GameInfo>()).Returns(Labels(
            ("BUTTON1", "Punch"),
            ("BUTTON2", "Kick")));

        ResolvedLabels labels = BuildTestFixture.Load(Game("3countb"), SharedShoulderMapping());

        // RB is an equally direct binding for both buttons, so nothing can choose between them,
        // and neither button's own text was written to describe RB specifically -- pressing it
        // does more than one thing, so showing either "Punch" or "Kick" there would be showing a
        // claim nobody made. Each button's own generic is unaffected.
        labels.LabelText.ShouldBeDictionaryOf(
            ("ButtonX", "Punch"),
            ("ButtonA", "Kick"));
        labels.LabelText.ContainsKey("ButtonRightShoulder").ShouldBeFalse();
        _logger.Received().Error(Arg.Is<string>(m =>
            m.Contains("ButtonRightShoulder") && m.Contains("BUTTON1") && m.Contains("BUTTON2")));
    }

    /// <summary>
    /// The rsgun shape: three buttons all fire together on one shared trigger (Sword), and each
    /// pair among them ALSO shares a second, distinct trigger of its own -- because all three
    /// individually reach Sword, every pairwise combination's intersection includes it too,
    /// alongside that pair's own generic.
    /// </summary>
    #pragma warning disable format
    private static ResolvedMapping ThreeWayOverlapMapping() => Mapping(
        platform: "Arcade",
        buttonToInput: new()
        {
            ["BUTTON1"] = ["ButtonX", "Sword", "ButtonY"],
            ["BUTTON2"] = ["ButtonA", "Sword", "ButtonY", "AxisTriggerLeft"],
            ["BUTTON3"] = ["ButtonB", "Sword", "AxisTriggerLeft"],
        });
    #pragma warning restore format

    [Fact]
    public void Load_CombinationsOverlapOnAGeneric_TheExactlyMatchingComboWins()
    {
        _loader.Load(Arg.Any<GameInfo>()).Returns(Labels(
            ("BUTTON1", "Vulcan"),
            ("BUTTON2", "Homing"),
            ("BUTTON3", "Spread"),
            ("BUTTON1 BUTTON2 BUTTON3", "Sword"),
            ("BUTTON1 BUTTON2", "Homing Plasma"),
            ("BUTTON2 BUTTON3", "Lock On Spread")));

        ResolvedLabels labels = BuildTestFixture.Load(Game("rsgun"), ThreeWayOverlapMapping());

        // All three buttons drive Sword, so the 3-button combo -- the only entry naming exactly
        // that set -- is its label; each 2-button combo's own distinct generic is unaffected.
        labels.LabelText.ShouldBeDictionaryOf(
            ("ButtonX", "Vulcan"),
            ("ButtonA", "Homing"),
            ("ButtonB", "Spread"),
            ("Sword", "Sword"),
            ("ButtonY", "Homing Plasma"),
            ("AxisTriggerLeft", "Lock On Spread"));
        _logger.DidNotReceive().Error(Arg.Any<string>());
    }

    [Fact]
    public void Load_NoEntryNamesTheFullSetOfButtonsSharingAGeneric_LogsAndLeavesItUnlabelled()
    {
        ResolvedMapping mapping = Mapping(platform: "Arcade", buttonToInput: new()
        {
            ["BUTTON1"] = ["ButtonX", "Shared"],
            ["BUTTON2"] = ["ButtonA", "Shared"],
            ["BUTTON3"] = ["ButtonB", "Shared"],
            ["BUTTON4"] = ["ButtonY", "Shared"],
        });
        _loader.Load(Arg.Any<GameInfo>()).Returns(Labels(
            ("BUTTON1 BUTTON2", "Combo A"),
            ("BUTTON3 BUTTON4", "Combo B")));

        ResolvedLabels labels = BuildTestFixture.Load(Game("rsgun"), mapping);

        // All four buttons drive Shared, but neither combo names that full set -- a label naming
        // a subset isn't a partial match, so showing either one would be showing a claim nobody
        // actually made.
        labels.LabelText.ContainsKey("Shared").ShouldBeFalse();
        _logger.Received().Error(Arg.Is<string>(m =>
            m.Contains("Shared") && m.Contains("BUTTON1") && m.Contains("BUTTON4")));
    }

    [Fact]
    public void Load_ThreeWayComboMissing_NoPairNamesTheFullSharedTrigger_LeavesItUnlabelled()
    {
        // rsgun with its "BUTTON1 BUTTON2 BUTTON3"="Sword" entry deleted: all three buttons still
        // drive Sword together, but every remaining entry names only two of them -- none is an
        // exact match, so Sword goes unlabelled rather than showing an unrelated pair's text.
        _loader.Load(Arg.Any<GameInfo>()).Returns(Labels(
            ("BUTTON1", "Vulcan"),
            ("BUTTON2", "Homing"),
            ("BUTTON3", "Spread"),
            ("BUTTON1 BUTTON2", "Homing Plasma"),
            ("BUTTON1 BUTTON3", "Back Wide"),
            ("BUTTON2 BUTTON3", "Lock On Spread")));

        ResolvedLabels labels = BuildTestFixture.Load(Game("rsgun"), ThreeWayOverlapMapping());

        labels.LabelText.ShouldBeDictionaryOf(
            ("ButtonX", "Vulcan"),
            ("ButtonA", "Homing"),
            ("ButtonB", "Spread"),
            ("ButtonY", "Homing Plasma"),
            ("AxisTriggerLeft", "Lock On Spread"));
        // Back Wide's own generic (ButtonRightShoulder) isn't in ThreeWayOverlapMapping's
        // reach lists, so it can't appear here either way -- ShouldBeDictionaryOf above already
        // pins the complete set, confirming Sword's absence alongside it.
        _logger.Received().Error(Arg.Is<string>(m => m.Contains("Sword")));
    }

    [Fact]
    public void Load_MirroredBindingDoesNotOverwriteTheInputsOwnLabel()
    {
        // the N64 shape: the pad mirrors its Dpad onto the left stick, which the platform already
        // drives with the console's own analog stick
        ResolvedMapping mapping = Mapping(platform: "Nintendo 64", buttonToInput: new()
        {
            ["Stick-Any"] = ["AxisLeftStick"],
            ["Dpad-Any"] = ["ButtonDpad", "AxisLeftStick"],
        });
        _loader.Load(Arg.Any<GameInfo>()).Returns(Labels(
            ("Stick-Any", "Look"),
            ("Dpad-Any", "Move")));

        ResolvedLabels labels = BuildTestFixture.Load(Game("Goldeneye 007"), mapping);

        // the stick is Stick-Any's own binding and only a mirrored one for Dpad-Any, so it keeps
        // its own label rather than being overwritten
        labels.LabelText.ShouldBeDictionaryOf(
            ("AxisLeftStick", "Look"),
            ("ButtonDpad", "Move"));
        _logger.DidNotReceive().Error(Arg.Any<string>());
    }

    // --- whole-direction collapse ---

    /// <summary>
    /// The MAME shape after WholeInputDeriver's extension: JOYSTICK carries its own whole-level
    /// claims (as it always has) plus, alongside them, every individual direction its siblings
    /// currently reach.
    /// </summary>
    #pragma warning disable format
    private static ResolvedMapping WholeJoystickWithBothFamilies() => Mapping(
        platform: "Arcade",
        buttonToInput: new()
        {
            ["JOYSTICK"] = [
                "ButtonDpad", "AxisLeftStick",
                "ButtonDpadUp", "AxisLeftStickUp", "ButtonDpadDown", "AxisLeftStickDown",
                "ButtonDpadLeft", "AxisLeftStickLeft", "ButtonDpadRight", "AxisLeftStickRight",
            ],
        });
    #pragma warning restore format

    [Fact]
    public void Load_AllFourDirectionsAgree_CollapseOntoBothWholes()
    {
        _loader.Load(Arg.Any<GameInfo>()).Returns(Labels(("JOYSTICK", "Move")));

        ResolvedLabels labels = BuildTestFixture.Load(Game("3on3dunk"), WholeJoystickWithBothFamilies());

        // Every direction of both families agrees ("Move" broadcasts from the one JOYSTICK
        // label), so both collapse onto their whole and none of the eight direction inputs
        // survive individually -- that's what makes the layout pick a single render instead of
        // four redundant per-direction ones for each control.
        labels.LabelText.ShouldBeDictionaryOf(
            ("ButtonDpad", "Move"),
            ("AxisLeftStick", "Move"));
    }

    [Fact]
    public void Load_OneDirectionSwappedOntoAnOrdinaryButton_ThatWholeStaysUncollapsed()
    {
        // BUTTON2 has been swapped onto ButtonDpadUp -- a real, independently-labelled button now
        // drives that one direction, while Down/Left/Right still only carry JOYSTICK's broadcast.
        ResolvedMapping mapping = Mapping(platform: "Arcade", buttonToInput: new()
        {
            ["JOYSTICK"] = [
                "ButtonDpad", "AxisLeftStick",
                "ButtonDpadUp", "AxisLeftStickUp", "ButtonDpadDown", "AxisLeftStickDown",
                "ButtonDpadLeft", "AxisLeftStickLeft", "ButtonDpadRight", "AxisLeftStickRight",
            ],
            ["BUTTON2"] = ["ButtonDpadUp"],
        });
        _loader.Load(Arg.Any<GameInfo>()).Returns(Labels(
            ("JOYSTICK", "Move"),
            ("BUTTON2", "Jump")));

        ResolvedLabels labels = BuildTestFixture.Load(Game("3on3dunk"), mapping);

        // BUTTON2's direct claim on ButtonDpadUp outranks JOYSTICK's derived one there, so the
        // four Dpad directions no longer all agree -- the whole stays uncollapsed and each
        // direction renders its own, correct text. AxisLeftStick is untouched by the swap, so it
        // still collapses normally.
        labels.LabelText.ShouldBeDictionaryOf(
            ("ButtonDpad", "Move"),
            ("ButtonDpadUp", "Jump"),
            ("ButtonDpadDown", "Move"),
            ("ButtonDpadLeft", "Move"),
            ("ButtonDpadRight", "Move"),
            ("AxisLeftStick", "Move"));
    }

    [Fact]
    public void Load_Combination_ClaimingAButtonsOnlyInput_TakesPrecedence()
    {
        ResolvedMapping mapping = Mapping(platform: "Arcade", buttonToInput: new()
        {
            ["BUTTON1"] = ["ButtonRightShoulder"],
            ["BUTTON2"] = ["ButtonA", "ButtonRightShoulder"],
        });
        _loader.Load(Arg.Any<GameInfo>()).Returns(Labels(
            ("BUTTON1", "Punch"),
            ("BUTTON2", "Kick"),
            ("BUTTON1 BUTTON2", "Power Move")));

        ResolvedLabels labels = BuildTestFixture.Load(Game("3countb"), mapping);

        // BUTTON1 drives nothing of its own, so its label has nowhere to go — that is correct,
        // since the only control it reaches performs the combined action
        labels.LabelText.ShouldBeDictionaryOf(
            ("ButtonRightShoulder", "Power Move"),
            ("ButtonA", "Kick"));
    }
}
