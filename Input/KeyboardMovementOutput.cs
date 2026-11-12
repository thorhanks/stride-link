using System.ComponentModel;
using System.Runtime.InteropServices;

namespace StrideLink.Input;

public sealed class KeyboardMovementOutput(ILogger<KeyboardMovementOutput> logger) : IMovementOutput
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventScanCode = 0x0008;
    private const ushort WScanCode = 0x11;
    private readonly object _gate = new();
    private bool _moving;

    static KeyboardMovementOutput()
    {
        var expectedSize = IntPtr.Size == 8 ? 40 : 28;
        var actualSize = Marshal.SizeOf<INPUT>();
        if (actualSize != expectedSize)
            throw new TypeInitializationException(
                nameof(KeyboardMovementOutput),
                new InvalidOperationException($"Win32 INPUT is {actualSize} bytes; expected {expectedSize}."));
    }

    public bool IsMoving
    {
        get { lock (_gate) return _moving; }
    }

    public void SetMoving(bool active)
    {
        lock (_gate)
        {
            if (_moving == active)
                return;

            if (!OperatingSystem.IsWindows())
            {
                _moving = active;
                logger.LogInformation("Movement {State} (keyboard output unavailable on this OS)", active ? "ON" : "OFF");
                return;
            }

            var input = new INPUT
            {
                Type = InputKeyboard,
                Data = new InputUnion
                {
                    Keyboard = new KEYBDINPUT
                    {
                        VirtualKey = 0,
                        ScanCode = WScanCode,
                        Flags = KeyEventScanCode | (active ? 0u : KeyEventKeyUp)
                    }
                }
            };

            if (SendInput(1, [input], Marshal.SizeOf<INPUT>()) != 1)
            {
                var error = new Win32Exception(Marshal.GetLastWin32Error());
                logger.LogError(error, "Could not send the W key event");
                return;
            }

            _moving = active;
            Console.WriteLine(active ? "Movement: W DOWN" : "Movement: W UP");
        }
    }

    public void Dispose() => SetMoving(false);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, INPUT[] inputs, int inputSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT Keyboard;
        // The native INPUT union is sized by its largest member. Including
        // MOUSEINPUT makes Marshal.SizeOf<INPUT>() match Win32 on x86/x64.
        [FieldOffset(0)] public MOUSEINPUT Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }
}
