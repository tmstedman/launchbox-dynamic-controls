using DynamicControls.Config;
using DynamicControls.InputMapping;
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
/// <see cref="ILogger"/> that keeps every <see cref="Error"/> it is given, for subsystem tests
/// asserting that a scenario is (or isn't) reported; the rest is dropped as by
/// <see cref="NullLogger"/>.
/// </summary>
internal sealed class ErrorRecordingLogger : ILogger
{
    public List<string> Errors { get; } = [];
    public bool IsDebugEnabled { get; set; }
    public void Debug(string message) { }
    public void Info(string message) { }
    public void Error(string message) => Errors.Add(message);
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

/// <summary>Wraps a delegate as an <see cref="IInputMappingTransform"/> so tests can describe
/// their own per-game overlay without dragging in the MAME plugin internals.</summary>
internal sealed class StubMappingTransform(Func<GameInfo, InputMappingConfig, InputMappingConfig?> fn)
    : IInputMappingTransform
{
    public bool IsEnabled(GlobalConfig config) => true;
    public InputMappingConfig? Transform(GameInfo game, InputMappingConfig baseline) => fn(game, baseline);
}
