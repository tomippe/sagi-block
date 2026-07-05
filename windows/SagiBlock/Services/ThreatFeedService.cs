using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;

namespace SagiBlock.Services;

public sealed class ThreatFeedService
{
    private const int LocalScoreThreshold = NotificationOriginScorer.SuspiciousThreshold;
    private const int FeedMaxAgeHours = 24;
    private const int CacheTtlDays = 7;

    private static readonly string FeedDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SagiBlock",
        "feeds");

    private static readonly string PhishTankHostsPath = Path.Combine(FeedDir, "phishtank-hosts.json");
    private static readonly string PhishTankMetaPath = Path.Combine(FeedDir, "phishtank-meta.json");
    private static readonly string CachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SagiBlock",
        "domain-threat-cache.json");

    private static readonly Uri PhishTankFeedUri =
        new("http://data.phishtank.com/data/online-valid.csv.gz");

    private static readonly Uri UrlHausHostApiUri =
        new("https://urlhaus-api.abuse.ch/v1/host/");

    private readonly HttpClient _http;
    private HashSet<string> _phishTankHosts = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private bool _feedsLoaded;

    public ThreatFeedService()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SagiBlock/0.1 (notification-permission-guard)");
    }

    public async Task EnsureFeedsReadyAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(FeedDir);
        LoadCache();
        LoadPhishTankHostsFromDisk();

        if (!IsPhishTankFresh())
            await RefreshPhishTankFeedAsync(cancellationToken).ConfigureAwait(false);

        _feedsLoaded = true;
    }

    public async Task<(bool IsThreat, string Source)> CheckHostAsync(string host, CancellationToken cancellationToken = default)
    {
        if (!_feedsLoaded)
            await EnsureFeedsReadyAsync(cancellationToken).ConfigureAwait(false);

        if (_phishTankHosts.Contains(host))
            return (true, "PhishTank");

        if (_cache.TryGetValue(host, out var cached) && !cached.IsExpired)
            return cached.IsThreat ? (true, cached.Source) : (false, "");

        var listed = await QueryUrlHausAsync(host, cancellationToken);
        SaveCacheEntry(host, listed.IsThreat, listed.Source);
        return listed;
    }

    private async Task<(bool IsThreat, string Source)> QueryUrlHausAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["host"] = host
            });
            using var response = await _http.PostAsync(UrlHausHostApiUri, content, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return (false, "");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var status = doc.RootElement.TryGetProperty("query_status", out var statusNode)
                ? statusNode.GetString()
                : null;

            if (string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
                return (true, "URLhaus");

            return (false, "");
        }
        catch
        {
            return (false, "");
        }
    }

    private async Task RefreshPhishTankFeedAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(PhishTankFeedUri, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return;

            await using var gzip = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var decompressed = new GZipStream(gzip, CompressionMode.Decompress);
            using var reader = new StreamReader(decompressed);

            var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (line.Length == 0 || line.StartsWith("phish_id", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!TryExtractHostFromPhishTankLine(line, out var host))
                    continue;

                hosts.Add(host);
            }

            if (hosts.Count == 0)
                return;

            _phishTankHosts = hosts;
            var payload = JsonSerializer.Serialize(hosts.OrderBy(static h => h).ToList());
            await File.WriteAllTextAsync(PhishTankHostsPath, payload, cancellationToken);
            await File.WriteAllTextAsync(
                PhishTankMetaPath,
                JsonSerializer.Serialize(new FeedMeta(DateTimeOffset.UtcNow)),
                cancellationToken);
        }
        catch
        {
            // Keep the previous on-disk feed if refresh fails.
        }
    }

    private static bool TryExtractHostFromPhishTankLine(string line, out string host)
    {
        host = "";
        var start = line.IndexOf("http://", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            start = line.IndexOf("https://", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return false;

        var end = line.IndexOf(',', start);
        var urlText = end >= 0 ? line[start..end] : line[start..];
        if (!Uri.TryCreate(urlText.Trim('"'), UriKind.Absolute, out var uri))
            return false;

        host = uri.Host.Trim('.').ToLowerInvariant();
        return !string.IsNullOrWhiteSpace(host);
    }

    private void LoadPhishTankHostsFromDisk()
    {
        try
        {
            if (!File.Exists(PhishTankHostsPath))
                return;

            var hosts = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(PhishTankHostsPath)) ?? [];
            _phishTankHosts = new HashSet<string>(hosts, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            _phishTankHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private bool IsPhishTankFresh()
    {
        try
        {
            if (!File.Exists(PhishTankMetaPath) || !File.Exists(PhishTankHostsPath))
                return false;

            var meta = JsonSerializer.Deserialize<FeedMeta>(File.ReadAllText(PhishTankMetaPath));
            return meta is not null &&
                   DateTimeOffset.UtcNow - meta.UpdatedAt <= TimeSpan.FromHours(FeedMaxAgeHours);
        }
        catch
        {
            return false;
        }
    }

    private void LoadCache()
    {
        try
        {
            if (!File.Exists(CachePath))
            {
                _cache = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            _cache = JsonSerializer.Deserialize<Dictionary<string, CacheEntry>>(File.ReadAllText(CachePath))
                ?? new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            _cache = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveCacheEntry(string host, bool isThreat, string source)
    {
        _cache[host] = new CacheEntry(isThreat, source, DateTimeOffset.UtcNow);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            File.WriteAllText(CachePath, JsonSerializer.Serialize(_cache));
        }
        catch
        {
            // Cache is optional.
        }
    }

    private sealed record FeedMeta(DateTimeOffset UpdatedAt);

    private sealed record CacheEntry(bool IsThreat, string Source, DateTimeOffset CheckedAt)
    {
        public bool IsExpired => DateTimeOffset.UtcNow - CheckedAt > TimeSpan.FromDays(CacheTtlDays);
    }
}
