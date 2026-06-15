// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using System.IO;

namespace Musait.Models
{
    public static class Constants
    {
        public const string PluginName = "Musait";
        public const string AuthorName = "Mashyo";
        public const string ProductGroup = "Mashyo Tools";
        public const string LicenseName = "Musait EULA";
        public const string RepositoryUrl = "https://github.com/mashyo/musait";
        public const string ReleasesUrl = "https://github.com/mashyo/musait/releases";
        public const string LatestReleaseApiUrl = "https://api.github.com/repos/mashyo/musait/releases/latest";
        public const string PatreonUrl = "https://www.patreon.com/mashyo/posts/musait-pro-image-161060999";
        public const string SponsorsUrl = "https://github.com/sponsors/mashyo";
        public static readonly string CurrentVersion = GetCurrentVersion();
        public static readonly string UpdateUserAgent = $"Musait/{CurrentVersion}";

        public static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Mashyo",
            "Musait"
        );

        public static readonly string SettingsFilePath = Path.Combine(AppDataFolder, "settings.json");
        
        public static readonly string BrowserDataFolder = Path.Combine(AppDataFolder, "BrowserData");

        public static readonly string CapturesFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "Mashyo_Captures"
        );

        private static string GetCurrentVersion()
        {
            Version? version = typeof(Constants).Assembly.GetName().Version;
            if (version == null)
            {
                return "0.0.0";
            }

            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }
}

