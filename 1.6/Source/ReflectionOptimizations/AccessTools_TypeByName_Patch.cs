using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Threading;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(AccessTools), "TypeByName")]
    public static class AccessTools_TypeByName_Patch
    {
        public static bool ignore;
        public static bool Prefix(ref Type __result, out (string originalTypeName, bool wasCacheHit) __state, ref string name)
        {
            if (ignore)
            {
                __state = default;
                return true;
            }
            if (ReflectionCacheManager.TryGetFromCache(ref name, out __result, out __state))
            {
                return false;
            }
            return true;
        }

        public static void Postfix(Type __result, string name, (string originalTypeName, bool wasCacheHit) __state)
        {
            if (ignore) return;
            
            ReflectionCacheManager.RegisterFoundType(__result, __state.originalTypeName, name, __state.wasCacheHit);
        }
    }
}
