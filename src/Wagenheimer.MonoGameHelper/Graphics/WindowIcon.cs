using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Wagenheimer.MonoGameHelper.Graphics;

/// <summary>
/// Applies a Windows <c>.ico</c> to the **game window** (title bar + taskbar).
///
/// The executable's own icon comes from the project's <c>&lt;ApplicationIcon&gt;</c>. This helper
/// covers the cases that property cannot: running the game through another host process (e.g.
/// <c>dotnet App.dll</c> under a debugger) or a backend that never sets the window icon.
///
/// On Windows it is a <c>WM_SETICON</c>; elsewhere it is a no-op. The icon is cosmetic, so every
/// failure path returns <c>false</c> instead of throwing.
/// </summary>
public static class WindowIcon
{
    private const int WmSetIcon = 0x0080;
    private const int IconSmall = 0;
    private const int IconBig = 1;
    private const uint ImageIcon = 1;
    private const uint LrLoadFromFile = 0x00000010;

    private const int SmCxIcon = 11;
    private const int SmCyIcon = 12;
    private const int SmCxSmallIcon = 49;
    private const int SmCySmallIcon = 50;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint load);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    // Kepta viva de propósito: o Windows não guarda uma referência própria ao HICON que passamos.
    private static IntPtr _bigIcon;
    private static IntPtr _smallIcon;

    /// <summary>
    /// Loads <paramref name="icoPath"/> and applies it as the window's small and big icon.
    /// </summary>
    /// <param name="windowHandle">Native window handle (MonoGame <c>GameWindow.Handle</c>).</param>
    /// <returns><c>true</c> when an icon was applied.</returns>
    public static bool Apply(IntPtr windowHandle, string icoPath)
    {
        if (!OperatingSystem.IsWindows() || windowHandle == IntPtr.Zero)
            return false;

        if (string.IsNullOrWhiteSpace(icoPath) || !File.Exists(icoPath))
            return false;

        try
        {
            return ApplyWindows(windowHandle, icoPath);
        }
        catch
        {
            return false; // ícone é cosmético — nunca derruba o jogo
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool ApplyWindows(IntPtr hWnd, string icoPath)
    {
        _bigIcon = LoadImage(IntPtr.Zero, icoPath, ImageIcon,
            GetSystemMetrics(SmCxIcon), GetSystemMetrics(SmCyIcon), LrLoadFromFile);

        _smallIcon = LoadImage(IntPtr.Zero, icoPath, ImageIcon,
            GetSystemMetrics(SmCxSmallIcon), GetSystemMetrics(SmCySmallIcon), LrLoadFromFile);

        if (_bigIcon != IntPtr.Zero)
            SendMessage(hWnd, WmSetIcon, (IntPtr)IconBig, _bigIcon);

        if (_smallIcon != IntPtr.Zero)
            SendMessage(hWnd, WmSetIcon, (IntPtr)IconSmall, _smallIcon);

        return _bigIcon != IntPtr.Zero || _smallIcon != IntPtr.Zero;
    }
}
