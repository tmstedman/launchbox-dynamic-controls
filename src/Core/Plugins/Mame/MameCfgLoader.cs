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

        // MAME writes one <port> per tag and bit a type sits on, so one type can appear several
        // times. The last of them keeps the bare names; each earlier one is kept whole, nudges
        // and all, under its qualified name (see QualifiedName), after everything else, and the
        // last is kept under its own qualified name too, so every port of the type has one. A port
        // states its type's bare name whatever else it binds, so a type has one owner at a time,
        // and a new port of the type takes over the owner's slots by name, keeping any it doesn't
        // restate stated but empty.
        var slots = new List<MamePortReach>();
        var indexByName = new Dictionary<string, int>();
        var ownerByType = new Dictionary<string, (string Qualified, List<string> Names)>();
        var usedQualified = new HashSet<string>();
        var positionByType = new Dictionary<string, int>();
        var displaced = new List<MamePortReach>();
        var placeholders = new HashSet<string>();
        var repeatedTypes = new List<string>();

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

                    // Every port of the type counts, bound or not, since the position is read
                    // off the cfg.
                    int position = positionByType[inputName] = positionByType.GetValueOrDefault(inputName) + 1;

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

                    string qualifiedPort = QualifiedName(inputName, portNode);
                    // Nothing on the port to name it by, or another port already named the same:
                    // its place among its type tells them apart.
                    if (qualifiedPort == inputName || !usedQualified.Add(qualifiedPort))
                    {
                        qualifiedPort += $"#{position}";
                        usedQualified.Add(qualifiedPort);
                    }

                    // The type's earlier owner gives up every slot it holds, and this port refills
                    // those it states again.
                    var freed = new Dictionary<string, int>();
                    if (ownerByType.TryGetValue(inputName, out (string Qualified, List<string> Names) owner))
                    {
                        if (!repeatedTypes.Contains(inputName)) repeatedTypes.Add(inputName);
                        foreach (string name in owner.Names)
                        {
                            int index = indexByName[name];
                            freed[name] = index;
                            if (placeholders.Remove(name)) continue;
                            // The port's own name or one of its nudges (PORT.inc), qualified alike.
                            MamePortReach moved = slots[index] with { Name = owner.Qualified + name[inputName.Length..] };
                            displaced.Add(moved);
                            _logger.Debug($"MAME cfg: {name} appears again, so the earlier one is {moved.Name}");
                        }
                    }

                    foreach (MamePortReach button in reach)
                    {
                        if (freed.TryGetValue(button.Name, out int index))
                        {
                            slots[index] = button;
                            indexByName[button.Name] = index;
                        }
                        else
                        {
                            indexByName[button.Name] = slots.Count;
                            slots.Add(button);
                        }
                        _logger.Debug(button.Inputs.Count > 0
                            ? $"MAME override: {button.Name} ({button.From}) -> {string.Join(", ", button.Inputs)}"
                                + (button.Derived.Count > 0 ? $" (derived: {string.Join(", ", button.Derived)})" : "")
                            : $"MAME override: {button.Name} ({button.From}) -> nothing, baseline dropped");
                    }

                    // A name the earlier owner stated and this port doesn't stays stated, reaching
                    // nothing: left unstated, Controllers.xml's guess for it would come back.
                    List<string> owned = [.. reach.Select(button => button.Name)];
                    foreach ((string name, int index) in freed)
                    {
                        if (owned.Contains(name)) continue;
                        slots[index] = new MamePortReach(name, [], [], "unbound");
                        placeholders.Add(name);
                        owned.Add(name);
                    }
                    ownerByType[inputName] = (qualifiedPort, owned);
                }
            }
        }

        // The last port of each repeated type answers to its qualified name as well as the bare
        // one, so a game's labels can name every port the same way. Its placeholders reach nothing
        // and have no qualified form to offer.
        foreach (string type in repeatedTypes)
        {
            (string qualified, List<string> names) = ownerByType[type];
            displaced.AddRange(names
                .Where(name => !placeholders.Contains(name))
                .Select(name => slots[indexByName[name]] with { Name = qualified + name[type.Length..] }));
        }

        return [.. slots, .. displaced];
    }

    /// <summary>
    /// The name a port is kept under if a later port of its type takes the bare one: its type,
    /// then its tag without MAME's leading colon, then its mask — <c>OTHER:STROBE1:2</c>. The
    /// caller adds the port's position (<c>#2</c>) where this alone would not tell it apart. A tag
    /// may itself hold colons (<c>COIN1:mainpcb:IN0:128</c>), so the name is matched whole, never
    /// split. MAME tells ports apart by tag and mask together, and neither alone is enough:
    /// escounts puts four <c>OTHER</c> ports on one tag, and gahaha's two sticks share a full-range
    /// mask on different tags. No spaces, which a Labels.xml name reserves for button combinations.
    /// </summary>
    private static string QualifiedName(string type, XmlElement portNode)
    {
        string tag = (portNode.Attributes["tag"]?.Value ?? "").TrimStart(':');
        string? mask = portNode.Attributes["mask"]?.Value;
        return string.Join(":", new[] { type, tag, mask }.Where(part => !string.IsNullOrEmpty(part)));
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
