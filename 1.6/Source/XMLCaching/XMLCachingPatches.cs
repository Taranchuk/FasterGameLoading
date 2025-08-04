using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(ModsConfig), nameof(ModsConfig.Reset))]
    public static class ModsConfig_Reset_ClearCachePatch
    {
        public static void Postfix()
        {
            var cacheDir = XmlCacheManager.CacheDirectory;
            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, true);
            }
        }
    }

    [HarmonyPatch(typeof(LoadedModManager), nameof(LoadedModManager.LoadModXML))]
    public static class LoadedModManager_LoadModXML_PrepareCachePatch
    {
        public static void Prefix()
        {
            DeepProfiler.Start("FGL: LoadModXML Prefix");
            if (!FasterGameLoadingSettings.xmlCaching)
            {
                XmlCacheManager.DeactivateCache();
                return;
            }

            var currentMods = ModsConfig.ActiveModsInLoadOrder.Select(m => m.packageIdLowerCase).ToList();
            var lastMods = FasterGameLoadingSettings.modsInLastSession;
            bool modsChanged = lastMods is null || !lastMods.SequenceEqual(currentMods);
            if (!modsChanged && File.Exists(XmlCacheManager.XMLCachePath))
            {
                XmlCacheManager.ActivateCache();
            }
            else
            {
                if (modsChanged && File.Exists(XmlCacheManager.XMLCachePath))
                {
                    Log.Warning("[FasterGameLoading] Mod list changed, invalidating cache.");
                }
                XmlCacheManager.InvalidateCache();
            }
            DeepProfiler.End();
        }
    }

    [HarmonyPatch(typeof(LoadedModManager), nameof(LoadedModManager.CombineIntoUnifiedXML))]
    public static class LoadedModManager_CombineIntoUnifiedXML_LoadFromCachePatch
    {
        public static bool Prefix(List<LoadableXmlAsset> xmls, ref XmlDocument __result, Dictionary<XmlNode, LoadableXmlAsset> assetlookup, out bool __state)
        {
            DeepProfiler.Start("FGL: CombineIntoUnifiedXML Prefix");
            __state = false;
            if (XmlCacheManager.CacheIsActive)
            {
                if (CheckHashes(xmls))
                {
                    if (XmlCacheManager.TryLoadXMLCache(out var cachedDoc))
                    {
                        Log.Warning("[FasterGameLoading] XML cache is valid, loading it and skipping CombineIntoUnifiedXML and ApplyPatches.");
                        __result = cachedDoc;
                        XmlCacheManager.HydrateLookupsFrom(__result, assetlookup);
                        XmlCacheManager.XMLCacheLoaded = true;
                        DeepProfiler.End();
                        return false;
                    }
                    else
                    {
                        Log.Warning("[FasterGameLoading] Failed to load XML cache.");
                        XmlCacheManager.InvalidateCache();
                    }
                }
                else
                {
                    Log.Warning("[FasterGameLoading] Hashes changed, invalidating cache.");
                    XmlCacheManager.InvalidateCache();
                }
            }

            __state = true;
            DeepProfiler.End();
            return true;
        }

        private static bool CheckHashes(List<LoadableXmlAsset> xmls)
        {
            var concurrentHashes = new ConcurrentDictionary<string, string>();
            var sw = Stopwatch.StartNew();
            Parallel.ForEach(xmls, asset =>
            {
                string key;
                string content;
                if (asset.FullFilePath != null)
                {
                    key = asset.FullFilePath;
                    content = File.ReadAllText(key);
                }
                else
                {
                    key = (asset.mod?.PackageIdPlayerFacing ?? "Core") + "://" + asset.name;
                    content = asset.xmlDoc.OuterXml;
                }
                concurrentHashes[key] = XmlCacheManager.GenerateSha256Hash(content);
            });
            XmlCacheManager.currentFileHashes = new Dictionary<string, string>(concurrentHashes);
            sw.Stop();
            var result = XmlCacheManager.currentFileHashes.Count == FasterGameLoadingMod.settings.xmlHashes.Count &&
                               !XmlCacheManager.currentFileHashes.Except(FasterGameLoadingMod.settings.xmlHashes).Any();
            Log.Warning($"[FasterGameLoading] Took {sw.ElapsedMilliseconds}ms to check {xmls.Count} hashes.");
            return result;
        }
    }


    [HarmonyPatch(typeof(LoadedModManager), nameof(LoadedModManager.ClearCachedPatches))]
    public static class LoadedModManager_ClearCachedPatches_Patch
    {
        public static bool Prefix()
        {
            if (XmlCacheManager.CacheIsActive)
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(LoadedModManager), "ApplyPatches")]
    public static class LoadedModManager_ApplyPatches_CachePatch
    {
        public static bool Prefix(XmlDocument xmlDoc, Dictionary<XmlNode, LoadableXmlAsset> assetlookup, out bool __state)
        {
            __state = true;
            if (XmlCacheManager.XMLCacheLoaded)
            {
                __state = false;
                return false;
            }
            return true;
        }

        public static void Postfix(XmlDocument xmlDoc, bool __state, Dictionary<XmlNode, LoadableXmlAsset> assetlookup)
        {
            DeepProfiler.Start("FGL: ApplyPatches Postfix");
            if (__state && FasterGameLoadingSettings.xmlCaching)
            {
                Log.Warning("[FasterGameLoading] Rebuilding XML cache.");
                var sw = Stopwatch.StartNew();
                XmlCacheManager.SaveXMLCache(xmlDoc, assetlookup);
                sw.Stop();
                Log.Warning($"[FasterGameLoading] Took {sw.ElapsedMilliseconds}ms to save patched cache.");
            }
            DeepProfiler.End();
        }
    }
}
