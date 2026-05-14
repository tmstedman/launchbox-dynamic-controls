using DynamicControls.Plugins.Mame;
using FsCheck;
using FsCheck.Xunit;

namespace DynamicControls.Core.Tests.Plugins.Mame;

/// <summary>
/// Unit tests for <see cref="JoycodeMapping"/>. The mapping is constructed from a
/// JOYCODE-to-generic-input dictionary and translates MAME sequences — possibly chained with
/// "OR" — into the corresponding generic input names. Non-JOYCODE tokens and unrecognized
/// JOYCODEs are dropped; results are de-duplicated and returned in source order.
/// </summary>
public class JoycodeMappingTests
{
    private static readonly Dictionary<string, string> _data = new()
    {
        ["JOYCODE_1_BUTTON1"] = "ButtonA",
        ["JOYCODE_1_BUTTON2"] = "ButtonB",
        ["JOYCODE_1_BUTTON3"] = "ButtonC",
    };
    private readonly JoycodeMapping _underTest = new(_data);

    [Fact]
    public void Translate_NullInput_ReturnsEmpty()
    {
        // given a null sequence (no MAME mapping specified)
        // when translated
        var result = _underTest.Translate(null);

        // then an empty list is returned without throwing
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Translate_EmptyInput_ReturnsEmpty()
    {
        // given an empty sequence
        // when translated
        var result = _underTest.Translate("");

        // then no tokens survive the JOYCODE_ filter
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Translate_SingleKnownJoycode_ReturnsMappedInput()
    {
        // given a single known JOYCODE
        // when translated
        var result = _underTest.Translate("JOYCODE_1_BUTTON1");

        // then the mapped generic input is returned
        result.ShouldBe(["ButtonA"]);
    }

    [Fact]
    public void Translate_OrSequence_ReturnsAllMappedInputsInSourceOrder()
    {
        // given a MAME OR-chain of two known JOYCODEs
        // when translated
        var result = _underTest.Translate("JOYCODE_1_BUTTON2 OR JOYCODE_1_BUTTON1");

        // then both inputs are returned in the order they appeared (the OR token itself is ignored)
        result.ShouldBe(["ButtonB", "ButtonA"]);
    }

    [Fact]
    public void Translate_UnknownJoycode_IsSkipped()
    {
        // given a sequence mixing a known and an unknown JOYCODE
        // when translated
        var result = _underTest.Translate("JOYCODE_1_BUTTON1 OR JOYCODE_1_BUTTON99");

        // then only the known JOYCODE contributes
        result.ShouldBe(["ButtonA"]);
    }

    [Fact]
    public void Translate_NonJoycodeTokens_AreSkipped()
    {
        // given a sequence with keyboard and mouse tokens alongside a JOYCODE
        // when translated
        var result = _underTest.Translate("KEYCODE_ENTER OR MOUSECODE_1_BUTTON1 OR JOYCODE_1_BUTTON1");

        // then only the JOYCODE survives the prefix filter
        result.ShouldBe(["ButtonA"]);
    }

    [Fact]
    public void Translate_DuplicateMappedInputs_AreDeduplicated()
    {
        // given a mapping where two distinct JOYCODEs alias the same generic input
        var mapping = new JoycodeMapping(new Dictionary<string, string>
        {
            ["JOYCODE_1_BUTTON1"] = "ButtonA",
            ["JOYCODE_1_BUTTON2"] = "ButtonA",
        });

        // when both appear in the same sequence
        var result = mapping.Translate("JOYCODE_1_BUTTON1 OR JOYCODE_1_BUTTON2");

        // then the duplicate output is collapsed
        result.ShouldBe(["ButtonA"]);
    }

    [Fact]
    public void Translate_NoKnownJoycodes_ReturnsEmpty()
    {
        // given a sequence with only non-JOYCODE tokens
        // when translated
        var result = _underTest.Translate("KEYCODE_A OR KEYCODE_B");

        // then nothing matches the filter and the result is empty
        result.ShouldBeEmpty();
    }

    // Properties — hold for arbitrary input strings:

    // Every element in the result is a value from the mapping dictionary.
    [Property]
    public bool Translate_ResultOnlyContainsMappedValues(NonNull<string> input) =>
        _underTest.Translate(input.Get).All(_data.Values.Contains);

    // The result never contains duplicate entries.
    [Property]
    public bool Translate_ResultHasNoDuplicates(NonNull<string> input)
    {
        var result = _underTest.Translate(input.Get);
        return result.Count == result.Distinct().Count();
    }

    // The result cannot be longer than the number of distinct values in the dictionary.
    [Property]
    public bool Translate_ResultCountBoundedByDistinctMappingValues(NonNull<string> input) =>
        _underTest.Translate(input.Get).Count <= _data.Values.Distinct().Count();
}
