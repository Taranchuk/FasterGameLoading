using HarmonyLib;
using RimWorld.IO;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(ModContentLoader<Texture2D>), "LoadTexture")]
    public static class ModContentLoaderTexture2D_LoadTexture_Patch
    {
        public static Dictionary<string, string> loadedTexturesThisSession = new Dictionary<string, string>();
        public static Dictionary<string, Texture2D> savedTextures = new Dictionary<string, Texture2D>();
        public static bool Prefix(VirtualFile file, out bool __state, ref Texture2D __result)
        {
            var fullPath = file.FullPath;
            if (savedTextures.TryGetValue(fullPath, out __result))
            {
                __state = false;
                return false;
            }

            __state = true;
            var index = fullPath.IndexOf("Textures\\");
            if (index >= 0)
            {
                var path = fullPath.Substring(index);
                loadedTexturesThisSession[path] = fullPath;
                if (FasterGameLoadingSettings.loadedTexturesSinceLastSession.TryGetValue(path, out var otherPath) && fullPath != otherPath)
                {
                    var texture = new Texture2D(2, 2);
                    texture.name = Path.GetFileNameWithoutExtension(file.Name);
                    __result = texture;
                    //foreach (var mod in ModLister.AllInstalledMods)
                    //{
                    //    if (fullPath.Contains(mod.RootDir.FullName))
                    //    {
                    //        foreach (var otherMod in ModLister.AllInstalledMods)
                    //        {
                    //            if (otherPath.Contains(otherMod.RootDir.FullName))
                    //            {
                    //                Utils.Log(mod.Name + " - Preventing loading " + texture.name + ", already replaced with " + otherMod.Name);
                    //            }
                    //        }
                    //    }
                    //}
                    return false;
                }
            }
            return true;
        }

        public static void Postfix(VirtualFile file, bool __state, Texture2D __result)
        {
            if (__state)
            {
                savedTextures[file.FullPath] = __result;
            }
        }
    }
}
