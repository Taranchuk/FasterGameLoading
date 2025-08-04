using HarmonyLib;
using RimWorld.IO;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(FilesystemFile), nameof(FilesystemFile.ReadAllBytes))]
    public static class FilesystemFile_ReadAllBytes_Patch
    {
        public static bool Prefix(FilesystemFile __instance, ref byte[] __result)
        {
            if (TexturePreloader.preloadedTextures.TryGetValue(__instance.FullPath, out var bytes))
            {
                __result = bytes;
                return false;
            }
            return true;
        }
    }
}
