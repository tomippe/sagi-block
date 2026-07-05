namespace SagiBlock.Services;

internal static class NotificationOriginScorer
{
    internal const int SuspiciousThreshold = 3;

    private static readonly string[] ScamWords =
    [
        "security", "defender", "microsoft", "windows", "support", "virus", "alert",
        "warning", "scan", "firewall", "malware", "trojan", "safe", "protect"
    ];

    private static readonly string[] NotificationBaitWords =
    [
        "quiz", "trivia", "genius", "prize", "spin", "viral", "lottery",
        "giveaway", "sweepstakes", "cashto", "reward", "gamebox", "funbox"
    ];

    private static readonly string[] SuspiciousTlds =
    [
        ".top", ".xyz", ".click", ".buzz", ".cyou", ".quest", ".icu", ".monster",
        ".live", ".site", ".online", ".shop", ".cam", ".lol", ".sbs", ".co.in"
    ];

    private static readonly string[] TrustedDomains =
    [
        "microsoft.com", "windows.com", "google.com", "youtube.com", "gmail.com",
        "apple.com", "icloud.com", "yahoo.co.jp", "amazon.co.jp", "rakuten.co.jp",
        "line.me", "slack.com", "notion.so", "chatwork.com"
    ];

    internal static bool IsTrustedHost(string host) =>
        TrustedDomains.Any(domain => host == domain || host.EndsWith("." + domain, StringComparison.Ordinal));

    internal static int GetScore(string host)
    {
        var score = 0;
        if (ScamWords.Any(word => host.Contains(word, StringComparison.OrdinalIgnoreCase)))
            score += 3;
        if (NotificationBaitWords.Any(word => host.Contains(word, StringComparison.OrdinalIgnoreCase)))
            score += 3;
        if (SuspiciousTlds.Any(tld => host.EndsWith(tld, StringComparison.OrdinalIgnoreCase)))
            score += 2;
        if (host.Count(c => c == '-') >= 2)
            score += 1;
        if (host.Count(char.IsDigit) >= 4)
            score += 1;
        if (host.Split('.').Length >= 4)
            score += 1;
        if (System.Net.IPAddress.TryParse(host, out _))
            score += 3;
        if (host.Length >= 28)
            score += 1;

        return score;
    }

    internal static bool TryGetHost(string origin, out string host)
    {
        host = "";
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            return false;

        host = uri.Host.Trim('.').ToLowerInvariant();
        return !string.IsNullOrWhiteSpace(host);
    }
}
