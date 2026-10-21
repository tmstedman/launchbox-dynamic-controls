using DynamicControls.Tools.Common;

namespace DynamicControls.LabelDump;

/// <summary>
/// Parsed command line. <see cref="CfgPack"/> is the directory holding the MAME
/// <c>cfg/</c> folder; it is not in the repository (the pack is git-excluded local data), so
/// it has no default and must always be supplied.
/// </summary>
/// <param name="Root">Plugin data root — the folder holding <c>Defaults/</c>, <c>User/</c>
/// and <c>Templates/</c>. The repository's <c>assets/</c> has exactly that shape.</param>
/// <param name="CfgPack">Directory containing <c>cfg/{rom}.cfg</c>. Also stands in as the
/// emulator directory: the synthetic <c>mame.exe</c> path handed to the pipeline lives here,
/// which is how <c>MameInputMappingSource</c> finds the cfg folder beside it.</param>
/// <param name="Out">File to write. Overwritten wholesale.</param>
/// <param name="Platform">Platform name passed to the pipeline, which selects
/// <c>Platforms/{Platform}/</c>.</param>
/// <param name="Limit">Stop after this many ROMs. For a quick check against a pack of
/// thousands; 0 means no limit.</param>
/// <param name="Artwork">The transcription to read titles from, and to re-emit in canonical
/// order when <paramref name="ArtworkOut"/> is set. Empty to skip both.</param>
/// <param name="ArtworkOut">Where to write the re-ordered copy of <paramref name="Artwork"/>.
/// Empty to write only the pipeline dump.</param>
/// <param name="ImagesOut">Where to write the images the ROMs rendered, grouped by distinct
/// render shape. A separate file rather than more elements in <paramref name="Out"/>, which is
/// diffed against a transcription that has no notion of an image — folding them together would
/// wreck that comparison. Empty to skip.</param>
/// <param name="PlatformsOut">Where to write what each platform draws under its default mapping.
/// Needs no cfg pack and does not read one: it resolves a game with no configuration of its own,
/// per platform found under <c>Defaults/Platforms/</c>. Empty to skip.</param>
public record Options(
    string Root,
    string CfgPack,
    string Out,
    string Platform,
    int Limit,
    string Artwork,
    string ArtworkOut,
    string ImagesOut,
    string PlatformsOut)
{
    private const string Usage = """
        Runs the real plugin pipeline over every ROM in a MAME cfg pack and writes the labels
        it renders, in arcade-artwork.xml's format, so the two can be diffed.

          --root <dir>        plugin data root (Defaults/, User/, Templates/). Default: assets
          --cfg <dir>         directory containing cfg/{rom}.cfg. Required.
          --out <file>        pipeline output.
                              Default: tools/LabelDump/output/arcade-pipeline-sorted.xml
          --platform <name>   platform to resolve as. Default: Arcade
          --limit <n>         stop after n roms (0 = all). Default: 0
          --artwork <file>    transcription to take titles from.
                              Default: label-sources/arcade-artwork.xml
          --artwork-out <f>   also write that transcription back in canonical order, so the
                              pair diffs cleanly.
                              Default: tools/LabelDump/output/arcade-artwork-sorted.xml
          --images-out <f>    also write the images the roms rendered, grouped by distinct
                              render shape, as a baseline for changes that never touch a label.
                              Default: tools/LabelDump/output/arcade-pipeline-images.xml
          --platforms-out <f> also write what every platform draws - its default mapping, plus
                              each game its Labels.xml names. Images and labels together; the
                              only coverage the console platforms have.
                              Default: tools/LabelDump/output/platform-renders.xml

        e.g. dotnet run --project tools/LabelDump -- --cfg romcontrols
        """;

    /// <summary>
    /// Parses <paramref name="args"/>, or returns null after printing usage when they are
    /// unusable. Null means "exit non-zero", not "carry on with defaults" — a typo'd flag
    /// silently falling back to a default would send the run at the wrong data.
    /// </summary>
    /// <summary>Where every file this tool writes goes. Generated, so ignored by git — only
    /// the hand-edited sources under <c>label-sources/</c> are tracked.</summary>
    private static readonly string OutputDir = ToolPaths.Output("LabelDump");

    public static Options? Parse(string[] args)
    {
        string root = "assets", cfg = "";
        string output = Path.Combine(OutputDir, "arcade-pipeline-sorted.xml");
        string platform = "Arcade";
        int limit = 0;
        string artwork = "label-sources/arcade-artwork.xml";
        string artworkOut = Path.Combine(OutputDir, "arcade-artwork-sorted.xml");
        string imagesOut = Path.Combine(OutputDir, "arcade-pipeline-images.xml");
        string platformsOut = Path.Combine(OutputDir, "platform-renders.xml");

        for (int i = 0; i < args.Length; i++)
        {
            string? Next() => i + 1 < args.Length ? args[++i] : null;
            string? value = args[i] switch
            {
                "--root" => root = Next() ?? "",
                "--cfg" => cfg = Next() ?? "",
                "--out" => output = Next() ?? "",
                "--platform" => platform = Next() ?? "",
                "--limit" => int.TryParse(Next(), out limit) ? "ok" : null,
                "--artwork" => artwork = Next() ?? "",
                "--artwork-out" => artworkOut = Next() ?? "",
                "--images-out" => imagesOut = Next() ?? "",
                "--platforms-out" => platformsOut = Next() ?? "",
                _ => null
            };
            if (value is not { Length: > 0 })
            {
                Console.Error.WriteLine($"Unrecognised or incomplete argument: {args[i]}\n\n{Usage}");
                return null;
            }
        }

        if (cfg.Length == 0)
        {
            Console.Error.WriteLine($"--cfg is required.\n\n{Usage}");
            return null;
        }

        return new Options(root, cfg, output, platform, limit, artwork, artworkOut, imagesOut, platformsOut);
    }
}
