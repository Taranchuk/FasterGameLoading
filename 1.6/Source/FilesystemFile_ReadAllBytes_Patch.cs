using HarmonyLib;
using RimWorld.IO;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(FilesystemFile), "ReadAllBytes")]
    public static class FilesystemFile_ReadAllBytes_Patch
    {
        public static bool Prefix(FilesystemFile __instance, ref byte[] __result)
        {
            if (ModContentPack_ReloadContentInt_Patch.textureBytesCache.TryGetValue(__instance.FullPath, out __result))
            {
                return false;
            }
            return true;
        }
    }
}
