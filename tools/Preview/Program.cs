using DynamicControls;
using DynamicControls.Composition;
using DynamicControls.Preview;
using SkiaSharp;

// --- arguments -------------------------------------------------------------------------------

const string Usage = """
    Renders what the pause screen would draw for one game, to a PNG, without WPF or LaunchBox.

      --root <dir>       plugin data root (Defaults/, User/, Templates/). Default: assets
      --platform <name>  platform to resolve as. Required unless --all.
      --rom <name>       rom name. Omit for the platform's default mapping.
      --all              every platform's default, plus each game its Labels.xml names -
                         the same set platform-renders.xml records. Ignores --platform/--rom.
      --no-labels        draw the buttons alone, with no label text.
      --out <file>       PNG to write. Default: preview/games/{platform}/{rom}.png,
                         or preview/platforms/{platform}.png when --rom is omitted.
      --cfg <dir>        emulator pack holding cfg/{rom}.cfg, for MAME games. Optional.
      --font <file>      TTF/TTC to draw labels with. Default: the closest match installed.
      --bg <hex>         canvas colour behind the controller. Default: 101014

    e.g. dotnet run --project tools/Preview -- --platform "Nintendo 64" --rom "GoldenEye 007"
         dotnet run --project tools/Preview -- --platform Arcade --rom 1942 --cfg romcontrols
         dotnet run --project tools/Preview -- --platform "Nintendo 64" --no-labels
         dotnet run --project tools/Preview -- --all --no-labels
    """;

// A rom name nothing can match, so no ControllerOverrides file and no Labels entry applies and
// what resolves is the platform's own default -- the same synthetic game LabelDump's platform
// sweep uses, so a render and the row in platform-renders.xml describe the same thing.
const string PlatformDefaultRom = "__platform_default__";

/// <summary>How many of a platform's named games <c>--all</c> will render before giving up on
/// it. Exists for Arcade, whose Labels.xml names thousands — a PNG each is hundreds of megabytes
/// nobody will page through, and its roms already have the cfg sweep and the label dump.</summary>
const int MaxGamesPerPlatform = 50;

string root = "assets", platform = "", rom = "", output = "", cfgPack = "", fontPath = "", bg = "101014";
bool noLabels = false, all = false;
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

// Say so when the labels are not in the font the theme asks for. Metrics drive wrapping inside
// the fixed-width label box, so a preview in a substitute can break a line where Windows would
// not -- worth knowing before trusting one to judge a layout change.
if (!noLabels && !typeface.FamilyName.StartsWith("Segoe UI", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine($"Note: labels drawn in {typeface.FamilyName}, not the Segoe UI the "
                          + "theme asks for. Metrics differ, so wrapping inside the label box "
                          + "may not match Windows.");
}

ControllerOverlayService service = ControllerOverlayFactory.Create(root);

// --- sweep -----------------------------------------------------------------------------------

// The same set platform-renders.xml records, so a PNG and a row in that file always describe the
// same render. --out is ignored here: a sweep writes a tree, not a file.
if (all)
{
    string platformsDir = Path.Combine(root, "Defaults", "Platforms");
    if (!Directory.Exists(platformsDir))
    {
        Console.Error.WriteLine($"No platforms folder: {platformsDir}");
        return 1;
    }

    int drawn = 0, empty = 0;
    foreach (string dir in Directory.EnumerateDirectories(platformsDir).OrderBy(d => d, StringComparer.Ordinal))
    {
        string name = Path.GetFileName(dir);
        if (Draw(name, "", DefaultPath(name, ""))) drawn++; else empty++;

        // Arcade declares thousands of games; rendering each as a PNG is hundreds of megabytes
        // nobody will look through, and its roms are what the cfg sweep and the label dump are
        // for. A platform past the cap keeps its default render and says what it dropped --
        // ask for one of its games by name to get it.
        IReadOnlyList<string> named = NamedGames(Path.Combine(dir, "Labels.xml"));
        if (named.Count > MaxGamesPerPlatform)
        {
            Console.Error.WriteLine($"{name}: {named.Count} named games, past the cap of "
                                  + $"{MaxGamesPerPlatform} — default only. Use --rom for one.");
            continue;
        }

        foreach (string namedRom in named)
            if (Draw(name, namedRom, DefaultPath(name, namedRom))) drawn++; else empty++;
    }

    Console.Error.WriteLine($"{drawn} rendered, {empty} with nothing to draw -> preview/");
    return 0;
}

// --- one render ------------------------------------------------------------------------------

return Draw(platform, rom, output.Length > 0 ? output : DefaultPath(platform, rom)) ? 0 : 1;

// --- helpers ---------------------------------------------------------------------------------

// Platform renders sit together so the set can be scanned side by side; game renders nest under
// their platform, which is the only grouping that stays legible once a platform has more than a
// handful. A re-render overwrites: the name says what was drawn, not how, so the same game with
// and without labels is one file -- pass --out to keep both.
string DefaultPath(string platformName, string romName) =>
    romName.Length == 0
        ? Path.Combine("preview", "platforms", $"{Sanitise(platformName)}.png")
        : Path.Combine("preview", "games", Sanitise(platformName), $"{Sanitise(romName)}.png");

// Resolves one game and writes its PNG. False when there was nothing to draw, which a sweep
// counts and a single render turns into a non-zero exit.
bool Draw(string platformName, string romName, string path)
{
    // Same synthetic mame.exe trick LabelDump uses: nothing opens it, but MameEmulator gates the
    // MAME chain on the filename and MameInputMappingSource takes the cfg folder from its
    // directory.
    string? emulatorPath = cfgPack.Length > 0 ? Path.Combine(cfgPack, "mame.exe") : null;

    ControllerOverlayModel model = service.Resolve(new GameInfo(
        Platform: platformName,
        RomName: romName.Length > 0 ? romName : PlatformDefaultRom,
        CloneOf: null,
        LaunchBoxId: null,
        EmulatorPath: emulatorPath,
        RomDirectory: null,
        RetroArchCore: null));

    if (model.RenderedImages.Count == 0 && model.InputLabels.Count == 0)
    {
        // ControllerOverlayService turns every exception into an empty model, so "nothing to
        // draw" and "the pipeline threw" are indistinguishable here. Say both.
        Console.Error.WriteLine($"Nothing rendered for {platformName}/{romName} — no mapping, or "
                              + $"the pipeline errored. Check {Path.Combine(root, "Logs")}.");
        return false;
    }

    // Dropped after resolving, not before: the labels still decide what is drawn. A control's
    // opacity comes from whether it has a label, so suppressing them earlier would dim the very
    // buttons the unlabelled render is meant to show.
    if (noLabels) model.InputLabels.Clear();

    using SKBitmap bitmap = OverlayRenderer.Render(model, typeface, background);

    string? dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

    using (SKData png = bitmap.Encode(SKEncodedImageFormat.Png, 100))
    using (FileStream file = File.Create(path))
        png.SaveTo(file);

    Console.Error.WriteLine($"{platformName}/{(romName.Length > 0 ? romName : "default")}: "
                          + $"{model.RenderedImages.Count} images, {model.InputLabels.Count} labels "
                          + $"-> {path}");
    return true;
}

// The rom names a platform's Labels.xml declares, matching LabelDump's platform sweep so the
// two produce the same set.
static IReadOnlyList<string> NamedGames(string labelsPath)
{
    if (!File.Exists(labelsPath)) return [];
    try
    {
        System.Xml.Linq.XElement? labelsRoot = System.Xml.Linq.XDocument.Load(labelsPath).Root;
        if (labelsRoot == null) return [];

        return [.. labelsRoot.Elements("Game")
            .Select(g => (string?)g.Attribute("romName"))
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal)];
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Could not read {labelsPath}: {ex.Message}");
        return [];
    }
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
        SKTypeface? loaded = SKTypeface.FromFile(path);
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
        SKTypeface? found = SKTypeface.FromFamilyName(family, SKFontStyleWeight.SemiBold,
            SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        return found != null && found.FamilyName.Equals(family, StringComparison.OrdinalIgnoreCase)
            ? found
            : null;
    }
}

static SKColor ParseColour(string hex) =>
    SKColor.TryParse(hex.StartsWith('#') ? hex : "#" + hex, out SKColor parsed)
        ? parsed
        : new SKColor(0x10, 0x10, 0x14);
