using System.Xml;

namespace DynamicControls.Plugins.Mame;

/// <summary>
/// Loads a single MAME .cfg file and returns its port-to-generic-input override map. Returns
/// null when the file does not exist or has no root element. Implementations may translate
/// joycodes via an <see cref="IJoycodeMappingLoader"/>, but that is an internal concern — the
/// consumer sees only the resulting overrides.
/// </summary>
public interface IMameCfgLoader
{
    Dictionary<string, List<string>>? Load(string path);
}

/// <summary>
/// Loads a MAME cfg file and translates its port assignments into a map of generic input name
/// to the generic inputs produced by their assigned joycodes. A single port can list multiple
/// JOYCODEs (joined with OR), each producing a separate generic name — all are recorded so the
/// renderer can mark every physical button visible. Returns null if the file does not exist or
/// has no document element; unknown joycodes are logged and skipped.
/// </summary>
public class MameCfgLoader(
    ILogger logger,
    IFileSystem fs,
    IJoycodeMappingLoader joycodeMappingLoader,
    IDelay delay) : IMameCfgLoader
{
    /// <summary>
    /// A true analogue port (DIAL, PADDLE, PEDAL, TRACKBALL_X/Y, ...) carries its "standard"
    /// joycode from real analogue hardware, but MAME also lets a player nudge the same value
    /// with two ordinary buttons instead — "increment" and "decrement" — for whichever direction
    /// analogue hardware isn't present. "NONE" means that sequence isn't bound.
    /// </summary>
    private static readonly string[] SequenceTypes = ["standard", "increment", "decrement"];

    /// <summary>
    /// Each sequence becomes its own platform button: the port's own name for the analogue
    /// binding, and a <c>.inc</c>/<c>.dec</c> sibling for each nudge direction.
    ///
    /// <para>They were unioned onto the one name until it proved lossy. The nudge buttons are
    /// routinely a different physical control from the axis — Air Combat 22 steers its throttle
    /// with the right stick and nudges it with the two triggers — and the artwork labels them
    /// separately ("Throttle", "Throttle Up", "Throttle Down"). One name carries one label, so
    /// unioning them made the port say one word about three controls.</para>
    ///
    /// <para>The suffix is MAME's own sequence type rather than a direction, because which way
    /// a nudge goes is the game's business: Air Combat 22's increment is its throttle
    /// <em>down</em>. A platform button standing for the pair is declared in Controllers.xml, so
    /// a game that draws no distinction still labels all three by naming the port alone.</para>
    /// </summary>
    private static readonly (string SeqType, string Suffix)[] SequenceButtons =
        [("standard", ""), ("increment", ".inc"), ("decrement", ".dec")];

    /// <summary>
    /// MAME rewrites a game's cfg on every clean exit, so a relaunch soon after closing the same
    /// game can land while MAME is still mid-write (most likely a write-then-rename, which makes
    /// the target genuinely not exist for a moment) — indistinguishable from the file never having
    /// been written at all. A short bounded retry closes that race; it costs nothing extra on a
    /// genuine first-ever launch, since that case still fails on the very first check.
    /// </summary>
    private const int MaxAttempts = 3;
    private const int RetryDelayMs = 50;

    private readonly ILogger _logger = logger;
    private readonly IFileSystem _fs = fs;
    private readonly IJoycodeMappingLoader _joycodeMappingLoader = joycodeMappingLoader;
    private readonly IDelay _delay = delay;

    public Dictionary<string, List<string>>? Load(string path)
    {
        if (!WaitForFile(path)) return null;

        JoycodeMapping joycodeMapping = _joycodeMappingLoader.Load();

        using Stream stream = _fs.OpenRead(path);
        var doc = new XmlDocument();
        doc.Load(stream);
        XmlElement root = doc.DocumentElement!;

        var overrides = new Dictionary<string, List<string>>();

        foreach (XmlElement systemNode in root.ChildNodes.OfType<XmlElement>())
        {
            if (systemNode.Name != "system") continue;

            foreach (XmlElement child in systemNode.ChildNodes.OfType<XmlElement>())
            {
                if (child.Name != "input") continue;

                foreach (XmlElement portNode in child.ChildNodes.OfType<XmlElement>())
                {
                    if (portNode.Name != "port") continue;

                    // The cfg's port type IS the platform button name — Controllers.xml and
                    // Labels.xml are written against MAME's own vocabulary, with no rewriting
                    // in between. Which ports can reach the overlay is settled below, by
                    // whether their joycode translates.
                    string? inputName = portNode.Attributes["type"]?.Value;
                    if (string.IsNullOrEmpty(inputName)) continue;

                    // Which sequences the port *declares* is how an analogue port announces
                    // itself: MAME writes increment and decrement on anything nudgeable, as NONE
                    // where unbound, and writes neither on an ordinary button. That is the signal
                    // for whether this port has .inc/.dec names at all — the values only say
                    // whether they currently reach anything.
                    var declaredSeqTypes = new HashSet<string>();
                    var joycodesBySeqType = new Dictionary<string, string>();
                    foreach (XmlElement seqNode in portNode.ChildNodes.OfType<XmlElement>())
                    {
                        if (seqNode.Name != "newseq") continue;
                        string? seqType = seqNode.Attributes["type"]?.Value;
                        if (seqType == null || !SequenceTypes.Contains(seqType)) continue;

                        declaredSeqTypes.Add(seqType);
                        string text = seqNode.InnerText.Trim();
                        if (text.Length > 0 && text != "NONE") joycodesBySeqType[seqType] = text;
                    }

                    if (joycodesBySeqType.Count == 0) continue;

                    var bySeqType = new Dictionary<string, List<string>>();
                    foreach (string seqType in SequenceTypes)
                    {
                        if (!joycodesBySeqType.TryGetValue(seqType, out string? joycode)) continue;
                        var generics = new List<string>();
                        foreach (string generic in joycodeMapping.Translate(joycode))
                            if (!generics.Contains(generic)) generics.Add(generic);
                        if (generics.Count > 0) bySeqType[seqType] = generics;
                    }

                    string joycodes = string.Join(", ", joycodesBySeqType.Values);
                    if (bySeqType.Count == 0)
                    {
                        _logger.Debug($"MAME cfg: {inputName} ({joycodes}) -> unknown JOYCODE");
                        continue;
                    }

                    // A bound port states all three of its names, empty where that sequence isn't
                    // bound. Stating them is what drops Controllers.xml's baseline for the ones
                    // this cfg doesn't use: the baseline describes the axis as the cabinet has it,
                    // and a cfg that moves the axis elsewhere, or drives it by nudges alone, would
                    // otherwise leave the unused names still pointing at the old guess — the port
                    // then reaching both where the cfg put it and where the baseline assumed.
                    foreach ((string seqType, string suffix) in SequenceButtons)
                    {
                        // The port's own name is always stated; a nudge name only when the port
                        // declares that sequence, so an ordinary button never grows one.
                        if (suffix.Length > 0 && !declaredSeqTypes.Contains(seqType)) continue;

                        List<string> generics = bySeqType.GetValueOrDefault(seqType, []);
                        string from = joycodesBySeqType.GetValueOrDefault(seqType, "unbound");

                        // A nudge sequence the cfg leaves unbound still names a real half of the
                        // axis, when the axis itself is bound: the hardware drives the value both
                        // ways, and the joycode says which end is which. Reading the half off the
                        // axis is what lets a label written for one direction land on a cabinet
                        // whose cfg has no nudge buttons at all — the common case by far, since
                        // nudges exist for cabinets lacking the analogue hardware. The baseline is
                        // still dropped: these come from the axis this cfg names, never from
                        // Controllers.xml's guess about which axis the port would use.
                        if (generics.Count == 0
                            && suffix.Length > 0
                            && !joycodesBySeqType.ContainsKey(seqType)
                            && joycodesBySeqType.TryGetValue("standard", out string? axis))
                        {
                            string half = suffix[1..];
                            generics = [.. joycodeMapping.TranslateHalf(axis, half)];
                            if (generics.Count > 0) from = $"{axis}, {half} half";
                        }

                        overrides[inputName + suffix] = generics;
                        _logger.Debug(generics.Count > 0
                            ? $"MAME override: {inputName}{suffix} ({from}) -> {string.Join(", ", generics)}"
                            : $"MAME override: {inputName}{suffix} ({from}) -> nothing, baseline dropped");
                    }
                }
            }
        }

        return overrides;
    }

    /// <summary>
    /// Retries <see cref="IFileSystem.FileExists"/> a few times, a short delay apart, before
    /// accepting that the file genuinely isn't there. See <see cref="MaxAttempts"/>.
    /// </summary>
    private bool WaitForFile(string path)
    {
        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            bool exists = _fs.FileExists(path);
            _logger.Debug($"MAME cfg path: {path}, Exists: {exists} (attempt {attempt}/{MaxAttempts})");
            if (exists) return true;
            if (attempt < MaxAttempts) _delay.Sleep(RetryDelayMs);
        }

        return false;
    }
}
