using System.Xml.Linq;

namespace DynamicControls.Tools.Common;

/// <summary>
/// What a sweep runs over: one platform and, for a game-specific render, the ROM within it. An
/// empty <paramref name="RomName"/> means the platform's own default.
/// </summary>
public readonly record struct SweepTarget(string Platform, string RomName)
{
    public bool IsPlatformDefault => RomName.Length == 0;

    /// <summary>What to call this target in a log line or a filename stem.</summary>
    public override string ToString() => IsPlatformDefault ? $"{Platform}/default" : $"{Platform}/{RomName}";
}

/// <summary>
/// Enumerates what the tools sweep. Shared because <c>LabelDump</c> and <c>Preview</c> have to
/// agree: a PNG and a row in <c>platform-renders.xml</c> are meant to describe the same render,
/// and that only holds while both ask the same question of the same data.
/// </summary>
public static class SweepTargets
{
    /// <summary>
    /// ROM name standing in for "no game at all". Nothing can match it — no
    /// <c>ControllerOverrides</c> file, no <c>Labels.xml</c> entry — so what resolves is the
    /// platform's own default. A constant rather than a literal in each tool because the two
    /// outputs only line up while both spell it identically.
    /// </summary>
    public const string PlatformDefaultRom = "__platform_default__";

    /// <summary>Platform names under <c>{root}/Defaults/Platforms</c>, ordinal by name. Empty
    /// when the folder is absent, which the caller should report rather than treat as "none".</summary>
    public static IReadOnlyList<string> Platforms(string root)
    {
        string dir = Path.Combine(root, "Defaults", "Platforms");
        return Directory.Exists(dir)
            ? [.. Directory.EnumerateDirectories(dir)
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n!)
                .OrderBy(n => n, StringComparer.Ordinal)]
            : [];
    }

    /// <summary>
    /// The ROM names a platform's <c>Labels.xml</c> declares, ordinal. Read straight from the
    /// file rather than through the loader: this asks what the data names, not what resolving
    /// one of them yields. An unreadable file reports and yields nothing, since a sweep losing
    /// one platform's games should not take the run down with it.
    /// </summary>
    public static IReadOnlyList<string> NamedGames(string root, string platform, Action<string>? onError = null)
    {
        string path = Path.Combine(root, "Defaults", "Platforms", platform, "Labels.xml");
        if (!File.Exists(path)) return [];

        try
        {
            XElement? labels = XDocument.Load(path).Root;
            if (labels == null) return [];

            return [.. labels.Elements("Game")
                .Select(g => (string?)g.Attribute("romName"))
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n!)
                .Distinct()
                .OrderBy(n => n, StringComparer.Ordinal)];
        }
        catch (Exception ex)
        {
            onError?.Invoke($"Could not read {path}: {ex.Message}");
            return [];
        }
    }
}
