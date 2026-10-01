using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using BebeRadio.Models;
using BebeRadio.Support;
using Windows.System;

namespace BebeRadio.Services;

/// <summary>
/// Servicio global de mapeo de teclas (hotkeys) para slots de paleta.
/// Usa <c>RegisterHotKey</c>/<c>UnregisterHotKey</c> (user32.dll) para KeyDown
/// y un hook de bajo nivel <c>WH_KEYBOARD_LL</c> para detectar KeyUp global
/// (necesario para el modo Momentáneo / push-to-talk).
/// Funciona aunque la app no tenga foco.
/// </summary>
public sealed class KeyMappingService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYUP = 0x0105;

    private readonly Microsoft.UI.Dispatching.DispatcherQueue _uiQueue;
    private readonly Dictionary<int, HotkeyEntry> _hotkeys = new();

    /// <summary>
    /// Slots en modo Momentáneo con la tecla físicamente pulsada.
    /// Suprime el auto-repeat de <c>WM_HOTKEY</c>: mientras el slot siga aquí,
    /// los <c>KeyDown</c> repetidos se ignoran; el <c>KeyUp</c> lo libera.
    /// Si el audio termina solo con la tecla aún pulsada, el slot sigue marcado
    /// hasta el <c>KeyUp</c> real: no re-dispara, exige soltar + volver a pulsar.
    /// </summary>
    private readonly HashSet<(string PropietariaId, int SlotIndex)> _pulsados = new();
    private readonly object _gate = new();
    private int _nextId = 1;
    private bool _disposed;

    // Hook de bajo nivel para KeyUp
    private IntPtr _hookHandle = IntPtr.Zero;
    private LowLevelKeyboardProc? _hookProc;

    // Handle de la ventana principal para RegisterHotKey
    private IntPtr _hWnd = IntPtr.Zero;

    /// <summary>Instancia singleton.</summary>
    public static KeyMappingService Instancia { get; } = new();

    /// <summary>
    /// Se invoca en el hilo UI cuando se pulsa una tecla registrada (KeyDown vía RegisterHotKey).
    /// <paramref name="string"/> = propietariaId, <paramref name="int"/> = slotIndex,
    /// <paramref name="ModoMapeo"/> = modo de interacción.
    /// </summary>
    public event Action<string, int, ModoMapeo>? OnHotkeyPressed;

    /// <summary>
    /// Se invoca en el hilo UI cuando se suelta una tecla registrada como Momentáneo (KeyUp vía hook LL).
    /// <paramref name="string"/> = propietariaId, <paramref name="int"/> = slotIndex.
    /// </summary>
    public event Action<string, int>? OnHotkeyReleased;

    private KeyMappingService()
    {
        _uiQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        Debug.Assert(_uiQueue is not null, "KeyMappingService debe crearse en el hilo UI.");

        // Instalar hook de bajo nivel para KeyUp global
        InstalarHookTeclado();
    }

    /// <summary>
    /// Inicializa el handle de la ventana principal.
    /// Debe llamarse ANTES de registrar cualquier hotkey.
    /// </summary>
    /// <param name="hWnd">Handle de la ventana principal (WindowNative.GetWindowHandle).</param>
    public void Inicializar(IntPtr hWnd)
    {
        _hWnd = hWnd;
        RegistroErrores.Traza("KeyMapping.Inicializar", $"hWnd=0x{hWnd:X}");
    }

    /// <summary>
    /// Registra un hotkey global para un slot.
    /// Idempotente: si el slot ya tenía registro, lo reemplaza (sin dejar huérfanos).
    /// Si solo cambió el modo (misma tecla+modificadores), actualiza en el lugar sin tocar el SO.
    /// </summary>
    /// <param name="propietariaId">Dueña de la paleta (enum o custom:id).</param>
    /// <param name="slotIndex">Índice 0-49 del slot.</param>
    /// <param name="tecla">Tecla (VirtualKey.None para no registrar).</param>
    /// <param name="modifiers">Modificadores (Control, Alt, Shift, Win).</param>
    /// <param name="modo">Modo de interacción.</param>
    /// <returns>True si se registró; false si la tecla ya está en uso por el sistema/otra app.</returns>
    public bool Registrar(string propietariaId, int slotIndex, VirtualKey tecla, VirtualKeyModifiers modifiers, ModoMapeo modo)
    {
        if (tecla == VirtualKey.None)
        {
            RegistroErrores.Traza("KeyMapping.Registrar", $"SKIP: tecla=None");
            return true;
        }

        var vk = (uint)tecla;
        var mod = ConvertirModifiers(modifiers);

        lock (_gate)
        {
            if (_disposed)
            {
                RegistroErrores.Traza("KeyMapping.Registrar", $"SKIP: disposed");
                return false;
            }

            var existentes = _hotkeys
                .Where(k => k.Value.PropietariaId == propietariaId && k.Value.SlotIndex == slotIndex)
                .ToList();

            // Solo cambió el modo: no tocar el SO, actualizar la entrada en el lugar.
            if (existentes.Count == 1
                && existentes[0].Value.Tecla == tecla
                && existentes[0].Value.Modifiers == modifiers)
            {
                if (existentes[0].Value.Modo != modo)
                {
                    _hotkeys[existentes[0].Key] = existentes[0].Value with { Modo = modo };
                    RegistroErrores.Traza("KeyMapping.Registrar", $"MODO: id={existentes[0].Key}, modo={modo}");
                }
                return true;
            }

            // Reemplazo: liberar registros previos del slot antes de registrar el nuevo.
            foreach (var previo in existentes)
            {
                UnregisterHotKey(_hWnd, previo.Key);
                _hotkeys.Remove(previo.Key);
            }

            var id = _nextId++;
            var ok = RegisterHotKey(_hWnd, id, mod, vk);
            if (!ok)
            {
                var error = Marshal.GetLastWin32Error();
                RegistroErrores.Traza("KeyMapping.Registrar", $"FAIL: vk={vk} (0x{vk:X}), mod={mod}, error={error} (0x{error:X}), id={id}");
                if (error == 1409) // HOTKEY_ALREADY_REGISTERED
                {
                    return false;
                }
                return false;
            }

            _hotkeys[id] = new HotkeyEntry(propietariaId, slotIndex, modo, tecla, modifiers);
            RegistroErrores.Traza("KeyMapping.Registrar", $"OK: id={id}, vk={vk} (0x{vk:X}), mod={mod}, propietaria={propietariaId}, slot={slotIndex}, modo={modo}");
            return true;
        }
    }

    /// <summary>Desregistra el hotkey de un slot específico (elimina todos sus registros, sin huérfanos).</summary>
    public void Desregistrar(string propietariaId, int slotIndex)
    {
        lock (_gate)
        {
            var ids = _hotkeys
                .Where(k => k.Value.PropietariaId == propietariaId && k.Value.SlotIndex == slotIndex)
                .Select(k => k.Key)
                .ToList();
            foreach (var id in ids)
            {
                UnregisterHotKey(_hWnd, id);
                _hotkeys.Remove(id);
            }

            _pulsados.Remove((propietariaId, slotIndex));
        }
    }

    /// <summary>Desregistra todos los hotkeys de una propietaria (al cambiar categoría).</summary>
    public void LimpiarPropietaria(string propietariaId)
    {
        lock (_gate)
        {
            var ids = _hotkeys.Where(k => k.Value.PropietariaId == propietariaId).Select(k => k.Key).ToList();
            foreach (var id in ids)
            {
                UnregisterHotKey(_hWnd, id);
                _hotkeys.Remove(id);
            }

            _pulsados.RemoveWhere(p => p.PropietariaId == propietariaId);
        }
    }

    /// <summary>Desregistra todos los hotkeys globales.</summary>
    public void LimpiarTodo()
    {
        lock (_gate)
        {
            foreach (var id in _hotkeys.Keys)
            {
                UnregisterHotKey(_hWnd, id);
            }
            _hotkeys.Clear();
            _pulsados.Clear();
        }
    }

    /// <summary>
    /// Actualiza solo el modo de interacción de un hotkey ya registrado, sin tocar el SO.
    /// </summary>
    /// <param name="propietariaId">Dueña de la paleta.</param>
    /// <param name="slotIndex">Índice del slot.</param>
    /// <param name="modo">Nuevo modo.</param>
    /// <returns>True si el slot tenía registro y se actualizó.</returns>
    public bool ActualizarModo(string propietariaId, int slotIndex, ModoMapeo modo)
    {
        lock (_gate)
        {
            var kvp = _hotkeys.FirstOrDefault(k => k.Value.PropietariaId == propietariaId && k.Value.SlotIndex == slotIndex);
            if (kvp.Key == 0)
            {
                return false;
            }

            if (kvp.Value.Modo != modo)
            {
                _hotkeys[kvp.Key] = kvp.Value with { Modo = modo };
            }
            return true;
        }
    }

    /// <summary>
    /// Verifica si una combinación de tecla+modificadores ya está en uso por el sistema,
    /// otra aplicación u otro slot propio, sin registrarla permanentemente.
    /// Self-aware: la combinación actual del propio slot no cuenta como conflicto.
    /// </summary>
    public bool HayConflicto(string propietariaId, int slotIndex, VirtualKey tecla, VirtualKeyModifiers modifiers)
    {
        if (tecla == VirtualKey.None)
        {
            return false;
        }

        var vk = (uint)tecla;
        var mod = ConvertirModifiers(modifiers);

        lock (_gate)
        {
            if (_disposed)
            {
                return false;
            }

            // Conflicto interno: otro slot nuestro ya usa esa combinación.
            var duplicadoInterno = _hotkeys.Values.Any(e =>
                !(e.PropietariaId == propietariaId && e.SlotIndex == slotIndex)
                && e.Tecla == tecla
                && e.Modifiers == modifiers);
            if (duplicadoInterno)
            {
                return true;
            }

            // Es la combinación actual del propio slot: no es conflicto.
            var propio = _hotkeys.Values.FirstOrDefault(e =>
                e.PropietariaId == propietariaId && e.SlotIndex == slotIndex);
            if (propio.PropietariaId == propietariaId
                && propio.SlotIndex == slotIndex
                && propio.Tecla == tecla
                && propio.Modifiers == modifiers)
            {
                return false;
            }

            const int testId = 0x7FFFFFFF;
            var ok = RegisterHotKey(_hWnd, testId, mod, vk);
            if (!ok)
            {
                var error = Marshal.GetLastWin32Error();
                if (error == 1409) // HOTKEY_ALREADY_REGISTERED
                {
                    return true;
                }
                return true;
            }

            UnregisterHotKey(_hWnd, testId);
            return false;
        }
    }

    /// <summary>
    /// Procesa WM_HOTKEY (KeyDown) desde el hook de ventana.
    /// En modo Momentáneo suprime el auto-repeat: si la tecla sigue físicamente
    /// pulsada, los KeyDown repetidos se ignoran (solo el KeyUp libera).
    /// </summary>
    internal void ProcesarHotkey(int id)
    {
        HotkeyEntry entry;
        lock (_gate)
        {
            if (!_hotkeys.TryGetValue(id, out entry))
            {
                RegistroErrores.Traza("KeyMapping.ProcesarHotkey", $"MISSING: id={id}, registeredCount={_hotkeys.Count}");
                return;
            }

            if (entry.Modo == ModoMapeo.Momentaneo
                && !_pulsados.Add((entry.PropietariaId, entry.SlotIndex)))
            {
                RegistroErrores.Traza("KeyMapping.ProcesarHotkey", $"REPEAT-IGNORED: propietaria={entry.PropietariaId}, slot={entry.SlotIndex}");
                return;
            }
        }

        RegistroErrores.Traza("KeyMapping.ProcesarHotkey", $"RECEIVED: id={id}, propietaria={entry.PropietariaId}, slot={entry.SlotIndex}, modo={entry.Modo}");
        _uiQueue?.TryEnqueue(() =>
        {
            OnHotkeyPressed?.Invoke(entry.PropietariaId, entry.SlotIndex, entry.Modo);
        });
    }

    /// <summary>
    /// Callback del hook de teclado de bajo nivel (WH_KEYBOARD_LL).
    /// Libera el slot Momentáneo al soltar su tecla principal, con o sin
    /// modificadores (el orden al soltar Ctrl/Alt/Shift puede variar, por eso
    /// no se exige su estado en el KeyUp).
    /// </summary>
    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP))
        {
            var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var vk = (VirtualKey)info.vkCode;

            // Buscar slots registrados con esta tecla en modo Momentaneo
            List<(string propietariaId, int slotIndex)> slotsMomentaneo;
            lock (_gate)
            {
                slotsMomentaneo = _hotkeys.Values
                    .Where(e => e.Modo == ModoMapeo.Momentaneo && e.Tecla == vk)
                    .Select(e => (e.PropietariaId, e.SlotIndex))
                    .ToList();

                foreach (var slot in slotsMomentaneo)
                {
                    _pulsados.Remove(slot);
                }
            }

            if (slotsMomentaneo.Count > 0)
            {
                _uiQueue?.TryEnqueue(() =>
                {
                    foreach (var (propietariaId, slotIndex) in slotsMomentaneo)
                    {
                        OnHotkeyReleased?.Invoke(propietariaId, slotIndex);
                    }
                });
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            foreach (var id in _hotkeys.Keys)
            {
                UnregisterHotKey(_hWnd, id);
            }
            _hotkeys.Clear();
            _pulsados.Clear();

            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
            _hookProc = null;
        }
    }

    private static uint ConvertirModifiers(VirtualKeyModifiers modifiers)
    {
        uint mod = 0;
        if (modifiers.HasFlag(VirtualKeyModifiers.Control)) mod |= 0x0002; // MOD_CONTROL
        if (modifiers.HasFlag(VirtualKeyModifiers.Menu)) mod |= 0x0001;    // MOD_ALT
        if (modifiers.HasFlag(VirtualKeyModifiers.Shift)) mod |= 0x0004;   // MOD_SHIFT
        if (modifiers.HasFlag(VirtualKeyModifiers.Windows)) mod |= 0x0008; // MOD_WIN
        return mod;
    }

    private void InstalarHookTeclado()
    {
        _hookProc = HookCallback;
        var hMod = GetModuleHandle(string.Empty);
        _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, hMod, 0);
        Debug.Assert(_hookHandle != IntPtr.Zero, "No se pudo instalar hook de teclado WH_KEYBOARD_LL");
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct HotkeyEntry(
        string PropietariaId,
        int SlotIndex,
        ModoMapeo Modo,
        VirtualKey Tecla,
        VirtualKeyModifiers Modifiers);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}