// Copyright (c) 2026 Mashyo. All Rights Reserved.

namespace Musait.Services
{
    public sealed class UpdateInfo
    {
        public UpdateInfo(
            string version,
            string tagName,
            string releaseUrl,
            string installerAssetUrl,
            string installerFileName)
        {
            Version = version;
            TagName = tagName;
            ReleaseUrl = releaseUrl;
            InstallerAssetUrl = installerAssetUrl;
            InstallerFileName = installerFileName;
        }

        public string Version { get; }
        public string TagName { get; }
        public string ReleaseUrl { get; }
        public string InstallerAssetUrl { get; }
        public string InstallerFileName { get; }
    }
}
