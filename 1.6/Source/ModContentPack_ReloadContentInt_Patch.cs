using HarmonyLib;
using System.Collections.Generic;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(ModContentPack), "ReloadContentInt")]
    public static class ModContentPack_ReloadContentInt_Patch
    {
        public static HashSet<ModContentPack> loadedMods = new HashSet<ModContentPack>();
        public static bool Prefix(ModContentPack __instance)
        {
            if (loadedMods.Contains(__instance)) return false;
            return true;
        }
        public static void Postfix(ModContentPack __instance)
        {
            loadedMods.Add(__instance);
        }
    }
}

