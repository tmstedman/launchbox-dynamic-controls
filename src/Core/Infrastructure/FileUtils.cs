namespace DynamicControls.Infrastructure;

/// <summary>
/// Extension methods for safe file name handling.
/// </summary>
public static class FileUtils
{
    /// <summary>
    /// Replaces characters that are invalid in Windows filenames with underscores.
    /// </summary>
    public static string SafeFileName(this string? name) =>
        Path.GetInvalidFileNameChars()
            .Aggregate(name ?? "", (current, c) => current.Replace(c, '_'));
}
