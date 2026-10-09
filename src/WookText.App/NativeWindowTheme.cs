using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WookText.App;

public static class NativeWindowTheme
{
    public static void Attach(Window window) => window.SourceInitialized += (_, _) =>
    {
        nint handle = new WindowInteropHelper(window).Handle;
        int enabled = 1, background = 0x0F0F10, text = 0xC3CDCE;
        // Unsupported attributes are ignored on older Windows builds.
        _ = DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 35, ref background, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 36, ref text, sizeof(int));
    };

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
