using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DynamicControls.LaunchBox;

/// <summary>Which way a source's buttons ask the pause screen to turn.</summary>
[Flags]
internal enum ControlSetPress
{
    None = 0,
    Back = 1,
    Forward = 2,
}

/// <summary>Somewhere a press to step control sets can come from, read by polling.</summary>
internal interface IPressSource
{
    /// <summary>What the source is called in the log ("keyboard").</summary>
    string Name { get; }

    /// <summary>Readies the source for a pause screen and describes what it found, for the log.</summary>
    string Begin();

    /// <summary>The step buttons down now.</summary>
    ControlSetPress Read();
}

/// <summary>What each source's buttons mean, apart from reading them.</summary>
internal static class ControlSetPresses
{
    private const int VkPageUp = 0x21;
    private const int VkPageDown = 0x22;
    private const int VkLeft = 0x25;
    private const int VkRight = 0x27;

    private const ushort DpadLeft = 0x0004;
    private const ushort DpadRight = 0x0008;
    private const ushort LeftShoulder = 0x0100;
    private const ushort RightShoulder = 0x0200;

    /// <summary>The step buttons among the virtual keys <paramref name="isDown"/> says are held.</summary>
    internal static ControlSetPress FromKeys(Func<int, bool> isDown) =>
        (isDown(VkLeft) || isDown(VkPageUp) ? ControlSetPress.Back : ControlSetPress.None)
        | (isDown(VkRight) || isDown(VkPageDown) ? ControlSetPress.Forward : ControlSetPress.None);

    /// <summary>The step buttons among an XInput pad's <c>wButtons</c>.</summary>
    internal static ControlSetPress FromGamepad(ushort buttons) =>
        ((buttons & (DpadLeft | LeftShoulder)) != 0 ? ControlSetPress.Back : ControlSetPress.None)
        | ((buttons & (DpadRight | RightShoulder)) != 0 ? ControlSetPress.Forward : ControlSetPress.None);
}

/// <summary>
/// The keyboard, asked for its state outright: WPF delivers no key events to a pause theme, and
/// this needs no focus.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Windows API shim; the mapping is covered through ControlSetPresses")]
internal sealed class KeyboardPressSource : IPressSource
{
    public string Name => "keyboard";

    public string Begin() => "keyboard (Left/Right, Page Up/Page Down)";

    public ControlSetPress Read() => ControlSetPresses.FromKeys(vk => (GetAsyncKeyState(vk) & 0x8000) != 0);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}

/// <summary>
/// XInput pads: Xbox controllers, and whatever drives as one (Steam Input, an XInput-mode
/// adapter). A PlayStation or generic DirectInput pad is not seen. Only the pads connected as the
/// pause screen opens are read, since asking after an empty slot is slow on some Windows versions.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Windows API shim; the mapping is covered through ControlSetPresses")]
internal sealed class GamepadPressSource : IPressSource
{
    private const uint Success = 0;
    private const uint MaxPads = 4;

    private uint[] _connected = [];
    private bool _unavailable;

    public string Name => "gamepad";

    public string Begin()
    {
        _connected = [];
        if (_unavailable) return "gamepad (XInput isn't available)";

        try
        {
            _connected = [.. Enumerable.Range(0, (int)MaxPads).Select(i => (uint)i).Where(i => XInputGetState(i, out _) == Success)];
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            _unavailable = true;
            return "gamepad (XInput isn't available)";
        }

        return _connected.Length == 0
            ? "gamepad (no XInput pad connected)"
            : $"gamepad (XInput, {_connected.Length} connected)";
    }

    public ControlSetPress Read()
    {
        ControlSetPress down = ControlSetPress.None;
        foreach (uint pad in _connected)
        {
            if (XInputGetState(pad, out XInputState state) == Success)
                down |= ControlSetPresses.FromGamepad(state.Buttons);
        }
        return down;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [DllImport("xinput1_4.dll")]
    private static extern uint XInputGetState(uint userIndex, out XInputState state);
}
