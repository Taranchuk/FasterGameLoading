using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch]
    public static class Startup
    {
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            if (ModsConfig.ActiveModsInLoadOrder.Any(x => x.Name == "BetterLoading"))
            {
                return AccessTools.Method("BetterLoading.BetterLoadingMain:CreateTimingReport");
            }
            return AccessTools.Method(typeof(StaticConstructorOnStartupUtility), "CallAll");
        }

        public static void Postfix()
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                var firstTimestampt = DateTime.Parse(Log.messageQueue.messages.First().timestamp);
                var timeSpent = DateTime.Now - firstTimestampt;
                Log.Warning("Mods installed: " + ModLister.AllInstalledMods.Where(x => x.Active).Count() + " - total startup time: " + timeSpent.ToString(@"m\:ss") + " - " + DateTime.Now.ToString());
            });
            FasterGameLoadingSettings.modsInLastSession = ModsConfig.ActiveModsInLoadOrder.Select(x => x.packageIdLowerCase).ToList();
            FasterGameLoadingSettings.loadedTexturesSinceLastSession = new Dictionary<string, string>(ModContentLoaderTexture2D_LoadTexture_Patch.loadedTexturesThisSession);
            FasterGameLoadingSettings.loadedTypesByFullNameSinceLastSession = new Dictionary<string, string>(GenTypes_GetTypeInAnyAssemblyInt_Patch.loadedTypesThisSession);
            FasterGameLoadingSettings.successfulXMLPathesSinceLastSession = new HashSet<string>(XmlNode_SelectSingleNode_Patch.successfulXMLPathesThisSession);
            FasterGameLoadingSettings.failedXMLPathesSinceLastSession = new HashSet<string>(XmlNode_SelectSingleNode_Patch.failedXMLPathesThisSession);
            FasterGameLoadingMod.settings.xmlHashes = new Dictionary<string, string>(XmlCacheManager.currentFileHashes);
            FasterGameLoadingMod.settings.gameVersion = VersionControl.CurrentVersionStringWithRev;
            LoadedModManager.GetMod<FasterGameLoadingMod>().WriteSettings();
            XmlCacheManager.Reset();
        }
    }
}

