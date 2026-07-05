using System.Globalization;
using System.Resources;

namespace SagiBlock;

public static class L
{
    private static readonly ResourceManager Rm =
        new("SagiBlock.Resources.Strings", typeof(L).Assembly);

    public static string Get(string key) =>
        Rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentUICulture, Get(key), args);
}
