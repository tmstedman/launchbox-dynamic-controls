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

    /// <summary>Gives <paramref name="mapping"/>'s controller a button the platform has a picture
    /// of, as Genesis and PS2 have and Arcade hasn't: such a platform pictures its emulated
    /// controller, so a remapped button with no picture shows what it normally drives.</summary>
    private ResolvedMapping OnAPlatformPicturingItsButtons(ResolvedMapping mapping)
    {
        StubResolve("Start.png", generic: "/template/Start.png", styled: "/template/styled/Start.png");
        return mapping with
        {
            NaturalButtonToInput = new Dictionary<string, IReadOnlyList<string>>(mapping.NaturalButtonToInput)
            {
                ["Start"] = ["ButtonStart"],
            },
        };
    }

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
            .Returns(new ResolvedImagePaths("C-Left C-Right.png", null) { GenericExists = false });
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

    // ---- an image the template doesn't have ----

    [Fact]
    public void Resolve_GenericIsAPlaceholder_LogsAnError()
    {
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "ButtonA.png");
        _imageSource.Resolve("ButtonA.png", Platform, Controller)
            .Returns(new ResolvedImagePaths("t/ButtonA.png", null) { GenericExists = false });

        string path = _underTest.Resolve(image, Input("ButtonA"), Mapping(), TestTemplate());

        path.ShouldBe("t/ButtonA.png");
        _logger.Received(1).Error("Image not found: t/ButtonA.png");
    }

    [Fact]
    public void Resolve_ChosenImageIsPlatformStyled_LogsNothingEvenIfTheGenericIsAbsent()
    {
        // A platform file with no generic counterpart is fine; only the image drawn matters.
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "ButtonA.png");
        _imageSource.Resolve("ButtonA.png", Platform, Controller)
            .Returns(new ResolvedImagePaths("t/ButtonA.png", "platform/ButtonA.png") { GenericExists = false });
        _imageSource.Resolve("B.png", Platform, Controller)
            .Returns(new ResolvedImagePaths("t/B.png", "platform/B.png") { GenericExists = false });
        ResolvedMapping mapping = Mapping(
            inputToButton: new() { ["ButtonA"] = "B" },
            naturalButtonToInput: new() { ["B"] = ["ButtonA"] });

        string path = _underTest.Resolve(image, Input("ButtonA"), mapping, TestTemplate());

        path.ShouldBe("platform/B.png");
        _logger.DidNotReceive().Error(Arg.Any<string>());
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
        // given a Remapped input on a platform with art of its own, but no platform-specific file
        var input = Input("ButtonA");
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "ButtonA.png");
        var mapping = Mapping(
            inputToButton: new() { ["ButtonA"] = "Cross" },
            naturalButtonToInput: new() { ["Cross"] = ["ButtonB"] });
        mapping = OnAPlatformPicturingItsButtons(mapping);
        StubResolve("ButtonA.png", generic: "/template/ButtonA.png");
        StubResolve("Cross.png", generic: "/template/Cross.png"); // no styled
        _imageSource.Resolve("ButtonB.png", Platform, Controller)  // nor a picture of what Cross drives
            .Returns(new ResolvedImagePaths("/template/ButtonB.png", null) { GenericExists = false });

        // when resolving
        var path = _underTest.Resolve(image, input, mapping, TestTemplate());

        // then the owning render's own generic is the last resort (so the player at least sees
        // the input's intended artwork rather than nothing)
        path.ShouldBe("/template/ButtonA.png");
    }

    [Fact]
    public void Resolve_Remapped_NoPlatformFileForThePhysicalButton_ShowsWhatItNormallyDrives()
    {
        // given Dpad-Left swapped onto the position ButtonA draws, on a platform with art of its
        // own but no Dpad-Left.png (a d-pad direction has no face-button picture)
        var input = Input("ButtonA");
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "ButtonA.png");
        var mapping = Mapping(
            inputToButton: new() { ["ButtonA"] = "Dpad-Left" },
            naturalButtonToInput: new() { ["Dpad-Left"] = ["ButtonDpadLeft", "AxisLeftStickLeft"] });
        mapping = OnAPlatformPicturingItsButtons(mapping);
        StubResolve("ButtonA.png", generic: "/template/ButtonA.png");
        _imageSource.Resolve("Dpad-Left.png", Platform, Controller)
            .Returns(new ResolvedImagePaths("/template/Dpad-Left.png", null) { GenericExists = false });
        StubResolve("ButtonDpadLeft.png", generic: "/template/ButtonDpadLeft.png");

        // when resolving
        var path = _underTest.Resolve(image, input, mapping, TestTemplate());

        // then the player sees the left arrow they are pressing, not the A glyph of the slot it
        // landed in -- the same swap in the other direction already shows B at the d-pad's spot,
        // because B.png exists
        path.ShouldBe("/template/ButtonDpadLeft.png");
    }

    [Fact]
    public void Resolve_Remapped_OntoAPositionThePlatformHasNoButtonAt_ShowsWhatItNormallyDrives()
    {
        // given Dpad-Left swapped onto LT, where the platform's pad has no button of its own
        var input = Input("AxisTriggerLeft");
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "AxisTriggerLeft.png");
        var mapping = Mapping(
            inputToButton: new() { ["AxisTriggerLeft"] = "Dpad-Left" },
            naturalButtonToInput: new() { ["Dpad-Left"] = ["ButtonDpadLeft"] });
        mapping = OnAPlatformPicturingItsButtons(mapping);
        StubResolve("AxisTriggerLeft.png", generic: "/template/AxisTriggerLeft.png");
        _imageSource.Resolve("Dpad-Left.png", Platform, Controller)
            .Returns(new ResolvedImagePaths("/template/Dpad-Left.png", null) { GenericExists = false });
        StubResolve("ButtonDpadLeft.png", generic: "/template/ButtonDpadLeft.png");

        // when resolving
        var path = _underTest.Resolve(image, input, mapping, TestTemplate());

        // then it is the same arrow as on a face button: the picture follows the button, not
        // whether the platform has a button where it landed
        path.ShouldBe("/template/ButtonDpadLeft.png");
    }

    [Fact]
    public void Resolve_Remapped_PlatformWithNoArtOfItsOwn_KeepsThePositionsOwnGeneric()
    {
        // an arcade cfg rebinding: the X position is driven by a button that naturally drives Y,
        // on a platform with no art. Its overlay pictures the player's own pad, so the position's
        // own glyph is the right one -- and Y is only the cabinet default's guess at that button
        var input = Input("ButtonX");
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "ButtonX.png");
        var mapping = Mapping(
            inputToButton: new() { ["ButtonX"] = "P1_BUTTON4" },
            naturalButtonToInput: new() { ["P1_BUTTON4"] = ["ButtonY"] });
        StubResolve("ButtonX.png", generic: "/template/ButtonX.png");
        StubResolve("ButtonY.png", generic: "/template/ButtonY.png");
        StubResolve("P1_BUTTON4.png", generic: "/template/P1_BUTTON4.png");

        var path = _underTest.Resolve(image, input, mapping, TestTemplate());

        path.ShouldBe("/template/ButtonX.png");
    }

    [Fact]
    public void Resolve_Remapped_PlatformFolderHoldingOnlyACombinedPicture_KeepsThePositionsOwnGeneric()
    {
        // an Arcade folder holding one CombinedImage picture (named after MAME ports) and no
        // picture of any single button: the overlay still pictures the player's own pad
        const string root = @"C:\plugin";
        string templateDir = Path.Combine(root, "Templates", "T");
        IFileSystem fs = TestFs.Create();
        fs.DirectoryExists(Path.Combine(templateDir, "Arcade")).Returns(true);
        fs.FileExists(Path.Combine(templateDir, "Arcade", "P1_BUTTON1 P1_BUTTON2.png")).Returns(true);
        fs.FileExists(Path.Combine(templateDir, "ButtonX.png")).Returns(true);
        fs.FileExists(Path.Combine(templateDir, "ButtonY.png")).Returns(true);
        var resolver = new TemplateImageResolver(_logger, fs, Substitute.For<IImageHeader>(), root);
        Template template = TemplateOf(imageSource: new TemplateImageSource(resolver, "T"));
        // a cfg rebinding P1_BUTTON4, which Controllers.xml guesses is ButtonY, onto X
        ResolvedMapping mapping = MappingOf(
            platform: "Arcade", controller: null,
            inputToButton: new Dictionary<string, string> { ["ButtonX"] = "P1_BUTTON4" },
            naturalButtonToInput: new Dictionary<string, IReadOnlyList<string>> { ["P1_BUTTON4"] = ["ButtonY"] });

        var path = _underTest.Resolve(new InputImageDefinition(X: 0, Y: 0, ImageFile: "ButtonX.png"), Input("ButtonX"), mapping, template);

        path.ShouldBe(Path.Combine(templateDir, "ButtonX.png"));
    }

    [Fact]
    public void Resolve_RemappedBorrowingRender_NoPlatformFile_KeepsTheBorrowedGeneric()
    {
        // a render that borrows another input's artwork (useImage) draws that asset, not a
        // picture of its own position, so it has no "what the button normally drives" to show
        var input = Input("AxisRightStickLeft");
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "AxisRightStickLeft.png", UseImageFile: "AxisLeftStickLeft.png");
        var mapping = Mapping(
            inputToButton: new() { ["AxisRightStickLeft"] = "Dpad-Left" },
            naturalButtonToInput: new() { ["Dpad-Left"] = ["ButtonDpadLeft"] });
        mapping = OnAPlatformPicturingItsButtons(mapping);
        StubResolve("AxisLeftStickLeft.png", generic: "/template/AxisLeftStickLeft.png");
        _imageSource.Resolve("Dpad-Left.png", Platform, Controller)
            .Returns(new ResolvedImagePaths("/template/Dpad-Left.png", null) { GenericExists = false });
        StubResolve("ButtonDpadLeft.png", generic: "/template/ButtonDpadLeft.png");

        var path = _underTest.Resolve(image, input, mapping, TestTemplate());

        path.ShouldBe("/template/AxisLeftStickLeft.png");
    }

    // ---- fixedImage: a picture that is all that says which control it is ----

    private static InputImageDefinition FixedImageOf(string inputName) =>
        new(X: 0, Y: 0, ImageFile: $"{inputName}.png", FixedImage: true);

    /// <summary>A template some of whose renders draw these inputs with fixedImage.</summary>
    private Template TemplateWithFixedImages(params string[] inputNames) =>
        TemplateOf(imageSource: _imageSource, fixedImageInputs: new HashSet<string>(inputNames));

    [Fact]
    public void Resolve_FixedImageRemapped_DrawsThePlatformArtOfTheButtonUsuallyThere()
    {
        // Nintendo 64 with C-Up and C-Down swapped: the right-stick list's up row is pressed as
        // C-Down now, but its icon is all that says "up", so it keeps the C-Up picture
        var input = Input("AxisRightStickUp");
        ResolvedMapping mapping = MappingOf(
            platform: Platform, controller: Controller,
            inputToButton: new Dictionary<string, string> { ["AxisRightStickUp"] = "C-Down" },
            naturalButtonToInput: new Dictionary<string, IReadOnlyList<string>>
            {
                ["C-Up"] = ["AxisRightStickUp"],
                ["C-Down"] = ["AxisRightStickDown"],
            },
            naturalInputToButton: new Dictionary<string, string> { ["AxisRightStickUp"] = "C-Up" });
        StubResolve("AxisRightStickUp.png", generic: "/template/AxisRightStickUp.png");
        StubResolve("C-Up.png", generic: "/template/C-Up.png", styled: "/template/Nintendo 64/C-Up.png");
        StubResolve("C-Down.png", generic: "/template/C-Down.png", styled: "/template/Nintendo 64/C-Down.png");
        Template template = TemplateWithFixedImages("AxisRightStickUp", "AxisRightStickDown");

        var path = _underTest.Resolve(FixedImageOf("AxisRightStickUp"), input, mapping, template);

        path.ShouldBe("/template/Nintendo 64/C-Up.png");
    }

    [Fact]
    public void Resolve_FixedImageRemapped_NoArtForTheButtonUsuallyThere_KeepsItsOwnGeneric()
    {
        // Crash Bandicoot's up/down hat swap on PS2, which has art but none for the d-pad: the up
        // row stays an up arrow, where an unfixed one would draw the down arrow its button sends
        var input = Input("ButtonDpadUp");
        ResolvedMapping mapping = MappingOf(
            platform: Platform, controller: Controller,
            inputToButton: new Dictionary<string, string> { ["ButtonDpadUp"] = "Dpad-Down" },
            naturalButtonToInput: new Dictionary<string, IReadOnlyList<string>>
            {
                ["Dpad-Up"] = ["ButtonDpadUp"],
                ["Dpad-Down"] = ["ButtonDpadDown"],
            },
            naturalInputToButton: new Dictionary<string, string> { ["ButtonDpadUp"] = "Dpad-Up" });
        mapping = OnAPlatformPicturingItsButtons(mapping);
        StubResolve("ButtonDpadUp.png", generic: "/template/ButtonDpadUp.png");
        StubResolve("ButtonDpadDown.png", generic: "/template/ButtonDpadDown.png");
        StubResolve("Dpad-Up.png", generic: "/template/Dpad-Up.png");
        StubResolve("Dpad-Down.png", generic: "/template/Dpad-Down.png");
        Template template = TemplateWithFixedImages("ButtonDpadUp", "ButtonDpadDown");

        var path = _underTest.Resolve(FixedImageOf("ButtonDpadUp"), input, mapping, template);

        path.ShouldBe("/template/ButtonDpadUp.png");
    }

    [Fact]
    public void Resolve_FixedImageRemappedOntoAPositionWithNoButtonOfItsOwn_KeepsItsOwnGeneric()
    {
        // drawn as with nothing remapped, where no button drives it at all
        var input = Input("AxisTriggerLeft");
        ResolvedMapping mapping = MappingOf(
            platform: Platform, controller: Controller,
            inputToButton: new Dictionary<string, string> { ["AxisTriggerLeft"] = "Dpad-Left" },
            naturalButtonToInput: new Dictionary<string, IReadOnlyList<string>> { ["Dpad-Left"] = ["ButtonDpadLeft"] },
            naturalInputToButton: new Dictionary<string, string> { ["ButtonDpadLeft"] = "Dpad-Left" });
        mapping = OnAPlatformPicturingItsButtons(mapping);
        StubResolve("AxisTriggerLeft.png", generic: "/template/AxisTriggerLeft.png");
        StubResolve("ButtonDpadLeft.png", generic: "/template/ButtonDpadLeft.png");
        StubResolve("Dpad-Left.png", generic: "/template/Dpad-Left.png");
        Template template = TemplateWithFixedImages("AxisTriggerLeft", "ButtonDpadLeft");

        var path = _underTest.Resolve(FixedImageOf("AxisTriggerLeft"), input, mapping, template);

        path.ShouldBe("/template/AxisTriggerLeft.png");
    }

    [Fact]
    public void Resolve_FixedImageNothingDrivesIt_IsNotDrawnAsTheButtonUsuallyThere()
    {
        // fixedImage undoes a remap only: a position whose button moved away and that nothing
        // drives now is Unmapped either way, and an absent button's platform art would mislead
        var input = Input("ButtonDpadUp");
        ResolvedMapping mapping = MappingOf(
            platform: Platform, controller: Controller,
            naturalButtonToInput: new Dictionary<string, IReadOnlyList<string>> { ["Dpad-Up"] = ["ButtonDpadUp"] },
            naturalInputToButton: new Dictionary<string, string> { ["ButtonDpadUp"] = "Dpad-Up" });
        StubResolve("ButtonDpadUp.png", generic: "/template/ButtonDpadUp.png");
        StubResolve("Dpad-Up.png", generic: "/template/Dpad-Up.png", styled: "/template/styled/Dpad-Up.png");

        var path = _underTest.Resolve(FixedImageOf("ButtonDpadUp"), input, mapping, TestTemplate());

        path.ShouldBe("/template/ButtonDpadUp.png");
    }

    [Fact]
    public void Resolve_FixedImage_AButtonFromAnUnfixedPositionMovedOnto_FollowsTheRemap()
    {
        // PS2 Cross (usually ButtonA, drawn unfixed) remapped onto the d-pad list's up row
        var input = Input("ButtonDpadUp");
        ResolvedMapping mapping = MappingOf(
            platform: Platform, controller: Controller,
            inputToButton: new Dictionary<string, string> { ["ButtonDpadUp"] = "Cross" },
            naturalButtonToInput: new Dictionary<string, IReadOnlyList<string>>
            {
                ["Dpad-Up"] = ["ButtonDpadUp"],
                ["Cross"] = ["ButtonA"],
            },
            naturalInputToButton: new Dictionary<string, string> { ["ButtonDpadUp"] = "Dpad-Up" });
        StubResolve("ButtonDpadUp.png", generic: "/template/ButtonDpadUp.png");
        StubResolve("Cross.png", generic: "/template/Cross.png", styled: "/template/Sony Playstation 2/Cross.png");
        Template template = TemplateWithFixedImages("ButtonDpadUp");

        var path = _underTest.Resolve(FixedImageOf("ButtonDpadUp"), input, mapping, template);

        path.ShouldBe("/template/Sony Playstation 2/Cross.png");
    }

    [Fact]
    public void Resolve_FixedWhole_APartsButtonMovedFromAnotherFixedPosition_DrawsWhatItWouldWithNothingMoved()
    {
        // the right-stick list's up-down axis row, with a cfg rotating the C cluster so C-Left and
        // C-Right drive up and down: the axis has no button of its own, so its picture is chosen
        // by the buttons driving its parts, and those came from other fixed rows
        var input = Input("AxisRightStickY");
        ResolvedMapping mapping = MappingOf(
            platform: Platform, controller: Controller,
            inputToButton: new Dictionary<string, string>
            {
                ["AxisRightStickUp"] = "C-Left",
                ["AxisRightStickDown"] = "C-Right",
                ["AxisRightStickLeft"] = "C-Up",
                ["AxisRightStickRight"] = "C-Down",
            },
            naturalButtonToInput: new Dictionary<string, IReadOnlyList<string>>
            {
                ["C-Up"] = ["AxisRightStickUp"],
                ["C-Down"] = ["AxisRightStickDown"],
                ["C-Left"] = ["AxisRightStickLeft"],
                ["C-Right"] = ["AxisRightStickRight"],
            },
            naturalInputToButton: new Dictionary<string, string>
            {
                ["AxisRightStickUp"] = "C-Up",
                ["AxisRightStickDown"] = "C-Down",
                ["AxisRightStickLeft"] = "C-Left",
                ["AxisRightStickRight"] = "C-Right",
            });
        StubResolve("AxisRightStickY.png", generic: "/template/AxisRightStickY.png");
        StubResolve("C-Down C-Up.png", generic: "/template/C-Down C-Up.png", styled: "/template/Nintendo 64/C-Down C-Up.png");
        StubResolve("C-Left C-Right.png", generic: "/template/C-Left C-Right.png", styled: "/template/Nintendo 64/C-Left C-Right.png");
        Template template = TemplateOf(
            imageSource: _imageSource,
            wholes: new Dictionary<string, IReadOnlyList<string>> { ["AxisRightStickY"] = ["AxisRightStickUp", "AxisRightStickDown"] },
            fixedImageInputs: new HashSet<string>
            {
                "AxisRightStickY", "AxisRightStickUp", "AxisRightStickDown", "AxisRightStickLeft", "AxisRightStickRight",
            });

        var path = _underTest.Resolve(FixedImageOf("AxisRightStickY"), input, mapping, template);

        // then it keeps the up-down pair's picture, as its own rows keep theirs
        path.ShouldBe("/template/Nintendo 64/C-Down C-Up.png");
    }

    [Fact]
    public void Resolve_FixedWhole_APartsButtonFromAnUnfixedPosition_FollowsTheSwap()
    {
        // the same axis row, with Z (usually the right trigger, drawn unfixed) moved onto up:
        // Z's picture can't be read as a direction, so the axis shows the buttons now driving it,
        // as a single row would show Z
        var input = Input("AxisRightStickY");
        ResolvedMapping mapping = MappingOf(
            platform: Platform, controller: Controller,
            inputToButton: new Dictionary<string, string>
            {
                ["AxisRightStickUp"] = "Z",
                ["AxisRightStickDown"] = "C-Down",
            },
            naturalButtonToInput: new Dictionary<string, IReadOnlyList<string>>
            {
                ["Z"] = ["AxisTriggerRight"],
                ["C-Up"] = ["AxisRightStickUp"],
                ["C-Down"] = ["AxisRightStickDown"],
            },
            naturalInputToButton: new Dictionary<string, string>
            {
                ["AxisTriggerRight"] = "Z",
                ["AxisRightStickUp"] = "C-Up",
                ["AxisRightStickDown"] = "C-Down",
            });
        StubResolve("AxisRightStickY.png", generic: "/template/AxisRightStickY.png");
        StubResolve("C-Down Z.png", generic: "/template/C-Down Z.png", styled: "/template/Nintendo 64/C-Down Z.png");
        StubResolve("C-Down C-Up.png", generic: "/template/C-Down C-Up.png", styled: "/template/Nintendo 64/C-Down C-Up.png");
        Template template = TemplateOf(
            imageSource: _imageSource,
            wholes: new Dictionary<string, IReadOnlyList<string>> { ["AxisRightStickY"] = ["AxisRightStickUp", "AxisRightStickDown"] },
            fixedImageInputs: new HashSet<string> { "AxisRightStickY", "AxisRightStickUp", "AxisRightStickDown" });

        var path = _underTest.Resolve(FixedImageOf("AxisRightStickY"), input, mapping, template);

        path.ShouldBe("/template/Nintendo 64/C-Down Z.png");
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
