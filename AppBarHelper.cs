using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SidebarDock;

/// <summary>
/// Registra una finestra WPF come AppBar di Windows (la stessa API che usa la taskbar).
/// Effetto: lo spazio occupato dalla sidebar viene "riservato" e le altre finestre
/// massimizzate non ci finiscono sotto, esattamente come la dock di Ubuntu.
/// </summary>
public enum AppBarEdge
{
    Left = 0,
    Top = 1,
    Right = 2,
    Bottom = 3
}

public static class AppBarHelper
{
    private const int ABM_NEW = 0x00000000;
    private const int ABM_REMOVE = 0x00000001;
    private const int ABM_QUERYPOS = 0x00000002;
    private const int ABM_SETPOS = 0x00000003;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uCallbackMessage;
        public int uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    [DllImport("shell32.dll")]
    private static extern uint SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    /// <summary>
    /// Registra la finestra come AppBar sul bordo indicato con la larghezza/altezza desiderata
    /// (in unità WPF/DIP a 96 DPI). Va chiamato dopo che la finestra ha già l'HWND
    /// (evento SourceInitialized o dopo Show()).
    /// </summary>
    /// <remarks>
    /// SHAppBarMessage lavora sempre in pixel fisici dello schermo, mentre le proprietà
    /// Window.Left/Top/Width/Height di WPF sono in DIP (indipendenti dal DPI). Con scaling
    /// diverso da 100% (es. 125%) mischiare le due unità senza conversione lascia un
    /// disallineamento tra la barra e il bordo reale dello schermo: qui si converte sempre
    /// da/verso pixel fisici usando il DPI effettivo della finestra.
    /// </remarks>
    public static void RegisterAppBar(Window window, AppBarEdge edge, double thickness)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            throw new InvalidOperationException("La finestra deve avere un handle valido prima di registrarsi come AppBar.");

        var dpi = GetDpiForWindow(hwnd);
        var scale = (dpi == 0 ? 96.0 : dpi) / 96.0;

        var abd = new APPBARDATA
        {
            cbSize = Marshal.SizeOf(typeof(APPBARDATA)),
            hWnd = hwnd
        };

        // 1. Registra la finestra come AppBar
        SHAppBarMessage(ABM_NEW, ref abd);

        // 2. Calcola la posizione richiesta in base al bordo scelto, tutto in pixel fisici
        var screenWidth = GetSystemMetrics(SM_CXSCREEN);
        var screenHeight = GetSystemMetrics(SM_CYSCREEN);
        var thicknessPx = (int)Math.Round(thickness * scale);

        abd.uEdge = (int)edge;
        abd.rc = edge switch
        {
            AppBarEdge.Left => new RECT { Left = 0, Top = 0, Right = thicknessPx, Bottom = screenHeight },
            AppBarEdge.Right => new RECT { Left = screenWidth - thicknessPx, Top = 0, Right = screenWidth, Bottom = screenHeight },
            AppBarEdge.Top => new RECT { Left = 0, Top = 0, Right = screenWidth, Bottom = thicknessPx },
            AppBarEdge.Bottom => new RECT { Left = 0, Top = screenHeight - thicknessPx, Right = screenWidth, Bottom = screenHeight },
            _ => throw new ArgumentOutOfRangeException(nameof(edge))
        };

        // 3. Chiede a Windows conferma/aggiustamento della posizione, poi la applica
        SHAppBarMessage(ABM_QUERYPOS, ref abd);
        SHAppBarMessage(ABM_SETPOS, ref abd);

        // 4. Riconverte le coordinate finali (pixel fisici) in DIP per WPF
        window.Left = abd.rc.Left / scale;
        window.Top = abd.rc.Top / scale;
        window.Width = (abd.rc.Right - abd.rc.Left) / scale;
        window.Height = (abd.rc.Bottom - abd.rc.Top) / scale;
    }

    /// <summary>Deregistra l'AppBar — va chiamata alla chiusura, altrimenti Windows lascia lo spazio riservato.</summary>
    public static void UnregisterAppBar(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var abd = new APPBARDATA
        {
            cbSize = Marshal.SizeOf(typeof(APPBARDATA)),
            hWnd = hwnd
        };
        SHAppBarMessage(ABM_REMOVE, ref abd);
    }
}
