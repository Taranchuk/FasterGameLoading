using HarmonyLib;
using System;
using System.Reflection;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch]
    public static class RedirectHugslibToMainThread
    {
        public static MethodBase targetMethod = AccessTools.Method("HugsLib.HugsLibController:OnDefsLoaded");
        public static bool Prepare() => (FasterGameLoadingSettings.delayGraphicLoading || FasterGameLoadingSettings.delayLongEventActionsLoading)
            && targetMethod != null;
        public static MethodBase TargetMethod() => targetMethod;

        [HarmonyReversePatch]
        public static void OnDefsLoaded(object __instance) => throw new NotImplementedException("It's a stub");
        public static bool Prefix(object __instance)
        {
            LongEventHandler.ExecuteWhenFinished(delegate
            {
                OnDefsLoaded(__instance);
            });
            return false;
        }
    }
}

