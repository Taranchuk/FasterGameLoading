using HarmonyLib;
using RimWorld;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(StaticConstructorOnStartupUtility), "ReportProbablyMissingAttributes")]
    public static class StaticConstructorOnStartupUtility_ReportProbablyMissingAttributes_Patch
    {
        public static bool Prefix()
        {
            return false;
        }
    }
}

