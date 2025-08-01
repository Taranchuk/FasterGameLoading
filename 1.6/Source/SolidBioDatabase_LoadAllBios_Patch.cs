using HarmonyLib;
using RimWorld;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(SolidBioDatabase), "LoadAllBios")]
    public static class SolidBioDatabase_LoadAllBios_Patch
    {
        public static bool canLoadThingGraphics;
        public static void Postfix()
        {
            canLoadThingGraphics = true;
        }
    }
}
