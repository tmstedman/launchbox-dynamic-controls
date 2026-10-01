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
    /// analogue hardware isn't present. All three are read and unioned in this order (most direct
    /// first) so a digital-only binding is recognized exactly like an analogue one; "NONE" means
    /// that sequence isn't bound and contributes nothing.
    /// </summary>
    private static readonly string[] SequenceTypes = ["standard", "increment", "decrement"];

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

                    string? portType = portNode.Attributes["type"]?.Value;
                    string? inputName = NormalizePortType(portType);
                    if (inputName == null) continue;

                    var joycodesBySeqType = new Dictionary<string, string>();
                    foreach (XmlElement seqNode in portNode.ChildNodes.OfType<XmlElement>())
                    {
                        if (seqNode.Name != "newseq") continue;
                        string? seqType = seqNode.Attributes["type"]?.Value;
                        if (seqType == null || !SequenceTypes.Contains(seqType)) continue;

                        string text = seqNode.InnerText.Trim();
                        if (text.Length > 0 && text != "NONE") joycodesBySeqType[seqType] = text;
                    }

                    if (joycodesBySeqType.Count == 0) continue;

                    var genericInputs = new List<string>();
                    foreach (string seqType in SequenceTypes)
                    {
                        if (!joycodesBySeqType.TryGetValue(seqType, out string? joycode)) continue;
                        foreach (string generic in joycodeMapping.Translate(joycode))
                            if (!genericInputs.Contains(generic)) genericInputs.Add(generic);
                    }

                    string joycodes = string.Join(", ", joycodesBySeqType.Values);
                    if (genericInputs.Count > 0)
                    {
                        overrides[inputName] = genericInputs;
                        _logger.Debug($"MAME override: {inputName} ({joycodes}) -> {string.Join(", ", genericInputs)}");
                    }
                    else
                    {
                        _logger.Debug($"MAME cfg: {inputName} ({joycodes}) -> unknown JOYCODE");
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

    /// <summary>
    /// Maps a MAME cfg port type onto the platform button vocabulary Controllers.xml and
    /// Labels.xml are written against. Only <c>START1</c>/<c>COIN1</c> are renamed; every other
    /// port keeps its own name, and null comes back only when the cfg named no type at all.
    ///
    /// <para><c>START1</c> and <c>COIN1</c> collapse to bare <c>START</c>/<c>COIN</c> because
    /// that is the spelling thousands of existing entries already use — renaming them now would
    /// strand every one. They are the only exception; nothing else is rewritten.</para>
    ///
    /// <para>Nothing is filtered out by name, either. Whether a port can reach the overlay is
    /// settled one step later by the joycode bound to it: <see cref="JoycodeMapping"/> only ever
    /// recognizes <c>JOYCODE_1_*</c> tokens, so another player's control translates to nothing
    /// and is dropped there. Filtering on the port name as well used to duplicate that check,
    /// less accurately — MAME routinely parks a player-one control in a slot named for somebody
    /// else, and a name-based filter discards precisely those. It cost hwchamp's second boxing
    /// lever (<c>P2_AD_STICK_Z</c> on <c>JOYCODE_1_RZAXIS</c>, see #16), 20pacgal's Galaga start
    /// button (<c>START3</c> on <c>JOYCODE_1_BUTTON6</c>), and the PlayChoice-10 cabinets' two
    /// game-menu buttons (<c>SERVICE</c> on <c>JOYCODE_1_BUTTON5</c>), each carved out in turn as
    /// it was noticed. Letting the joycode decide retires the whole class of bug.</para>
    /// </summary>
    private static string? NormalizePortType(string? portType) => portType switch
    {
        null => null,
        "START1" => "START",
        "COIN1" => "COIN",
        string t => t
    };
}
