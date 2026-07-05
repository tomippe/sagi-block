using System.Globalization;
using System.Runtime.InteropServices;
using Windows.Globalization;

namespace SagiBlock.Helpers;

internal static class CultureHelper
{
    private static readonly string[] SupportedCultures = ["ja-JP", "en-US", "zh-Hans"];

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetUserDefaultUILanguage();

    internal static void ApplyUserInterfaceCulture()
    {
        var culture = GetBestUiCulture();
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
    }

    internal static CultureInfo GetBestUiCulture()
    {
        foreach (var tag in GetPreferredLanguageTags())
        {
            var normalized = NormalizeCultureTag(tag);
            if (normalized is null)
                continue;

            try
            {
                return CultureInfo.GetCultureInfo(normalized);
            }
            catch
            {
                // Try the next preferred language.
            }
        }

        try
        {
            return CultureInfo.GetCultureInfo((int)GetUserDefaultUILanguage());
        }
        catch
        {
            return CultureInfo.CurrentUICulture;
        }
    }

    private static IEnumerable<string> GetPreferredLanguageTags()
    {
        IReadOnlyList<string> tags;
        try
        {
            tags = ApplicationLanguages.Languages;
        }
        catch
        {
            return [];
        }

        return tags.Where(tag => !string.IsNullOrWhiteSpace(tag));
    }

    private static string? NormalizeCultureTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return null;

        if (SupportedCultures.Contains(tag, StringComparer.OrdinalIgnoreCase))
            return SupportedCultures.First(c => c.Equals(tag, StringComparison.OrdinalIgnoreCase));

        if (tag.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
            return "ja-JP";

        if (tag.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            return "zh-Hans";

        if (tag.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            return "en-US";

        return null;
    }
}
