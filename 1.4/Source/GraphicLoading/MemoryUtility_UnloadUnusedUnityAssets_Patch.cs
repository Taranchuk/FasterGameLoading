using HarmonyLib;
using Verse;
using Verse.Profile;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(MemoryUtility), nameof(MemoryUtility.UnloadUnusedUnityAssets))]
    public static class MemoryUtility_UnloadUnusedUnityAssets_Patch
    {
        public static void Postfix()
        {
            LongEventHandler.toExecuteWhenFinished.Add(delegate
            {
                FasterGameLoadingMod.loadGraphicsPerFrames.StartCoroutine(FasterGameLoadingMod.loadGraphicsPerFrames.LoadGraphics());
            });
        }
    }
}
