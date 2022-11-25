using HarmonyLib;
using RuntimeAudioClipLoader;
using System.Threading;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(DeepProfiler), "End")]
    public static class DeepProfiler_End_Patch
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

