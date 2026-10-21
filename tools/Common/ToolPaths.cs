namespace DynamicControls.Tools.Common;

/// <summary>
/// Where a developer tool puts what it produces: <c>tools/{tool}/output/</c>, beside the tool
/// that writes it and ignored by git.
///
/// <para>Everything transient goes here — rendered PNGs, and the pipeline's own
/// <c>Logs/debug.log</c>. The log in particular used to land in whatever data root the tool was
/// pointed at, which for these tools is the repository's own <c>assets/</c>: the folder that
/// gets packaged, accumulating tens of megabytes of debug output inside it.</para>
///
/// <para>Committed baselines are deliberately not here. <c>label-sources/</c> holds those, and
/// they stay tracked because the Arcade ones cannot be regenerated without a local MAME cfg
/// pack that is not in the repository — ignoring them would remove the record of what the
/// pipeline renders rather than merely relocating it.</para>
/// </summary>
public static class ToolPaths
{
    /// <summary>The output directory for <paramref name="toolName"/>, e.g. "Preview".</summary>
    public static string Output(string toolName) => Path.Combine("tools", toolName, "output");
}
