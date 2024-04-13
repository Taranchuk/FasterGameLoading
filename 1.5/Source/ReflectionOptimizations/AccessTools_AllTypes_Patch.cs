using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(AccessTools), "AllTypes")]
    public static class AccessTools_AllTypes_Patch
    {
        public static List<Type> allTypesCached;
        public static bool Prefix(ref IEnumerable<Type> __result)
        {
            if (allTypesCached is null)
            {
                return true;
            }
            else
            {
                __result = allTypesCached;
                return false;
            }
        }

        public static void Postfix(IEnumerable<Type> __result)
        {
            if (allTypesCached is null)
            {
                allTypesCached = __result.ToList();
            }
        }
    }
}