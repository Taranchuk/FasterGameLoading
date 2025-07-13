using HarmonyLib;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(GlobalTextureAtlasManager), "TryInsertStatic")]
    public static class GlobalTextureAtlasManager_TryInsertStatic_Patch
    {
        public static bool Prepare() => FasterGameLoadingSettings.disableStaticAtlasesBaking;
        public static bool Prefix()
        {
            return false;
        }
    }
}

