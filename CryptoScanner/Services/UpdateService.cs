using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace CryptoScanner.Services;

public sealed class UpdateService
{
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/TBE-ThomasTian/CryptoScannerPro/releases/latest";
    private const string ReleasesPageUrl = "https://github.com/TBE-ThomasTian/CryptoScannerPro/releases";
    private readonly HttpClient _httpClient = new();

    public UpdateService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CryptoScanner", CurrentVersion));
    }

    public string CurrentVersion => Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "1.0.0";

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(LatestReleaseApiUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<GitHubReleaseResponse>(stream, cancellationToken: cancellationToken);
        if (release == null || string.IsNullOrWhiteSpace(release.tag_name))
            return new UpdateCheckResult(CurrentVersion, null, false, ReleasesPageUrl);

        var latestVersion = NormalizeVersion(release.tag_name);
        var currentVersion = NormalizeVersion(CurrentVersion);
        var hasUpdate = latestVersion > currentVersion;
        return new UpdateCheckResult(CurrentVersion, release.tag_name, hasUpdate, release.html_url ?? ReleasesPageUrl);
    }

    public void OpenReleasesPage(string? url = null)
    {
        var target = string.IsNullOrWhiteSpace(url) ? ReleasesPageUrl : url;
        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true
        });
    }

    private static Version NormalizeVersion(string raw)
    {
        var cleaned = raw.Trim().TrimStart('v', 'V');
        return Version.TryParse(cleaned, out var version) ? version : new Version(0, 0, 0);
    }

    private sealed class GitHubReleaseResponse
    {
        public string? tag_name { get; set; }
        public string? html_url { get; set; }
    }
}

public sealed record UpdateCheckResult(string CurrentVersion, string? LatestVersion, bool HasUpdate, string ReleaseUrl);
