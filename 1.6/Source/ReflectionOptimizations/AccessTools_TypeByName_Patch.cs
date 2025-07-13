using HarmonyLib;
using System;
using System.Collections.Generic;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(AccessTools), "TypeByName")]
    public static class AccessTools_TypeByName_Patch
    {
        public static bool Prefix(ref Type __result, out (bool, string) __state, ref string name)
        {
            var oldName = name;
            if (FasterGameLoadingSettings.loadedTypesByFullNameSinceLastSession.TryGetValue(name, out var fullName))
            {
                name = fullName;
            }
            if (GenTypes_GetTypeInAnyAssemblyInt_Patch.cachedResults.TryGetValue(name, out var result))
            {
                __result = result;
                __state = new (true, oldName);
                return false;
            }
            else
            {
                __state = new(false, oldName);
                return true;
            }
        }

        public static void Postfix(Type __result, string name, (bool, string) __state)
        {
            if (__state.Item1 is false)
            {
                GenTypes_GetTypeInAnyAssemblyInt_Patch.cachedResults[name] = __result;
                if (__result != null && __result.FullName != name)
                {
                    FasterGameLoadingSettings.loadedTypesByFullNameSinceLastSession[__state.Item2] = __result.FullName;
                    GenTypes_GetTypeInAnyAssemblyInt_Patch.cachedResults[__result.FullName] = __result;
                }
            }
        }
    }
}