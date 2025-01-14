﻿using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using BattleTech.Data;
using BattleTech.UI;
using Harmony;
using HBS;
using ModTek.Features.Manifest.BTRL;
using ModTek.Features.Manifest.MDD;
using ModTek.Features.Manifest.Patches;
using UnityEngine.Video;
using static ModTek.Features.Logging.MTLogger;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace ModTek.Features.Manifest
{
    internal class ModsManifestPreloader
    {
        internal static int finishedChecksAndPreloadsCounter;
        private static readonly Stopwatch preloadSW = new Stopwatch();
        internal static bool isPreloading => preloader != null;
        private static ModsManifestPreloader preloader;

        internal static void PreloadResources(bool rebuildMDDB, HashSet<CacheKey> preloadResources)
        {
            if (preloader != null)
            {
                Log("ERROR: Can't start preloading, preload load request exists already");
                return;
            }

            Log("Prewarming and/or preloading resources.");

            preloadSW.Start();
            preloader = new ModsManifestPreloader(rebuildMDDB, preloadResources);
            preloader.StartWaiting();
        }

        private static void FinalizePreloadResources()
        {
            finishedChecksAndPreloadsCounter++;
            preloader = null;
            preloadSW.Stop();
            LogIfSlow(preloadSW, "Preloading");
        }

        private readonly DataManager dataManager = UnityGameInstance.BattleTechGame.DataManager;

        private readonly bool rebuildMDDB;
        private readonly HashSet<CacheKey> preloadResources;

        private ModsManifestPreloader(bool rebuildMDDB, HashSet<CacheKey> preloadResources)
        {
            this.rebuildMDDB = rebuildMDDB;
            this.preloadResources = preloadResources;
        }

        private void StartWaiting()
        {
            LoadingCurtain.ExecuteWhenVisible(() =>
            {
                if (dataManager.IsLoading)
                {
                    UnityGameInstance.BattleTechGame.MessageCenter.AddFiniteSubscriber(
                        MessageCenterMessageType.DataManagerLoadCompleteMessage,
                        _ =>
                        {
                            StartLoading();
                            return true;
                        }
                    );
                }
                else
                {
                    StartLoading();
                }
            });
            ShowLoadingCurtainForMainMenuPreloading();
        }

        private void StartLoading()
        {
            try
            {
                AddPrewarmRequestsToQueue();
                AddPreloadResourcesToQueue();

                {
                    var customResourcesQueue = loadingResourcesQueue
                        .Where(e => BTConstants.ICResourceType(e.Type, out _))
                        .ToList();
                    ModsManifest.IndexCustomResources(customResourcesQueue);
                }

                {
                    var loadRequest = dataManager.CreateLoadRequest(PreloadFinished);
                    foreach (var entry in loadingResourcesQueue)
                    {
                        if (BTConstants.BTResourceType(entry.Type, out var resourceType))
                        {
                            loadRequest.AddBlindLoadRequest(resourceType, entry.Id, true);
                        }
                    }
                    loadRequest.ProcessRequests();
                }
            }
            catch (Exception e)
            {
                Log("ERROR: Couldn't start loading via preload", e);
            }
        }

        private void AddPrewarmRequestsToQueue()
        {
            if (!ModTek.Config.DelayPrewarmUntilPreload)
            {
                Log("Prewarming during preload disabled.");
                return;
            }

            var prewarmRequests = DataManager_ProcessPrewarmRequests_Patch.GetAndClearPrewarmRequests();
            if (prewarmRequests.Count == 0)
            {
                Log("Skipping prewarm during preload.");
                return;
            }

            Log("Prewarming resources during preload.");
            foreach (var prewarm in prewarmRequests)
            {
                if (prewarm.PrewarmAllOfType)
                {
                    Log($"\tPrewarming resources of type {prewarm.ResourceType}.");
                    foreach (var entry in BetterBTRL.Instance.AllEntriesOfResource(prewarm.ResourceType, true))
                    {
                        QueueLoadingResource(entry);
                    }
                }
                else
                {
                    var entry = BetterBTRL.Instance.EntryByID(prewarm.ResourceID, prewarm.ResourceType, true);
                    if (entry != null)
                    {
                        Log($"\tPrewarming resource {entry.ToShortString()}.");
                        QueueLoadingResource(entry);
                    }
                }
            }
        }

        private void AddPreloadResourcesToQueue()
        {
            if (!ModTek.Config.PreloadResourcesForCache)
            {
                Log("Skipping preload, disabled in config.");
                return;
            }

            if (!rebuildMDDB && preloadResources.Count == 0)
            {
                Log("Skipping preload, no changes detected.");
                return;
            }

            Log("Preloading resources.");
            if (rebuildMDDB)
            {
                foreach (var type in BTConstants.VanillaMDDBTypes)
                {
                    foreach (var entry in BetterBTRL.Instance.AllEntriesOfResource(type, true))
                    {
                        QueueLoadingResource(entry);
                    }
                }
            }
            foreach (var resource in preloadResources)
            {
                var entry = BetterBTRL.Instance.EntryByIDAndType(resource.Id, resource.Type, true);
                if (entry != null)
                {
                    QueueLoadingResource(entry);
                }
            }
        }

        private readonly HashSet<CacheKey> loadingResourcesIndex = new HashSet<CacheKey>();
        private readonly List<VersionManifestEntry> loadingResourcesQueue = new List<VersionManifestEntry>();
        private void QueueLoadingResource(VersionManifestEntry entry)
        {
            if (entry.IsTemplate)
            {
                return;
            }

            var key = new CacheKey(entry);
            if (loadingResourcesIndex.Add(key))
            {
                loadingResourcesQueue.Add(entry);
            }
        }

        private void PreloadFinished(LoadRequest loadRequest)
        {
            try
            {
                Log("Preloader finished");
                if (ModTek.Config.DelayPrewarmUntilPreload)
                {
                    try
                    {
                        Traverse.Create(dataManager).Method("PrewarmComplete", loadRequest).GetValue();
                    }
                    catch (Exception e)
                    {
                        Log("ERROR execute PrewarmComplete", e);
                    }
                }

                ModsManifest.SaveCaches();
                FinalizePreloadResources();
            }
            catch (Exception e)
            {
                Log("ERROR can't fully finish preload", e);
            }
        }

        private static void ShowLoadingCurtainForMainMenuPreloading()
        {
            Log("Showing LoadingCurtain for Preloading.");
            LoadingCurtain.ShowPopupUntil(
                PopupClosureConditionalCheck,
                "Indexing modded data, might take a while.\nGame can temporarily freeze."
            );
        }

        private static bool PopupClosureConditionalCheck()
        {
            try
            {
                var condition = !isPreloading && !UnityGameInstance.BattleTechGame.DataManager.IsLoading;

                var videoPlayer = GetMainMenuBGVideoPlayer();
                if (videoPlayer == null)
                {
                    return condition;
                }

                if (condition)
                {
                    if (videoPlayer.isPaused)
                    {
                        Log("Resuming MainMenu background video.");
                        videoPlayer.Play();
                    }
                    return true;
                }
                else
                {
                    if (videoPlayer.isPlaying)
                    {
                        Log("Pausing MainMenu background video.");
                        videoPlayer.Pause();
                    }
                    return false;
                }
            }
            catch (Exception e)
            {
                Log("Can't properly check if popup can be closed", e);
            }
            return false;
        }

        private static VideoPlayer GetMainMenuBGVideoPlayer()
        {
            var mainMenu = LazySingletonBehavior<UIManager>.Instance.GetFirstModule<MainMenu>();
            if (mainMenu == null)
            {
                return null;
            }
            return Traverse.Create(mainMenu).Field("bgVideoPlayer").GetValue<VideoPlayer>();
        }
    }
}
