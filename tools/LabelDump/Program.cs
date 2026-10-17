using DynamicControls;
using DynamicControls.Composition;
using DynamicControls.LabelDump;

Options? options = Options.Parse(args);
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

// A synthetic path: nothing opens it, but MameEmulator.IsMameExecutable gates the whole MAME
// chain on the filename starting with "mame", and MameInputMappingSource derives the cfg
// folder from its directory. Placing it in the pack root is what points the pipeline at
// {pack}/cfg -- the same relationship a real install has between mame.exe and its cfg folder.
string emulatorPath = Path.Combine(options.CfgPack, "mame.exe");

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

ControllerOverlayService service = ControllerOverlayFactory.Create(options.Root);

List<GameLabels> games = [];
List<(string RomName, IReadOnlyList<GameImage> Images)> renders = [];
List<string> empty = [];
List<string> conflicting = [];

foreach (string rom in roms)
{
    var game = new GameInfo(
        Platform: options.Platform,
        RomName: rom,
        CloneOf: null,
        LaunchBoxId: null,
        EmulatorPath: emulatorPath,
        RomDirectory: null,
        RetroArchCore: null);

    ControllerOverlayModel model = service.Resolve(game);

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
                      + $"-- check {Path.Combine(options.Root, "Logs")})");
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
    ImageWriter.Write(options.ImagesOut, renders, """
        GENERATED - do not edit by hand.

        The images the plugin renders, per ROM, from the same run that produced the label dump
        beside it. That file answers "what text does the player read"; this one answers "what
        does the player look at" - the half a labels-only comparison cannot see. An input
        silently losing its platform-specific artwork and falling back to the generic leaves no
        trace in the labels at all.

        Position and size are omitted deliberately: they come from the template and are the same
        for every ROM. What varies is the source file, the size and the opacity. On a platform with
        artwork of its own - Arcade is one - every source is generic and the opacity is the whole
        of the per-game signal, so it is what a visibility change shows up in.
        """, command);
    Console.Error.WriteLine($"{renders.Count} roms rendered "
                          + $"{renders.Sum(r => r.Images.Count)} images -> {options.ImagesOut}");
}

return 0;
