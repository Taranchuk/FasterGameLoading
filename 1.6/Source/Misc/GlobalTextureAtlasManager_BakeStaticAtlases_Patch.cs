using HarmonyLib;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(GlobalTextureAtlasManager), "BakeStaticAtlases")]
    public static class GlobalTextureAtlasManager_BakeStaticAtlases_Patch
    {
        public static bool Prefix()
        {
            return !FasterGameLoadingSettings.disableStaticAtlasesBaking
            && DelayedActions.AllGraphicLoaded;
        }
    }
}

