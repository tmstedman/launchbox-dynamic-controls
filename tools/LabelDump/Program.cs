using DynamicControls;
using DynamicControls.Composition;
using DynamicControls.LabelDump;
using DynamicControls.Tools.Common;

const string ToolName = "LabelDump";

var options = Options.Parse(args);
if (options == null) return 1;

if (!Directory.Exists(options.Root))
{
    Console.Error.WriteLine($"Data root not found: {options.Root}");
    return 1;
}

string cfgDir = Path.Combine(options.CfgPack, "cfg");
if (!Directory.Exists(cfgDir))
{
    Console.Error.WriteLine($"No cfg folder in the pack: {cfgDir}");
    return 1;
}

string[] roms = [.. Directory.EnumerateFiles(cfgDir, "*.cfg")
    .Select(Path.GetFileNameWithoutExtension)
    .Where(name => name is { Length: > 0 } && !name.Equals("default", StringComparison.OrdinalIgnoreCase))
    .Select(name => name!)
    .OrderBy(name => name, StringComparer.Ordinal)];

if (options.Limit > 0) roms = [.. roms.Take(options.Limit)];

// Titles come from the transcription -- the pipeline has no notion of one. Carrying them
// across is what keeps a <Game> line in the two files byte-identical when the labels agree,
// so a diff shows only real disagreement.
Dictionary<string, GameLabels> artwork = [];
if (options.Artwork.Length > 0)
{
    if (!File.Exists(options.Artwork))
    {
        Console.Error.WriteLine($"Artwork file not found: {options.Artwork}");
        return 1;
    }
    artwork = ArtworkReader.Read(options.Artwork).ToDictionary(g => g.RomName);
    Console.Error.WriteLine($"{artwork.Count} roms read from {options.Artwork}");
}

Console.Error.WriteLine($"{roms.Length} roms from {cfgDir}");
Console.Error.WriteLine($"Resolving against {options.Root} as platform '{options.Platform}'...");

// Resolves every rom against the cfg pack; the platform sweep below uses its own resolver
// with no pack, a platform render being what the data alone produces.
var romResolver = new ToolResolver(options.Root, ToolName, options.CfgPack);

// Platform renders resolve with no cfg pack, unlike the rom sweep: a platform render is
// what the data alone produces, and pointing it at an emulator pack would make that file
// describe one machine's configuration rather than the plugin's defaults.
var platformResolver = new ToolResolver(options.Root, ToolName);

List<GameLabels> games = [];
List<(string RomName, IReadOnlyList<GameImage> Images)> renders = [];
List<string> empty = [];
List<string> conflicting = [];

foreach (string rom in roms)
{
    ControllerOverlayModel model = romResolver.Resolve(new SweepTarget(options.Platform, rom));

    // One RenderedLabel per label slot the layout draws, so the same input legitimately
    // repeats when a template gives it several positions. Group to one entry per control.
    // A control whose slots somehow disagree on text is recorded rather than silently
    // resolved -- one input carries one label value, so disagreement would mean a real bug.
    var byControl = model.InputLabels
        .Where(l => !string.IsNullOrEmpty(l.InputName) && !string.IsNullOrEmpty(l.Text))
        .GroupBy(l => l.InputName!)
        .Select(g => (Control: g.Key, Texts: g.Select(l => l.Text).Distinct().ToList()))
        .OrderBy(e => e.Control, StringComparer.Ordinal)
        .ToList();

    foreach ((string control, List<string> texts) in byControl.Where(e => e.Texts.Count > 1))
        conflicting.Add($"{rom} {control}: {string.Join(" | ", texts)}");

    // Collected before the empty-label check below, because a ROM that renders no label still
    // renders a controller: on a platform whose artwork replaces buttons rather than labelling
    // them, that render is the entire output and skipping it would hide exactly the platforms
    // this file exists to cover.
    if (options.ImagesOut.Length > 0)
    {
        renders.Add((rom, [.. model.RenderedImages.Select(i => new GameImage(
            Control: i.InputName is { Length: > 0 } n ? n : "(none)",
            Source: ImageWriter.Relative(i.Source),
            Opacity: i.Opacity,
            BlurRadius: i.BlurRadius,
            Width: i.Width,
            Height: i.Height))]));
    }

    if (byControl.Count == 0)
    {
        // ControllerOverlayService swallows every exception into an empty model, so "no
        // labels" and "the pipeline threw" look identical here. Recorded either way -- a
        // silently dropped rom is exactly the failure this tool exists to stop hiding.
        empty.Add(rom);
        continue;
    }

    games.Add(new GameLabels(
        rom,
        artwork.TryGetValue(rom, out GameLabels? art) ? art.Title : null,
        [.. byControl.Select(e => (e.Control, e.Texts[0]))],
        []));
}

string command = $"dotnet run --project tools/LabelDump -- --root {options.Root} "
               + $"--cfg {options.CfgPack} --out {options.Out}"
               + (options.Platform == "Arcade" ? "" : $" --platform {options.Platform}");

ArtworkWriter.Write(options.Out, games, """
    GENERATED - do not edit by hand.

    What the plugin actually renders, per ROM, produced by running the real pipeline
    (tools/LabelDump) over a MAME cfg pack. Written in arcade-artwork.xml's format, under the
    same ordering as arcade-artwork-sorted.xml beside it, so the two diff directly: that file
    is what the cabinet artwork says, this one is what a player ends up seeing.

    Labels here are filtered by the template - an input the layout never draws cannot appear,
    however well it resolved. That is deliberate: this file answers "what does the player
    see", not "what did the label pipeline produce".

    A control differing between the two files is not automatically a fault. The artwork names
    the button as the cabinet drew it; the cfg decides which pad button the player presses, so
    a label legitimately moves (sasuke's Shoot is ButtonX on the cabinet, ButtonA on the pad).
    A label text present there and missing here is the difference that matters.
    """, command);

Console.Error.WriteLine($"{games.Count} roms wrote at least one label -> {options.Out}");
Console.Error.WriteLine($"{empty.Count} roms produced none (no labels, or the pipeline errored "
                      + $"-- check {Path.Combine(ToolPaths.Output(ToolName), "Logs")})");
foreach (string c in conflicting) Console.Error.WriteLine($"CONFLICT {c}");

// Same writer, same ordering, so the only differences left between the two files are the
// labels themselves.
if (options.ArtworkOut.Length > 0 && artwork.Count > 0)
{
    ArtworkWriter.Write(options.ArtworkOut, [.. artwork.Values], """
        GENERATED - do not edit by hand. Edit label-sources/arcade-artwork.xml instead.

        arcade-artwork.xml re-emitted in one fixed order: ROMs by name, labels by physical
        position on the controller. Content is untouched - every label, title and note is
        carried across verbatim - only the ordering is imposed.

        It exists to be diffed against arcade-pipeline-sorted.xml beside it, which the same
        writer produces under the same ordering. The transcription's own order is whatever the
        reading pass happened to record, which is not reproducible and would otherwise swamp
        that diff with reshuffling.
        """, command);
    Console.Error.WriteLine($"{artwork.Count} roms re-ordered -> {options.ArtworkOut}");
}

if (options.ImagesOut.Length > 0)
{
    ImageWriter.WriteRomShapes(options.ImagesOut, renders, """
        GENERATED - do not edit by hand.

        The images the plugin renders for the roms in the cfg pack, from the same run that
        produced the label dump beside it. That file answers "what text does the player read";
        this one answers "what does the player look at" - the half a labels-only comparison
        cannot see. An input silently losing its platform-specific artwork and falling back to
        the generic leaves no trace in the labels at all.

        Grouped by distinct render shape, not listed per rom. Thousands of roms produce a few
        hundred shapes, because what varies between them is which OneOf alternative fired rather
        than the artwork itself - a per-rom listing would be the same few hundred facts repeated
        at forty times the size. Shapes are ordered by their first rom rather than numbered, so
        a new one appearing does not renumber the rest.

        This file covers the per-rom axis only. Bespoke artwork varies by platform, which
        platform-renders.xml beside it records.

        Left and top are omitted: they come from the template and say nothing a change here
        would not already show. Size is kept, because it is what separates a whole stick's
        background glyph from the alternative a OneOf selected when both draw the same file.
        """, command);
    Console.Error.WriteLine($"{renders.Count} roms rendered "
                          + $"{renders.Sum(r => r.Images.Count)} images -> {options.ImagesOut}");
}

// Independent of the cfg pack: a game with no configuration of its own resolves to the
// platform's default mapping, which is the axis bespoke artwork varies along. Every console
// platform is here, none of which has a rom pack to be swept the way Arcade just was.
if (options.PlatformsOut.Length > 0)
{
    IReadOnlyList<string> platforms = SweepTargets.Platforms(options.Root);
    if (platforms.Count == 0)
    {
        Console.Error.WriteLine($"No platforms under {options.Root}, skipping --platforms-out.");
        return 0;
    }

    List<ImageWriter.PlatformRender> platformRenders = [];
    foreach (string name in platforms)
    {
        (IReadOnlyList<GameImage> defImages, IReadOnlyList<(string, string)> defLabels) =
            RenderOf(new SweepTarget(name, ""));

        // Every game the platform's own Labels.xml names -- the only game-specific coverage a
        // console platform can have, none of them having a rom pack. Skipped for the platform
        // the rom sweep above already covered, whose thousands of games are in its own file.
        List<(string, IReadOnlyList<GameImage>, IReadOnlyList<(string, string)>)> namedGames = [];
        if (!name.Equals(options.Platform, StringComparison.OrdinalIgnoreCase))
        {
            foreach (string namedRom in SweepTargets.NamedGames(options.Root, name, Console.Error.WriteLine))
            {
                (IReadOnlyList<GameImage> images, IReadOnlyList<(string, string)> labels) =
                    RenderOf(new SweepTarget(name, namedRom));
                namedGames.Add((namedRom, images, labels));
            }
        }

        platformRenders.Add(new ImageWriter.PlatformRender(name, defImages, defLabels, namedGames));
    }

    ImageWriter.WritePlatforms(options.PlatformsOut, platformRenders, """
        GENERATED - do not edit by hand.

        What the template draws for each platform: first its default - a game with no config, no
        remap and no per-game override, so what is left is the platform's own Controllers.xml and
        Labels.xml defaults - then one block per game that Labels.xml names.

        This is the axis bespoke artwork varies along. A control resolves to its platform's own
        image when a platform button drives it (Nintendo 64's C-Any drives AxisRightStick, so
        the whole stick draws C-Any.png) and falls back to the generic when none does. That
        fallback is silent, changes nothing about any label, and is invisible to every other
        file here.

        It is also the only coverage the console platforms have: none of them ships a rom pack,
        so the per-rom sweep reaches Arcade alone - which has no artwork of its own at all and
        could never have surfaced this. Arcade's own games are skipped here for that reason,
        being covered in full by the files beside this one.

        Images and labels sit together, unlike the rom sweep which splits them. That split exists
        only so the label half stays diffable against the artwork transcription, and no such
        transcription exists for a platform.

        Opacity reflects the default rendering mode, where showIf="auto" resolves to IsMapped
        rather than HasLabel: a control the platform does not have is dimmed, not absent. A
        platform's named games render in the game-specific mode instead, where it resolves to
        HasLabel, so an unlabelled control dims there even when the platform has it.
        """, command);
    Console.Error.WriteLine(
        $"{platformRenders.Count} platforms + {platformRenders.Sum(p => p.Games.Count)} named games "
      + $"rendered {platformRenders.Sum(p => p.Images.Count + p.Games.Sum(g => g.Images.Count))} images, "
      + $"{platformRenders.Sum(p => p.Labels.Count + p.Games.Sum(g => g.Labels.Count))} labels "
      + $"-> {options.PlatformsOut}");
}

return 0;

// Resolves one render and reduces it to what the file records. Shared by the platform default
// and its named games so the two can never diverge in what they capture.
//
// Resolved through a ToolResolver with no cfg pack, unlike the rom sweep above: a platform
// render is what the data alone produces, and pointing it at an emulator pack would make the
// file describe one machine's configuration rather than the plugin's defaults.
(IReadOnlyList<GameImage>, IReadOnlyList<(string, string)>) RenderOf(SweepTarget target)
{
    ControllerOverlayModel m = platformResolver.Resolve(target);

    IReadOnlyList<GameImage> images = [.. m.RenderedImages.Select(i => new GameImage(
        Control: i.InputName is { Length: > 0 } n ? n : "(none)",
        Source: ImageWriter.Relative(i.Source),
        Opacity: i.Opacity,
        BlurRadius: i.BlurRadius,
        Width: i.Width,
        Height: i.Height))];

    // Same grouping the rom sweep applies: a template may draw one input's label in several
    // places, which is one label as far as this file is concerned.
    IReadOnlyList<(string, string)> labels = [.. m.InputLabels
        .Where(l => !string.IsNullOrEmpty(l.InputName) && !string.IsNullOrEmpty(l.Text))
        .GroupBy(l => l.InputName!)
        .Select(g => (g.Key, g.First().Text))];

    return (images, labels);
}
