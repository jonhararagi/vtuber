using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using VtuberBot.Core;

namespace VtuberBot.App;

/// <summary>
/// Sends one explicit, user-requested key only after locating PNGTuber Plus and bringing its
/// window to the foreground. Windows reports key injection, not whether PNGTuber Plus changed a sprite.
/// </summary>
internal sealed class WindowsPngtuberHotkeySender : IPngtuberHotkeySender
{
    private const uint InputKeyboard = 1;
    private const uint KeyUp = 0x0002;

    public HotkeySendResult Send(string executablePath, string key)
    {
        if (!OperatingSystem.IsWindows())
            return new(HotkeySendStatus.Unsupported, "El envío de teclas solo está disponible en Windows.");
        if (!File.Exists(executablePath))
            return new(HotkeySendStatus.NotConfigured, "El ejecutable de PNGTuber Plus no existe.");

        if (!TryGetVirtualKey(key, out var virtualKey))
            return new(HotkeySendStatus.Unsupported, "La tecla no está soportada por este adaptador.");

        Process? target = null;
        try
        {
            var processName = Path.GetFileNameWithoutExtension(executablePath);
            foreach (var candidate in Process.GetProcessesByName(processName))
            {
                try
                {
                    var imagePath = candidate.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(imagePath) &&
                        string.Equals(Path.GetFullPath(imagePath), Path.GetFullPath(executablePath),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        target = candidate;
                        break;
                    }
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Do not target a process whose executable identity cannot be verified.
                }
                catch (InvalidOperationException)
                {
                    // The process exited while being inspected.
                }

                candidate.Dispose();
            }

            if (target is null)
                return new(HotkeySendStatus.TargetNotRunning, "No se encontró el proceso de PNGTuber Plus para esa ruta.");

            var window = target.MainWindowHandle;
            if (window == IntPtr.Zero)
                return new(HotkeySendStatus.Cancelled, "PNGTuber Plus no expone una ventana principal visible para el envío del atajo.");

            _ = SetForegroundWindow(window);
            Thread.Sleep(80);
            if (GetForegroundWindow() != window)
                return new(HotkeySendStatus.Cancelled, "Windows no permitió enfocar PNGTuber Plus; no se envió la tecla.");

            var inputs = new[]
            {
                new Input { Type = InputKeyboard, Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = virtualKey } } },
                new Input { Type = InputKeyboard, Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = virtualKey, Flags = KeyUp } } }
            };
            var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
            if (sent != inputs.Length)
            {
                var error = Marshal.GetLastWin32Error();
                return new(HotkeySendStatus.Error, $"Windows solo aceptó {sent} de {inputs.Length} eventos de tecla (error {error}).");
            }

            return new(HotkeySendStatus.SentToForegroundApplication,
                $"Windows envió la tecla {key.ToUpperInvariant()} a PNGTuber Plus. El cambio visual no se puede verificar automáticamente.");
        }
        catch (Exception exception)
        {
            return new(HotkeySendStatus.Error, "Falló el envío del atajo: " + exception.Message);
        }
        finally
        {
            target?.Dispose();
        }
    }

    private static bool TryGetVirtualKey(string key, out ushort virtualKey)
    {
        var normalized = key.Trim().ToUpperInvariant();
        var consoleName = normalized.Length == 1 && normalized[0] is >= '0' and <= '9'
            ? "D" + normalized
            : normalized;
        if (Enum.TryParse<ConsoleKey>(consoleName, ignoreCase: true, out var consoleKey))
        {
            virtualKey = (ushort)consoleKey;
            return true;
        }

        virtualKey = 0;
        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        // INPUT contains a union whose size is determined by MOUSEINPUT on x64.
        [FieldOffset(0)] public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);
}
