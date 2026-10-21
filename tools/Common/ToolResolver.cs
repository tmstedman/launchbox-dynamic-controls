using DynamicControls.Composition;

namespace DynamicControls.Tools.Common;

/// <summary>
/// Runs the real pipeline for a <see cref="SweepTarget"/>. Shared so the tools cannot resolve
/// the same target differently — the synthetic emulator path and the default-ROM substitution
/// are both easy to get subtly wrong in a second copy, and a difference would show up as two
/// baselines disagreeing about a render neither tool got wrong.
/// </summary>
/// <param name="root">Plugin data root — the folder holding <c>Defaults/</c>, <c>User/</c> and
/// <c>Templates/</c>.</param>
/// <param name="cfgPack">Directory containing <c>cfg/{rom}.cfg</c>, or empty for none. Also
/// stands in as the emulator directory.</param>
/// <param name="toolName">The tool doing the resolving, e.g. "Preview". Decides where the
/// pipeline writes <c>Logs/debug.log</c> — the tool's own output directory rather than
/// <paramref name="root"/>, so running against the repository's <c>assets/</c> does not leave
/// diagnostics inside the folder that gets packaged. See <see cref="ToolPaths"/>.</param>
public sealed class ToolResolver(string root, string toolName, string cfgPack = "")
{
    private readonly ControllerOverlayService _service =
        ControllerOverlayFactory.Create(root, ToolPaths.Output(toolName));

    /// <summary>
    /// A path nothing opens. <c>MameEmulator.IsMameExecutable</c> gates the whole MAME chain on
    /// the filename starting with "mame", and <c>MameInputMappingSource</c> derives the cfg
    /// folder from its directory — so placing it in the pack root is what points the pipeline at
    /// <c>{pack}/cfg</c>, the same relationship a real install has between mame.exe and its cfg
    /// folder. Null when no pack was given, which is how a console platform resolves.
    /// </summary>
    private readonly string? _emulatorPath =
        cfgPack.Length > 0 ? Path.Combine(cfgPack, "mame.exe") : null;

    public ControllerOverlayModel Resolve(SweepTarget target) => _service.Resolve(new GameInfo(
        Platform: target.Platform,
        RomName: target.IsPlatformDefault ? SweepTargets.PlatformDefaultRom : target.RomName,
        CloneOf: null,
        LaunchBoxId: null,
        EmulatorPath: _emulatorPath,
        RomDirectory: null,
        RetroArchCore: null));

    /// <summary>
    /// True when the pipeline produced nothing at all. <c>ControllerOverlayService</c> turns
    /// every exception into an empty model, so "this game draws nothing" and "the pipeline
    /// threw" are indistinguishable from out here — callers should say both.
    /// </summary>
    public static bool IsEmpty(ControllerOverlayModel model) =>
        model.RenderedImages.Count == 0 && model.InputLabels.Count == 0;
}
