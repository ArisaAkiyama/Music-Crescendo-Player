using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Linq;

namespace DesktopMusicPlayer.Services;

public class UpdateService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/ArisaAkiyama/Music-Crescendo-Player/releases/latest";
    private readonly HttpClient _httpClient;

    public UpdateService()
    {
        _httpClient = new HttpClient();
        // GitHub API requires a User-Agent header
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Crescendo-Desktop-Player");
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(GitHubApiUrl);
            
            if (release == null) return null;

            // Parse version from tag (remove 'v' prefix if present)
            var releaseVersionStr = release.TagName?.TrimStart('v');
            if (string.IsNullOrEmpty(releaseVersionStr)) return null;

            // Handle pre-release suffixes (e.g. 1.0.1-beta.2)
            // System.Version only handles 1.0.1.0 format
            var cleanVersionStr = releaseVersionStr.Split('-')[0];
            
            if (Version.TryParse(cleanVersionStr, out var releaseVersion))
            {
                var currentVersion = Assembly.GetEntryAssembly()?.GetName().Version;
                
                // Compare base version first
                if (currentVersion != null)
                {
                    // If base version is higher (e.g. 1.0.2 > 1.0.1), it's an update
                    if (releaseVersion > currentVersion)
                    {
                        // Logic simplified for beta: if base version is newer, it's an update.
                        // Ideally we check suffixes too, but for beta to stable/beta, this is often enough
                        // if we assume strictly increasing version numbers.
                        
                        var exeAsset = release.Assets?.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
                    
                        return new UpdateInfo
                        {
                            Version = release.TagName,
                            ReleaseNotes = release.Body,
                            DownloadUrl = exeAsset?.BrowserDownloadUrl ?? release.HtmlUrl
                        };
                    }
                    // If base versions are equal (1.0.2 == 1.0.2), we need to check if user is on older beta
                    // NOTE: This simple logic might skip beta updates if only suffix changes (1.0.2-beta.1 -> 1.0.2-beta.2)
                    // But standard Version class ignores suffixes.
                    // For now, let's rely on base version bumps for reliable updates.
                    // Or implement rudimentary string build compare.
                }

            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update check failed: {ex.Message}");
        }

        return null;
    }

    public async Task DownloadInstallerAsync(string url, IProgress<double> progress)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "CrescendoSetup.exe");

        using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes != -1;

            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[8192];
                var totalRead = 0L;
                var bytesRead = 0;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (canReportProgress)
                    {
                        progress.Report((double)totalRead / totalBytes * 100);
                    }
                }
            }
        }

        // Run the installer
        Process.Start(new ProcessStartInfo(tempPath)
        {
            UseShellExecute = true
        });
        
        // App needs to close to allow update
        System.Windows.Application.Current.Shutdown();
    }
}

public class UpdateInfo
{
    public string Version { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
}

// JSON mapping classes
public class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("assets")]
    public GitHubAsset[]? Assets { get; set; }
}

public class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = "";
}
