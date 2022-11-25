using HarmonyLib;
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
            return ModsConfig.ActiveModsInLoadOrder.Any(x => x.Name == "BetterLoading")
                ? AccessTools.Method("BetterLoading.BetterLoadingMain:CreateTimingReport")
                : (MethodBase)AccessTools.Method(typeof(StaticConstructorOnStartupUtility), "CallAll");
        }
        public static void Postfix()
        {
            FasterGameLoadingSettings.modsInLastSession = ModsConfig.ActiveModsInLoadOrder.Select(x => x.packageIdLowerCase).ToList();
            FasterGameLoadingSettings.loadedTexturesSinceLastSession = ModContentLoaderTexture2D_LoadTexture_Patch.loadedTexturesThisSession;
            FasterGameLoadingSettings.loadedTypesByFullNameSinceLastSession = GenTypes_GetTypeInAnyAssemblyInt_Patch.loadedTypesThisSession;
            LoadedModManager.GetMod<FasterGameLoadingMod>().WriteSettings();
            if (FasterGameLoadingMod.stopwatches.Any())
            {
                Log.Message("Logging stopwatches: " + FasterGameLoadingMod.stopwatches.Count);
                foreach (var stopwatch in FasterGameLoadingMod.stopwatches.OrderByDescending(x => x.Value.totalTime))
                {
                    stopwatch.Value.LogTime();
                }
            }
        }
    }
}

