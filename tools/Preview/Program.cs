using DynamicControls;
using DynamicControls.Composition;
using DynamicControls.Preview;
using SkiaSharp;

// --- arguments -------------------------------------------------------------------------------

const string Usage = """
    Renders what the pause screen would draw for one game, to a PNG, without WPF or LaunchBox.

      --root <dir>       plugin data root (Defaults/, User/, Templates/). Default: assets
      --platform <name>  platform to resolve as. Required.
      --rom <name>       rom name. Required. Use any name for a platform's default render.
      --out <file>       PNG to write. Default: preview/{platform}-{rom}.png
      --cfg <dir>        emulator pack holding cfg/{rom}.cfg, for MAME games. Optional.
      --font <file>      TTF/TTC to draw labels with. Default: the closest match installed.
      --bg <hex>         canvas colour behind the controller. Default: 101014

    e.g. dotnet run --project tools/Preview -- --platform "Nintendo 64" --rom "GoldenEye 007"
         dotnet run --project tools/Preview -- --platform Arcade --rom 1942 --cfg romcontrols
    """;

string root = "assets", platform = "", rom = "", output = "", cfgPack = "", fontPath = "", bg = "101014";
for (int i = 0; i < args.Length; i++)
{
    string? Next() => i + 1 < args.Length ? args[++i] : null;
    string? value = args[i] switch
    {
        "--root" => root = Next() ?? "",
        "--platform" => platform = Next() ?? "",
        "--rom" => rom = Next() ?? "",
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

if (platform.Length == 0 || rom.Length == 0)
{
    Console.Error.WriteLine($"--platform and --rom are both required.\n\n{Usage}");
    return 1;
}
if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"Data root not found: {root}");
    return 1;
}
if (output.Length == 0)
    output = Path.Combine("preview", $"{Sanitise(platform)}-{Sanitise(rom)}.png");

// --- resolve ---------------------------------------------------------------------------------

// Same synthetic mame.exe trick LabelDump uses: nothing opens it, but MameEmulator gates the
// MAME chain on the filename and MameInputMappingSource takes the cfg folder from its directory.
string? emulatorPath = cfgPack.Length > 0 ? Path.Combine(cfgPack, "mame.exe") : null;

ControllerOverlayService service = ControllerOverlayFactory.Create(root);
ControllerOverlayModel model = service.Resolve(new GameInfo(
    Platform: platform,
    RomName: rom,
    CloneOf: null,
    LaunchBoxId: null,
    EmulatorPath: emulatorPath,
    RomDirectory: null,
    RetroArchCore: null));

if (model.RenderedImages.Count == 0 && model.InputLabels.Count == 0)
{
    // ControllerOverlayService turns every exception into an empty model, so "nothing to draw"
    // and "the pipeline threw" are indistinguishable here. Say both.
    Console.Error.WriteLine($"Nothing rendered for {platform}/{rom} — no mapping, or the "
                          + $"pipeline errored. Check {Path.Combine(root, "Logs")}.");
    return 1;
}

// --- draw ------------------------------------------------------------------------------------

SKTypeface typeface = LoadTypeface(fontPath);
using SKBitmap bitmap = OverlayRenderer.Render(model, typeface, ParseColour(bg));

string? dir = Path.GetDirectoryName(output);
if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

using (SKData png = bitmap.Encode(SKEncodedImageFormat.Png, 100))
using (FileStream file = File.Create(output))
    png.SaveTo(file);

Console.Error.WriteLine($"{platform}/{rom}: {model.RenderedImages.Count} images, "
                      + $"{model.InputLabels.Count} labels, {typeface.FamilyName} -> {output}");
return 0;

// --- helpers ---------------------------------------------------------------------------------

static string Sanitise(string name) =>
    string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

// The theme asks for Segoe UI SemiBold, which ships on Windows only. The fallbacks are ordered
// by how close their metrics are to it, so a preview taken off Windows is as near as the host
// allows -- and the chosen family is printed, because a different font means text wraps inside
// the fixed-width label box at different points.
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
        SKTypeface? found = SKTypeface.FromFamilyName(family, SKFontStyleWeight.SemiBold,
            SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        if (found != null && found.FamilyName.Equals(family, StringComparison.OrdinalIgnoreCase))
            return found;
    }
    return SKTypeface.Default;
}

static SKColor ParseColour(string hex) =>
    SKColor.TryParse(hex.StartsWith('#') ? hex : "#" + hex, out SKColor parsed)
        ? parsed
        : new SKColor(0x10, 0x10, 0x14);
