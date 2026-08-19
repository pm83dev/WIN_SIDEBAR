using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SidebarDock;

/// <summary>Estrae l'icona di sistema associata a un file o una cartella (la stessa che mostra Esplora file).</summary>
public static class IconExtractor
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    /// Restituisce l'icona associata al percorso (eseguibile o cartella), oppure null se il
    /// percorso non esiste o non è risolvibile (es. un argomento shell tipo "search-ms:").
    /// </summary>
    public static ImageSource? GetIcon(string path)
    {
        try
        {
            var exists = System.IO.File.Exists(path) || System.IO.Directory.Exists(path);
            var shinfo = new SHFILEINFO();
            var flags = SHGFI_ICON | SHGFI_LARGEICON;
            if (!exists) flags |= SHGFI_USEFILEATTRIBUTES; // per estensioni note anche se il file non esiste ancora

            var result = SHGetFileInfo(path, FILE_ATTRIBUTE_NORMAL, ref shinfo, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
            if (result == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero)
                return null;

            try
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(shinfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                DestroyIcon(shinfo.hIcon);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Carica un'immagine (png/ico/jpg/...) da un percorso indicato dall'utente per usarla come
    /// icona, oppure null se il file non esiste o non è leggibile come immagine.
    /// </summary>
    public static ImageSource? LoadCustomImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            return null;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad; // libera subito il file, niente lock
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
