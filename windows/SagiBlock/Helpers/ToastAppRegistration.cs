using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SagiBlock.Helpers;

/// <summary>
/// Win32 非 MSIX アプリのトースト表示名は AUMID 登録と Start Menu ショートカットの両方で決まる。
/// </summary>
public static class ToastAppRegistration
{
    public const string AppUserModelId = "jp.tomippe.sagiBlock";
    private const string ShortcutFileName = "SagiBlock.lnk";

    private static readonly string[] LegacyShortcutNames =
    [
        "SAGI BLOCK.lnk",
        "詐欺ブロック.lnk",
        "诈骗拦截.lnk"
    ];

    private static string ProgramsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");

    private static string ShortcutPath => Path.Combine(ProgramsDirectory, ShortcutFileName);

    public static void EnsureRegistered()
    {
        if (PackageHelper.IsPackaged())
            return;

        try
        {
            CultureHelper.ApplyUserInterfaceCulture();
            var displayName = L.Get("AppName");
            var iconPath = TrayIconHelper.EnsureLogoFilePath();

            SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
            RegisterAppIdentity(displayName, iconPath);
            EnsureStartMenuShortcut(displayName, iconPath);
        }
        catch (Exception ex)
        {
            StartupLog.Write(ex, "ToastAppRegistration failed");
        }
    }

    /// <summary>
    /// トースト上部のアプリ名を現在の UI 言語に合わせて更新する。
    /// </summary>
    public static void RefreshDisplayName()
    {
        if (PackageHelper.IsPackaged())
            return;

        try
        {
            CultureHelper.ApplyUserInterfaceCulture();
            var displayName = L.Get("AppName");
            var iconPath = TrayIconHelper.EnsureLogoFilePath();

            RegisterAppIdentity(displayName, iconPath);

            if (File.Exists(ShortcutPath))
                TrySetShortcutProperties(ShortcutPath, displayName);
            else
                EnsureStartMenuShortcut(displayName, iconPath);

            NotifyShellAssociationChanged();
            StartupLog.Write(
                $"Toast display name refreshed: {displayName} ({CultureInfo.CurrentUICulture.Name})");
        }
        catch (Exception ex)
        {
            StartupLog.Write(ex, "RefreshDisplayName failed");
        }
    }

    private static void RegisterAppIdentity(string displayName, string iconPath)
    {
        try
        {
            var keyPath = $@"Software\Classes\AppUserModelId\{AppUserModelId}";
            using var key = Registry.CurrentUser.CreateSubKey(keyPath);
            if (key is null)
                return;

            key.SetValue("DisplayName", displayName, RegistryValueKind.String);
            key.SetValue("IconUri", iconPath, RegistryValueKind.String);
            key.SetValue("IconBackgroundColor", "FFDDDDDD", RegistryValueKind.String);
        }
        catch (Exception ex)
        {
            StartupLog.Write(ex, "RegisterAppIdentity failed");
        }
    }

    private static void EnsureStartMenuShortcut(string displayName, string iconPath)
    {
        Directory.CreateDirectory(ProgramsDirectory);

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            StartupLog.Write("Toast shortcut skipped: ProcessPath missing");
            return;
        }

        foreach (var legacyName in LegacyShortcutNames)
        {
            var legacyPath = Path.Combine(ProgramsDirectory, legacyName);
            if (File.Exists(legacyPath))
                File.Delete(legacyPath);
        }

        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
        {
            StartupLog.Write("Toast shortcut skipped: WScript.Shell unavailable");
            return;
        }

        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(ShortcutPath);
        shortcut.TargetPath = exePath;
        shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
        shortcut.Description = displayName;
        shortcut.Arguments = "";
        shortcut.SetIconLocation(string.IsNullOrWhiteSpace(iconPath) ? exePath : iconPath, 0);
        shortcut.Save();

        if (TrySetShortcutProperties(ShortcutPath, displayName))
            StartupLog.Write($"Toast shortcut ready: {AppUserModelId} -> {ShortcutPath}");
        else
            StartupLog.Write($"Toast shortcut saved but AppUserModelID was not applied: {ShortcutPath}");
    }

    private static bool TrySetShortcutProperties(string shortcutPath, string displayName)
    {
        try
        {
            var shellLinkType = Type.GetTypeFromProgID("ShellLink");
            if (shellLinkType is null)
                return false;

            dynamic link = Activator.CreateInstance(shellLinkType)!;
            var persistFile = (IPersistFile)link;
            persistFile.Load(shortcutPath, 0);
            var propertyStore = (IPropertyStore)link;

            var appId = PropVariant.FromString(AppUserModelId);
            var appIdKey = PropertyKeys.AppUserModelId;
            propertyStore.SetValue(ref appIdKey, appId);

            var empty = PropVariant.FromString("");
            var resourceKey = PropertyKeys.RelaunchDisplayNameResource;
            propertyStore.SetValue(ref resourceKey, empty);

            var name = PropVariant.FromString(displayName);
            var nameKey = PropertyKeys.RelaunchDisplayName;
            propertyStore.SetValue(ref nameKey, name);

            propertyStore.Commit();
            persistFile.Save(shortcutPath, true);
            return true;
        }
        catch (Exception ex)
        {
            StartupLog.Write(ex, "TrySetShortcutProperties failed");
            return false;
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SetCurrentProcessExplicitAppUserModelID(string appId);

    private static void NotifyShellAssociationChanged()
    {
        const int shcneAssocChanged = 0x08000000;
        SHChangeNotify(shcneAssocChanged, 0, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    [ComImport]
    [Guid("00000109-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    [ComImport]
    [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        uint GetCount([Out] out uint cProps);
        void GetAt([In] uint iProp, out PropertyKey pkey);
        void GetValue([In] ref PropertyKey key, out PropVariant pv);
        void SetValue([In] ref PropertyKey key, [In] PropVariant pv);
        void Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
    {
        public Guid FmtId;
        public uint Pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        public ushort Vt;
        public ushort Reserved1;
        public ushort Reserved2;
        public ushort Reserved3;
        public IntPtr Pointer;

        public static PropVariant FromString(string value) =>
            new() { Vt = 31, Pointer = Marshal.StringToCoTaskMemUni(value) };
    }

    private static class PropertyKeys
    {
        public static readonly PropertyKey AppUserModelId = new()
        {
            FmtId = new Guid("9F4C2855-9F59-4B38-8AE8-CFAB9F2B5C6C"),
            Pid = 5
        };

        public static readonly PropertyKey RelaunchDisplayNameResource = new()
        {
            FmtId = new Guid("9F4C2855-9F59-4B38-8AE8-CFAB9F2B5C6C"),
            Pid = 4
        };

        public static readonly PropertyKey RelaunchDisplayName = new()
        {
            FmtId = new Guid("F4F239C6-1976-4265-90E0-9C4EB1F6C0BF"),
            Pid = 5
        };
    }
}
