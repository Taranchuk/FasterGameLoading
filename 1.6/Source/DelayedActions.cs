using HarmonyLib;
using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace FasterGameLoading
{
    public class DelayedActions : MonoBehaviour
    {
        public float MaxImpactThisFrame => Current.Game != null ? 0.001f : 0.03f;
        public List<(ThingDef def, Action action)> graphicsToLoad = new();
        public List<(BuildableDef def, Action action)> iconsToLoad = new();

        public static bool AllGraphicLoaded = false;
        private Stopwatch stopwatch = new();
        private bool ElapsedMaxImpact => (float)stopwatch.ElapsedTicks / Stopwatch.Frequency >= MaxImpactThisFrame;
        public void LateUpdate()
        {
            if (FasterGameLoadingSettings.earlyModContentLoading)
            {
                var modToLoad = LoadedModManager.RunningMods.Where(x =>
                    ModContentPack_ReloadContentInt_Patch.loadedMods.Contains(x) is false).FirstOrDefault();
                if (modToLoad != null)
                {
                    modToLoad.ReloadContentInt();
                }
            }
        }

        public IEnumerator PerformActions()
        {
            stopwatch.Start();
            var count = 0;
            Log.Warning("Starting loading graphics: " + graphicsToLoad.Count + " - " + DateTime.Now.ToString());
            List<ThingDef> loadedDefs = [];
            while (graphicsToLoad.Any())
            {
                if (UnityData.IsInMainThread is false)
                {
                    yield return 0;
                }
                var (def, action) = graphicsToLoad.PopFirst();
                try
                {
                    action();
                    loadedDefs.Add(def);
                }
                catch (Exception ex)
                {
                    Error("Error loading graphic for " + def, ex);
                }
                count++;
                if (ElapsedMaxImpact)
                {
                    count = 0;
                    yield return 0;
                    stopwatch.Restart();
                }

                if (def.plant != null)
                {
                    def.plant.PostLoadSpecial(def);
                }
            }
            Log.Warning("Finished loading graphics - " + DateTime.Now.ToString());
            if (!FasterGameLoadingSettings.disableStaticAtlasesBaking)
            {
                AllGraphicLoaded = true;//Icon Doesn't matter
                Log.Warning("Starting baking StaticAtlases - " + DateTime.Now.ToString());
                #region BakeAtlas
                int pixels = 0;
                List<(Texture2D main, Texture2D mask)> currentBatch = [];
                Queue<(TextureAtlasGroupKey key, List<(Texture2D main, Texture2D mask)> batch)> atlases = [];

                //20 is my personal experience, 0.8*(maxTextureSize/4)^2 to make textures fit better in atlas
                int MaxPixelsPerAtlas = SystemInfo.maxTextureSize * SystemInfo.maxTextureSize / 20;
                //Seperating each bake to different update to avoid freezing, but may cause screen tearing
                stopwatch.Restart();
                foreach (var kvp in GlobalTextureAtlasManager.buildQueue)
                {
                    bool hasMask = kvp.Key.hasMask;
                    foreach (Texture2D texture in kvp.Value.Item1)
                    {
                        int size = texture.width * texture.height;
                        if (size + pixels > MaxPixelsPerAtlas)
                        {
                            atlases.Enqueue((kvp.Key, currentBatch));
                            pixels = 0;
                            currentBatch = [];
                        }
                        pixels += size;
                        Texture2D mask = hasMask
                            && GlobalTextureAtlasManager.buildQueueMasks
                            .TryGetValue(texture, out var m) ? m : null;

                        currentBatch.Add((texture, mask));
                    }
                    atlases.Enqueue((kvp.Key, currentBatch));
                    pixels = 0;
                    currentBatch = [];
                }

                while (atlases.Any())
                {
                    if (!UnityData.IsInMainThread)
                    {
                        yield return 0;
                    }
                    var (key, batch) = atlases.Dequeue();
                    StaticTextureAtlas staticTextureAtlas = new(key);
                    foreach (var (main, mask) in batch)
                    {
                        staticTextureAtlas.textures.Add(main);
                        if (mask != null)
                        {
                            staticTextureAtlas.masks.Add(main, mask);
                        }
                    }
                    staticTextureAtlas.Bake();
                    GlobalTextureAtlasManager.staticTextureAtlases.Add(staticTextureAtlas);
                    if (ElapsedMaxImpact)
                    {
                        yield return 0;
                        stopwatch.Restart();
                    }
                    
                }
                #endregion
                Log.Warning("Finished baking StaticAtlases - " + DateTime.Now.ToString());
            }
            //then update mesh
            if (Current.Game != null)
            {
                foreach (var map in Find.Maps)
                {
                    if (map.mapDrawer.sections != null)
                    {
                        foreach (var thing in map.listerThings.ThingsOfDefs(loadedDefs))
                        {
                            map.mapDrawer.MapMeshDirty(thing.Position, MapMeshFlagDefOf.Things | MapMeshFlagDefOf.Buildings);
                        }
                    }
                }
            }

            count = 0; 

            Log.Warning("Starting loading icons: " + iconsToLoad.Count + " - " + DateTime.Now.ToString());
            while (iconsToLoad.Any())
            {
                if (UnityData.IsInMainThread is false)
                {
                    yield return 0;
                }
                var (def, action) = iconsToLoad.PopFirst();
                if (def.uiIcon == BaseContent.BadTex)
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        Error("Error loading icon for " + def, ex);
                    }
                    count++;
                   
                    if (ElapsedMaxImpact)
                    {
                        count = 0;
                        yield return 0;
                        stopwatch.Restart();
                    }
                }
            }
            stopwatch.Stop();
            Log.Warning("Finished loading icons - " + DateTime.Now.ToString());
            this.enabled = false;
            yield return null;
        }

        public void Error(string message, Exception ex)
        {
            Log.Error(message + " - " + ex + " - " + new StackTrace());
        }
    }
}

