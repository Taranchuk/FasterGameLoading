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
using Verse.Sound;

namespace FasterGameLoading
{
    public class DelayedActions : MonoBehaviour
    {
        public float MaxImpactThisFrame => Current.Game != null ? 0.001f : 0.03f;
        public List<(ThingDef def, Action action)> graphicsToLoad = new();
        public List<(BuildableDef def, Action action)> iconsToLoad = new();
        public Queue<(SubSoundDef def, Action action)> subSoundDefToResolve = new();

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
                    ModContentPack_ReloadContentInt_Patch.loadedMods.Add(modToLoad);
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
                #region BakeAtlas (Adaptive)
                const float TARGET_BAKE_TIME_SECONDS = 0.008f;
                const float ADAPTATION_FACTOR = 0.2f;
                // Player won't notice this initial lag (Hopefully) 
                const int INITIAL_PIXELS_PER_SLICE = 2048 * 2048;
                // I dont think atlas smaller than 1024x makes any sense for render optimization
                // the original 64x will logs out every texture divided
                // use ShowMoreActions/DumpStaticAtlases while in game map to see dumped atlases
                // dont know why we cant use this action in main menu
                const int MIN_PIXELS_PER_SLICE = 1024 * 1024;
                // For those who have good gpus
                const int MAX_PIXELS_PER_SLICE = 4096 * 4096;
                // Personal Experience 0.7-0.9 can reduce empty spaces in a texture atlas
                const float PACK_DENSITY = 0.8f;

                float measuredBakeSpeed_PixelsPerSecond = 2_000_000f; 
                int adaptivePixelsPerSlice = INITIAL_PIXELS_PER_SLICE;

                var bakeStopwatch = new Stopwatch();

                foreach (var kvp in GlobalTextureAtlasManager.buildQueue)
                {
                    var key = kvp.Key;
                    var allTexturesForThisGroup = kvp.Value.Item1;

                    int pixelsInCurrentSlice = 0;
                    var batchForNextBake = new List<(Texture2D main, Texture2D mask)>();

                    foreach (Texture2D texture in allTexturesForThisGroup)
                    {
                        Texture2D mask = key.hasMask && GlobalTextureAtlasManager.buildQueueMasks.TryGetValue(texture, out var m) ? m : null;
                        batchForNextBake.Add((texture, mask)); 
                        pixelsInCurrentSlice += texture.width * texture.height;
                        if (pixelsInCurrentSlice >= adaptivePixelsPerSlice)
                        {
                            FlushBatch();
                            yield return null;
                            batchForNextBake.Clear();
                            pixelsInCurrentSlice = 0;
                        }
                    }
                    if (batchForNextBake.Any())
                    {
                        FlushBatch();

                        yield return null;
                    }
                    void FlushBatch()
                    {
                        var staticTextureAtlas = new StaticTextureAtlas(key);
                        // Bake doesn't work when have only 1 texture, it just stopped here
                        // Make it use the original texture
                        // not sure how to remove texture from using static atlas
                        // and yes, because of it, using the initial 64x setup actually never bake the atlases
                        // No baking, No tearing
                        if (batchForNextBake.Count == 1)
                        {
                            staticTextureAtlas.colorTexture = batchForNextBake.First().main;
                            if (key.hasMask)
                            {
                                staticTextureAtlas.maskTexture = batchForNextBake.First().mask;
                            }
                            staticTextureAtlas.BuildMeshesForUvs([new(0, 0, 1, 1)]);
                            bakeStopwatch.Reset();
                        }
                        else
                        {
                            foreach (var (main, msk) in batchForNextBake)
                            {
                                staticTextureAtlas.Insert(main, msk);
                            }

                            bakeStopwatch.Restart();
                            staticTextureAtlas.Bake();
                            bakeStopwatch.Stop();
                        }


                        GlobalTextureAtlasManager.staticTextureAtlases.Add(staticTextureAtlas);
                        double secondsElapsed = bakeStopwatch.Elapsed.TotalSeconds;
                        if (secondsElapsed > 0)
                        {
                            float latestBakeSpeed = (float)(pixelsInCurrentSlice / secondsElapsed);
                            measuredBakeSpeed_PixelsPerSecond = Mathf.Lerp(measuredBakeSpeed_PixelsPerSecond, latestBakeSpeed, ADAPTATION_FACTOR);
                            float newSliceSize = measuredBakeSpeed_PixelsPerSecond * TARGET_BAKE_TIME_SECONDS;
                            //First make it rectangle, then apply density to it
                            adaptivePixelsPerSlice = (int)
                                (((int)Mathf.Clamp(newSliceSize, MIN_PIXELS_PER_SLICE, MAX_PIXELS_PER_SLICE))
                                .FloorToPowerOfTwo() * PACK_DENSITY); 
                        }
                    }
                }
                #endregion
                Log.Warning("Finished baking StaticAtlases - " + DateTime.Now.ToString());
            }
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
            
            Log.Warning("Finished loading icons - " + DateTime.Now.ToString());

            count = 0;
            Log.Warning("Starting resolving SubSoundDefs: " + subSoundDefToResolve.Count + " - " + DateTime.Now.ToString());
            while (subSoundDefToResolve.Any())
            {
                var (def, action) = subSoundDefToResolve.Dequeue();
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Error("Error resolving AudioGrain for " + def, ex);
                }
                count++;
                if (ElapsedMaxImpact)
                {
                    count = 0;
                    yield return 0;
                    stopwatch.Restart();
                }
            }
            SoundStarter_Patch.Unpatch();
            Log.Warning("Finished resolving SubSoundDefs - " + DateTime.Now.ToString());
            
            stopwatch.Stop();
            this.enabled = false;
            yield return null;
        }

        public void Error(string message, Exception ex)
        {
            Log.Error(message + " - " + ex + " - " + new StackTrace());
        }
    }
}

