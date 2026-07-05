using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace SagiBlock.Helpers;

/// <summary>
/// Win32 native popup menu.
/// Uses CreatePopupMenu / TrackPopupMenuEx for OS-native styling.
/// On Windows 11 this gives the modern rounded-corner menu.
/// On Windows 10 this gives the standard flat menu.
/// Both follow the current system theme automatically.
/// </summary>
public sealed class NativeMenu : IDisposable
{
    // ── Menu P/Invoke ──

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, nuint uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SetMenuItemInfoW(IntPtr hMenu, uint item, bool fByPosition, ref MENUITEMINFO lpmii);

    [DllImport("user32.dll")]
    private static extern int GetMenuItemCount(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(
        IntPtr hMenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    // ── Shell / GDI P/Invoke ──

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi,
        uint cbSizeFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    // ── Structs ──

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MENUITEMINFO
    {
        public uint cbSize;
        public uint fMask;
        public uint fType;
        public uint fState;
        public uint wID;
        public IntPtr hSubMenu;
        public IntPtr hbmpChecked;
        public IntPtr hbmpUnchecked;
        public UIntPtr dwItemData;
        public IntPtr dwTypeData;
        public uint cch;
        public IntPtr hbmpItem;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
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

    // ── Constants ──

    private const uint MF_STRING    = 0x0000;
    private const uint MF_SEPARATOR = 0x0800;
    private const uint MF_GRAYED    = 0x0001;
    private const uint MF_CHECKED   = 0x0008;
    private const uint MF_POPUP     = 0x0010;

    private const uint TPM_RETURNCMD   = 0x0100;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_LEFTALIGN   = 0x0000;
    private const uint TPM_BOTTOMALIGN = 0x0020;

    private const uint MIIM_BITMAP = 0x0080;

    private const uint SHGFI_ICON        = 0x000000100;
    private const uint SHGFI_SMALLICON   = 0x000000001;
    private const uint SHGFI_DISPLAYNAME = 0x000000200;

    // ── Fields ──

    private readonly IntPtr _hMenu;
    private int _nextId;
    private readonly Dictionary<int, Action> _actions = new();
    private readonly List<IntPtr> _ownedBitmaps = new();

    public NativeMenu(int startId = 1)
    {
        _hMenu = CreatePopupMenu();
        _nextId = startId;
    }

    /// <summary>Add a clickable menu item.</summary>
    public NativeMenu AddItem(string text, Action onClick)
    {
        var id = _nextId++;
        AppendMenu(_hMenu, MF_STRING, (nuint)id, text);
        _actions[id] = onClick;
        return this;
    }

    /// <summary>Add a clickable menu item with a check mark.</summary>
    public NativeMenu AddCheckedItem(string text, bool isChecked, Action onClick)
    {
        var id = _nextId++;
        var flags = MF_STRING | (isChecked ? MF_CHECKED : 0);
        AppendMenu(_hMenu, flags, (nuint)id, text);
        _actions[id] = onClick;
        return this;
    }

    /// <summary>Add a disabled (grayed out) label.</summary>
    public NativeMenu AddLabel(string text)
    {
        var id = _nextId++;
        AppendMenu(_hMenu, MF_STRING | MF_GRAYED, (nuint)id, text);
        return this;
    }

    /// <summary>Add a separator line.</summary>
    public NativeMenu AddSeparator()
    {
        AppendMenu(_hMenu, MF_SEPARATOR, 0, null);
        return this;
    }

    /// <summary>Add a submenu, optionally with a bitmap icon.</summary>
    public NativeMenu AddSubmenu(string text, Action<NativeMenu> buildSubmenu, IntPtr hBitmap = default)
    {
        var sub = new NativeMenu(_nextId);
        buildSubmenu(sub);
        _nextId = sub._nextId;
        foreach (var kvp in sub._actions)
            _actions[kvp.Key] = kvp.Value;
        AppendMenu(_hMenu, MF_STRING | MF_POPUP, (nuint)sub._hMenu, text);

        if (hBitmap != IntPtr.Zero)
        {
            _ownedBitmaps.Add(hBitmap);
            var pos = (uint)(GetMenuItemCount(_hMenu) - 1);
            var mii = new MENUITEMINFO
            {
                cbSize = (uint)Marshal.SizeOf<MENUITEMINFO>(),
                fMask = MIIM_BITMAP,
                hbmpItem = hBitmap
            };
            SetMenuItemInfoW(_hMenu, pos, true, ref mii);
        }

        return this;
    }

    // ──────────────────────────────────────────────
    // Shell helpers (display name + icon bitmap)
    // ──────────────────────────────────────────────

    /// <summary>Get the shell display name for a path (e.g. localized "Desktop").</summary>
    public static string GetDisplayName(string path)
    {
        var shfi = new SHFILEINFO();
        SHGetFileInfo(path, 0, ref shfi,
            (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_DISPLAYNAME);
        return string.IsNullOrEmpty(shfi.szDisplayName)
            ? Path.GetFileName(path)
            : shfi.szDisplayName;
    }

    /// <summary>
    /// Get the shell icon for a folder as a 32bpp premultiplied-alpha HBITMAP
    /// suitable for menu item bitmaps. Caller should pass the returned handle
    /// to <see cref="AddSubmenu"/> which takes ownership for cleanup.
    /// </summary>
    public static IntPtr GetFolderBitmap(string folderPath)
    {
        var shfi = new SHFILEINFO();
        SHGetFileInfo(folderPath, 0, ref shfi,
            (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_SMALLICON);

        if (shfi.hIcon == IntPtr.Zero)
            return IntPtr.Zero;

        try
        {
            using var icon = Icon.FromHandle(shfi.hIcon);
            using var src = icon.ToBitmap();

            // Premultiplied alpha bitmap  Erequired for proper menu rendering
            var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(dst))
                g.DrawImage(src, 0, 0, src.Width, src.Height);

            var hBitmap = dst.GetHbitmap(Color.FromArgb(0));
            dst.Dispose();
            return hBitmap;
        }
        finally
        {
            DestroyIcon(shfi.hIcon);
        }
    }

    // ──────────────────────────────────────────────
    // Show + Dispose
    // ──────────────────────────────────────────────

    /// <summary>
    /// Show the menu at the current cursor position and execute the selected action.
    /// </summary>
    /// <param name="ownerHwnd">Window handle for the menu (required by TrackPopupMenuEx)</param>
    public void Show(IntPtr ownerHwnd)
    {
        GetCursorPos(out var pt);

        // Required: set foreground window so the menu closes when clicking elsewhere
        SetForegroundWindow(ownerHwnd);

        var cmd = TrackPopupMenuEx(
            _hMenu,
            TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_LEFTALIGN | TPM_BOTTOMALIGN,
            pt.X, pt.Y,
            ownerHwnd,
            IntPtr.Zero);

        if (cmd > 0 && _actions.TryGetValue(cmd, out var action))
            action();
    }

    public void Dispose()
    {
        DestroyMenu(_hMenu);
        foreach (var hBmp in _ownedBitmaps)
            DeleteObject(hBmp);
    }
}
