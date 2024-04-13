using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(GenTypes), "AllLeafSubclasses")]
    public static class GenTypes_AllLeafSubclasses_Patch
    {
        public static Dictionary<Type, HashSet<Type>> keyValuePairs = new Dictionary<Type, HashSet<Type>>();
        public static bool Prefix(ref IEnumerable<Type> __result, Type baseType)
        {
            if (!keyValuePairs.TryGetValue(baseType, out var final))
            {
                var subClasses = baseType.AllSubclasses().ToHashSet(); // o(n)
                final = new HashSet<Type>(subClasses);
                foreach (var sub in subClasses)
                {
                    if (!final.Contains(sub)) continue;
                    var type = sub.BaseType;
                    while (type != null)
                    {
                        if (final.Contains(type))
                        {
                            final.Remove(type);
                        }
                        else
                        {
                            break;
                        }
                        type = type.BaseType;
                    }
                }
                keyValuePairs[baseType] = final;
            }
            __result = final;
            return false;
        }
    }
}

