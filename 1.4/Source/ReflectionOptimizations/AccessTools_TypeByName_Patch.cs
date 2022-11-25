using HarmonyLib;
using System;
using System.Collections.Generic;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(AccessTools), "TypeByName")]
    public static class AccessTools_TypeByName_Patch
    {
        public static Dictionary<string, Type> cachedResults = new Dictionary<string, Type>();
        public static bool Prefix(ref Type __result, out bool __state, string name)
        {
            if (cachedResults.TryGetValue(name, out var result))
            {
                __result = result;
                __state = true;
                return false;
            }
            else
            {
                __state = false;
                return true;
            }
        }

        public static void Postfix(Type __result, string name, bool __state)
        {
            if (__state is false)
            {
                cachedResults[name] = __result;
            }
        }
    }
}