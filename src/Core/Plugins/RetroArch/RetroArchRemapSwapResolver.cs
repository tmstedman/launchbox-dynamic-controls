namespace DynamicControls.Plugins.RetroArch;

/// <summary>
/// Extracts slot→coreId swap entries from a RetroArch .rmp remap file. Processes
/// <c>input_player1_btn_*</c> (integer or hat-notation d-pad) and <c>input_player1_axis_*</c>
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
        const string axisPrefix = "input_player1_axis_";

        var swaps = new Dictionary<string, int>();
        foreach ((string? key, string? value) in data.Game)
        {
            string slot;
            int coreId;
            if (key.StartsWith(btnPrefix))
            {
                slot = key[btnPrefix.Length..];
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

            if (!RetroArchMappings.SlotToId.TryGetValue(slot, out int defaultId) || coreId == defaultId) continue;
            swaps[slot] = coreId;
            string? targetName = RetroArchMappings.IdToSlot.GetValueOrDefault(coreId);
            _logger.Debug($"RetroArch remap entry: '{slot}' -> core ID {coreId} ({targetName ?? "?"})");
        }
        return swaps;
    }
}
