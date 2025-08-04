using HarmonyLib;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(LoadedModManager), nameof(LoadedModManager.LoadAllActiveMods))]
    public static class LoadedModManager_LoadAllActiveMods_Patch
    {
        public static void Postfix()
        {
            ModContentLoaderTexture2D_LoadTexture_Patch.savedTextures.Clear();
            TexturePreloader.preloadedTextures.Clear();
        }
    }
}
