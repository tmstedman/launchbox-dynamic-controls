
namespace DynamicControls.Core.IntegrationTests.TestHelpers;

/// <summary>
/// Test-only path utilities. Centralizes the convention that fixture files are copied to
/// <c>AppContext.BaseDirectory/Fixtures</c> via the test csproj's <c>CopyToOutputDirectory</c>
/// glob, and provides string extensions for writing readable backslash path literals that work
/// on any host OS.
/// </summary>
public static class TestPaths
{
    /// <summary>
    /// Absolute path to the test assembly's <c>Fixtures/</c> directory. Pass this to factories
    /// that need a root directory of fixture data.
    /// </summary>
    public static string FixturesRoot { get; } = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    /// <summary>
    /// Returns the path with backslashes replaced by the host OS's directory separator.
    /// On Windows this is a no-op (separator is already <c>\</c>); on Mac/Linux it produces
    /// the equivalent forward-slash form.
    /// </summary>
    public static string AsPath(this string path) =>
        path.Replace('\\', Path.DirectorySeparatorChar);

    /// <summary>
    /// Resolves a relative path under <see cref="FixturesRoot"/>, with backslash separators
    /// normalized to the host OS.
    /// </summary>
    public static string AsFixturePath(this string relative) =>
        Path.Combine(FixturesRoot, relative.AsPath());
}
