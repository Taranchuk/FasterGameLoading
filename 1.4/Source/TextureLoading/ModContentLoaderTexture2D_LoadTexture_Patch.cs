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
        public static Texture2D stubTexture;
        public static bool Prefix(VirtualFile file, out bool __state, ref Texture2D __result)
        {
            if (savedTextures.TryGetValue(file.FullPath, out __result))
            {
                __state = false;
                return false;
            }
            __state = true;
            var fullPath = file.FullPath;
            var index = fullPath.IndexOf("Textures\\");
            if (index >= 0)
            {
                var path = fullPath.Substring(index);
                loadedTexturesThisSession[path] = file.FullPath;
                if (FasterGameLoadingSettings.loadedTexturesSinceLastSession.TryGetValue(path, out var otherPath) && fullPath != otherPath)
                {
                    if (stubTexture is null)
                    {
                        stubTexture = new Texture2D(2, 2);
                    }
                    __result = stubTexture;
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
