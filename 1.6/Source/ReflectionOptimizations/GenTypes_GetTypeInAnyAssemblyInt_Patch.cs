using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Threading;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(GenTypes), "GetTypeInAnyAssemblyInt")]
    public static class GenTypes_GetTypeInAnyAssemblyInt_Patch
    {
        public static bool Prefix(ref Type __result, out (string originalTypeName, bool wasCacheHit) __state, ref string typeName)
        {
            if (ReflectionCacheManager.TryGetFromCache(ref typeName, out __result, out __state))
            {
                return false;
            }
            return true;
        }

        public static void Postfix(Type __result, string typeName, (string originalTypeName, bool wasCacheHit) __state)
        {
            ReflectionCacheManager.RegisterFoundType(__result, __state.originalTypeName, typeName, __state.wasCacheHit);
        }
    }
}
