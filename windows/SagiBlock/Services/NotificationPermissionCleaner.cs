using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using SagiBlock.Helpers;
using SagiBlock.Models;

namespace SagiBlock.Services;

public sealed class NotificationPermissionCleaner
{
    private const int Allow = 1;
    private const int Block = 2;
    private const int RestoreOnStartupNewTab = 1;

    private readonly ThreatFeedService _threatFeeds = new();

    public async Task<IReadOnlyList<GuardEvent>> BlockScamNotificationPermissionsAsync(
        CancellationToken cancellationToken = default)
    {
        await _threatFeeds.EnsureFeedsReadyAsync(cancellationToken).ConfigureAwait(false);

        var events = new List<GuardEvent>();
        var plans = new Dictionary<string, List<ProfilePlan>>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in ChromiumProfiles())
        {
            try
            {
                var targets = await CollectTargetsAsync(profile, cancellationToken).ConfigureAwait(false);
                if (targets.Count == 0)
                    continue;

                if (!plans.TryGetValue(profile.BrowserName, out var list))
                {
                    list = [];
                    plans[profile.BrowserName] = list;
                }

                list.Add(new ProfilePlan(profile, targets));
            }
            catch (Exception ex)
            {
                events.Add(new GuardEvent(
                    GuardEventKind.CheckFailed,
                    L.Get("NotifyCheckFailedTitle"),
                    L.Format("NotifyCheckFailedDetail", profile.BrowserName, ex.Message),
                    $"profile-error:{profile.BrowserName}:{profile.PreferencesPath}:{ex.Message}"));
            }
        }

        foreach (var (browserName, profilePlans) in plans)
        {
            var closedBrowser = false;
            string? executablePath = null;
            if (IsBrowserRunning(browserName))
            {
                executablePath = TryGetBrowserExecutable(browserName);
                if (!TryStopBrowser(browserName))
                {
                    foreach (var plan in profilePlans)
                    {
                        foreach (var target in plan.Targets)
                        {
                            events.Add(new GuardEvent(
                                GuardEventKind.CheckFailed,
                                L.Get("NotifyBlockFailedTitle"),
                                L.Format("NotifyCloseFailedForBlockDetail", browserName, target.Origin),
                                $"notification-failed:{browserName}:{target.Origin}"));
                        }
                    }

                    continue;
                }

                closedBrowser = true;
                Thread.Sleep(800);
            }

            var anyBlocked = false;
            foreach (var plan in profilePlans)
            {
                try
                {
                    var blockEvents = ApplyBlocks(plan.Profile, plan.Targets, closedBrowser);
                    events.AddRange(blockEvents);
                    if (blockEvents.Any(e => e.Kind == GuardEventKind.NotificationPermissionBlocked))
                        anyBlocked = true;
                }
                catch (Exception ex)
                {
                    events.Add(new GuardEvent(
                        GuardEventKind.CheckFailed,
                        L.Get("NotifyCheckFailedTitle"),
                        L.Format("NotifyCheckFailedDetail", plan.Profile.BrowserName, ex.Message),
                        $"profile-error:{plan.Profile.BrowserName}:{plan.Profile.PreferencesPath}:{ex.Message}"));
                }
            }

            if (closedBrowser && anyBlocked)
            {
                foreach (var plan in profilePlans)
                    PrepareSafeRestart(plan.Profile);

                TryStartBrowserSafely(browserName, executablePath);
            }
        }

        return events;
    }

    private static IReadOnlyList<GuardEvent> ApplyBlocks(
        ChromiumProfile profile,
        IReadOnlyList<NotificationTarget> targets,
        bool closedBrowser)
    {
        if (!File.Exists(profile.PreferencesPath))
            return [];

        var json = File.ReadAllText(profile.PreferencesPath);
        var root = JsonNode.Parse(json)?.AsObject();
        var notifications = root?["profile"]?["content_settings"]?["exceptions"]?["notifications"]?.AsObject();
        if (root is null || notifications is null)
            return [];

        var changed = false;
        foreach (var target in targets)
        {
            if (notifications[target.StorageKey] is not JsonObject permission)
                continue;

            if (TryGetInt(permission["setting"]) != Allow)
                continue;

            permission["setting"] = Block;
            permission["last_modified"] = ChromeTimestamp();
            changed = true;
        }

        if (!changed)
            return [];

        if (closedBrowser)
            ApplySafeStartupPrefs(root);

        var backup = profile.PreferencesPath + ".sagi-block.bak";
        if (!File.Exists(backup))
            File.Copy(profile.PreferencesPath, backup);

        var options = new JsonSerializerOptions { WriteIndented = false };
        File.WriteAllText(profile.PreferencesPath, root.ToJsonString(options));

        var events = new List<GuardEvent>();
        foreach (var target in targets)
        {
            if (!VerifyBlocked(profile.PreferencesPath, target.StorageKey))
            {
                events.Add(new GuardEvent(
                    GuardEventKind.CheckFailed,
                    L.Get("NotifyBlockFailedTitle"),
                    L.Format("NotifyBlockFailedDetail", profile.BrowserName, target.Origin),
                    $"notification-failed:{profile.BrowserName}:{target.Origin}"));
                continue;
            }

            var detail = BuildBlockedDetail(profile.BrowserName, target, closedBrowser);
            events.Add(new GuardEvent(
                GuardEventKind.NotificationPermissionBlocked,
                L.Get("NotifyBlockedTitle"),
                detail,
                $"notification:{profile.BrowserName}:{target.Origin}"));
        }

        return events;
    }

    private static string BuildBlockedDetail(string browserName, NotificationTarget target, bool closedBrowser)
    {
        if (!string.IsNullOrWhiteSpace(target.DetectionSource))
        {
            return closedBrowser
                ? L.Format("NotifyBlockedAfterCloseFeedDetail", browserName, target.Origin, target.DetectionSource)
                : L.Format("NotifyBlockedFeedDetail", browserName, target.Origin, target.DetectionSource);
        }

        return closedBrowser
            ? L.Format("NotifyBlockedAfterCloseDetail", browserName, target.Origin)
            : L.Format("NotifyBlockedDetail", browserName, target.Origin);
    }

    private async Task<List<NotificationTarget>> CollectTargetsAsync(
        ChromiumProfile profile,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(profile.PreferencesPath))
            return [];

        var json = File.ReadAllText(profile.PreferencesPath);
        var root = JsonNode.Parse(json)?.AsObject();
        var notifications = root?["profile"]?["content_settings"]?["exceptions"]?["notifications"]?.AsObject();
        if (notifications is null)
            return [];

        var targets = new List<NotificationTarget>();
        foreach (var item in notifications)
        {
            if (item.Value is not JsonObject permission)
                continue;

            if (TryGetInt(permission["setting"]) != Allow)
                continue;

            var origin = ExtractOrigin(item.Key);
            var suspicious = await IsSuspiciousOriginAsync(origin, cancellationToken).ConfigureAwait(false);
            if (!suspicious.IsThreat)
                continue;

            targets.Add(new NotificationTarget(item.Key, origin, suspicious.Source));
        }

        return targets;
    }

    private async Task<(bool IsThreat, string Source)> IsSuspiciousOriginAsync(
        string origin,
        CancellationToken cancellationToken)
    {
        if (!NotificationOriginScorer.TryGetHost(origin, out var host))
            return (false, "");

        if (NotificationOriginScorer.IsTrustedHost(host))
            return (false, "");

        var score = NotificationOriginScorer.GetScore(host);
        if (score >= NotificationOriginScorer.SuspiciousThreshold)
            return (true, "local");

        return await _threatFeeds.CheckHostAsync(host, cancellationToken).ConfigureAwait(false);
    }

    private static bool VerifyBlocked(string preferencesPath, string storageKey)
    {
        try
        {
            var json = File.ReadAllText(preferencesPath);
            var root = JsonNode.Parse(json)?.AsObject();
            var permission = root?["profile"]?["content_settings"]?["exceptions"]?["notifications"]?[storageKey]?.AsObject();
            return TryGetInt(permission?["setting"]) == Block;
        }
        catch
        {
            return false;
        }
    }

    private static string ChromeTimestamp() =>
        (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000).ToString();

    private static void ApplySafeStartupPrefs(JsonObject root)
    {
        root["profile"] ??= new JsonObject();
        root["profile"]!["exited_cleanly"] = true;
        root["profile"]!["exit_type"] = "Normal";

        root["session"] ??= new JsonObject();
        root["session"]!["restore_on_startup"] = RestoreOnStartupNewTab;
    }

    private static void PrepareSafeRestart(ChromiumProfile profile)
    {
        var profileDir = Path.GetDirectoryName(profile.PreferencesPath);
        if (!string.IsNullOrEmpty(profileDir))
            DeleteSessionSnapshots(profileDir);

        if (!File.Exists(profile.PreferencesPath))
            return;

        try
        {
            var json = File.ReadAllText(profile.PreferencesPath);
            var root = JsonNode.Parse(json)?.AsObject();
            if (root is null)
                return;

            ApplySafeStartupPrefs(root);

            var options = new JsonSerializerOptions { WriteIndented = false };
            File.WriteAllText(profile.PreferencesPath, root.ToJsonString(options));
        }
        catch (Exception ex)
        {
            StartupLog.Write(ex, $"PrepareSafeRestart failed for {profile.BrowserName}");
        }
    }

    private static void DeleteSessionSnapshots(string profileDir)
    {
        foreach (var name in new[] { "Current Session", "Current Tabs", "Last Session", "Last Tabs" })
            TryDeleteFile(Path.Combine(profileDir, name));

        var sessionsDir = Path.Combine(profileDir, "Sessions");
        if (!Directory.Exists(sessionsDir))
            return;

        foreach (var file in Directory.EnumerateFiles(sessionsDir))
            TryDeleteFile(file);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best effort only.
        }
    }

    private static void TryStartBrowserSafely(string browserName, string? executablePath)
    {
        executablePath ??= GetDefaultBrowserExecutable(browserName);
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            StartupLog.Write($"Browser restart skipped: executable not found ({browserName})");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = "--new-window about:blank",
                UseShellExecute = true
            });
            StartupLog.Write($"Browser restarted safely: {browserName} ({executablePath})");
        }
        catch (Exception ex)
        {
            StartupLog.Write(ex, $"Failed to restart {browserName}");
        }
    }

    private static string? TryGetBrowserExecutable(string browserName)
    {
        foreach (var processName in GetProcessNames(browserName))
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    return process.MainModule?.FileName;
                }
                catch
                {
                    // Ignore and try the next process.
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        return GetDefaultBrowserExecutable(browserName);
    }

    private static string? GetDefaultBrowserExecutable(string browserName)
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return browserName switch
        {
            "Chrome" => FirstExisting(
                Path.Combine(local, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe")),
            "Edge" => FirstExisting(
                Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(programFiles, "Microsoft", "Edge", "Application", "msedge.exe")),
            "Brave" => FirstExisting(
                Path.Combine(local, "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),
                Path.Combine(programFiles, "BraveSoftware", "Brave-Browser", "Application", "brave.exe")),
            "Vivaldi" => FirstExisting(
                Path.Combine(local, "Vivaldi", "Application", "vivaldi.exe"),
                Path.Combine(programFiles, "Vivaldi", "Application", "vivaldi.exe")),
            "Opera" => FirstExisting(
                Path.Combine(local, "Programs", "Opera", "opera.exe"),
                Path.Combine(programFiles, "Opera", "launcher.exe")),
            "Opera GX" => FirstExisting(
                Path.Combine(local, "Programs", "Opera GX", "opera.exe"),
                Path.Combine(programFiles, "Opera GX", "launcher.exe")),
            _ => null
        };
    }

    private static string? FirstExisting(params string[] paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static bool TryStopBrowser(string browserName)
    {
        foreach (var processName in GetProcessNames(browserName))
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    if (process.MainWindowHandle != IntPtr.Zero)
                        process.CloseMainWindow();
                }
                catch
                {
                    // Ignore and fall back to Kill().
                }
            }
        }

        Thread.Sleep(1200);

        foreach (var processName in GetProcessNames(browserName))
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Ignore and verify by process presence below.
                }
            }
        }

        for (var i = 0; i < 16; i++)
        {
            if (!IsBrowserRunning(browserName))
                return true;

            Thread.Sleep(250);
        }

        return !IsBrowserRunning(browserName);
    }

    private static string[] GetProcessNames(string browserName) =>
        browserName switch
        {
            "Chrome" => ["chrome"],
            "Edge" => ["msedge"],
            "Brave" => ["brave"],
            "Vivaldi" => ["vivaldi"],
            "Opera" or "Opera GX" => ["opera"],
            _ => []
        };

    private static bool IsBrowserRunning(string browserName) =>
        GetProcessNames(browserName).Any(name => Process.GetProcessesByName(name).Length > 0);

    private static int? TryGetInt(JsonNode? node)
    {
        try
        {
            return node?.GetValue<int>();
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractOrigin(string key)
    {
        var first = key.Split(',')[0];
        if (first.StartsWith("[*.]", StringComparison.Ordinal))
            first = "https://" + first[4..];
        return first;
    }

    private static IEnumerable<ChromiumProfile> ChromiumProfiles()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        foreach (var profile in UserDataProfiles(Path.Combine(local, "Google", "Chrome", "User Data"), "Chrome"))
            yield return profile;
        foreach (var profile in UserDataProfiles(Path.Combine(local, "Microsoft", "Edge", "User Data"), "Edge"))
            yield return profile;
        foreach (var profile in UserDataProfiles(Path.Combine(local, "BraveSoftware", "Brave-Browser", "User Data"), "Brave"))
            yield return profile;
        foreach (var profile in UserDataProfiles(Path.Combine(local, "Vivaldi", "User Data"), "Vivaldi"))
            yield return profile;

        foreach (var direct in DirectProfiles(Path.Combine(roaming, "Opera Software", "Opera Stable"), "Opera"))
            yield return direct;
        foreach (var direct in DirectProfiles(Path.Combine(roaming, "Opera Software", "Opera GX Stable"), "Opera GX"))
            yield return direct;
    }

    private static IEnumerable<ChromiumProfile> UserDataProfiles(string userDataDir, string browserName)
    {
        if (!Directory.Exists(userDataDir))
            yield break;

        foreach (var dir in Directory.EnumerateDirectories(userDataDir))
        {
            var name = Path.GetFileName(dir);
            if (!name.Equals("Default", StringComparison.OrdinalIgnoreCase) &&
                !name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var preferences = Path.Combine(dir, "Preferences");
            if (File.Exists(preferences))
                yield return new ChromiumProfile(browserName, preferences);
        }
    }

    private static IEnumerable<ChromiumProfile> DirectProfiles(string profileDir, string browserName)
    {
        var preferences = Path.Combine(profileDir, "Preferences");
        if (File.Exists(preferences))
            yield return new ChromiumProfile(browserName, preferences);
    }

    private sealed record NotificationTarget(string StorageKey, string Origin, string DetectionSource);
    private sealed record ProfilePlan(ChromiumProfile Profile, IReadOnlyList<NotificationTarget> Targets);
    private sealed record ChromiumProfile(string BrowserName, string PreferencesPath);
}
