using HarmonyLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(GenTypes), "GetTypeInAnyAssemblyInt")]
    public static class GenTypes_GetTypeInAnyAssemblyInt_Patch
    {
        public static ConcurrentDictionary<string, Type> cachedResults = new ();
        public static ConcurrentDictionary<string, string> loadedTypesThisSession = new();
        public static bool Prefix(ref Type __result, out (string, bool) __state, ref string typeName)
        {
            if (cachedResults.TryGetValue(typeName, out var result))
            {
                __result = result;
                __state = new (typeName, true);
                return false;
            }
            else
            {
                __state = new(typeName, false);
                if (FasterGameLoadingSettings.loadedTypesByFullNameSinceLastSession.TryGetValue(typeName, out var fullName))
                {
                    typeName = fullName;
                }
                return true;
            }
        }

        public static void Postfix(Type __result, (string, bool) __state)
        {
            if (__result != null)
            {
                var fullName = __result.FullName;
                if (__state.Item2 is false)
                {
                    cachedResults[__state.Item1] = __result;
                    if (fullName != __state.Item1)
                    {
                        cachedResults[fullName] = __result;
                    }
                }
                if (__state.Item1 != fullName)
                {
                    loadedTypesThisSession[__state.Item1] = fullName;
                }
            }
        }
    }
}
