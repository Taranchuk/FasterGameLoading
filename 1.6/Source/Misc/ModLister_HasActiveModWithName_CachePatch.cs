using HarmonyLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(ModLister), nameof(ModLister.HasActiveModWithName), new Type[] { typeof(string) })]
    public static class ModLister_HasActiveModWithName_CachePatch
    {
        private static ConcurrentDictionary<string, bool> _cache =
            new ConcurrentDictionary<string, bool>();

        public static bool Prefix(string name, out bool __result)
        {
            if (_cache.TryGetValue(name, out var cachedResult))
            {
                __result = cachedResult;
                return false;
            }

            __result = default(bool);
            return true;
        }

        public static void Postfix(string name, bool __result)
        {
            _cache.TryAdd(name, __result);
        }

    }
}
