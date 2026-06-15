// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Musait.Models;

namespace Musait.Services
{
    public sealed class UpdateService
    {
        private static readonly HttpClient Client = CreateClient();
        private static string? _pendingInstallerPath;

        public static string? PendingInstallerPath => _pendingInstallerPath;

        public async Task<UpdateInfo?> CheckLatestAsync()
        {
            using var response = await Client.GetAsync(Constants.LatestReleaseApiUrl).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var release = JObject.Parse(json);
            string tagName = release.Value<string>("tag_name") ?? string.Empty;
            string releaseUrl = release.Value<string>("html_url") ?? Constants.ReleasesUrl;
            string latestVersion = NormalizeVersion(tagName);

            if (!IsNewerVersion(latestVersion, Constants.CurrentVersion))
            {
                return null;
            }

            var assets = release["assets"] as JArray;
            var installerAsset = assets?
                .OfType<JObject>()
                .Select(asset => new
                {
                    Name = asset.Value<string>("name") ?? string.Empty,
                    Url = asset.Value<string>("browser_download_url") ?? string.Empty
                })
                .FirstOrDefault(asset =>
                    string.Equals(asset.Name, "Musait-Setup.exe", StringComparison.OrdinalIgnoreCase));

            if (installerAsset == null || string.IsNullOrWhiteSpace(installerAsset.Url))
            {
                return new UpdateInfo(latestVersion, tagName, releaseUrl, string.Empty, string.Empty);
            }

            return new UpdateInfo(latestVersion, tagName, releaseUrl, installerAsset.Url, installerAsset.Name);
        }

        public async Task<string> DownloadInstallerAsync(UpdateInfo updateInfo, IProgress<string>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(updateInfo.InstallerAssetUrl) ||
                string.IsNullOrWhiteSpace(updateInfo.InstallerFileName))
            {
                throw new InvalidOperationException("This release does not include the setup installer.");
            }

            progress?.Report("Downloading update");
            string downloadFolder = Path.Combine(Path.GetTempPath(), "Musait", "Updates");
            Directory.CreateDirectory(downloadFolder);

            string installerPath = Path.Combine(downloadFolder, updateInfo.InstallerFileName);
            using var response = await Client.GetAsync(updateInfo.InstallerAssetUrl).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var target = File.Create(installerPath))
            {
                await source.CopyToAsync(target).ConfigureAwait(false);
            }

            _pendingInstallerPath = installerPath;
            progress?.Report("Installer will run after Revit closes");
            return installerPath;
        }

        public static void ClearPendingInstaller()
        {
            _pendingInstallerPath = null;
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(Constants.UpdateUserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        private static string NormalizeVersion(string tagName)
        {
            return tagName.Trim().TrimStart('v', 'V');
        }

        private static bool IsNewerVersion(string latestVersion, string currentVersion)
        {
            if (!Version.TryParse(latestVersion, out Version? latest))
            {
                return false;
            }

            if (!Version.TryParse(currentVersion, out Version? current))
            {
                current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            }

            return latest > current;
        }
    }
}
