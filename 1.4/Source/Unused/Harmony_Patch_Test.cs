using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FasterGameLoading
{
    [HarmonyPatch]
    public static class Harmony_Patch_Test
    {
        public static HashSet<MethodBase> registeredMethods = new HashSet<MethodBase>();
    
        public static bool preventRecursion;
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PatchInfo), nameof(PatchInfo.AddPrefixes));
            yield return AccessTools.Method(typeof(PatchInfo), nameof(PatchInfo.AddPostfixes));
        }
        public static void Postfix(string owner, params HarmonyMethod[] methods)
        {
            if (preventRecursion || methods is null)
            {
                return;
            }
            foreach (var f in methods.Where(x => x != null).Select(x => x.method))
            {
                if (f != null && registeredMethods.Contains(f) is false && f.DeclaringType.Assembly != typeof(Harmony_Patch_Test).Assembly)
                {
                    preventRecursion = true;
                    registeredMethods.Add(f);
                    FasterGameLoadingMod.TryProfileMethod(f);
                    preventRecursion = false;
                }
            }
        }
    }
}

