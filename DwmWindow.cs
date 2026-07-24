using System.Runtime.InteropServices;

namespace SteamAccountSwitcher;

internal static class DwmWindow
{
    private const int WmSetRedraw = 0x000B;
    private const uint RdwInvalidate = 0x0001;
    private const uint RdwErase = 0x0004;
    private const uint RdwAllChildren = 0x0080;
    private const uint RdwUpdateNow = 0x0100;
    private const uint RdwFrame = 0x0400;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private const int DwmwcpRound = 2;

    public static void ApplyModernDarkFrame(Form form)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return;
        }

        var enabled = 1;
        _ = DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return;
        }

        var cornerPreference = DwmwcpRound;
        var captionColor = ColorTranslator.ToWin32(Theme.Chrome);
        var borderColor = ColorTranslator.ToWin32(Theme.Line);
        var textColor = ColorTranslator.ToWin32(Theme.TextMain);

        _ = DwmSetWindowAttribute(form.Handle, DwmwaWindowCornerPreference, ref cornerPreference, sizeof(int));
        _ = DwmSetWindowAttribute(form.Handle, DwmwaCaptionColor, ref captionColor, sizeof(int));
        _ = DwmSetWindowAttribute(form.Handle, DwmwaBorderColor, ref borderColor, sizeof(int));
        _ = DwmSetWindowAttribute(form.Handle, DwmwaTextColor, ref textColor, sizeof(int));
    }

    public static void UpdateAtomically(Control control, Action update)
    {
        SuspendRedraw(control);
        control.SuspendLayout();
        try
        {
            update();
        }
        finally
        {
            control.ResumeLayout(performLayout: true);
            ResumeRedraw(control);
        }
    }

    public static void SuspendRedraw(Control control)
    {
        _ = SendMessage(control.Handle, WmSetRedraw, IntPtr.Zero, IntPtr.Zero);
    }

    public static void ResumeRedraw(Control control)
    {
        _ = SendMessage(control.Handle, WmSetRedraw, new IntPtr(1), IntPtr.Zero);
        _ = RedrawWindow(
            control.Handle,
            IntPtr.Zero,
            IntPtr.Zero,
            RdwInvalidate | RdwErase | RdwAllChildren | RdwUpdateNow | RdwFrame);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RedrawWindow(IntPtr hwnd, IntPtr updateRectangle, IntPtr updateRegion, uint flags);
}

internal static class Theme
{
    public static readonly Color Bg = Color.FromArgb(13, 17, 23);
    public static readonly Color Chrome = Color.FromArgb(22, 27, 34);
    public static readonly Color Surface = Color.FromArgb(22, 27, 34);
    public static readonly Color SurfaceAlt = Color.FromArgb(30, 36, 45);
    public static readonly Color Line = Color.FromArgb(48, 54, 61);
    public static readonly Color Selected = Color.FromArgb(32, 60, 96);
    public static readonly Color TextMain = Color.FromArgb(230, 237, 243);
    public static readonly Color TextMuted = Color.FromArgb(139, 148, 158);
    public static readonly Color Accent = Color.FromArgb(47, 129, 247);
    public static readonly Color AccentHover = Color.FromArgb(64, 143, 255);
}
