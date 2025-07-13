using HarmonyLib;
using System.Collections.Generic;
using System.Threading.Tasks;
using Verse;
using System.IO;
using UnityEngine;
using System.Collections.Concurrent;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(ModContentPack), "ReloadContentInt")]
    public static class ModContentPack_ReloadContentInt_Patch
    {
        public static ConcurrentDictionary<string, byte[]> textureBytesCache = new ConcurrentDictionary<string, byte[]>();
        public static ConcurrentDictionary<ModContentPack, byte> loadedMods = new ConcurrentDictionary<ModContentPack, byte>();
        
        public static bool Prefix(ModContentPack __instance)
        {
            if (loadedMods.ContainsKey(__instance)) return false;

            Parallel.ForEach(ModContentPack.GetAllFilesForMod(__instance,
                GenFilePaths.ContentPath<Texture2D>(), ModContentLoader<Texture2D>.IsAcceptableExtension), file =>
            {
                var fullPath = file.Value.FullName;
                textureBytesCache[fullPath] = File.ReadAllBytes(fullPath);
            });

            return true;
        }

        public static void Postfix(ModContentPack __instance)
        {
            loadedMods.TryAdd(__instance, 0);
        }
    }

}
