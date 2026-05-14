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
