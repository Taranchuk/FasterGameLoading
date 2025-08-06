using HarmonyLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(AccessTools), "AllTypes")]
    public static class AccessTools_AllTypes_Patch
    {
        public static ConcurrentBag<Type> allTypesCached;
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
                allTypesCached = new ConcurrentBag<Type>(__result);
            }
        }

        public static void DoCache()
        {
            allTypesCached = new ConcurrentBag<Type>(AccessTools.AllTypes());
        }
    }
}
