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
        public float MaxImpactThisFrame => 0.05f;
        public Queue<(ThingDef def, Action action)> thingGraphicsToLoad = new();
        public Queue<(TerrainDef def, Action action)> terrainGraphicsToLoad = new();
        public Queue<(BuildableDef def, Action action)> iconsToLoad = new();
        public Queue<(SubSoundDef def, Action action)> subSoundDefToResolve = new();
        public static bool AllGraphicLoaded = false;
        public static List<ThingDef> loadedThingDefsForMapRefresh = new();
        public static List<TerrainDef> loadedTerrainDefsForMapRefresh = new();
        private List<ProgressTracker> progressTrackers = new List<ProgressTracker>();
        private Stopwatch stopwatch = new();
        private bool ElapsedMaxImpact => (float)stopwatch.ElapsedTicks / Stopwatch.Frequency >= MaxImpactThisFrame;
        public static bool coroutineRunning = false;
        private bool gameInitializationBegan = false;

        public static void StartCoroutine()
        {
            if (!coroutineRunning)
            {
                coroutineRunning = true;
                FasterGameLoadingMod.delayedActions.StartCoroutine(FasterGameLoadingMod.delayedActions.PerformActions());
            }
        }

        public void LateUpdate()
        {
            if (FasterGameLoadingSettings.earlyModContentLoading)
            {
                var modToLoad = LoadedModManager.RunningMods.FirstOrDefault(x => !ModContentPack_ReloadContentInt_Patch.loadedMods.Contains(x));
                if (modToLoad != null)
                {
                    modToLoad.ReloadContentInt();
                    ModContentPack_ReloadContentInt_Patch.loadedMods.Add(modToLoad);
                }
            }

            if (gameInitializationBegan is false && Current.Game != null)
            {
                gameInitializationBegan = true;
                StopCoroutine(nameof(PerformActions));
                LongEventHandler.ExecuteWhenFinished(FinalizeLoading);
            }
        }

        private class ProgressTracker
        {
            public string label;
            public int total;
            public int processed;
            public float Progress => total > 0 ? (float)processed / total : 0f;
        }

        public void OnGUI()
        {
            if (coroutineRunning is false || !progressTrackers.Any())
            {
                return;
            }

            float barWidth = 300f;
            float barHeight = 32f;
            float x = 15;
            float y = 15;

            var originalColor = GUI.color;
            foreach (var tracker in progressTrackers)
            {
                if (tracker.total > 0 && tracker.processed < tracker.total)
                {
                    var rect = new Rect(x, y, barWidth, barHeight);
                    GUI.color = Color.green;
                    Widgets.FillableBar(rect, tracker.Progress, BaseContent.WhiteTex);
                    GUI.color = originalColor;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(rect, $"Loading {tracker.label}: {tracker.processed} / {tracker.total}");
                    Text.Anchor = TextAnchor.UpperLeft;
                    y += barHeight + 5f;
                }
            }
        }
        public IEnumerator PerformActions()
        {
            stopwatch.Start();

            var graphicsProgress = new ProgressTracker { label = "thing graphics" };
            progressTrackers.Add(graphicsProgress);
            yield return StartCoroutine(ProcessQueueIncrementally(thingGraphicsToLoad, (d, a) => ProcessSingleGraphic(d, a), graphicsProgress));

            var terrainProgress = new ProgressTracker { label = "terrain graphics" };
            progressTrackers.Add(terrainProgress);
            yield return StartCoroutine(ProcessQueueIncrementally(terrainGraphicsToLoad, (d, a) => ProcessSingleTerrainGraphic(d, a), terrainProgress));

            if (!FasterGameLoadingSettings.disableStaticAtlasesBaking)
            {
                AllGraphicLoaded = true;
                yield return StartCoroutine(BakeStaticAtlases());
            }

            var iconsProgress = new ProgressTracker { label = "icons" };
            progressTrackers.Add(iconsProgress);
            yield return StartCoroutine(ProcessQueueIncrementally(iconsToLoad, (d, a) => ProcessSingleIcon(d, a), iconsProgress));

            var soundsProgress = new ProgressTracker { label = "sounds" };
            progressTrackers.Add(soundsProgress);
            yield return StartCoroutine(ProcessQueueIncrementally(subSoundDefToResolve, (d, a) => ProcessSingleSubSoundDef(d, a), soundsProgress));

            stopwatch.Stop();
            coroutineRunning = false;
        }

        public void FinalizeLoading()
        {
            ProcessQueueSynchronously(thingGraphicsToLoad, (d, a) => ProcessSingleGraphic(d, a));
            ProcessQueueSynchronously(terrainGraphicsToLoad, (d, a) => ProcessSingleTerrainGraphic(d, a));
            ProcessQueueSynchronously(iconsToLoad, (d, a) => ProcessSingleIcon(d, a));
            RefreshMap();
            ProcessQueueSynchronously(subSoundDefToResolve, (d, a) => ProcessSingleSubSoundDef(d, a));
            SoundStarter_Patch.Unpatch();
            stopwatch.Stop();
        }

        private void RefreshMap()
        {
            if (Current.Game == null) return;
            foreach (var map in Find.Maps)
            {
                if (map.mapDrawer.sections == null) continue;
                bool shouldRefresh = false;
                if (loadedThingDefsForMapRefresh.Any())
                {
                    shouldRefresh = true;
                    foreach (var thing in map.listerThings.ThingsOfDefs(loadedThingDefsForMapRefresh))
                    {
                        map.mapDrawer.MapMeshDirty(thing.Position, MapMeshFlagDefOf.Things | MapMeshFlagDefOf.Buildings);
                    }
                }
                if (loadedTerrainDefsForMapRefresh.Any())
                {
                    shouldRefresh = true;
                    map.mapDrawer.globalDirtyFlags |= MapMeshFlagDefOf.Terrain;
                    foreach (var section in map.mapDrawer.sections)
                    {
                        section.dirtyFlags |= MapMeshFlagDefOf.Terrain;
                    }
                }
                if (shouldRefresh)
                {
                    map.mapDrawer.RegenerateEverythingNow();
                }
            }
            loadedThingDefsForMapRefresh.Clear();
            loadedTerrainDefsForMapRefresh.Clear();
        }

        private IEnumerator BakeStaticAtlases()
        {
            Log.Warning("Starting baking StaticAtlases - " + DateTime.Now.ToString());
            var atlasProgress = new ProgressTracker { label = "atlas baking" };
            if (GlobalTextureAtlasManager.buildQueue.Any())
            {
                progressTrackers.Add(atlasProgress);
                atlasProgress.total = GlobalTextureAtlasManager.buildQueue.Sum(kvp => kvp.Value.Item1.Count);
                atlasProgress.processed = 0;
            }

            const float TARGET_BAKE_TIME_SECONDS = 0.008f;
            const float ADAPTATION_FACTOR = 0.2f;
            const int INITIAL_PIXELS_PER_SLICE = 1024 * 1024;
            const int MIN_PIXELS_PER_SLICE = 1024 * 1024;
            const int MAX_PIXELS_PER_SLICE = 4096 * 4096;
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
                    if (atlasProgress.total > 0)
                    {
                        atlasProgress.processed++;
                    }
                    if (pixelsInCurrentSlice >= adaptivePixelsPerSlice)
                    {
                        FlushBatch(key, batchForNextBake);
                        yield return null;
                        batchForNextBake.Clear();
                        pixelsInCurrentSlice = 0;
                    }
                }
                if (batchForNextBake.Any())
                {
                    FlushBatch(key, batchForNextBake);
                    yield return null;
                }
            }
            Log.Warning("Finished baking StaticAtlases - " + DateTime.Now.ToString());

            void FlushBatch(TextureAtlasGroupKey key, List<(Texture2D main, Texture2D mask)> batch)
            {
                var staticTextureAtlas = new StaticTextureAtlas(key);
                if (batch.Count == 1)
                {
                    staticTextureAtlas.colorTexture = batch.First().main;
                    if (key.hasMask)
                    {
                        staticTextureAtlas.maskTexture = batch.First().mask;
                    }
                    staticTextureAtlas.BuildMeshesForUvs([new(0, 0, 1, 1)]);
                    bakeStopwatch.Reset();
                }
                else
                {
                    foreach (var (main, msk) in batch)
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
                    float latestBakeSpeed = (float)(batch.Sum(t => t.main.width * t.main.height) / secondsElapsed);
                    measuredBakeSpeed_PixelsPerSecond = Mathf.Lerp(measuredBakeSpeed_PixelsPerSecond, latestBakeSpeed, ADAPTATION_FACTOR);
                    float newSliceSize = measuredBakeSpeed_PixelsPerSecond * TARGET_BAKE_TIME_SECONDS;
                    adaptivePixelsPerSlice = (int)(((int)Mathf.Clamp(newSliceSize, MIN_PIXELS_PER_SLICE, MAX_PIXELS_PER_SLICE)).FloorToPowerOfTwo() * PACK_DENSITY);
                }
            }
        }

        public void Error(string message, Exception ex)
        {
            Log.Error(message + " - " + ex + " - " + new StackTrace());
        }

        private void ProcessSingleGraphic(ThingDef def, Action action)
        {
            try
            {
                action();
                loadedThingDefsForMapRefresh.Add(def);
                if (def.plant != null)
                {
                    def.plant.PostLoadSpecial(def);
                }
            }
            catch (Exception ex)
            {
                Error("Error loading graphic for " + def, ex);
            }
        }

        private void ProcessSingleTerrainGraphic(TerrainDef def, Action action)
        {
            try
            {
                action();
                loadedTerrainDefsForMapRefresh.Add(def);
            }
            catch (Exception ex)
            {
                Error("Error loading graphic for " + def, ex);
            }
        }

        private void ProcessSingleIcon(BuildableDef def, Action action)
        {
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
            }
        }

        private void ProcessSingleSubSoundDef(SubSoundDef def, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Error("Error resolving AudioGrain for " + def, ex);
            }
        }

        private IEnumerator ProcessQueueIncrementally<T>(Queue<(T, Action)> queue, Action<T, Action> processor, ProgressTracker tracker)
        {
            if (!queue.Any())
            {
                yield break;
            }
            Log.Warning($"Starting loading {tracker.label}: {queue.Count} - {DateTime.Now}");
            tracker.total = queue.Count;
            tracker.processed = 0;
            int count = 0;
            while (queue.Any())
            {
                if (!UnityData.IsInMainThread)
                {
                    yield return null;
                }
                var (def, action) = queue.Dequeue();
                processor(def, action);
                tracker.processed++;
                count++;
                if (ElapsedMaxImpact)
                {
                    count = 0;
                    yield return null;
                    stopwatch.Restart();
                }
            }
            Log.Warning($"Finished loading {tracker.label} - {DateTime.Now}");
        }

        private void ProcessQueueSynchronously<T>(Queue<(T, Action)> queue, Action<T, Action> processor)
        {
            while (queue.Any())
            {
                var (def, action) = queue.Dequeue();
                processor(def, action);
            }
        }
    }
}
