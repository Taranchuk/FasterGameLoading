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
                #region BakeAtlas (Adaptive)
                const float TARGET_BAKE_TIME_SECONDS = 0.008f;
                const float ADAPTATION_FACTOR = 0.2f;
                const int INITIAL_PIXELS_PER_SLICE = 64 * 64;
                const int MIN_PIXELS_PER_SLICE = 32 * 32;
                const int MAX_PIXELS_PER_SLICE = 2048 * 2048;
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
                            var staticTextureAtlas = new StaticTextureAtlas(key);
                            foreach (var (main, msk) in batchForNextBake) 
                            { 
                                staticTextureAtlas.Insert(main, msk);
                            }

                            bakeStopwatch.Restart();
                            staticTextureAtlas.Bake();
                            bakeStopwatch.Stop();

                            GlobalTextureAtlasManager.staticTextureAtlases.Add(staticTextureAtlas);
                            double secondsElapsed = bakeStopwatch.Elapsed.TotalSeconds;
                            if (secondsElapsed > 0)
                            {
                                float latestBakeSpeed = (float)(pixelsInCurrentSlice / secondsElapsed);
                                measuredBakeSpeed_PixelsPerSecond = Mathf.Lerp(measuredBakeSpeed_PixelsPerSecond, latestBakeSpeed, ADAPTATION_FACTOR);
                                float newSliceSize = measuredBakeSpeed_PixelsPerSecond * TARGET_BAKE_TIME_SECONDS;
                                adaptivePixelsPerSlice = (int)Mathf.Clamp(newSliceSize, MIN_PIXELS_PER_SLICE, MAX_PIXELS_PER_SLICE);
                            }
                            yield return null;
                            batchForNextBake.Clear();
                            pixelsInCurrentSlice = 0;
                        }
                    }
                    if (batchForNextBake.Any())
                    {
                        var staticTextureAtlas = new StaticTextureAtlas(key);
                        foreach (var (main, msk) in batchForNextBake) 
                        { 
                            staticTextureAtlas.Insert(main, msk); 
                        }

                        bakeStopwatch.Restart();
                        staticTextureAtlas.Bake();
                        bakeStopwatch.Stop();

                        GlobalTextureAtlasManager.staticTextureAtlases.Add(staticTextureAtlas);
                        double secondsElapsed = bakeStopwatch.Elapsed.TotalSeconds;
                        if (secondsElapsed > 0)
                        {
                            float latestBakeSpeed = (float)(pixelsInCurrentSlice / secondsElapsed);
                            measuredBakeSpeed_PixelsPerSecond = Mathf.Lerp(measuredBakeSpeed_PixelsPerSecond, latestBakeSpeed, ADAPTATION_FACTOR);
                            float newSliceSize = measuredBakeSpeed_PixelsPerSecond * TARGET_BAKE_TIME_SECONDS;
                            adaptivePixelsPerSlice = (int)Mathf.Clamp(newSliceSize, MIN_PIXELS_PER_SLICE, MAX_PIXELS_PER_SLICE);
                        }

                        yield return null;
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

