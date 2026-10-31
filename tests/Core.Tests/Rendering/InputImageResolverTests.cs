using DynamicControls.InputMapping;
using DynamicControls.Rendering;
using DynamicControls.Templates;
using NSubstitute;
using static DynamicControls.Core.TestHelpers.InputMapping.MappingFixtures;
using static DynamicControls.Core.TestHelpers.Templates.LayoutElements;
using static DynamicControls.Core.TestHelpers.Templates.TemplateFixtures;

namespace DynamicControls.Core.Tests.Rendering;

/// <summary>
/// Unit tests for <see cref="InputImageResolver"/>. Drives the three mapping-state branches
/// (Unmapped, MappedDefault, Remapped) and the asset-borrow case (<c>UseImageFile</c>) by
/// constructing <see cref="ResolvedMapping"/> with specific InputToButton / NaturalButtonToInput
/// shapes, and verifies the resulting image path via a substituted
/// <see cref="ITemplateImageSource"/>.
/// </summary>
public class InputImageResolverTests
{
    private const string Platform = "Sega Genesis";
    private const string Controller = "Pad";

    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly ITemplateImageSource _imageSource = Substitute.For<ITemplateImageSource>();
    private readonly InputImageResolver _underTest;

    public InputImageResolverTests()
    {
        _underTest = new InputImageResolver(_logger);
    }

    // ---- fixtures ----

    private Template TestTemplate() => TemplateOf(imageSource: _imageSource);

    private static ResolvedMapping Mapping(
        Dictionary<string, string>? inputToButton = null,
        Dictionary<string, IReadOnlyList<string>>? naturalButtonToInput = null) =>
        MappingOf(
            platform: Platform,
            controller: Controller,
            inputToButton: inputToButton,
            naturalButtonToInput: naturalButtonToInput);

    private void StubResolve(string src, string generic, string? styled = null) =>
        _imageSource.Resolve(src, Platform, Controller).Returns(new ResolvedImagePaths(generic, styled));

    // ---- a declared composite no button drives directly ----

    [Fact]
    public void Resolve_CompositeDrivenOnlyThroughItsParts_TakesTheCoveringButtonsImage()
    {
        // Nintendo 64's C cluster: the stick position is drawn by C-Any.png, the picture of the
        // four C buttons, rather than a generic stick. Once a platform button reaches the four
        // directions instead of naming the cluster, nothing drives AxisRightStick itself — and an
        // identity render of an unmapped input falls through to the generic image, silently,
        // because no label moves. The button whose reach covers every part stands in for it.
        var input = Input("AxisRightStick");
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "AxisRightStick.png");
        Template template = TemplateOf(imageSource: _imageSource, wholes: new Dictionary<string, IReadOnlyList<string>>()
        {
            ["AxisRightStick"] = ["AxisRightStickUp", "AxisRightStickDown", "AxisRightStickLeft", "AxisRightStickRight"],
        });
        ResolvedMapping mapping = MappingOf(
            platform: Platform, controller: Controller,
            buttonToInput: new Dictionary<string, IReadOnlyList<string>>
            {
                ["C-Any"] = ["AxisRightStickUp", "AxisRightStickDown", "AxisRightStickLeft", "AxisRightStickRight"],
            },
            naturalButtonToInput: new Dictionary<string, IReadOnlyList<string>>
            {
                ["C-Any"] = ["AxisRightStickUp", "AxisRightStickDown", "AxisRightStickLeft", "AxisRightStickRight"],
            });
        _imageSource.Resolve("AxisRightStick.png", Platform, Controller)
            .Returns(new ResolvedImagePaths("AxisRightStick.png", null));
        _imageSource.Resolve("C-Any.png", Platform, Controller)
            .Returns(new ResolvedImagePaths("C-Any.png", "platform/C-Any.png"));

        string path = _underTest.Resolve(image, input, mapping, template);

        path.ShouldBe("platform/C-Any.png");
    }

    // ---- a composite driven by several buttons together ----

    private static readonly string[] CParts = ["AxisRightStickLeft", "AxisRightStickRight"];

    /// <summary>Nintendo 64's C cluster as the mapping carries it: each C button driving its own
    /// direction, and C-Any, the control standing for all four, covering the pair too.</summary>
    private static ResolvedMapping CClusterMapping(string left = "C-Left", string right = "C-Right") =>
        MappingOf(
            platform: Platform, controller: Controller,
            buttonToInput: new Dictionary<string, IReadOnlyList<string>>
            {
                [left] = ["AxisRightStickLeft"],
                [right] = ["AxisRightStickRight"],
                ["C-Any"] = ["AxisRightStickUp", "AxisRightStickDown", "AxisRightStickLeft", "AxisRightStickRight"],
            },
            inputToButton: new Dictionary<string, string>
            {
                ["AxisRightStickLeft"] = left,
                ["AxisRightStickRight"] = right,
            },
            naturalButtonToInput: new Dictionary<string, IReadOnlyList<string>>
            {
                [left] = ["AxisRightStickLeft"],
                [right] = ["AxisRightStickRight"],
                ["C-Any"] = ["AxisRightStickUp", "AxisRightStickDown", "AxisRightStickLeft", "AxisRightStickRight"],
            });

    private Template CPairTemplate() =>
        TemplateOf(imageSource: _imageSource, wholes: new Dictionary<string, IReadOnlyList<string>>()
        {
            ["AxisRightStickX"] = CParts,
        });

    private static readonly InputImageDefinition CPairImage =
        new(X: 0, Y: 0, ImageFile: "AxisRightStickX.png", UseImageFile: "AxisLeftStickX.png");

    /// <summary>Every file the template has and the platform doesn't: later stubs name the ones it does.</summary>
    private void StubNothingStyled() =>
        _imageSource.Resolve(Arg.Any<string>(), Platform, Controller)
            .Returns(call => new ResolvedImagePaths(call.ArgAt<string>(0), null));

    [Fact]
    public void Resolve_CompositeDrivenByTwoButtons_TakesTheImageNamedForBoth()
    {
        StubNothingStyled();
        // C-Any covers the pair too, and used to win: its four-circle picture for two directions.
        // The picture of the two buttons that drive it is the better answer when the platform
        // has one, named for them in alphabetical order.
        _imageSource.Resolve("AxisLeftStickX.png", Platform, Controller)
            .Returns(new ResolvedImagePaths("AxisLeftStickX.png", null));
        _imageSource.Resolve("C-Left C-Right.png", Platform, Controller)
            .Returns(new ResolvedImagePaths("C-Left C-Right.png", "platform/C-Left C-Right.png"));
        _imageSource.Resolve("C-Any.png", Platform, Controller)
            .Returns(new ResolvedImagePaths("C-Any.png", "platform/C-Any.png"));

        string path = _underTest.Resolve(CPairImage, Input("AxisRightStickX"), CClusterMapping(), CPairTemplate());

        path.ShouldBe("platform/C-Left C-Right.png");
        // and the debug log names the buttons, not a classification that played no part
        _logger.Received().Debug("Button image: AxisRightStickX <- C-Left C-Right (the buttons driving its parts)");
        _logger.DidNotReceive().Debug(Arg.Is<string>(m => m.Contains("(no platform mapping)")));
    }

    [Fact]
    public void Resolve_VerticalCompositeDrivenByTwoButtons_NamesThemByName()
    {
        // The up-down pair is its own Whole. The buttons are named in alphabetical order, so Down
        // comes before Up, whatever order the template lists the parts in.
        StubNothingStyled();
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "AxisRightStickY.png", UseImageFile: "AxisLeftStickY.png");
        Template template = TemplateOf(imageSource: _imageSource, wholes: new Dictionary<string, IReadOnlyList<string>>()
        {
            ["AxisRightStickY"] = ["AxisRightStickUp", "AxisRightStickDown"],
        });
        ResolvedMapping mapping = MappingOf(
            platform: Platform, controller: Controller,
            buttonToInput: new Dictionary<string, IReadOnlyList<string>>
            {
                ["C-Down"] = ["AxisRightStickDown"],
                ["C-Up"] = ["AxisRightStickUp"],
            },
            inputToButton: new Dictionary<string, string>
            {
                ["AxisRightStickDown"] = "C-Down",
                ["AxisRightStickUp"] = "C-Up",
            });
        _imageSource.Resolve("C-Down C-Up.png", Platform, Controller)
            .Returns(new ResolvedImagePaths("C-Down C-Up.png", "platform/C-Down C-Up.png"));

        string path = _underTest.Resolve(image, Input("AxisRightStickY"), mapping, template);

        path.ShouldBe("platform/C-Down C-Up.png");
    }

    [Fact]
    public void Resolve_CompositeDrivenByTwoButtonsWithNoNamedImage_KeepsTheCoveringButtonsImage()
    {
        StubNothingStyled();
        // Nothing named for the pair: what a platform without that art drew before is unchanged.
        _imageSource.Resolve("AxisLeftStickX.png", Platform, Controller)
            .Returns(new ResolvedImagePaths("AxisLeftStickX.png", null));
        _imageSource.Resolve("C-Left C-Right.png", Platform, Controller)
            .Returns(new ResolvedImagePaths("C-Left C-Right.png", null));
        _imageSource.Resolve("C-Any.png", Platform, Controller)
            .Returns(new ResolvedImagePaths("C-Any.png", "platform/C-Any.png"));

        string path = _underTest.Resolve(CPairImage, Input("AxisRightStickX"), CClusterMapping(), CPairTemplate());

        path.ShouldBe("platform/C-Any.png");
    }

    [Fact]
    public void Resolve_PairWithItsButtonsSwapped_DrawsTheSameImage()
    {
        // C-Right now drives the left direction and C-Left the right one. The two buttons are the
        // same, so the picture of them is too: the name is the sorted set of buttons, not which
        // part each drives this game.
        StubNothingStyled();
        _imageSource.Resolve("C-Left C-Right.png", Platform, Controller)
            .Returns(new ResolvedImagePaths("C-Left C-Right.png", "platform/C-Left C-Right.png"));
        ResolvedMapping swapped = MappingOf(
            platform: Platform, controller: Controller,
            buttonToInput: new Dictionary<string, IReadOnlyList<string>>
            {
                ["C-Left"] = ["AxisRightStickRight"],
                ["C-Right"] = ["AxisRightStickLeft"],
            },
            inputToButton: new Dictionary<string, string>
            {
                ["AxisRightStickLeft"] = "C-Right",
                ["AxisRightStickRight"] = "C-Left",
            });

        string path = _underTest.Resolve(CPairImage, Input("AxisRightStickX"), swapped, CPairTemplate());

        path.ShouldBe("platform/C-Left C-Right.png");
    }

    [Fact]
    public void Resolve_PairNamedForButtonsOfUnrelatedNames_SortsThemOrdinally()
    {
        // Any two buttons name a pair, not only a platform's C buttons: the name is the ordinal
        // sort, whichever drives which part.
        StubNothingStyled();
        ResolvedMapping mapping = MappingOf(
            platform: Platform, controller: Controller,
            buttonToInput: new Dictionary<string, IReadOnlyList<string>>
            {
                ["Z"] = ["AxisRightStickLeft"],
                ["A"] = ["AxisRightStickRight"],
            },
            inputToButton: new Dictionary<string, string>
            {
                ["AxisRightStickLeft"] = "Z",
                ["AxisRightStickRight"] = "A",
            });

        _underTest.Resolve(CPairImage, Input("AxisRightStickX"), mapping, CPairTemplate());

        _imageSource.Received(1).Resolve("A Z.png", Platform, Controller);
    }

    [Fact]
    public void Resolve_CompositeWithOnlySomeOfItsPartsDriven_DoesNotLookForANamedCombination()
    {
        // Three of four parts have a button, three different ones; the fourth has none. A name
        // for those three would picture a control that is missing a direction.
        StubNothingStyled();
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "ButtonDpad.png");
        Template template = TemplateOf(imageSource: _imageSource, wholes: new Dictionary<string, IReadOnlyList<string>>()
        {
            ["ButtonDpad"] = ["ButtonDpadUp", "ButtonDpadDown", "ButtonDpadLeft", "ButtonDpadRight"],
        });
        ResolvedMapping mapping = MappingOf(
            platform: Platform, controller: Controller,
            buttonToInput: new Dictionary<string, IReadOnlyList<string>>
            {
                ["Dpad-Up"] = ["ButtonDpadUp"],
                ["Dpad-Down"] = ["ButtonDpadDown"],
                ["Dpad-Left"] = ["ButtonDpadLeft"],
            },
            inputToButton: new Dictionary<string, string>
            {
                ["ButtonDpadUp"] = "Dpad-Up",
                ["ButtonDpadDown"] = "Dpad-Down",
                ["ButtonDpadLeft"] = "Dpad-Left",
            });

        _underTest.Resolve(image, Input("ButtonDpad"), mapping, template);

        _imageSource.DidNotReceive().Resolve(Arg.Is<string>(s => s.Contains(' ')), Platform, Controller);
    }

    [Fact]
    public void Resolve_CompositeDrivenByOneButton_DoesNotLookForANamedCombination()
    {
        // One button is the covering button's business; a "name" of one button would only be
        // that button's own image looked up a second time.
        var input = Input("AxisRightStick");
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "AxisRightStick.png");
        Template template = TemplateOf(imageSource: _imageSource, wholes: new Dictionary<string, IReadOnlyList<string>>()
        {
            ["AxisRightStick"] = ["AxisRightStickLeft", "AxisRightStickRight"],
        });
        ResolvedMapping mapping = MappingOf(
            platform: Platform, controller: Controller,
            buttonToInput: new Dictionary<string, IReadOnlyList<string>> { ["C-Any"] = CParts },
            inputToButton: new Dictionary<string, string> { ["AxisRightStickLeft"] = "C-Any", ["AxisRightStickRight"] = "C-Any" },
            naturalButtonToInput: new Dictionary<string, IReadOnlyList<string>> { ["C-Any"] = CParts });
        _imageSource.Resolve("AxisRightStick.png", Platform, Controller)
            .Returns(new ResolvedImagePaths("AxisRightStick.png", null));
        _imageSource.Resolve("C-Any.png", Platform, Controller)
            .Returns(new ResolvedImagePaths("C-Any.png", "platform/C-Any.png"));

        string path = _underTest.Resolve(image, input, mapping, template);

        path.ShouldBe("platform/C-Any.png");
        _imageSource.Received(1).Resolve("C-Any.png", Platform, Controller);
        // the cover chose it, so the log names no combination
        _logger.DidNotReceive().Debug(Arg.Is<string>(m => m.Contains("the buttons driving its parts")));
    }

    [Fact]
    public void Resolve_CompositeWithOnePartUndriven_DoesNotLookForANamedCombination()
    {
        StubNothingStyled();
        // A part no button drives leaves nothing to name the pair by.
        ResolvedMapping mapping = CClusterMapping() with
        {
            InputToButton = new Dictionary<string, string> { ["AxisRightStickLeft"] = "C-Left" },
        };
        _imageSource.Resolve("AxisLeftStickX.png", Platform, Controller)
            .Returns(new ResolvedImagePaths("AxisLeftStickX.png", null));

        _underTest.Resolve(CPairImage, Input("AxisRightStickX"), mapping, CPairTemplate());

        _imageSource.DidNotReceive().Resolve(Arg.Is<string>(s => s.Contains(' ')), Platform, Controller);
    }

    [Fact]
    public void Resolve_CompositeNamedForButtonsWithPathCharacters_LooksUpASafeFileName()
    {
        StubNothingStyled();
        // Arcade buttons are MAME port names (OTHER:STROBE1:2); a path can't hold every character
        // a name can, and the slash is the one no filesystem does.
        _imageSource.Resolve("AxisLeftStickX.png", Platform, Controller)
            .Returns(new ResolvedImagePaths("AxisLeftStickX.png", null));

        _underTest.Resolve(CPairImage, Input("AxisRightStickX"),
            CClusterMapping(left: "P1/A", right: "P1/B"), CPairTemplate());

        _imageSource.Received(1).Resolve("P1_A P1_B.png", Platform, Controller);
    }

    [Fact]
    public void Resolve_ButtonDrivesTheCompositeDirectly_DoesNotLookForANamedCombination()
    {
        StubNothingStyled();
        // A button bound straight at the whole stands for it outright.
        ResolvedMapping mapping = CClusterMapping() with
        {
            InputToButton = new Dictionary<string, string>
            {
                ["AxisRightStickX"] = "C-Any",
                ["AxisRightStickLeft"] = "C-Left",
                ["AxisRightStickRight"] = "C-Right",
            },
        };
        _imageSource.Resolve("C-Any.png", Platform, Controller)
            .Returns(new ResolvedImagePaths("C-Any.png", "platform/C-Any.png"));

        string path = _underTest.Resolve(CPairImage, Input("AxisRightStickX"), mapping, CPairTemplate());

        path.ShouldBe("platform/C-Any.png");
        _imageSource.DidNotReceive().Resolve("C-Left C-Right.png", Platform, Controller);
    }

    [Fact]
    public void Resolve_CompositeCoveredByMoreThanOneButton_IsUnmapped()
    {
        // Two buttons each reach every part -- an analogToDigital mirror puts the directions
        // under a second button alongside the first. Neither one stands for the cluster on its
        // own, so there is nothing to borrow a picture from and the generic is right.
        var input = Input("AxisRightStick");
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "AxisRightStick.png");
        Template template = TemplateOf(imageSource: _imageSource, wholes: new Dictionary<string, IReadOnlyList<string>>()
        {
            ["AxisRightStick"] = ["AxisRightStickUp", "AxisRightStickDown"],
        });
        ResolvedMapping mapping = MappingOf(
            platform: Platform, controller: Controller,
            buttonToInput: new Dictionary<string, IReadOnlyList<string>>
            {
                ["C-Any"] = ["AxisRightStickUp", "AxisRightStickDown"],
                ["Dpad-Any"] = ["AxisRightStickUp", "AxisRightStickDown"],
            });
        _imageSource.Resolve("AxisRightStick.png", Platform, Controller)
            .Returns(new ResolvedImagePaths("AxisRightStick.png", "platform/AxisRightStick.png"));

        string path = _underTest.Resolve(image, input, mapping, template);

        path.ShouldBe("AxisRightStick.png");
    }

    [Fact]
    public void Resolve_CompositeWhosePartsSplitAcrossButtons_IsUnmapped()
    {
        // A remap moved the directions onto different buttons, so no single one stands for the
        // cluster. Falling back to any of them would picture a control that is no longer there.
        var input = Input("AxisRightStick");
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "AxisRightStick.png");
        Template template = TemplateOf(imageSource: _imageSource, wholes: new Dictionary<string, IReadOnlyList<string>>()
        {
            ["AxisRightStick"] = ["AxisRightStickUp", "AxisRightStickDown"],
        });
        ResolvedMapping mapping = MappingOf(
            platform: Platform, controller: Controller,
            buttonToInput: new Dictionary<string, IReadOnlyList<string>>
            {
                ["C-Up"] = ["AxisRightStickUp"],
                ["C-Down"] = ["AxisRightStickDown"],
            });
        _imageSource.Resolve("AxisRightStick.png", Platform, Controller)
            .Returns(new ResolvedImagePaths("AxisRightStick.png", "platform/AxisRightStick.png"));

        string path = _underTest.Resolve(image, input, mapping, template);

        // Unmapped identity render: the plain generic, never the platform-styled variant
        path.ShouldBe("AxisRightStick.png");
    }

    // ---- Unmapped: input not in mapping ----

    [Fact]
    public void Resolve_Unmapped_NoUseImageFile_ReturnsGeneric()
    {
        // given an input that the mapping doesn't drive (Unmapped) and a render whose
        // ImageFile resolves to a generic but no styled variant
        var input = Input("ButtonA");
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "ButtonA.png");
        StubResolve("ButtonA.png", generic: "/template/ButtonA.png", styled: "/template/styled/ButtonA.png");

        // when resolving against an empty mapping (input is unmapped)
        var path = _underTest.Resolve(image, input, Mapping(), TestTemplate());

        // then the styled variant is NOT used — an identity render for an absent button must
        // not show platform-specific artwork
        path.ShouldBe("/template/ButtonA.png");
    }

    [Fact]
    public void Resolve_Unmapped_WithUseImageFile_PrefersStyled_BorrowedAssetGetsPlatformVariant()
    {
        // given a borrow render (UseImageFile set) for an input not on this controller
        var input = Input("ButtonA");
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "ButtonA.png", UseImageFile: "Stick.png");
        StubResolve("Stick.png", generic: "/template/Stick.png", styled: "/template/styled/Stick.png");

        // when resolving
        var path = _underTest.Resolve(image, input, Mapping(), TestTemplate());

        // then the styled variant of the borrowed asset is used — a borrow gets its platform-
        // specific artwork even when the owning input isn't mapped
        path.ShouldBe("/template/styled/Stick.png");
    }

    [Fact]
    public void Resolve_Unmapped_WithUseImageFile_FallsBackToGeneric_WhenNoStyled()
    {
        // given a borrow render and no platform-specific variant of the borrowed asset
        var input = Input("ButtonA");
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "ButtonA.png", UseImageFile: "Stick.png");
        StubResolve("Stick.png", generic: "/template/Stick.png");

        // when resolving
        var path = _underTest.Resolve(image, input, Mapping(), TestTemplate());

        // then the generic borrowed asset is used
        path.ShouldBe("/template/Stick.png");
    }

    // ---- MappedDefault: button drives the input naturally ----

    [Fact]
    public void Resolve_MappedDefault_PrefersPlatformButtonStyledImage_OverGeneric()
    {
        // given an input driven by its natural platform button (e.g. Cross naturally drives
        // ButtonA on this controller)
        var input = Input("ButtonA");
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "ButtonA.png");
        var mapping = Mapping(
            inputToButton: new() { ["ButtonA"] = "Cross" },
            naturalButtonToInput: new() { ["Cross"] = ["ButtonA"] });
        // outer call (for ButtonA.png) — generic ButtonA
        StubResolve("ButtonA.png", generic: "/template/ButtonA.png");
        // inner platform-named call (for Cross.png) — styled Cross
        StubResolve("Cross.png", generic: "/template/Cross.png", styled: "/template/styled/Cross.png");

        // when resolving
        var path = _underTest.Resolve(image, input, mapping, TestTemplate());

        // then the platform-button-named styled file wins over the input's own image
        path.ShouldBe("/template/styled/Cross.png");
    }

    [Fact]
    public void Resolve_MappedDefault_FallsBackToGeneric_WhenNoPlatformNamedStyledFile()
    {
        // given a MappedDefault input but no styled variant for the platform-button name
        var input = Input("ButtonA");
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "ButtonA.png");
        var mapping = Mapping(
            inputToButton: new() { ["ButtonA"] = "Cross" },
            naturalButtonToInput: new() { ["Cross"] = ["ButtonA"] });
        StubResolve("ButtonA.png", generic: "/template/ButtonA.png");
        StubResolve("Cross.png", generic: "/template/Cross.png"); // no styled

        // when resolving
        var path = _underTest.Resolve(image, input, mapping, TestTemplate());

        // then the outer generic (the input's own ImageFile) is used as the fallback
        path.ShouldBe("/template/ButtonA.png");
    }

    [Fact]
    public void Resolve_MappedDefault_ButtonAbsentFromNaturalMapping_TreatedAsDefault()
    {
        // given an input driven by a button that has no entry in NaturalButtonToInput at all
        // (e.g. a custom or unknown controller button) — can't determine it's a remap, so treat as default
        var input = Input("ButtonA");
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "ButtonA.png");
        var mapping = Mapping(
            inputToButton: new() { ["ButtonA"] = "Cross" });
        // naturalButtonToInput is empty — Cross is absent
        StubResolve("ButtonA.png", generic: "/template/ButtonA.png");
        StubResolve("Cross.png", generic: "/template/Cross.png", styled: "/template/styled/Cross.png");

        // when resolving
        var path = _underTest.Resolve(image, input, mapping, TestTemplate());

        // then the platform-button-named styled file is used, same as an unambiguous MappedDefault
        path.ShouldBe("/template/styled/Cross.png");
    }

    // ---- Remapped: button drives a different input than it does by default ----

    [Fact]
    public void Resolve_Remapped_PrefersRemappedPlatformButtonStyledImage()
    {
        // given a button (Cross) currently driving ButtonA, but Cross naturally drives ButtonB
        // on this controller — image follows the physical button, so the player sees the actual
        // button they pressed rather than the logical role it's been redirected to
        var input = Input("ButtonA");
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "ButtonA.png");
        var mapping = Mapping(
            inputToButton: new() { ["ButtonA"] = "Cross" },
            naturalButtonToInput: new() { ["Cross"] = ["ButtonB"] });
        StubResolve("ButtonA.png", generic: "/template/ButtonA.png");
        StubResolve("Cross.png", generic: "/template/Cross.png", styled: "/template/styled/Cross.png");

        // when resolving
        var path = _underTest.Resolve(image, input, mapping, TestTemplate());

        // then the styled artwork for the physical button (Cross) wins
        path.ShouldBe("/template/styled/Cross.png");
    }

    [Fact]
    public void Resolve_RemappedOrdinaryInput_IsClassifiedRemapped_NotMappedDefault()
    {
        // The two states resolve the same file -- both look up {platformButton}.png -- so every
        // other test here passes under either classification. The diagnostic is the only place
        // they differ, and it is what this reads.
        //
        // Regression guard: the composite check added alongside <Whole> was `parts.All(...)`,
        // which is vacuously true for the empty parts list every ordinary input has. That made
        // each one a MappedDefault and left Remapped unreachable, with nothing red to say so.
        var input = Input("ButtonA");
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "ButtonA.png");
        var mapping = Mapping(
            inputToButton: new() { ["ButtonA"] = "Cross" },
            naturalButtonToInput: new() { ["Cross"] = ["ButtonB"] });
        StubResolve("ButtonA.png", generic: "/template/ButtonA.png");
        StubResolve("Cross.png", generic: "/template/Cross.png", styled: "/template/styled/Cross.png");

        _underTest.Resolve(image, input, mapping, TestTemplate());

        _logger.Received().Debug(Arg.Is<string>(s => s.Contains("ButtonA") && s.Contains("(remapped)")));
        _logger.DidNotReceive().Debug(Arg.Is<string>(s => s.Contains("ButtonA") && s.Contains("(not remapped)")));
    }

    [Fact]
    public void Resolve_Remapped_FallsBackToGeneric_WhenNoPlatformNamedStyledFile()
    {
        // given a Remapped input but no platform-specific file
        var input = Input("ButtonA");
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "ButtonA.png");
        var mapping = Mapping(
            inputToButton: new() { ["ButtonA"] = "Cross" },
            naturalButtonToInput: new() { ["Cross"] = ["ButtonB"] });
        StubResolve("ButtonA.png", generic: "/template/ButtonA.png");
        StubResolve("Cross.png", generic: "/template/Cross.png"); // no styled

        // when resolving
        var path = _underTest.Resolve(image, input, mapping, TestTemplate());

        // then the owning render's own generic is used as the fallback (so the player at least
        // sees the input's intended artwork rather than nothing)
        path.ShouldBe("/template/ButtonA.png");
    }

    // ---- default arms (unknown MappingState subtype) ----

    [Fact]
    public void SelectImagePath_UnknownState_Throws()
    {
        // given an ILayoutElement subtype that the switch does not handle
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "ButtonA.png");
        var mapping = Mapping();

        // when SelectImagePath is called with the unknown state
        // then an InvalidOperationException is thrown
        Should.Throw<InvalidOperationException>(() =>
            _underTest.SelectImagePath(new UnknownState(), image, "generic.png", null, mapping, TestTemplate()));
    }

    [Fact]
    public void DescribeResolution_UnknownState_Throws()
    {
        // given an unknown MappingState subtype
        // when DescribeResolution is called with it
        // then an InvalidOperationException is thrown
        Should.Throw<InvalidOperationException>(() =>
            _underTest.DescribeResolution(new UnknownState(), "ButtonA"));
    }

    private record UnknownState : MappingState;
}
