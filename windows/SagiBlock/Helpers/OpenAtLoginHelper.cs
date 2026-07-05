using Microsoft.Win32;
using Windows.ApplicationModel;

namespace SagiBlock.Helpers;

public static class OpenAtLoginHelper
{
    private const string StartupTaskId = "SagiBlockStartupTask";
    private const string RunRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "SagiBlock";

    public static async Task<bool> IsEnabledAsync()
    {
        if (PackageHelper.IsPackaged())
        {
            try
            {
                var startupTask = await StartupTask.GetAsync(StartupTaskId);
                return startupTask.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
            }
            catch (Exception ex)
            {
                StartupLog.Write(ex, "Failed to read StartupTask state, falling back to registry");
            }
        }

        return IsEnabledInRegistry();
    }

    public static async Task<bool> SetEnabledAsync(bool enable)
    {
        if (PackageHelper.IsPackaged())
        {
            try
            {
                var startupTask = await StartupTask.GetAsync(StartupTaskId);
                if (enable)
                {
                    var result = await startupTask.RequestEnableAsync();
                    return result is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
                }

                startupTask.Disable();
                return false;
            }
            catch (Exception ex)
            {
                StartupLog.Write(ex, "StartupTask toggle failed, falling back to registry");
            }
        }

        if (enable)
            return EnableInRegistry();

        DisableInRegistry();
        return false;
    }

    public static async Task<bool> ToggleAsync()
    {
        var enabled = await IsEnabledAsync();
        return await SetEnabledAsync(!enabled);
    }

    private static bool IsEnabledInRegistry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
        return key?.GetValue(RunValueName) != null;
    }

    private static bool EnableInRegistry()
    {
        var exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(exePath))
            return false;

        using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
        key?.SetValue(RunValueName, $"\"{exePath}\"");
        StartupLog.Write($"Added to startup (registry) -> {exePath}");
        return true;
    }

    private static void DisableInRegistry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
        key?.DeleteValue(RunValueName, false);
        StartupLog.Write("Removed from startup (registry)");
    }
}
