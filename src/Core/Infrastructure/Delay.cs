using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace DynamicControls.Infrastructure;

/// <summary>
/// Blocks the calling thread for a short, fixed period. Exists so callers that need to wait out
/// a transient race (e.g. <see cref="DynamicControls.Plugins.Mame.MameCfgLoader"/> retrying a
/// file that another process may be mid-write on) can be tested without actually sleeping.
/// </summary>
public interface IDelay
{
    void Sleep(int milliseconds);
}

[ExcludeFromCodeCoverage]
public sealed class SystemDelay : IDelay
{
    public void Sleep(int milliseconds) => Thread.Sleep(milliseconds);
}
