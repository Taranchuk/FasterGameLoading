using HarmonyLib;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(ThinkTreeDef), "ResolveReferences")]
    public static class ThinkTreeDef_ResolveReferences_Patch
    {
        public static void Prefix()
        {
            ThinkTreeKeyAssigner.Reset();
        }
    }
}
