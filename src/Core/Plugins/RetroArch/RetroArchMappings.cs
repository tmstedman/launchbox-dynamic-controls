namespace DynamicControls.Plugins.RetroArch;

/// <summary>
/// Shared RetroPad slot name to generic input name mapping used by both the remap
/// and cfg mapping sources.
/// </summary>
internal static class RetroArchMappings
{
    // RetroPad slot name -> generic input name used in Controllers.xml
    // cfg/rmp: slot name extracted from key, e.g. input_player1_b_btn (cfg) or input_player1_btn_b (rmp)
    //          → "b" → "ButtonA" (South face button in Controllers.xml vocabulary)
    internal static readonly Dictionary<string, string> RetroPadToGeneric = new()
    {
        { "b", "ButtonA" },
        { "a", "ButtonB" },
        { "y", "ButtonX" },
        { "x", "ButtonY" },
        { "l", "ButtonLeftShoulder" },
        { "r", "ButtonRightShoulder" },
        { "l2", "AxisTriggerLeft" },
        { "r2", "AxisTriggerRight" },
        { "l3", "ButtonLeftStick" },
        { "r3", "ButtonRightStick" },
        { "up", "ButtonDpadUp" },
        { "down", "ButtonDpadDown" },
        { "left", "ButtonDpadLeft" },
        { "right", "ButtonDpadRight" },
        { "start", "ButtonStart" },
        { "select", "ButtonBack" },
        // Analog stick axes — cfg/rmp files split each axis into _plus/_minus directions
        { "l_x_plus", "AxisLeftStickRight" }, { "l_x_minus", "AxisLeftStickLeft" },
        { "l_y_plus", "AxisLeftStickDown" },  { "l_y_minus", "AxisLeftStickUp" },
        { "r_x_plus", "AxisRightStickRight" }, { "r_x_minus", "AxisRightStickLeft" },
        { "r_y_plus", "AxisRightStickDown" },  { "r_y_minus", "AxisRightStickUp" },
    };

    // RetroPad slot name -> coreId (the integer swap target written in rmp files)
    // rmp: input_player1_btn_a = 8   (slot "a" → coreId 8, canonical — no change)
    // rmp: input_player1_btn_a = 0   (slot "a" → coreId 0 = "b" — "a" now displays "b"'s label)
    internal static readonly Dictionary<string, int> SlotToId = new()
    {
        { "b", 0 }, { "y", 1 }, { "select", 2 }, { "start", 3 },
        { "up", 4 }, { "down", 5 }, { "left", 6 }, { "right", 7 },
        { "a", 8 }, { "x", 9 }, { "l", 10 }, { "r", 11 },
        { "l2", 12 }, { "r2", 13 }, { "l3", 14 }, { "r3", 15 }
    };

    // coreId -> RetroPad slot name (inverse of SlotToId, used to resolve swap targets)
    internal static readonly Dictionary<int, string> IdToSlot =
        SlotToId.ToDictionary(e => e.Value, e => e.Key);

    // RetroPad slot name -> canonical SDL/XInput physical button number.
    // cfg: input_player1_a_btn = "1"  → slot "a", physical btn 1 (canonical, no swap)
    // cfg: input_player1_a_btn = "0"  → slot "a", physical btn 0 (non-canonical → swap detected)
    // D-pad uses hat notation (e.g. "h0up") rather than a plain integer — see SlotToHatValue.
    internal static readonly Dictionary<string, int> SlotToBtnNumber = new()
    {
        { "b", 0 }, { "a", 1 }, { "y", 2 }, { "x", 3 },
        { "l", 4 }, { "r", 5 }, { "select", 6 }, { "start", 7 },
        { "l3", 8 }, { "r3", 9 }
    };

    // Physical button number -> RetroPad slot name (inverse of SlotToBtnNumber, used to
    // identify which slot canonically owns the physical button found in a cfg swap entry)
    internal static readonly Dictionary<int, string> BtnNumberToSlot =
        SlotToBtnNumber.ToDictionary(e => e.Value, e => e.Key);

    // RetroPad slot name -> canonical SDL/XInput axis value string ("+N" or "-N").
    // cfg: input_player1_l2_axis = "+2"  → slot "l2", axis "+2" (canonical, no swap)
    // cfg: input_player1_l2_axis = "+5"  → slot "l2", axis "+5" (non-canonical → swap detected)
    // rmp: input_player1_axis_l2 = "+5"  → slot "l2" remapped to axis "+5" → coreId of "r2"
    // Axis layout: 0=LX, 1=LY, 2=LT, 3=RX, 4=RY, 5=RT.
    internal static readonly Dictionary<string, string> SlotToAxisValue = new()
    {
        { "l2", "+2" }, { "r2", "+5" },
        { "l_x_plus", "+0" }, { "l_x_minus", "-0" },
        { "l_y_plus", "+1" }, { "l_y_minus", "-1" },
        { "r_x_plus", "+3" }, { "r_x_minus", "-3" },
        { "r_y_plus", "+4" }, { "r_y_minus", "-4" },
    };

    // Axis value string -> RetroPad slot name (inverse of SlotToAxisValue, used to identify
    // which slot canonically owns the axis value found in a cfg or rmp swap entry)
    internal static readonly Dictionary<string, string> AxisValueToSlot =
        SlotToAxisValue.ToDictionary(e => e.Value, e => e.Key);

    // RetroPad slot name -> canonical SDL/XInput hat value string for d-pad directions.
    // cfg: input_player1_up_btn = "h0up"    → slot "up", hat "h0up" (canonical, no swap)
    // cfg: input_player1_up_btn = "h0down"  → slot "up", hat "h0down" (non-canonical → swap detected)
    internal static readonly Dictionary<string, string> SlotToHatValue = new()
    {
        { "up", "h0up" }, { "down", "h0down" }, { "left", "h0left" }, { "right", "h0right" }
    };

    // Hat value string -> RetroPad slot name (inverse of SlotToHatValue, used to identify
    // which d-pad direction canonically owns the hat value found in a cfg swap entry)
    internal static readonly Dictionary<string, string> HatValueToSlot =
        SlotToHatValue.ToDictionary(e => e.Value, e => e.Key);
}
