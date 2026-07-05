using CommunityToolkit.WinUI.Notifications;
using SagiBlock.Helpers;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace SagiBlock.Services;

public sealed class NotificationService
{
    private readonly Dictionary<string, DateTimeOffset> _lastShown = new(StringComparer.OrdinalIgnoreCase);

    public void Show(string title, string body, string key)
    {
        var now = DateTimeOffset.Now;
        if (_lastShown.TryGetValue(key, out var last) && now - last < TimeSpan.FromMinutes(10))
            return;

        _lastShown[key] = now;

        var logoPath = TrayIconHelper.EnsureLogoFilePath();
        var logoUri = new Uri(logoPath, UriKind.Absolute);

        var content = new ToastContentBuilder()
            .AddAppLogoOverride(logoUri)
            .AddText(title)
            .AddText(body)
            .GetToastContent();

        var xml = new XmlDocument();
        xml.LoadXml(content.GetContent());

        var toast = new ToastNotification(xml)
        {
            Tag = key,
            Group = PackageHelper.IsPackaged()
                ? Windows.ApplicationModel.Package.Current.Id.FamilyName + "!App"
                : ToastAppRegistration.AppUserModelId
        };

        var notifier = PackageHelper.IsPackaged()
            ? ToastNotificationManager.CreateToastNotifier()
            : ToastNotificationManager.CreateToastNotifier(ToastAppRegistration.AppUserModelId);
        notifier.Show(toast);
    }
}
