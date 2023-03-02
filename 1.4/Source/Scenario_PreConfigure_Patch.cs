using HarmonyLib;
using RimWorld;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(Scenario), "PreConfigure")]
    public static class Scenario_PreConfigure_Patch
    {
        [HarmonyPriority(int.MaxValue)]
        public static void Prefix()
        {
            MemoryUtility_ClearAllMapsAndWorld_Patch.PerformPatchesIfAny();
        }
    }
}

