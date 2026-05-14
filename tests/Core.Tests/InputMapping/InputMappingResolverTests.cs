using DynamicControls.InputMapping;
using FsCheck;
using FsCheck.Xunit;
using NSubstitute;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;

namespace DynamicControls.Core.Tests.InputMapping;

/// <summary>
/// Unit tests for <see cref="InputMappingResolver"/>. Covers the raw <see cref="InputMappingConfig"/>
/// → <see cref="ResolvedMapping"/> transformation: building the forward (button → inputs) and
/// reverse (input → primary button) maps, deduping repeated entries, first-seen wins on reverse
/// collisions, AnalogToDigital mirror invocation, and the contract that the resolver's output has
/// <c>Natural*</c> maps equal to active maps (the "natural" snapshot is taken at this layer).
/// Mirror semantics themselves are tested in <see cref="AnalogToDigitalMirrorTests"/>.
/// </summary>
public class InputMappingResolverTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly InputMappingResolver _underTest;

    public InputMappingResolverTests()
    {
        _underTest = new InputMappingResolver(_logger);
    }

    [Fact]
    public void Resolve_EmptyConfig_ReturnsEmptyMaps()
    {
        // given an empty config
        var config = MappingConfig();

        // when resolving
        var result = _underTest.Resolve(Platform, config, controller: "Pad");

        // then both forward and reverse maps are empty
        result.ButtonToInput.ShouldBeEmpty();
        result.InputToButton.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_PopulatesButtonToInput_AndReverseInputToButton()
    {
        // given a config with one entry per button
        var config = MappingConfig(mappings:
        [
            ("A", "ButtonA"),
            ("B", "ButtonB"),
            ("Start", "ButtonStart"),
        ]);

        // when resolving
        var result = _underTest.Resolve(Platform, config, controller: "Pad");

        // then the forward map lists each input under its driving platform button
        result.ButtonToInput.ShouldBeDictionaryOf(
            ("A", ["ButtonA"]),
            ("B", ["ButtonB"]),
            ("Start", ["ButtonStart"]));

        // and the reverse map points each input back to its platform button
        result.InputToButton.ShouldBeDictionaryOf(
            ("ButtonA", "A"),
            ("ButtonB", "B"),
            ("ButtonStart", "Start"));
    }

    [Fact]
    public void Resolve_DeduplicatesRepeatedEntriesWithinButtonList()
    {
        // given the same (button, input) pair listed twice in the config
        var config = MappingConfig(mappings: [("A", "ButtonA"), ("A", "ButtonA"),]);

        // when resolving
        var result = _underTest.Resolve(Platform, config, controller: "Pad");

        // then the input is recorded once under that button
        result.ButtonToInput.ShouldBeDictionaryOf(("A", ["ButtonA"]));
    }

    [Fact]
    public void Resolve_WhenMultipleButtonsDriveSameInput_ReverseUsesFirstSeen()
    {
        // given two platform buttons that both drive the same generic input
        var config = MappingConfig(mappings: [("A", "ButtonA"), ("B", "ButtonA")]);

        // when resolving
        var result = _underTest.Resolve(Platform, config, controller: "Pad");

        // then the forward map records both buttons driving the input
        result.ButtonToInput.ShouldBeDictionaryOf(
            ("A", ["ButtonA"]),
            ("B", ["ButtonA"]));

        // but the reverse map keeps the first button it saw (declaration order)
        result.InputToButton.ShouldBeDictionaryOf(("ButtonA", "A"));
    }

    [Fact]
    public void Resolve_WithAnalogToDigital_AppliesDpadMirror()
    {
        // given a Dpad mapping with AnalogToDigital=Left — mirror semantics live in
        // AnalogToDigitalMirrorTests; this test only verifies the resolver invokes the mirror
        var config = MappingConfig(
            analogToDigital: AnalogToDigitalMode.Left,
            mappings: [("Dpad-Up", "ButtonDpadUp")]);

        // when resolving
        var result = _underTest.Resolve(Platform, config, controller: "Pad");

        // then the dpad button's list has the matching left-stick input appended
        result.ButtonToInput.ShouldBeDictionaryOf(("Dpad-Up", ["ButtonDpadUp", "AxisLeftStickUp"]));

        // and both genericss are reachable via the reverse (first wins; ButtonDpadUp was first)
        result.InputToButton.ShouldBeDictionaryOf(
            ("ButtonDpadUp", "Dpad-Up"),
            ("AxisLeftStickUp", "Dpad-Up"));
    }

    [Fact]
    public void Resolve_WithoutAnalogToDigital_DoesNotMirror()
    {
        // given a Dpad mapping without AnalogToDigital set
        var config = MappingConfig(mappings: [("Dpad-Up", "ButtonDpadUp")]);

        // when resolving
        var result = _underTest.Resolve(Platform, config, controller: "Pad");

        // then no stick input is appended — the list is exactly what the config declared
        result.ButtonToInput.ShouldBeDictionaryOf(("Dpad-Up", ["ButtonDpadUp"]));
    }

    [Fact]
    public void Resolve_PassesThroughPlatformControllerAndAnalogToDigital()
    {
        // given a config with AnalogToDigital set
        var config = MappingConfig(analogToDigital: AnalogToDigitalMode.Right);

        // when resolving with a specific platform and controller
        var result = _underTest.Resolve("My Platform", config, controller: "My Controller");

        // then all three identity fields are copied to the resolved mapping
        result.Platform.ShouldBe("My Platform");
        result.Controller.ShouldBe("My Controller");
        result.AnalogToDigital.ShouldBe(AnalogToDigitalMode.Right);
    }

    [Fact]
    public void Resolve_NaturalMapsEqualActiveMaps_AtThisLayer()
    {
        // given a config — the resolver is the raw → resolved transformation, so the natural
        // snapshot IS this mapping. Any divergence between active and natural maps only appears
        // later in InputMappingService when a transform is layered on top
        var config = MappingConfig(mappings: [("A", "ButtonA"), ("B", "ButtonB")]);

        // when resolving
        var result = _underTest.Resolve(Platform, config, controller: "Pad");

        // then the natural maps are the same references as the active maps
        result.NaturalButtonToInput.ShouldBeSameAs(result.ButtonToInput);
        result.NaturalInputToButton.ShouldBeSameAs(result.InputToButton);
    }

    // Properties — hold for arbitrary mapping entry collections:

    // Every input appearing in ButtonToInput has a corresponding entry in InputToButton.
    [Property]
    public bool Resolve_EveryInputInForwardMapHasReverseEntry(
        NonNull<string>[] buttons, NonNull<string>[] inputs)
    {
        var mappings = buttons.Zip(inputs, (b, i) => new MappingEntry { Name = b.Get, Input = i.Get }).ToList();
        var result = _underTest.Resolve(Platform, new InputMappingConfig { Mappings = mappings }, "Pad");
        return result.ButtonToInput.Values.SelectMany(x => x)
            .All(result.InputToButton.ContainsKey);
    }

    // Every button referenced in InputToButton is a key in ButtonToInput.
    [Property]
    public bool Resolve_ReverseMapOnlyReferencesKnownButtons(
        NonNull<string>[] buttons, NonNull<string>[] inputs)
    {
        var mappings = buttons.Zip(inputs, (b, i) => new MappingEntry { Name = b.Get, Input = i.Get }).ToList();
        var result = _underTest.Resolve(Platform, new InputMappingConfig { Mappings = mappings }, "Pad");
        return result.InputToButton.Values.All(result.ButtonToInput.ContainsKey);
    }
}
