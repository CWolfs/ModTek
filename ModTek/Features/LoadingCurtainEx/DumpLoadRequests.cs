﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using BattleTech.Data;
using Harmony;
using ModTek.Features.Logging;
using ModTek.Features.Manifest;
using ModTek.Features.Manifest.MDD;

namespace ModTek.Features.LoadingCurtainEx
{
    internal class DumpLoadRequests
    {

        internal static void DumpProcessing(DataManagerLoadingCurtain.LoadStats stats, List<LoadRequest> loadRequests)
        {
            MTLogger.Log($"Detected stuck DataManager, dumping stats: {stats}");
            var dumper = new DumpLoadRequests();
            dumper.Analyze(loadRequests);
            dumper.LogSummary();
        }

        private HashSet<CacheKey> waiting = new HashSet<CacheKey>();
        private void AddWaiting(CacheKey key)
        {
            waiting.Add(key);
        }

        private HashSet<CacheKey> processing = new HashSet<CacheKey>();
        private void AddProcessing(CacheKey key)
        {
            processing.Add(key);
        }

        private Dictionary<CacheKey, int> incoming = new Dictionary<CacheKey, int>();
        private void AddIncoming(CacheKey key)
        {
            if (!incoming.TryGetValue(key, out var counter))
            {
                counter = 0;
            }
            incoming[key] = ++counter;
        }

        private void LogSummary()
        {
            MTLogger.Log($"Which resource are blocking:");
            foreach (var kv in incoming.OrderByDescending(x => x.Value))
            {
                MTLogger.Log($"\t{kv.Key} {kv.Value}");
            }
        }

        private DumpLoadRequests()
        {
        }

        private void Analyze(List<LoadRequest> loadRequests)
        {
            foreach (var loadRequest in loadRequests)
            {
                DumpTrackers(loadRequest, "\t");
            }
        }

        private void DumpTrackers(LoadRequest loadRequest, string level)
        {
            DumpTrackers(loadRequest, level, "Pending", "pendingRequests");
            DumpTrackers(loadRequest, level, "LinkedPending", "linkedPendingRequests");
            DumpTrackers(loadRequest, level, "Processing", "processingRequests");
        }
        private void DumpTrackers(LoadRequest loadRequest, string level, string prefix, string field)
        {
            var trackers = Traverse.Create(loadRequest).Field(field).GetValue<ICollection>();
            if (trackers != null)
            {
                DumpTrackers(trackers, level, prefix);
            }
        }

        private void DumpTrackers(ICollection trackers, string level, string prefix)
        {
            foreach (var tracker in trackers)
            {
                var message = level + prefix;

                var resource = Traverse.Create(tracker).Field<VersionManifestEntry>("resourceManifestEntry").Value;
                if (resource != null)
                {
                    message += " entry=" + resource.ToShortString();
                    if (prefix == "LinkedPending")
                    {
                        AddIncoming(new CacheKey(resource));
                    }
                    else if (prefix == "Pending")
                    {
                        AddWaiting(new CacheKey(resource));
                    }
                    else if (prefix == "Processing")
                    {
                        AddProcessing(new CacheKey(resource));
                    }
                }

                var backing = Traverse.Create(tracker).Field<DataManager.FileLoadRequest>("backingRequest").Value;
                if (backing != null)
                {
                    message += " state=" + backing.State;
                }

                var newLevel = level + "\t";
                var dependency = Traverse.Create(tracker).Field<DataManager.DependencyLoadRequest>("dependencyLoader").Value;
                if (dependency != null)
                {
                    if (dependency.IsLoadComplete)
                    {
                        message += " dependenciesLoaded";
                    }
                }

                MTLogger.Log(message);

                if (dependency != null)
                {
                    var dependencyLoads = Traverse.Create(dependency).Field("loadRequests").GetValue<List<LoadRequest>>();
                    if (!dependency.IsLoadComplete && dependencyLoads != null)
                    {
                        foreach (var dependencyLoad in dependencyLoads)
                        {
                            DumpTrackers(dependencyLoad, newLevel);
                        }
                    }
                }
            }
        }
    }
}
