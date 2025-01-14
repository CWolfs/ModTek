﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HBS;
using ModTek.Features.Manifest;
using ModTek.UI;
using Newtonsoft.Json;
using static ModTek.Features.Logging.MTLogger;

namespace ModTek.Features.SoundBanks
{
    internal static class SoundBanksFeature
    {
        internal static readonly Dictionary<string, SoundBankDef> soundBanks = new Dictionary<string, SoundBankDef>();

        internal static bool Add(ModEntry entry)
        {
            if (!entry.IsTypeSoundBankDef)
            {
                return false;
            }
            try
            {
                var path = entry.AbsolutePath;
                Log($"\tAdd SoundBankDef {path}");
                var def = JsonConvert.DeserializeObject<SoundBankDef>(File.ReadAllText(path));
                def.filename = Path.Combine(Path.GetDirectoryName(path), def.filename);
                if (soundBanks.ContainsKey(def.name))
                {
                    soundBanks[def.name] = def;
                    Log("\t\tReplace:" + def.name);
                }
                else
                {
                    soundBanks.Add(def.name, def);
                    Log("\t\tAdd:" + def.name);
                }
            }
            catch (Exception e)
            {
                Log("\tError while reading SoundBankDef:" + e);
            }
            return true;
        }

        internal static IEnumerator<ProgressReport> SoundBanksProcessing()
        {
            LogIf(soundBanks.Count > 0, $"Processing sound banks ({soundBanks.Count}):");
            if (SceneSingletonBehavior<WwiseManager>.HasInstance == false)
            {
                Log("\tWWise manager not inited");
                yield break;
            }

            yield return new ProgressReport(0, "Processing sound banks", "");
            if (soundBanks.Count == 0)
            {
                yield break;
            }

            var loadedBanks = (List<LoadedAudioBank>) typeof(WwiseManager).GetField("loadedBanks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(SceneSingletonBehavior<WwiseManager>.Instance);
            var progeress = 0;
            foreach (var soundBank in soundBanks)
            {
                ++progeress;
                yield return new ProgressReport(progeress / (float) soundBanks.Count, "Processing sound bank", soundBank.Key, true);
                Log($"\t{soundBank.Key}:{soundBank.Value.filename}:{soundBank.Value.type}");
                if (soundBank.Value.type != SoundBankType.Default)
                {
                    continue;
                }

                ;
                if (soundBank.Value.loaded)
                {
                    continue;
                }

                loadedBanks.Add(new LoadedAudioBank(soundBank.Key, true));
            }
        }
    }
}
