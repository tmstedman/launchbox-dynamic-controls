using DynamicControls.Templates;

namespace DynamicControls.Core.IntegrationTests.Subsystem;

/// <summary>
/// Silent <see cref="ILogger"/> for subsystem tests — debug output is irrelevant to behavior
/// assertions and a real logger would touch disk.
/// </summary>
internal sealed class NullLogger : ILogger
{
    public bool IsDebugEnabled { get; set; }
    public void Debug(string message) { }
    public void Info(string message) { }
    public void Error(string message) { }
    public void ClearLog() { }
}

/// <summary>
/// Programmable <see cref="ITemplateImageSource"/>: the I/O boundary substituted at the
/// subsystem-test seam so tests can pin exactly what generic/styled paths the resolver gets
/// without touching disk. Unregistered <c>(src, platform, controller)</c> tuples resolve to
/// <c>(src, null)</c> — the same shape <see cref="TemplateImageSource"/> uses when no
/// platform-specific file exists.
/// </summary>
internal sealed class FakeTemplateImageSource : ITemplateImageSource
{
    private readonly Dictionary<(string Src, string? Platform, string? Controller), ResolvedImagePaths> _entries = [];

    public FakeTemplateImageSource With(
        string src,
        string generic,
        string? styled = null,
        string? platform = null,
        string? controller = null)
    {
        _entries[(src, platform, controller)] = new ResolvedImagePaths(generic, styled);
        return this;
    }

    public ResolvedImagePaths Resolve(string src, string? platform, string? controller = null) =>
        _entries.TryGetValue((src, platform, controller), out ResolvedImagePaths? r) ? r : new ResolvedImagePaths(src, null);
}
