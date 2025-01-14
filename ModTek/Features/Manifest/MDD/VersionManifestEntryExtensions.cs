﻿using System.Linq;
using BattleTech;

namespace ModTek.Features.Manifest.MDD
{
    internal static class VersionManifestEntryExtensions
    {
        internal static bool IsInDefaultMDDB(this VersionManifestEntry entry)
        {
            return entry.IsMemoryAsset || entry.IsResourcesAsset || entry.IsStreamingAssetData() || entry.IsContentPackAssetBundle();
        }

        private static bool IsStreamingAssetData(this VersionManifestEntry entry)
        {
            return entry.IsFileAsset && (entry.GetRawPath()?.StartsWith("data/") ?? false);
        }

        private static bool IsContentPackAssetBundle(this VersionManifestEntry entry)
        {
            return entry.IsAssetBundled && BTConstants.HBSContentNames.Contains(entry.AssetBundleName);
        }

        internal static string ToShortString(this VersionManifestEntry entry)
        {
            return $"{entry.Id} ({entry.Type})";
        }
    }
}
