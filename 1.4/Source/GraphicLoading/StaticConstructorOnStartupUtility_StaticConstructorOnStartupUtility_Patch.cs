using HarmonyLib;
using Verse;
using Verse.Profile;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(StaticConstructorOnStartupUtility), nameof(StaticConstructorOnStartupUtility.CallAll))]
    public static class StaticConstructorOnStartupUtility_StaticConstructorOnStartupUtility_Patch
    {
        public static bool Prepare() => FasterGameLoadingSettings.delayGraphicLoading;
        public static void Postfix()
        {
            LongEventHandler.toExecuteWhenFinished.Add(delegate
            {
                FasterGameLoadingMod.loadGraphicsPerFrames.StartCoroutine(FasterGameLoadingMod.loadGraphicsPerFrames.LoadGraphics());
            });
        }
    }
}
