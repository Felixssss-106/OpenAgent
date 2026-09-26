using System;
using System.Runtime.InteropServices;

namespace OpenAgent.Windows.Native;

/// <summary>
/// Registers a global hotkey for a WinUI window and raises <see cref="Pressed"/>
/// when it fires. WinUI has no hotkey API, so this subclasses the HWND and
/// listens for WM_HOTKEY.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly int _id;
    private readonly NativeMethods.SubclassProc _proc;
    private bool _registered;
    private bool _disposed;

    public event EventHandler? Pressed;

    public HotkeyManager(IntPtr hwnd, int id = 0x4F41)
    {
        _hwnd = hwnd;
        _id = id;
        // The delegate must outlive the call, otherwise the subclass callback
        // is collected and the process dies on the next message.
        _proc = OnSubclassProc;
        NativeMethods.SetWindowSubclass(_hwnd, _proc, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>Registers Alt+Space. Returns false when another app owns it.</summary>
    public bool TryRegister(uint modifiers, uint virtualKey)
    {
        if (_registered)
            return true;

        _registered = NativeMethods.RegisterHotKey(_hwnd, _id, modifiers, virtualKey);
        return _registered;
    }

    private IntPtr OnSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, IntPtr id, IntPtr refData)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == _id)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            return IntPtr.Zero;
        }

        if (msg == NativeMethods.WM_DESTROY)
        {
            UnregisterCore();
        }

        return NativeMethods.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    private void UnregisterCore()
    {
        if (_registered)
        {
            NativeMethods.UnregisterHotKey(_hwnd, _id);
            _registered = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        UnregisterCore();
        NativeMethods.RemoveWindowSubclass(_hwnd, _proc, IntPtr.Zero);
    }
}
