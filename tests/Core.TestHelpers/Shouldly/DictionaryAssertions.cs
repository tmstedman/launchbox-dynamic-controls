using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130 // intentional: extending the Shouldly namespace so assertions surface alongside built-ins
namespace Shouldly;
#pragma warning restore IDE0130

/// <summary>
/// Shouldly assertions for dictionary contents.
/// </summary>
public static class DictionaryAssertions
{
    /// <summary>
    /// Asserts that <paramref name="actual"/> is non-null and contains exactly the given
    /// key/value pairs and no others — order-insensitive, since dictionary enumeration order
    /// isn't part of the contract. Works on any sequence of <see cref="KeyValuePair{TKey,TValue}"/>,
    /// which covers <see cref="IDictionary{TKey,TValue}"/>, <see cref="IReadOnlyDictionary{TKey,TValue}"/>,
    /// and the concrete <see cref="Dictionary{TKey,TValue}"/>. Values are compared with Shouldly's
    /// <c>ShouldBe</c>, so collection-valued dicts (e.g. <c>Dictionary&lt;string, List&lt;string&gt;&gt;</c>)
    /// get deep sequence equality on the values.
    /// </summary>
    public static void ShouldBeDictionaryOf<TKey, TValue>(
        [NotNull] this IEnumerable<KeyValuePair<TKey, TValue>>? actual,
        params (TKey Key, TValue Value)[] expected) where TKey : notnull
    {
        actual.ShouldNotBeNull();
        var actualDict = actual.ToDictionary(kv => kv.Key, kv => kv.Value);
        Dictionary<TKey, TValue> expectedDict = expected.ToDictionary(p => p.Key, p => p.Value);

        actualDict.Keys.ShouldBe(expectedDict.Keys, ignoreOrder: true);
        foreach ((TKey key, TValue expectedValue) in expected)
        {
            actualDict[key].ShouldBe(expectedValue);
        }
    }
}
