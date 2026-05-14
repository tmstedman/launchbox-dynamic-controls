using System.Runtime.InteropServices;
using FsCheck.Xunit;

namespace DynamicControls.Core.Tests.Infrastructure;

public class FileUtilsTests
{
    [Fact]
    public void SafeFileName_NullInput_ReturnsEmptyString()
    {
        // given a null input
        string? name = null;

        // when sanitized
        string result = name.SafeFileName();

        // then the result is an empty string
        result.ShouldBe("");
    }

    [Fact]
    public void SafeFileName_AllValidCharacters_ReturnsInputUnchanged()
    {
        // given a name with no invalid characters
        string name = "Street Fighter II";

        // when sanitized
        string result = name.SafeFileName();

        // then the result is the input unchanged
        result.ShouldBe("Street Fighter II");
    }

    [SkippableTheory]
    [InlineData("foo/bar\\baz:qux*name?", "foo_bar_baz_qux_name_")]
    [InlineData("<game>", "_game_")]
    [InlineData("a|b", "a_b")]
    [InlineData("he said \"hi\"", "he said _hi_")]
    [InlineData("???", "___")]
    public void SafeFileName_InvalidCharacters_ReplacedWithUnderscore(string input, string expected)
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
                  "Test asserts Windows invalid-filename chars");

        // given a name containing characters that are invalid in Windows filenames

        // when sanitized
        string result = input.SafeFileName();

        // then each invalid character is replaced with an underscore
        result.ShouldBe(expected);
    }

    // Property: the result never contains any character considered invalid by the OS.
    // Holds for any input on any platform — the implementation defers to
    // Path.GetInvalidFileNameChars(), so the invariant adapts to whichever set the OS reports.
    [Property]
    public bool SafeFileName_ResultNeverContainsInvalidCharacter(string input)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        return !input.SafeFileName().Any(c => invalidChars.Contains(c));
    }

    // Property: SafeFileName is a character-by-character substitution — it never adds or removes
    // characters, only replaces them. So the result length always equals the input length
    // (treating a null input as the empty string, matching SafeFileName's null-to-empty contract).
    [Property]
    public bool SafeFileName_PreservesLength(string input) =>
        input.SafeFileName().Length == (input?.Length ?? 0);

    // Property: SafeFileName is idempotent — sanitising an already-safe name is a no-op.
    // Catches a subtle regression where the replacement character itself ever becomes invalid.
    [Property]
    public bool SafeFileName_IsIdempotent(string input)
    {
        string once = input.SafeFileName();
        string twice = once.SafeFileName();
        return once == twice;
    }
}
