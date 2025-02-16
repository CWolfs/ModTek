﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BattleTech;
using ModTek.Features.Manifest.BTRL;
using ModTek.Features.Manifest.MDD;
using ModTek.Misc;
using ModTek.Util;
using static ModTek.Features.Logging.MTLogger;
using CacheDB = System.Collections.Generic.Dictionary<ModTek.Features.Manifest.CacheKey, ModTek.Features.Manifest.Merges.MergeCacheEntry>;
using CacheKeyValue = System.Collections.Generic.KeyValuePair<ModTek.Features.Manifest.CacheKey, ModTek.Features.Manifest.Merges.MergeCacheEntry>;

namespace ModTek.Features.Manifest.Merges
{
    internal class MergeCache
    {
        private static string PersistentDirPath => FilePaths.MergeCacheDirectory;
        private readonly string PersistentFilePath;

        private readonly CacheDB CachedMerges; // stuff in here was merged
        private readonly CacheDB QueuedMerges = new CacheDB(); // stuff in here has merges queued

        private static bool HasChanges;

        internal MergeCache()
        {
            PersistentFilePath = Path.Combine(PersistentDirPath, "merge_cache.json");

            if (ModTekCacheStorage.CompressedExists(PersistentFilePath))
            {
                try
                {
                    CachedMerges = ModTekCacheStorage.CompressedReadFrom<List<CacheKeyValue>>(PersistentFilePath)
                        .ToDictionary(kv => kv.Key, kv => kv.Value);
                    return;
                }
                catch (Exception e)
                {
                    Log("MergeCache: Loading merge cache failed.", e);
                }
            }

            FileUtils.CleanDirectory(PersistentDirPath);

            // create a new one if it doesn't exist or couldn't be added'
            Log("MergeCache: Rebuilding cache.");
            CachedMerges = new CacheDB();
        }

        private readonly Stopwatch saveSW = new Stopwatch();
        internal void Save()
        {
            try
            {
                saveSW.Restart();
                if (!HasChanges)
                {
                    Log($"MergeCache: No changes detected, skipping save.");
                    return;
                }

                ModTekCacheStorage.CompressedWriteTo(CachedMerges.ToList(), PersistentFilePath);
                Log($"MergeCache: Saved to {PersistentFilePath}.");
                HasChanges = false;
            }
            catch (Exception e)
            {
                Log($"MergeCache: Couldn't write to {PersistentFilePath}", e);
            }
            finally
            {
                saveSW.Stop();
                LogIfSlow(saveSW);
            }
        }

        internal bool HasMergedContentCached(VersionManifestEntry loadingEntry, bool fetchContent, out string cachedContent)
        {
            cachedContent = null;

            var key = new CacheKey(loadingEntry);
            if (!QueuedMerges.TryGetValue(key, out var queuedMerge))
            {
                return false;
            }
            queuedMerge.SetCachedPathAndOriginalUpdatedOn(loadingEntry);

            if (!CachedMerges.TryGetValue(key, out var cachedMerge))
            {
                return false;
            }

            if (!queuedMerge.Equals(cachedMerge))
            {
                return false;
            }

            try
            {
                if (fetchContent)
                {
                    cachedContent = ModTekCacheStorage.CompressedStringReadFrom(queuedMerge.CachedAbsolutePath);
                }
                else if (!ModTekCacheStorage.CompressedExists(queuedMerge.CachedAbsolutePath))
                {
                    return false;
                }
            }
            catch
            {
                Log($"MergeCache: Couldn't read cached merge result at {queuedMerge.CachedAbsolutePath}");
                return false;
            }

            return true;
        }

        internal void MergeAndCacheContent(VersionManifestEntry loadedEntry, ref string content)
        {
            if (content == null)
            {
                return;
            }

            var key = new CacheKey(loadedEntry);
            if (!QueuedMerges.TryGetValue(key, out var queuedMerge))
            {
                return;
            }

            try
            {
                Log($"MergeCache: Merging {loadedEntry.ToShortString()}");
                content = queuedMerge.Merge(content);
            }
            catch (Exception e)
            {
                Log($"MergeCache: Couldn't merge {queuedMerge.CachedAbsolutePath}", e);
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(queuedMerge.CachedAbsolutePath) ?? throw new InvalidOperationException());
                ModTekCacheStorage.CompressedStringWriteTo(queuedMerge.CachedAbsolutePath, content);
                queuedMerge.SetVersionManifestUpdatedOn(loadedEntry);
                CachedMerges[key] = queuedMerge;
                HasChanges = true;
            }
            catch (Exception e)
            {
                Log($"MergeCache: Couldn't write cached merge result to {queuedMerge.CachedAbsolutePath}", e);
            }
        }

        internal bool HasMerges(VersionManifestEntry entry)
        {
            var key = new CacheKey(entry);
            return QueuedMerges.ContainsKey(key);
        }

        internal bool AddModEntry(ModEntry entry)
        {
            if (entry.ShouldMergeJSON && entry.IsJson)
            {
                AddTemp(entry);
                return true;
            }

            if (entry.ShouldAppendText && (entry.IsTxt || entry.IsCsv))
            {
                AddTemp(entry);
                return true;
            }

            return false;
        }

        private void AddTemp(ModEntry entry)
        {
            Log($"\tMerge: {entry}");
            if (entry.IsTypeCustomResource)
            {
                Log($"\t\tError: Custom resources can't be merged.");
                return;
            }

            var key = new CacheKey(entry);
            var temp = QueuedMerges.GetOrCreate(key);
            temp.SetCachedPath(entry);
            temp.Add(entry);
        }

        internal void CleanCacheWithCompleteManifest(ref bool flagForRebuild, HashSet<CacheKey> preloadResources)
        {
            // find entries missing in cache
            foreach (var kv in QueuedMerges)
            {
                var key = kv.Key;
                var queuedEntry = kv.Value;
                if (CachedMerges.TryGetValue(key, out var cachedEntry))
                {
                    cachedEntry.CacheHit = true;

                    var manifestEntry = BetterBTRL.Instance.EntryByIDAndType(key.Id, key.Type);
                    if (manifestEntry == null)
                    {
                        // can happen if the type was specified explicitly for merge, but the actual base resource never was supplied by any mod
                        Log($"MergeCache: Warning: Resource {key} is missing, can't preload.");
                        continue;
                    }
                    if (queuedEntry.OriginalUpdatedOn == null)
                    {
                        queuedEntry.SetCachedPathAndOriginalUpdatedOn(manifestEntry);
                        queuedEntry.SetVersionManifestUpdatedOn(manifestEntry);
                    }

                    if (!cachedEntry.Equals(queuedEntry))
                    {
                        Log($"MergeCache: {key} outdated in cache.");
                        preloadResources.Add(key);
                    }
                }
                else
                {
                    Log($"MergeCache: {key} missing in cache.");
                    preloadResources.Add(key);
                }
            }

            // find entries that shouldn't be in cache (anymore)
            foreach (var kv in CachedMerges.ToList())
            {
                var key = kv.Key;
                var entry = kv.Value;
                if (entry.CacheHit)
                {
                    continue;
                }

                Log($"MergeCache: {key} left over in cache.");

                CachedMerges.Remove(key);
                try
                {
                    if (File.Exists(entry.CachedAbsolutePath))
                    {
                        File.Delete(entry.CachedAbsolutePath);
                    }
                }
                catch (Exception e)
                {
                    Log($"MergeCache: Error when deleting cached file for {kv.Key}", e);
                }

                // check for changes in MDDB
                if (BTConstants.MDDBTypes.Contains(key.Type))
                {
                    // original still exists, lets preload it, hopefully overwriting old changes
                    if (BetterBTRL.Instance.EntryByIDAndType(key.Id, key.Type) != null)
                    {
                        preloadResources.Add(key);
                    }
                    else
                    {
                        // original was also removed, since we can't (reliably) remove data from the mddb, we have to rebuild from scratch
                        flagForRebuild = true;
                        break;
                    }
                }
            }
        }
    }
}
