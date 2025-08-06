using HarmonyLib;
using System;
using System.Collections.Generic;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(GenTypes), "GetTypeInAnyAssemblyRaw")]
    public static class GenTypes_GetTypeInAnyAssemblyRaw_Patch
    {
        public static bool Prefix(string typeName, ref Type __result)
        {
            if (GenTypes_GetTypeInAnyAssemblyInt_Patch.cachedResults.TryGetValue(typeName, out __result))
            {
                return false;
            }
            return true;
        }

        public static void Postfix(string typeName, Type __result)
        {
            GenTypes_GetTypeInAnyAssemblyInt_Patch.cachedResults[typeName] = __result;
        }
    }
}
