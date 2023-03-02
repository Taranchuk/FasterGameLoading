using HarmonyLib;
using Verse;
using Verse.Sound;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(SubSoundDef), nameof(SubSoundDef.TryPlay) )]
    public static class SubSoundDef_TryPlay_Patch
    {
        public static bool Prepare() => FasterGameLoadingSettings.delayLongEventActionsLoading;
        public static void Prefix(SubSoundDef __instance)
        {
            if (__instance.resolvedGrains.Count == 0)
            {
                for (var i = FasterGameLoadingMod.delayedActions.actionsToPerform.Count - 1; i >= 0; i--)
                {
                    var action = FasterGameLoadingMod.delayedActions.actionsToPerform[i];
                    if (action.Target == __instance)
                    {
                        Log.Message("Found action target: " + action.Method.FullDescription());
                        action();
                        FasterGameLoadingMod.delayedActions.actionsToPerform.RemoveAt(i);
                    }
                }
            }
        }
    }
}

