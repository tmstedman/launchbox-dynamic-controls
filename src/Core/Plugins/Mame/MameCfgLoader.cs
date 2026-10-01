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

                    // The cfg's port type IS the platform button name — Controllers.xml and
                    // Labels.xml are written against MAME's own vocabulary, with no rewriting
                    // in between. Which ports can reach the overlay is settled below, by
                    // whether their joycode translates.
                    string? inputName = portNode.Attributes["type"]?.Value;
                    if (string.IsNullOrEmpty(inputName)) continue;

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
}
