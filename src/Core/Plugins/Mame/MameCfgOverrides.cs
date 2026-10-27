namespace DynamicControls.Plugins.Mame;

/// <summary>
/// What a MAME cfg binds: each port's platform button name mapped to the generic inputs it
/// reaches, own bindings first. <see cref="Derived"/> names which of those a button reaches only
/// because its port's axis does — a nudge reaching its half of the stick — so the mapping can
/// rank them below a binding of the button's own.
/// </summary>
public class MameCfgOverrides : Dictionary<string, List<string>>
{
    /// <summary>
    /// For each button with any, the inputs in its list that are derived rather than bound. Each
    /// also appears in the button's own entry.
    /// </summary>
    public Dictionary<string, List<string>> Derived { get; } = [];
}
