using DynamicControls.InputMapping;

namespace DynamicControls.Plugins.RetroArch;

public interface IRetroArchSwapApplier
{
    InputMappingConfig Apply(InputMappingConfig baseConfig, Dictionary<string, int> swaps);
}

/// <summary>
/// Applies RetroArch slot→coreId swaps to an <see cref="InputMappingConfig"/>. Both the cfg
/// cascade and the .rmp file produce swaps in the same shape (slot name → libretro core ID), so
/// the same applier handles both — see <c>RetroArchMappingSource</c> for the call order
/// (cfg first, then rmp on top).
/// </summary>
public class RetroArchSwapApplier(ILogger logger) : IRetroArchSwapApplier
{
    private readonly ILogger _logger = logger;

    /// <summary>
    /// Applies slot→coreId swaps to a base mapping. For each entry:
    /// - The slot's physical button leaves its original generic position (removed from base).
    /// - If the entry targets another valid slot, the slot's physical button is added at that
    ///   slot's generic position, labelled with the original platform button there (so
    ///   simultaneous remaps don't see each other's intermediate state). A slot with no
    ///   original base entry (e.g. l2 on a 3-button controller) only adds without removing.
    /// Handles pure swaps (A↔B each removed and re-added at the other position), copies
    /// (l2 also does B), and disabled slots (coreId == -1, only remove).
    /// </summary>
    public InputMappingConfig Apply(InputMappingConfig baseConfig, Dictionary<string, int> swaps)
    {
        if (swaps.Count == 0) return baseConfig;

        var originalByGeneric = new Dictionary<string, MappingEntry>();
        foreach (MappingEntry m in baseConfig.Mappings)
        {
            originalByGeneric.TryAdd(m.Input, m);
        }

        var removes = new HashSet<string>();
        var additions = new List<MappingEntry>();

        foreach ((string? slot, int coreId) in swaps)
        {
            if (!RetroArchMappings.RetroPadToGeneric.TryGetValue(slot, out string? sourceGeneric)) continue;

            if (originalByGeneric.ContainsKey(sourceGeneric))
                removes.Add(sourceGeneric);

            if (!RetroArchMappings.IdToSlot.TryGetValue(coreId, out string? targetSlot)) continue;
            if (!RetroArchMappings.RetroPadToGeneric.TryGetValue(targetSlot, out string? targetGeneric)) continue;

            if (originalByGeneric.TryGetValue(targetGeneric, out MappingEntry? targetEntry))
            {
                additions.Add(new MappingEntry { Name = targetEntry.Name, Input = sourceGeneric });
                _logger.Debug($"RetroArch swap: '{targetEntry.Name}' at {sourceGeneric} (slot '{slot}' -> core '{targetSlot}')");
            }
        }

        return new InputMappingConfig
        {
            Controller = baseConfig.Controller,
            AnalogToDigital = baseConfig.AnalogToDigital,
            Mappings = [.. baseConfig.Mappings.Where(m => !removes.Contains(m.Input)), .. additions]
        };
    }
}
