namespace DynamicControls.Plugins.RetroArch;

/// <summary>
/// Detects input swaps from the game-level RetroArch cfg file. Reads <c>input_joypad_driver</c>
/// from the global level to skip swap detection for dinput users — they bypass SDL's
/// GameControllerDB normalisation. Translates <c>input_player1_*_btn</c> (integer or
/// hat-notation d-pad) and <c>_axis</c> entries from the game level into a slot→coreId swap map
/// matching the rmp swap shape.
/// </summary>
public class RetroArchCfgSwapResolver(ILogger logger) : IRetroArchSwapResolver
{
    private readonly ILogger _logger = logger;

    public Dictionary<string, int> ResolveSwaps(RetroArchGameData data)
    {
        string? joypadDriver = data.Global?.GetValueOrDefault("input_joypad_driver");

        if (string.Equals(joypadDriver, "dinput", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Info("RetroArch cfg: dinput driver detected; per-game button swap detection is not supported. Switch to the sdl2 joypad driver in RetroArch settings for full support.");
            return [];
        }

        if (data.Game == null) return [];

        (Dictionary<string, int> btnEntries, Dictionary<string, string> hatEntries, Dictionary<string, string> axisEntries) = ParseEntries(data.Game);
        if (btnEntries.Count == 0 && hatEntries.Count == 0 && axisEntries.Count == 0) return [];

        var swaps = new Dictionary<string, int>();
        ProcessBtnSwaps(btnEntries, swaps);
        ProcessHatSwaps(hatEntries, swaps);
        ProcessAxisSwaps(axisEntries, swaps);
        return swaps;
    }

    private static (Dictionary<string, int> Btn, Dictionary<string, string> Hat, Dictionary<string, string> Axis)
        ParseEntries(Dictionary<string, string> gameCfg)
    {
        var btn = new Dictionary<string, int>();
        var hat = new Dictionary<string, string>();
        var axis = new Dictionary<string, string>();

        const string prefix = "input_player1_";
        foreach ((string key, string value) in gameCfg)
        {
            if (!key.StartsWith(prefix)) continue;
            string rest = key[prefix.Length..];

            if (rest.EndsWith("_btn") && int.TryParse(value, out int physBtn))
                btn[rest[..^4]] = physBtn;
            else if (rest.EndsWith("_btn") && value.StartsWith("h"))
                hat[rest[..^4]] = value;
            else if (rest.EndsWith("_axis"))
                axis[rest[..^5]] = value;
        }

        return (btn, hat, axis);
    }

    private void ProcessBtnSwaps(Dictionary<string, int> btnEntries, Dictionary<string, int> swaps)
    {
        foreach ((string slot, int physBtn) in btnEntries)
        {
            if (!RetroArchMappings.SlotToBtnNumber.TryGetValue(slot, out int canonical) || physBtn == canonical) continue;
            if (physBtn == -1)
            {
                swaps[slot] = -1;
                _logger.Debug($"RetroArch cfg swap: '{slot}' disabled (btn -1)");
                continue;
            }
            if (RetroArchMappings.BtnNumberToSlot.TryGetValue(physBtn, out string? targetSlot) &&
                RetroArchMappings.SlotToId.TryGetValue(targetSlot, out int coreId))
            {
                swaps[slot] = coreId;
                _logger.Debug($"RetroArch cfg swap: '{slot}' btn {physBtn} (canonical {canonical}) → slot '{targetSlot}' coreId {coreId}");
            }
        }
    }

    private void ProcessHatSwaps(Dictionary<string, string> hatEntries, Dictionary<string, int> swaps)
    {
        foreach ((string slot, string hatVal) in hatEntries)
        {
            if (!RetroArchMappings.SlotToHatValue.TryGetValue(slot, out string? canonical) || hatVal == canonical) continue;
            if (RetroArchMappings.HatValueToSlot.TryGetValue(hatVal, out string? targetSlot) &&
                RetroArchMappings.SlotToId.TryGetValue(targetSlot, out int coreId))
            {
                swaps[slot] = coreId;
                _logger.Debug($"RetroArch cfg swap: '{slot}' hat {hatVal} (canonical {canonical}) → slot '{targetSlot}' coreId {coreId}");
            }
        }
    }

    private void ProcessAxisSwaps(Dictionary<string, string> axisEntries, Dictionary<string, int> swaps)
    {
        foreach ((string slot, string axisVal) in axisEntries)
        {
            if (!RetroArchMappings.SlotToAxisValue.TryGetValue(slot, out string? canonical) || axisVal == canonical) continue;
            if (!RetroArchMappings.AxisValueToSlot.TryGetValue(axisVal, out string? targetSlot))
            {
                _logger.Debug($"RetroArch cfg: '{slot}' axis {axisVal} not in canonical baseline; swap skipped");
                continue;
            }
            if (!RetroArchMappings.SlotToId.TryGetValue(targetSlot, out int coreId))
            {
                _logger.Debug($"RetroArch cfg: '{slot}' axis {axisVal} targets stick slot '{targetSlot}'; stick-axis swaps not supported");
                continue;
            }
            swaps[slot] = coreId;
            _logger.Debug($"RetroArch cfg swap: '{slot}' axis {axisVal} (canonical {canonical}) → slot '{targetSlot}' coreId {coreId}");
        }
    }
}
