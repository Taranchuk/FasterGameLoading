using HarmonyLib;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(PlayDataLoader), "DoPlayLoad")]
    public static class PlayDataLoader_DoPlayLoad_Patch
    {
        public static void Prefix()
        {
            ReflectionCacheManager.PreloadTask?.Wait();
        }
    }
}
