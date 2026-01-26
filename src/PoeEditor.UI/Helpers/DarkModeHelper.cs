using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PoeEditor.UI.Helpers;

/// <summary>
/// Helper class to enable dark mode title bar on Windows 10/11.
/// </summary>
public static class DarkModeHelper
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    /// <summary>
    /// Enables dark mode for the window's title bar.
    /// Call this after the window is loaded (e.g., in Loaded event or after Show()).
    /// </summary>
    public static void EnableDarkMode(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            if (hwnd == IntPtr.Zero) return;

            int value = 1; // 1 = enable dark mode

            // Try the newer attribute first (Windows 10 20H1+)
            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int)) != 0)
            {
                // Fall back to the older attribute for older Windows 10 versions
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref value, sizeof(int));
            }
        }
        catch
        {
            // Silently ignore - dark mode title bar is cosmetic only
        }
    }

    /// <summary>
    /// Applies dark mode when window is loaded.
    /// Use this in constructor: DarkModeHelper.ApplyDarkMode(this);
    /// </summary>
    public static void ApplyDarkMode(Window window)
    {
        window.SourceInitialized += (s, e) => EnableDarkMode(window);
    }
}
