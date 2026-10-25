using DynamicControls;
using DynamicControls.Composition;
using DynamicControls.Preview;
using DynamicControls.Tools.Common;
using SkiaSharp;

// --- arguments -------------------------------------------------------------------------------

const string ToolName = "Preview";

const string Usage = """
    Renders what the pause screen would draw for one game, to a PNG, without WPF or LaunchBox.

      --root <dir>       plugin data root (Defaults/, User/, Templates/). Default: assets
      --platform <name>  platform to resolve as. Required unless --all.
      --rom <name>       rom name. Omit for the platform's default mapping.
      --all              every platform's default, plus each game its Labels.xml names -
                         the same set platform-renders.xml records. Ignores --platform/--rom.
      --no-labels        draw the buttons alone, with no label text. For a game render where
                         the artwork is the question; most platform defaults carry no labels
                         anyway, so it changes nothing on a sweep.
      --out <file>       PNG to write. Default: tools/Preview/output/games/{platform}/{rom}.png,
                         or .../output/platforms/{platform}.png when --rom is omitted.
      --cfg <dir>        emulator pack holding cfg/{rom}.cfg, for MAME games. Optional.
      --font <file>      TTF/TTC to draw labels with. Default: the closest match installed.
      --bg <hex>         canvas colour behind the controller. Default: 101014
      --backdrop <file>  artwork to blur behind the controller, as the pause screen does with
                         the game's background image. Omitted, the canvas stays flat --bg.
      --backdrops <dir>  a screenshot library: each game takes {dir}/{platform}/{rom}.png
                         (or .jpg) as its own backdrop, falling back to --backdrop where
                         there is none. Platform defaults always use --backdrop.
      --backdrop-blur <r>  blur radius over the backdrop. Default: 60, the theme's own.
      --scrim <hex>      colour laid over the blurred backdrop. Default: 90000000 (ARGB),
                         the theme's own. 0 to see the artwork undimmed.
      --no-title         draw the pause-screen frame alone, without the band above it that
                         carries the game title.
      --titles <file>    transcription to take game titles from, by romName; a game it does
                         not name is titled by its rom name.
                         Default: label-sources/arcade-artwork.xml, where present.

    e.g. dotnet run --project tools/Preview -- --platform "Nintendo 64" --rom "GoldenEye 007"
         dotnet run --project tools/Preview -- --platform Arcade --rom 1942 --cfg romcontrols
         dotnet run --project tools/Preview -- --all
         dotnet run --project tools/Preview -- --platform Arcade --rom 1942 --cfg romcontrols --no-labels
         dotnet run --project tools/Preview -- --platform Arcade --rom 1942 --cfg romcontrols --backdrop art/fanart.jpg
         dotnet run --project tools/Preview -- --all --backdrops romcontrols/Screenshots
    """;

/// <summary>How many of a platform's named games <c>--all</c> will render before giving up on
/// it. Exists for Arcade, whose Labels.xml names thousands — a PNG each is hundreds of megabytes
/// nobody will page through, and its roms already have the cfg sweep and the label dump.</summary>
const int MaxGamesPerPlatform = 50;

string root = "assets", platform = "", rom = "", output = "", cfgPack = "", fontPath = "", bg = "101014";
string backdrop = "", backdropDir = "", scrim = "90000000", backdropBlur = "60";
string titlesPath = "";
bool noLabels = false, all = false, noTitle = false;
for (int i = 0; i < args.Length; i++)
{
    string? Next() => i + 1 < args.Length ? args[++i] : null;
    string? value = args[i] switch
    {
        "--root" => root = Next() ?? "",
        "--platform" => platform = Next() ?? "",
        "--rom" => rom = Next() ?? "",
        // "ok" rather than a value: the switch arms all yield the string a flag consumed, and
        // the loop treats empty or null as a parse failure, so a valueless flag needs a stand-in.
        "--all" => Set(ref all),
        "--no-labels" => Set(ref noLabels),
        "--out" => output = Next() ?? "",
        "--cfg" => cfgPack = Next() ?? "",
        "--font" => fontPath = Next() ?? "",
        "--bg" => bg = Next() ?? "",
        "--backdrop" => backdrop = Next() ?? "",
        "--backdrops" => backdropDir = Next() ?? "",
        "--backdrop-blur" => backdropBlur = Next() ?? "",
        "--scrim" => scrim = Next() ?? "",
        "--no-title" => Set(ref noTitle),
        "--titles" => titlesPath = Next() ?? "",
        _ => null
    };
    if (value is not { Length: > 0 })
    {
        Console.Error.WriteLine($"Unrecognised or incomplete argument: {args[i]}\n\n{Usage}");
        return 1;
    }
}

if (platform.Length == 0 && !all)
{
    Console.Error.WriteLine($"--platform is required unless --all.\n\n{Usage}");
    return 1;
}
if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"Data root not found: {root}");
    return 1;
}

SKTypeface typeface = LoadTypeface(fontPath);
SKColor background = ParseColour(bg);

if (backdrop.Length > 0 && !File.Exists(backdrop))
{
    Console.Error.WriteLine($"Backdrop image not found: {backdrop}");
    return 1;
}
if (backdropDir.Length > 0 && !Directory.Exists(backdropDir))
{
    Console.Error.WriteLine($"Backdrop library not found: {backdropDir}");
    return 1;
}
if (!double.TryParse(backdropBlur, out double backdropRadius) || backdropRadius < 0)
{
    Console.Error.WriteLine($"--backdrop-blur wants a number of 0 or more, not: {backdropBlur}");
    return 1;
}
SKColor scrimColour = ParseColour(scrim, SKColors.Transparent);

// An explicit --titles that is missing is a typo worth stopping for; the default is only there
// when run from the repo root, and its absence just means every game is titled by rom name.
if (titlesPath.Length > 0 && !File.Exists(titlesPath))
{
    Console.Error.WriteLine($"Titles file not found: {titlesPath}");
    return 1;
}
IReadOnlyDictionary<string, string> titles = noTitle
    ? new Dictionary<string, string>()
    : LoadTitles(titlesPath.Length > 0 ? titlesPath : "label-sources/arcade-artwork.xml");

// Preferred first: a png in this library is the lossless original, a jpg the same shot
// re-encoded, and several games carry both.
string[] backdropExtensions = [".png", ".jpg", ".jpeg"];

// The library is searched by rom name, so a platform default -- which has none -- always
// falls through to --backdrop, as does a game the library has no shot of.
string? BackdropFor(SweepTarget target)
{
    if (backdropDir.Length == 0 || target.IsPlatformDefault)
        return backdrop.Length > 0 ? backdrop : null;

    foreach (string ext in backdropExtensions)
    {
        string candidate = Path.Combine(backdropDir, target.Platform, target.RomName + ext);
        if (File.Exists(candidate)) return candidate;
    }
    return backdrop.Length > 0 ? backdrop : null;
}

// Say so when the labels are not in the font the theme asks for. Metrics drive wrapping inside
// the fixed-width label box, so a preview in a substitute can break a line where Windows would
// not -- worth knowing before trusting one to judge a layout change.
if (!noLabels && !typeface.FamilyName.StartsWith("Segoe UI", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine($"Note: labels drawn in {typeface.FamilyName}, not the Segoe UI the "
                          + "theme asks for. Metrics differ, so wrapping inside the label box "
                          + "may not match Windows.");
}

var resolver = new ToolResolver(root, ToolName, cfgPack);

// --- sweep -----------------------------------------------------------------------------------

// The same set platform-renders.xml records, so a PNG and a row in that file always describe the
// same render. --out is ignored here: a sweep writes a tree, not a file.
if (all)
{
    IReadOnlyList<string> platforms = SweepTargets.Platforms(root);
    if (platforms.Count == 0)
    {
        Console.Error.WriteLine($"No platforms under {root}.");
        return 1;
    }

    int drawn = 0, empty = 0;
    foreach (string name in platforms)
    {
        if (Draw(new SweepTarget(name, ""))) drawn++; else empty++;

        // Arcade declares thousands of games; rendering each as a PNG is hundreds of megabytes
        // nobody will look through, and its roms are what the cfg sweep and the label dump are
        // for. A platform past the cap keeps its default render and says what it dropped --
        // ask for one of its games by name to get it.
        IReadOnlyList<string> named = SweepTargets.NamedGames(root, name, Console.Error.WriteLine);
        if (named.Count > MaxGamesPerPlatform)
        {
            Console.Error.WriteLine($"{name}: {named.Count} named games, past the cap of "
                                  + $"{MaxGamesPerPlatform} — default only. Use --rom for one.");
            continue;
        }

        foreach (string namedRom in named)
            if (Draw(new SweepTarget(name, namedRom))) drawn++; else empty++;
    }

    Console.Error.WriteLine($"{drawn} rendered, {empty} with nothing to draw -> {ToolPaths.Output(ToolName)}/");
    return 0;
}

// --- one render ------------------------------------------------------------------------------

return Draw(new SweepTarget(platform, rom), output) ? 0 : 1;

// --- helpers ---------------------------------------------------------------------------------

// Platform renders sit together so the set can be scanned side by side; game renders nest under
// their platform, which is the only grouping that stays legible once a platform has more than a
// handful. A re-render overwrites: the name says what was drawn, not how, so the same game with
// and without labels is one file -- pass --out to keep both.
static string DefaultPath(SweepTarget target) =>
    target.IsPlatformDefault
        ? Path.Combine(ToolPaths.Output(ToolName), "platforms", $"{Sanitise(target.Platform)}.png")
        : Path.Combine(ToolPaths.Output(ToolName), "games", Sanitise(target.Platform), $"{Sanitise(target.RomName)}.png");

// Resolves one game and writes its PNG. False when there was nothing to draw, which a sweep
// counts and a single render turns into a non-zero exit.
bool Draw(SweepTarget target, string explicitPath = "")
{
    string path = explicitPath.Length > 0 ? explicitPath : DefaultPath(target);
    ControllerOverlayModel model = resolver.Resolve(target);

    if (ToolResolver.IsEmpty(model))
    {
        // ControllerOverlayService turns every exception into an empty model, so "nothing to
        // draw" and "the pipeline threw" are indistinguishable here. Say both.
        Console.Error.WriteLine($"Nothing rendered for {target} — no mapping, or "
                              + $"the pipeline errored. Check {Path.Combine(ToolPaths.Output(ToolName), "Logs")}.");
        return false;
    }

    // Dropped after resolving, not before: the labels still decide what is drawn. A control's
    // opacity comes from whether it has a label, so suppressing them earlier would dim the very
    // buttons the unlabelled render is meant to show.
    if (noLabels) model.InputLabels.Clear();

    var backdropLayer = new OverlayRenderer.Backdrop(
        BackdropFor(target), backdropRadius, scrimColour);
    // With a title the backdrop spans the whole taller image, so the frame is drawn bare on a
    // transparent canvas and TitleFrame lays background and backdrop beneath it.
    using SKBitmap bitmap = noTitle
        ? OverlayRenderer.Render(model, typeface, background, backdropLayer)
        : Titled(OverlayRenderer.Render(model, typeface, SKColors.Transparent), target, backdropLayer);

    string? dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

    using (SKData png = bitmap.Encode(SKEncodedImageFormat.Png, 100))
    using (FileStream file = File.Create(path))
        png.SaveTo(file);

    Console.Error.WriteLine($"{target}: {model.RenderedImages.Count} images, "
                          + $"{model.InputLabels.Count} labels -> {path}");
    return true;
}

SKBitmap Titled(SKBitmap frame, SweepTarget target, OverlayRenderer.Backdrop backdropLayer)
{
    using (frame)
        return TitleFrame.Wrap(frame, TitleFor(target), SubtitleFor(target), typeface, background, backdropLayer);
}

string TitleFor(SweepTarget target) =>
    target.IsPlatformDefault ? target.Platform
    : titles.TryGetValue(target.RomName, out string? title) ? title
    : target.RomName;

static string SubtitleFor(SweepTarget target) =>
    target.IsPlatformDefault ? "platform default" : $"{target.Platform} · {target.RomName}";

// romName -> title from a transcription in arcade-artwork.xml's shape. Read leniently: a missing
// or unreadable file costs only the titles, never the render.
static IReadOnlyDictionary<string, string> LoadTitles(string path)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (!File.Exists(path)) return map;
    try
    {
        foreach (System.Xml.Linq.XElement game in System.Xml.Linq.XDocument.Load(path).Root!.Elements("Game"))
        {
            if ((string?)game.Attribute("romName") is { Length: > 0 } romName
                && (string?)game.Attribute("title") is { Length: > 0 } title)
            {
                map.TryAdd(romName, title);
            }
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Could not read titles from {path}: {ex.Message}");
    }
    return map;
}

static string Set(ref bool flag)
{
    flag = true;
    return "ok";
}

static string Sanitise(string name) =>
    string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

// The theme asks for Segoe UI SemiBold. Install it and this picks it up with no further
// argument; otherwise it walks down to whatever the host has, ordered by how close the metrics
// are. Selawik is the one worth having if Segoe UI is not an option -- it is metric-compatible
// by design, so text wraps inside the fixed-width label box at the same points.
//
// Two spellings are tried per family because Windows ships SemiBold as its own face: asking the
// "Segoe UI" family for weight 600 finds it when the whole family is installed, while a
// standalone install presents itself as the family "Segoe UI Semibold" and would otherwise be
// rejected as a substitute.
static SKTypeface LoadTypeface(string path)
{
    if (path.Length > 0)
    {
        var loaded = SKTypeface.FromFile(path);
        if (loaded != null) return loaded;
        Console.Error.WriteLine($"Could not read font {path}, falling back.");
    }

    foreach (string family in new[] { "Segoe UI", "Selawik", "Tahoma", "Verdana", "Arial", "Helvetica Neue" })
    {
        if (Exact(family) is { } hit) return hit;
        if (Exact(family + " Semibold") is { } semibold) return semibold;
    }
    return SKTypeface.Default;

    // FromFamilyName substitutes silently for a family the host does not have, so the name has
    // to be compared back -- without this every lookup "succeeds" and the first entry always wins.
    static SKTypeface? Exact(string family)
    {
        var found = SKTypeface.FromFamilyName(family, SKFontStyleWeight.SemiBold,
            SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        return found != null && found.FamilyName.Equals(family, StringComparison.OrdinalIgnoreCase)
            ? found
            : null;
    }
}

static SKColor ParseColour(string hex, SKColor? fallback = null) =>
    SKColor.TryParse(hex.StartsWith('#') ? hex : "#" + hex, out SKColor parsed)
        ? parsed
        : fallback ?? new SKColor(0x10, 0x10, 0x14);
