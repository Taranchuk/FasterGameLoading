using HarmonyLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(ParseHelper), nameof(ParseHelper.FromString), new Type[] { typeof(string), typeof(Type) })]
    public static class ParseHelper_FromString_CachePatch
    {
        private static ConcurrentDictionary<Tuple<string, Type>, object> _cache =
            new ConcurrentDictionary<Tuple<string, Type>, object>();

        public static bool Prefix(string str, Type itemType, ref object __result, out Tuple<string, Type> __state)
        {
            __state = Tuple.Create(str, itemType);
            if (_cache.TryGetValue(__state, out var cachedResult))
            {
                __result = cachedResult;
                return false;
            }
            return true;
        }

        public static void Postfix(object __result, Tuple<string, Type> __state)
        {
            if (__result != null)
            {
                _cache.TryAdd(__state, __result);
            }
        }
    }
}
