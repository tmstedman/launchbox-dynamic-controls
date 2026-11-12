namespace DynamicControls.Plugins.RetroArch;

/// <summary>
/// Extracts slot→coreId swap entries from a RetroArch .rmp remap file. Processes
/// <c>input_player1_btn_*</c> (integer or hat-notation d-pad), <c>input_player1_stk_*</c> (a stick
/// direction, by RetroArch's own name for it: <c>r_y+</c>) and <c>input_player1_axis_*</c>
/// entries from <see cref="RetroArchGameData.Game"/> (game-level only, per the trust boundary —
/// Controllers.xml already incorporates non-game-specific remap swaps).
/// </summary>
public class RetroArchRemapSwapResolver(ILogger logger) : IRetroArchSwapResolver
{
    private readonly ILogger _logger = logger;

    public Dictionary<string, int> ResolveSwaps(RetroArchGameData data)
    {
        if (data.Game == null) return [];

        const string btnPrefix = "input_player1_btn_";
        const string stkPrefix = "input_player1_stk_";
        const string axisPrefix = "input_player1_axis_";

        var swaps = new Dictionary<string, int>();
        var remappedStickEnds = new List<string>();
        foreach ((string? key, string? value) in data.Game)
        {
            string slot;
            int coreId;
            if (key.StartsWith(btnPrefix))
            {
                slot = key[btnPrefix.Length..];
                if (!int.TryParse(value, out coreId)) continue;
            }
            else if (key.StartsWith(stkPrefix))
            {
                if (!RetroArchMappings.StickRemapSuffixToSlot.TryGetValue(key[stkPrefix.Length..], out string? stickSlot)) continue;
                slot = stickSlot;
                if (!int.TryParse(value, out coreId)) continue;
            }
            else if (key.StartsWith(axisPrefix))
            {
                slot = key[axisPrefix.Length..];
                if (!RetroArchMappings.AxisValueToSlot.TryGetValue(value, out string? targetSlot) ||
                    !RetroArchMappings.SlotToId.TryGetValue(targetSlot, out coreId))
                {
                    continue;
                }
            }
            else
            {
                continue;
            }

            if (RetroArchMappings.IdOfSlot(slot) is not int defaultId || coreId == defaultId) continue;
            swaps[slot] = coreId;
            if (key.StartsWith(stkPrefix)) remappedStickEnds.Add(slot);
            string? targetName = RetroArchMappings.TargetSlotOf(coreId);
            _logger.Debug($"RetroArch remap entry: '{slot}' -> core ID {coreId} ({targetName ?? "?"})");
        }

        // RetroArch takes a stick's axis from the core whole once either end is remapped, and gives
        // back only the ends that are: an end the file leaves alone presses nothing.
        foreach (string end in remappedStickEnds)
        {
            string otherEnd = RetroArchMappings.StickAxisOtherEnd[end];
            if (swaps.TryAdd(otherEnd, -1))
                _logger.Debug($"RetroArch remap entry: '{otherEnd}' presses nothing, '{end}' being remapped");
        }
        return swaps;
    }
}
