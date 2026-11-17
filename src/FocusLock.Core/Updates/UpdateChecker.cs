using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Updates;

public sealed record UpdateInfo(Version Version, string DownloadUrl);

/// <summary>
/// Asks GitHub for the newest FocusLock release. Any failure (offline, no release yet, rate limit) just means
/// "no update", so the app never bothers the person about it.
/// </summary>
public sealed class UpdateChecker(HttpClient http, ILogger<UpdateChecker> logger)
{
    public const string DownloadUrl = "https://github.com/RobelYonas/FocusLock/releases/latest";
    private const string LatestReleaseApi = "https://api.github.com/repos/RobelYonas/FocusLock/releases/latest";

    public async Task<UpdateInfo?> CheckAsync(Version current, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
            // GitHub rejects API requests without a User-Agent.
            request.Headers.UserAgent.ParseAdd("FocusLock-update-check");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogInformation("Update check: GitHub answered {Status}", (int)response.StatusCode);
                return null;
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
            var tag = json.RootElement.TryGetProperty("tag_name", out var tagName) && tagName.ValueKind == JsonValueKind.String
                ? tagName.GetString()
                : null;
            var latest = ParseVersion(tag);
            if (latest is null || latest <= Normalize(current))
            {
                return null;
            }

            logger.LogInformation("Update check: version {Latest} is available", latest);
            return new UpdateInfo(latest, DownloadUrl);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Update check failed");
            return null;
        }
    }

    /// <summary>"v1.2.0", "1.2" or "1.2.0.0" become 1.2.0; anything else is null.</summary>
    public static Version? ParseVersion(string? tag)
    {
        var text = tag?.Trim().TrimStart('v', 'V');
        return Version.TryParse(text, out var version) ? Normalize(version) : null;
    }

    // Assembly versions have four parts and tags usually three; Version treats 0.1.0 and 0.1.0.0 as different.
    private static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(version.Build, 0));
}
