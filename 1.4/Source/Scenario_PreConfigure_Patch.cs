using HarmonyLib;
using RimWorld;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(Scenario), "PreConfigure")]
    public static class Scenario_PreConfigure_Patch
    {
        [HarmonyPriority(int.MaxValue)]
        public static void Prefix()
        {
            Startup.doNotDelayLongEventsWhenFinished = true;
            LongEventHandler.ExecuteWhenFinished(MemoryUtility_ClearAllMapsAndWorld_Patch.PerformPatchesIfAny);
        }
    }
}

