using System.Collections.Generic;
using DynamicControls.InputMapping;

#pragma warning disable IDE0130 // intentional: extending the Shouldly namespace so assertions surface alongside built-ins
namespace Shouldly;
#pragma warning restore IDE0130

/// <summary>
/// Shouldly assertions for <see cref="MappingEntry"/> sequences. Match a whole entry by value
/// (both <see cref="MappingEntry.Name"/> and <see cref="MappingEntry.Input"/>) rather than a
/// field-by-field predicate, and give a readable failure message naming the missing/present entry.
/// </summary>
public static class MappingEntryAssertions
{
    /// <summary>Asserts a <paramref name="name"/> → <paramref name="input"/> mapping is present.</summary>
    public static void ShouldContainEntry(
        this IEnumerable<MappingEntry> actual, string name, string input) =>
        actual.ShouldContain(new MappingEntry { Name = name, Input = input });

    /// <summary>Asserts no mapping for exactly <paramref name="name"/> → <paramref name="input"/> exists.</summary>
    public static void ShouldNotContainEntry(
        this IEnumerable<MappingEntry> actual, string name, string input) =>
        actual.ShouldNotContain(new MappingEntry { Name = name, Input = input });
}
