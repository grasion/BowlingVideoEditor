using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Octokit;

namespace BowlingVideoEditor.Services
{
    public class UpdateService
    {
        private readonly string _owner;
        private readonly string _repo;
        private readonly GitHubClient _client;

        public UpdateService(string owner, string repo)
        {
            _owner = owner;
            _repo = repo;
            _client = new GitHubClient(new ProductHeaderValue("BowlingVideoEditor"));
        }

        public Version GetCurrentVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            return assembly.GetName().Version ?? new Version(1, 0, 0);
        }

        public async Task<(bool Available, string TagName, string DownloadUrl, string Body)?> CheckForUpdateAsync()
        {
            try
            {
                var releases = await _client.Repository.Release.GetAll(_owner, _repo);
                var latest = releases.FirstOrDefault(r => !r.Prerelease);

                if (latest == null) return null;

                var latestVersion = ParseVersion(latest.TagName);
                var currentVersion = GetCurrentVersion();

                if (latestVersion > currentVersion)
                {
                    var asset = latest.Assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                    if (asset != null)
                    {
                        return (true, latest.TagName, asset.BrowserDownloadUrl, latest.Body);
                    }
                }

                return (false, latest.TagName, string.Empty, string.Empty);
            }
            catch
            {
                return null;
            }
        }

        public async Task DownloadAndApplyUpdateAsync(string downloadUrl, IProgress<double>? progress = null)
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var updateDir = Path.Combine(Path.GetTempPath(), "BowlingVideoEditor_Update");
            Directory.CreateDirectory(updateDir);

            var zipPath = Path.Combine(updateDir, "update.zip");

            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(zipPath, System.IO.FileMode.Create))
            {
                var buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalRead += bytesRead;
                    if (totalBytes > 0)
                        progress?.Report((double)totalRead / totalBytes * 100);
                }
            }

            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, updateDir, true);

            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var batchPath = Path.Combine(updateDir, "update.bat");
            var batchContent = $"""
                @echo off
                timeout /t 2 /nobreak >nul
                xcopy /s /y "{updateDir}\*.*" "{appDir}"
                del /q "{zipPath}"
                start "" "{Path.Combine(appDir, "BowlingVideoEditor.exe")}"
                del "%~f0"
                """;

            await File.WriteAllTextAsync(batchPath, batchContent);

            Process.Start(new ProcessStartInfo
            {
                FileName = batchPath,
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            Environment.Exit(0);
        }

        private static Version ParseVersion(string tag)
        {
            var cleaned = tag.TrimStart('v', 'V');
            return Version.TryParse(cleaned, out var version) ? version : new Version(0, 0, 0);
        }
    }
}
