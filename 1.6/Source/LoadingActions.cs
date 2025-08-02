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
    public class LoadingActions : MonoBehaviour
    {
        public float MaxImpactThisFrame => 0.05f;
        public Queue<(ThingDef def, Action action)> thingGraphicsToLoad = new();
        public Queue<(TerrainDef def, Action action)> terrainGraphicsToLoad = new();
        public Queue<(BuildableDef def, Action action)> iconsToLoad = new();
        public Queue<(SubSoundDef def, Action action)> subSoundDefToResolve = new();
        public static bool AllGraphicLoaded = false;
        public static List<ThingDef> loadedThingDefsForMapRefresh = new();
        public static List<TerrainDef> loadedTerrainDefsForMapRefresh = new();
        private static readonly List<ProgressTracker> progressTrackers;
        public static readonly ProgressTracker ThingGraphicsProgress = new() { label = "thing graphics" };
        public static readonly ProgressTracker TerrainGraphicsProgress = new() { label = "terrain graphics" };
        public static readonly ProgressTracker IconsProgress = new() { label = "icons" };
        public static readonly ProgressTracker SoundsProgress = new() { label = "sounds" };
        public static readonly ProgressTracker AtlasProgress = new() { label = "atlas baking" };
        public static readonly Dictionary<MethodBase, ProfilingTracker> profiledMethods = new();
        public static readonly List<ProfilingTracker> profilingTrackers = new();
        private Stopwatch stopwatch = new();
        private bool ElapsedMaxImpact => (float)stopwatch.ElapsedTicks / Stopwatch.Frequency >= MaxImpactThisFrame;
        private bool graphicsProcessingStarted = false;
        private bool assetsProcessingStarted = false;
        static LoadingActions()
        {
            progressTrackers = new List<ProgressTracker>
            {
                ThingGraphicsProgress,
                TerrainGraphicsProgress,
                AtlasProgress,
                IconsProgress,
                SoundsProgress
            };
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

            if (!graphicsProcessingStarted)
            {
                if (thingGraphicsToLoad.Any() || terrainGraphicsToLoad.Any() || iconsToLoad.Any())
                {
                    graphicsProcessingStarted = true;
                    StartCoroutine(ProcessGraphicsIncrementally());
                }
            }

            if (!assetsProcessingStarted)
            {
                if (subSoundDefToResolve.Any() || !FasterGameLoadingSettings.disableStaticAtlasesBaking && GlobalTextureAtlasManager.buildQueue.Any())
                {
                    assetsProcessingStarted = true;
                    StartCoroutine(ProcessAssetsIncrementally());
                }
            }
        }
        public class ProfilingTracker
        {
            public string methodName;
            public Type declaringType;
            public long totalTicks;
            public int callCount;
            public float AverageTimeMs => callCount > 0 ? (float)totalTicks / callCount / Stopwatch.Frequency * 1000f : 0f;
            public float TotalTimeMs => (float)totalTicks / Stopwatch.Frequency * 1000f;
        }
        public class ProgressTracker
        {
            public string label;
            public int total;
            public int processed;
            public float Progress => total > 0 ? (float)processed / total : 0f;
        }

        public void OnGUI()
        {
            if (FasterGameLoadingSettings.debugMode is false || !progressTrackers.Any() && !profilingTrackers.Any())
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
            if (profilingTrackers.Any())
            {
                var sortedTrackers = profilingTrackers.Where(t => t.TotalTimeMs > 500).OrderByDescending(t => t.TotalTimeMs).ToList();
                if (sortedTrackers.Any())
                {
                    float profilingY = y + 20;
                    float profilingHeight = (sortedTrackers.Count + 1) * 20f + 10f;
                    var backgroundRect = new Rect(x - 5, profilingY - 5, barWidth * 2 + 10, profilingHeight);
                    Widgets.DrawBoxSolid(backgroundRect, Color.black);

                    Text.Font = GameFont.Small;
                    Widgets.Label(new Rect(x, profilingY, barWidth, 20), "Method Profiling (methods > 500ms):");
                    profilingY += 25;

                    foreach (var tracker in sortedTrackers)
                    {
                        string label = $"{tracker.declaringType.Name}.{tracker.methodName}: {tracker.TotalTimeMs:F4}ms total, {tracker.AverageTimeMs:F4}ms avg, {tracker.callCount} calls";
                        Widgets.Label(new Rect(x, profilingY, barWidth * 2, 20), label);
                        profilingY += 20;
                    }
                }
            }
        }

        public IEnumerator ProcessAssetsIncrementally()
        {
            yield return StartCoroutine(ProcessQueueIncrementally(subSoundDefToResolve, ProcessSingleSubSoundDef, SoundsProgress));

            if (!FasterGameLoadingSettings.disableStaticAtlasesBaking)
            {
                yield return StartCoroutine(BakeStaticAtlases());
            }

            AllGraphicLoaded = true;
        }

        private IEnumerator ProcessGraphicsIncrementally()
        {
            while (!SolidBioDatabase_LoadAllBios_Patch.canLoadThingGraphics)
            {
                yield return null;
            }

            yield return StartCoroutine(ProcessQueueIncrementally(thingGraphicsToLoad, ProcessSingleGraphic, ThingGraphicsProgress));
            yield return StartCoroutine(ProcessQueueIncrementally(terrainGraphicsToLoad, ProcessSingleTerrainGraphic, TerrainGraphicsProgress));
            yield return StartCoroutine(ProcessQueueIncrementally(iconsToLoad, ProcessSingleIcon, IconsProgress));
        }

        public void FinalizeLoading()
        {
            ProcessQueueSynchronously(thingGraphicsToLoad, ProcessSingleGraphic, ThingGraphicsProgress);
            ProcessQueueSynchronously(terrainGraphicsToLoad, ProcessSingleTerrainGraphic, TerrainGraphicsProgress);
            ProcessQueueSynchronously(iconsToLoad, ProcessSingleIcon, IconsProgress);
            RefreshMap();
            ProcessQueueSynchronously(subSoundDefToResolve, ProcessSingleSubSoundDef, SoundsProgress);
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
            Log.Warning("Starting baking StaticAtlases - " + DateTime.Now);

            var buildQueueSnapshot = GlobalTextureAtlasManager.buildQueue.ToList();
            if (!buildQueueSnapshot.Any())
            {
                yield break;
            }
            GlobalTextureAtlasManager.buildQueue.Clear();

            AtlasProgress.total += buildQueueSnapshot.Sum(kvp => kvp.Value.Item1.Count);

            const float TARGET_BAKE_TIME_SECONDS = 0.008f;
            const float ADAPTATION_FACTOR = 0.2f;
            const int INITIAL_PIXELS_PER_SLICE = 1024 * 1024;
            const int MIN_PIXELS_PER_SLICE = 1024 * 1024;
            const int MAX_PIXELS_PER_SLICE = 4096 * 4096;
            const float PACK_DENSITY = 0.8f;

            float measuredBakeSpeed_PixelsPerSecond = 2_000_000f;
            int adaptivePixelsPerSlice = INITIAL_PIXELS_PER_SLICE;
            var bakeStopwatch = new Stopwatch();

            stopwatch.Restart();

            foreach (var kvp in buildQueueSnapshot)
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

                    if (AtlasProgress.total > 0)
                    {
                        AtlasProgress.processed++;
                    }

                    if (ElapsedMaxImpact)
                    {
                        yield return null;
                        stopwatch.Restart();
                    }

                    if (pixelsInCurrentSlice >= adaptivePixelsPerSlice)
                    {
                        yield return null;
                        FlushBatch(key, batchForNextBake, bakeStopwatch);
                        batchForNextBake.Clear();
                        pixelsInCurrentSlice = 0;
                        stopwatch.Restart();
                    }
                }
                if (batchForNextBake.Any())
                {
                    yield return null;
                    FlushBatch(key, batchForNextBake, bakeStopwatch);
                }
            }

            Log.Warning("Finished baking StaticAtlases - " + DateTime.Now);

            void FlushBatch(TextureAtlasGroupKey key, List<(Texture2D main, Texture2D mask)> batch, Stopwatch sw)
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
                    sw.Reset();
                }
                else
                {
                    foreach (var (main, msk) in batch)
                    {
                        staticTextureAtlas.Insert(main, msk);
                    }
                    sw.Restart();
                    staticTextureAtlas.Bake();
                    sw.Stop();
                }

                GlobalTextureAtlasManager.staticTextureAtlases.Add(staticTextureAtlas);
                double secondsElapsed = sw.Elapsed.TotalSeconds;
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
            if (def.uiIcon == BaseContent.BadTex || def.uiIcon == null)
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
            tracker.total += queue.Count;
            stopwatch.Restart();
            while (queue.Any())
            {
                if (!UnityData.IsInMainThread)
                {
                    yield return null;
                    continue;
                }
                var (def, action) = queue.Dequeue();
                processor(def, action);
                tracker.processed++;
                if (ElapsedMaxImpact)
                {
                    yield return null;
                    stopwatch.Restart();
                }
            }
            Log.Warning($"Finished loading {tracker.label} - {DateTime.Now}");
        }

        private void ProcessQueueSynchronously<T>(Queue<(T, Action)> queue, Action<T, Action> processor, ProgressTracker tracker)
        {
            while (queue.Any())
            {
                var (def, action) = queue.Dequeue();
                processor(def, action);
                tracker.processed++;
            }
        }
    }
}
