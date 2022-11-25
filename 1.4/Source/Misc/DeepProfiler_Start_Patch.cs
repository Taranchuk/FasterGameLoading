using HarmonyLib;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(DeepProfiler), "Start")]
    public static class DeepProfiler_Start_Patch
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return Prefs.LogVerbose is false;
        }
        public static bool Prefix()
        {
            return false;
        }
    }
}

