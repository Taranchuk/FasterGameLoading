using HarmonyLib;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(XmlInheritance), nameof(XmlInheritance.Resolve))]
    public static class XmlInheritance_Resolve_CachePatch
    {
        public static bool Prefix()
        {
            if (FasterGameLoadingSettings.xmlInheritanceCaching && XmlCacheManager.CacheIsActive && XmlCacheManager.TryApplyInheritanceCache())
            {
                return false;
            }

            return true;
        }

        public static void Postfix()
        {
            if (FasterGameLoadingSettings.xmlInheritanceCaching && FasterGameLoadingSettings.xmlCaching && !XmlCacheManager.CacheIsActive)
            {
                XmlCacheManager.BuildAndSaveInheritanceCache();
            }
        }
    }
}
