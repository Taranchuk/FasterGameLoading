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
            FasterGameLoadingSettings.successfulXMLPathesSinceLastSession = XmlNode_SelectSingleNode_Patch.successfulXMLPathesThisSession;
            FasterGameLoadingSettings.failedXMLPathesSinceLastSession = XmlNode_SelectSingleNode_Patch.failedXMLPathesThisSession;
            LoadedModManager.GetMod<FasterGameLoadingMod>().WriteSettings();
        }
    }
}

