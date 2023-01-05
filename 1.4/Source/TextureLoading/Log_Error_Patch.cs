using HarmonyLib;
using System;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(Log), nameof(Log.Error), new Type[] { typeof(string) })]
    public static class Log_Error_Patch
    {
        public static bool suppressErrorMessages;
        public static bool Prefix()
        {
            if (suppressErrorMessages)
            {
                return false;
            }
            return true;
        }
    }
}

