using HarmonyLib;
using RimWorld;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(SolidBioDatabase), "LoadAllBios")]
    public static class DefOfHelper_RebindAllDefOfs_Patch
    {
        public static void Postfix()
        {
            DelayedActions.StartCoroutine();

        }
    }
}
