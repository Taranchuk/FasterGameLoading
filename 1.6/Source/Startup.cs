using HarmonyLib;
using RimWorld;
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
            DeepProfiler.Start("FGL: Startup Postfix");
            FasterGameLoadingSettings.modsInLastSession = ModsConfig.ActiveModsInLoadOrder.Select(x => x.packageIdLowerCase).ToList();
            FasterGameLoadingSettings.loadedTexturesSinceLastSession = new Dictionary<string, string>(ModContentLoaderTexture2D_LoadTexture_Patch.loadedTexturesThisSession);
            FasterGameLoadingSettings.loadedTypesByFullNameSinceLastSession = new Dictionary<string, string>(GenTypes_GetTypeInAnyAssemblyInt_Patch.loadedTypesThisSession);
            FasterGameLoadingSettings.successfulXMLPathesSinceLastSession = new HashSet<string>(XmlNode_SelectSingleNode_Patch.successfulXMLPathesThisSession);
            FasterGameLoadingSettings.failedXMLPathesSinceLastSession = new HashSet<string>(XmlNode_SelectSingleNode_Patch.failedXMLPathesThisSession);
            FasterGameLoadingMod.settings.xmlHashes = new Dictionary<string, string>(XmlCacheManager.currentFileHashes);
            FasterGameLoadingMod.settings.gameVersion = VersionControl.CurrentVersionStringWithRev;
            LoadedModManager.GetMod<FasterGameLoadingMod>().WriteSettings();
            XmlCacheManager.Reset();
            LongEventHandler.toExecuteWhenFinished.Add(delegate
            {
                FasterGameLoadingMod.delayedActions.StartCoroutine(FasterGameLoadingMod.delayedActions.PerformActions());
            });
            DeepProfiler.End();
        }
    }
}

