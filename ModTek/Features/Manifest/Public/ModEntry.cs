using System;
using System.ComponentModel;
using System.IO;
using BattleTech;
using ModTek.Features.CustomResources;
using ModTek.Features.CustomStreamingAssets;
using ModTek.Features.Manifest;
using ModTek.Misc;
using ModTek.Util;
using Newtonsoft.Json;

// ReSharper disable once CheckNamespace
namespace ModTek
{
    // TODO probably exposing too much as public, go through usages in RT
    public class ModEntry
    {
        [JsonProperty(Required = Required.Always)]
        public string Path { get; set; }

        public bool IsModdedContentPackBasePath => Path.Equals(FilePaths.ModdedContentPackDirectoryName);

        // directory based methods, used during normalization
        public bool IsDirectory => Directory.Exists(AbsolutePath);

        // file based methods
        public bool IsFile => File.Exists(AbsolutePath);
        internal DateTime LastWriteTimeUtc => File.GetLastWriteTimeUtc(AbsolutePath);
        public string FileExtension => System.IO.Path.GetExtension(Path);
        public string FileNameWithoutExtension => System.IO.Path.GetFileNameWithoutExtension(Path);
        internal string RelativePathToMods => FileUtils.GetRelativePath(FilePaths.ModsDirectory, AbsolutePath);

        internal bool IsJson => FileUtils.IsJson(Path);
        internal bool IsTxt => FileUtils.IsTxt(Path);
        internal bool IsCsv => FileUtils.IsCsv(Path);

        public string Type { get; set; }
        internal bool IsTypeSoundBankDef => Type == nameof(SoundBankDef);
        internal BattleTechResourceType? ResourceType => BTConstants.BTResourceType(Type, out var type) ? type : (BattleTechResourceType?)null;
        internal bool IsTypeBattleTechResourceType => ResourceType != null;
        internal bool IsTypeCustomStreamingAsset => BTConstants.CSAssetsType(Type, out _);
        internal bool IsTypeCustomResource => CustomResourcesFeature.IsCustomResourceType(Type);

        public string Id { get; set; }

        public string AddToAddendum { get; set; }
        public string[] RequiredContentPacks { get; set; }
        public string AssetBundleName { get; set; }
        public bool? AssetBundlePersistent { get; set; }

        [DefaultValue(false)]
        public bool ShouldMergeJSON { get; set; }

        [DefaultValue(false)]
        public bool ShouldAppendText { get; set; }

        [DefaultValue(true)]
        public bool AddToDB { get; set; } = true;

        public ModEntry copy()
        {
            return (ModEntry) MemberwiseClone();
        }

        [JsonIgnore]
        public ModDefEx ModDef { get; set; }

        [JsonIgnore]
        public string AbsolutePath => ModDef.GetFullPath(Path);

        public override string ToString()
        {
            var extra = "";
            if (AddToAddendum != null)
            {
                extra += $" AddToAddendum={AddToAddendum}";
            }
            if (AssetBundleName != null)
            {
                extra += $" AssetBundleName={AssetBundleName}";
            }
            if (Type != null)
            {
                extra += $" Type={Type}";
            }
            return $"{Id} ({extra} ): {RelativePathToMods}";
        }

        [JsonIgnore]
        private VersionManifestEntry customResourceEntry;
        internal VersionManifestEntry CreateVersionManifestEntry()
        {
            return customResourceEntry = customResourceEntry ?? new VersionManifestEntry(
                Id,
                AbsolutePath,
                Type,
                LastWriteTimeUtc,
                "1",
                AssetBundleName,
                AssetBundlePersistent
            );
        }
    }
}
