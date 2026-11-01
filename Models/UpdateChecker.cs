using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CircuitCanvas.Models
{
    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }
        public string CurrentVersion { get; set; }
        public string LatestVersion { get; set; }
        public string ReleaseNotes { get; set; }
        public string DownloadUrl { get; set; }
        public string ReleasePageUrl { get; set; }
    }

    public static class UpdateChecker
    {
        public const string CurrentVersion = "1.0.0";
        public const string RepoOwner = "m1ssl3";
        public const string RepoName = "ElectroStudio";

        public static async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            var result = new UpdateInfo
            {
                CurrentVersion = CurrentVersion,
                IsUpdateAvailable = false
            };

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "ElectroStudio-Updater");
                client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

                string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
                string json = await client.GetStringAsync(url);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tagName = root.GetProperty("tag_name").GetString() ?? "";
                string cleanLatest = tagName.TrimStart('v', 'V');
                string body = root.TryGetProperty("body", out var b) ? (b.GetString() ?? "") : "";
                string htmlUrl = root.TryGetProperty("html_url", out var h) ? (h.GetString() ?? "") : "";

                result.LatestVersion = cleanLatest;
                result.ReleaseNotes = body;
                result.ReleasePageUrl = htmlUrl;

                if (root.TryGetProperty("assets", out var assets) && assets.GetArrayLength() > 0)
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        string name = asset.GetProperty("name").GetString() ?? "";
                        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            result.DownloadUrl = asset.GetProperty("browser_download_url").GetString();
                            break;
                        }
                    }
                }

                if (Version.TryParse(cleanLatest, out var latestVer) &&
                    Version.TryParse(CurrentVersion, out var currVer))
                {
                    if (latestVer > currVer)
                    {
                        result.IsUpdateAvailable = true;
                    }
                }
            }
            catch
            {
                // Сеть недоступна или нет релиза — тихо игнорируем
            }

            return result;
        }

        public static async Task<bool> DownloadAndApplyUpdateAsync(string downloadUrl, Action<int> progressCallback = null)
        {
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string tempDir = Path.Combine(Path.GetTempPath(), "ElectroStudio_Update_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);

                string zipPath = Path.Combine(tempDir, "update.zip");

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "ElectroStudio-Updater");
                    using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                    var buffer = new byte[8192];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;
                        if (totalBytes > 0 && progressCallback != null)
                        {
                            int percent = (int)((totalRead * 100) / totalBytes);
                            progressCallback(percent);
                        }
                    }
                }

                string extractDir = Path.Combine(tempDir, "extracted");
                ZipFile.ExtractToDirectory(zipPath, extractDir);

                // Создаём скрипт обновления updater.bat
                string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? Path.Combine(appDir, "ElectroStudio.exe");
                int currentPid = Process.GetCurrentProcess().Id;

                string batchScript = $@"@echo off
timeout /t 1 /nobreak > nul
:wait_loop
tasklist /FI ""PID eq {currentPid}"" 2>NUL | find /I ""{currentPid}"" >NUL
if not errorlevel 1 (
    timeout /t 1 /nobreak > nul
    goto wait_loop
)

xcopy /Y /E /I ""{extractDir}\*"" ""{appDir}"" > nul
start """" ""{currentExe}""
del ""%~f0""
";

                string updaterBat = Path.Combine(tempDir, "updater.bat");
                await File.WriteAllTextAsync(updaterBat, batchScript, System.Text.Encoding.ASCII);

                var psi = new ProcessStartInfo
                {
                    FileName = updaterBat,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(psi);
                Process.GetCurrentProcess().Kill();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Update failed: " + ex.Message);
                return false;
            }
        }
    }
}
