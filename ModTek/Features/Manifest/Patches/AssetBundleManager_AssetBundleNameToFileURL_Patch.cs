using System;
using BattleTech.Assetbundles;
using Harmony;
using static ModTek.Features.Logging.MTLogger;

namespace ModTek.Features.Manifest.Patches
{
    [HarmonyPatch(typeof(AssetBundleManager), "AssetBundleNameToFileURL")]
    internal static class AssetBundleManager_AssetBundleNameToFileURL_Patch
    {
        public static bool Prepare()
        {
            return ModTek.Enabled;
        }

        public static bool Prefix(string assetBundleName, ref string __result)
        {
            try
            {
                var filePath = AssetBundleManager_AssetBundleNameToFilepath_Patch.AssetBundleNameToFilepath(assetBundleName);
                __result = $"file://{filePath}";
                return false;
            }
            catch (Exception e)
            {
                Log("Error running prefix", e);
            }
            return true;
        }
    }
}
