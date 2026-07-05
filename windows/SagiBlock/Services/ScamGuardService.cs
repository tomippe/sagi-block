using System.IO;
using System.Text.Json;
using SagiBlock.Models;

namespace SagiBlock.Services;

public sealed class ScamGuardService
{
    private readonly BrowserWindowScanner _windowScanner = new();
    private readonly NotificationPermissionCleaner _permissionCleaner = new();
    private readonly string _logPath;

    public ScamGuardService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "SagiBlock");
        Directory.CreateDirectory(dir);
        _logPath = Path.Combine(dir, "events.jsonl");
    }

    public async Task<IReadOnlyList<GuardEvent>> CheckOnceAsync()
    {
        var events = new List<GuardEvent>();
        try
        {
            events.AddRange(_windowScanner.CloseScarewareWindows());
            events.AddRange(await _permissionCleaner.BlockScamNotificationPermissionsAsync());
        }
        catch (Exception ex)
        {
            events.Add(new GuardEvent(
                GuardEventKind.CheckFailed,
                L.Get("CheckFailedTitle"),
                ex.Message,
                $"check-failed:{ex.GetType().Name}:{ex.Message}"));
        }

        AppendLog(events);
        return events;
    }

    private void AppendLog(IEnumerable<GuardEvent> events)
    {
        foreach (var item in events)
        {
            var row = new
            {
                time = DateTimeOffset.Now,
                kind = item.Kind.ToString(),
                item.Title,
                item.Detail,
                item.Key
            };
            File.AppendAllText(_logPath, JsonSerializer.Serialize(row) + Environment.NewLine);
        }
    }
}

