using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace BebeRadio.Services;

/// <summary>
/// Hook de mensajes de ventana para recibir WM_HOTKEY (y otros) en WinUI 3.
/// Usa <c>SetWindowSubclass</c> para inyectar un <c>WndProc</c> en la ventana principal.
/// </summary>
public sealed class WindowMessageHook : IDisposable
{
    private const uint WM_HOTKEY = 0x0312;
    private readonly IntPtr _hWnd;
    private readonly SubclassProc _subclassProc;
    private readonly IntPtr _subclassProcPtr;
    private bool _disposed;

    /// <summary>
    /// Se invoca cuando llega un WM_HOTKEY. <paramref name="int"/> = id del hotkey.
    /// </summary>
    public event Action<int>? HotkeyReceived;

    /// <summary>Crea el hook para una <see cref="Window"/>.</summary>
    public WindowMessageHook(Window window)
    {
        _hWnd = WindowNative.GetWindowHandle(window);
        _subclassProc = SubclassWndProc;
        _subclassProcPtr = Marshal.GetFunctionPointerForDelegate(_subclassProc);

        var ok = SetWindowSubclass(_hWnd, _subclassProcPtr, 0, IntPtr.Zero);
        if (!ok)
        {
            throw new InvalidOperationException("No se pudo instalar el hook de ventana (SetWindowSubclass).");
        }
    }

    private IntPtr SubclassWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            HotkeyReceived?.Invoke(id);
        }

        return DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        RemoveWindowSubclass(_hWnd, _subclassProcPtr, 0);
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr SubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, IntPtr pfnSubclass, uint uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, IntPtr pfnSubclass, uint uIdSubclass);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}