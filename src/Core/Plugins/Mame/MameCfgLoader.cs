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
    IJoycodeMappingLoader joycodeMappingLoader) : IMameCfgLoader
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

    private readonly ILogger _logger = logger;
    private readonly IFileSystem _fs = fs;
    private readonly IJoycodeMappingLoader _joycodeMappingLoader = joycodeMappingLoader;

    public Dictionary<string, List<string>>? Load(string path)
    {
        _logger.Debug($"MAME cfg path: {path}, Exists: {_fs.FileExists(path)}");
        if (!_fs.FileExists(path)) return null;

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
    /// Normalizes a MAME cfg port type to the canonical input name used by the platform XML and
    /// labels. Returns null for ports we ignore (player 3-4, unrecognized types).
    ///
    /// <para><c>P1_*</c>/<c>P2_*</c> per-player actions (BUTTONn, JOYSTICK_*, AD_STICK_*, ...)
    /// pass through unchanged, prefix included. A single-player game sometimes has no room left
    /// in its P1 input slots for an extra axis or button, so MAME borrows a P2 slot for it — the
    /// player-1 controller's own JOYCODE still drives that <c>P2_*</c> port (see #16). Keeping
    /// the prefix, rather than stripping "P1_" and dropping "P2_" as before, is what lets a
    /// borrowed P2 slot and a real P1 slot coexist as distinct button names instead of colliding
    /// on write; a genuine second player's <c>P2_*</c> port still produces nothing downstream,
    /// since <see cref="JoycodeMapping"/> only ever recognizes <c>JOYCODE_1_*</c> tokens.</para>
    ///
    /// <para>Cabinet/system inputs use a trailing "1" instead (START1, COIN1) -> drop the digit;
    /// these have no per-player borrowing concern, so they stay collapsed to one name.</para>
    /// </summary>
    private static string? NormalizePortType(string? portType) => portType switch
    {
        null => null,
        string t when t.StartsWith("P1_") || t.StartsWith("P2_") => t,
        "START1" => "START",
        "COIN1" => "COIN",
        _ => null
    };
}
