using System.Text.RegularExpressions;

namespace DynamicControls.Infrastructure;

public static class RomNameUtils
{
    private static readonly Regex _parens = new(@"\s*\([^)]*\)", RegexOptions.Compiled);
    private static readonly Regex _brackets = new(@"\s*\[[^\]]*\]", RegexOptions.Compiled);

    /// <summary>
    /// Normalizes a ROM name for fuzzy label lookup: strips balanced parenthesis and bracket
    /// groups (e.g. region tags, revision suffixes) then lowercases and trims.
    /// </summary>
    public static string NormalizeRomName(this string name) =>
        _brackets.Replace(_parens.Replace(name, ""), "").Trim().ToLowerInvariant();
}
