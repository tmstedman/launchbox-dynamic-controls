using System.Xml;

namespace DynamicControls.Plugins.Mame;

/// <summary>
/// Loads a single MAME .cfg file and returns every platform button its ports state, with what
/// each reaches. Returns null when the file does not exist. Implementations may translate
/// joycodes via an <see cref="IJoycodeMappingLoader"/>, but that is an internal concern — the
/// consumer sees only the resulting buttons.
/// </summary>
public interface IMameCfgLoader
{
    IReadOnlyList<MamePortReach>? Load(string path);
}

/// <summary>
/// Reads a MAME cfg file: waits out MAME's own write, walks system/input/port, and hands each
/// port's declared <c>&lt;newseq&gt;</c>s to <see cref="MamePortBinding"/>, which decides what
/// the port's buttons reach. Returns null if the file does not exist; a port whose joycodes
/// translate to nothing is logged and skipped.
/// </summary>
public class MameCfgLoader(
    ILogger logger,
    IFileSystem fs,
    IJoycodeMappingLoader joycodeMappingLoader,
    IDelay delay) : IMameCfgLoader
{
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

    public IReadOnlyList<MamePortReach>? Load(string path)
    {
        if (!WaitForFile(path)) return null;

        JoycodeMapping joycodeMapping = _joycodeMappingLoader.Load();

        using Stream stream = _fs.OpenRead(path);
        var doc = new XmlDocument();
        doc.Load(stream);
        XmlElement root = doc.DocumentElement!;

        // MAME writes one <port> per tag a type sits on, so one type can appear twice. A button
        // carries one binding, so the later replaces the earlier, in the earlier's place.
        var overrides = new List<MamePortReach>();
        var indexByName = new Dictionary<string, int>();

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

                    var declared = new Dictionary<string, MameSequence>();
                    foreach (XmlElement seqNode in portNode.ChildNodes.OfType<XmlElement>())
                    {
                        if (seqNode.Name != "newseq") continue;
                        string? seqType = seqNode.Attributes["type"]?.Value;
                        if (seqType == null || !MamePortBinding.SequenceTypes.Contains(seqType)) continue;
                        declared[seqType] = MameSequence.Parse(seqNode.InnerText);
                    }

                    if (!declared.Values.Any(s => s.IsBound)) continue;

                    IReadOnlyList<MamePortReach> reach = MamePortBinding.Resolve(inputName, declared, joycodeMapping);
                    if (reach.Count == 0)
                    {
                        string joycodes = string.Join(", ", declared.Values.Where(s => s.IsBound));
                        _logger.Debug($"MAME cfg: {inputName} ({joycodes}) -> unknown JOYCODE");
                        continue;
                    }

                    foreach (MamePortReach button in reach)
                    {
                        if (indexByName.TryGetValue(button.Name, out int index))
                        {
                            overrides[index] = button;
                        }
                        else
                        {
                            indexByName[button.Name] = overrides.Count;
                            overrides.Add(button);
                        }
                        _logger.Debug(button.Inputs.Count > 0
                            ? $"MAME override: {button.Name} ({button.From}) -> {string.Join(", ", button.Inputs)}"
                                + (button.Derived.Count > 0 ? $" (derived: {string.Join(", ", button.Derived)})" : "")
                            : $"MAME override: {button.Name} ({button.From}) -> nothing, baseline dropped");
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
