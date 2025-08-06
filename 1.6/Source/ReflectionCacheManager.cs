using HarmonyLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Verse;
namespace FasterGameLoading
{
    public static class ReflectionCacheManager
    {
        public static Task PreloadTask;
        public static readonly ConcurrentDictionary<string, Type> FoundTypes = new();

        public static void RegisterFoundType(Type type, string originalName, string usedName, bool wasCacheHit)
        {
            if (type == null)
            {
                return;
            }
            var watch = Stopwatch.StartNew();
            var fullname = type.FullName;

            if (!wasCacheHit)
            {
                FoundTypes[originalName] = type;
                FoundTypes[usedName] = type;
                FoundTypes[fullname] = type;
            }
            FasterGameLoadingSettings.typesLoadedThisSession.TryAdd(fullname, 0);

            if (originalName != fullname)
            {
                FasterGameLoadingSettings.loadedTypesByFullNameSinceLastSession[originalName] = fullname;
            }

            watch.Stop();
            LoadingActions.RegisterFoundTypeTracker.totalTicks += watch.ElapsedTicks;
            LoadingActions.RegisterFoundTypeTracker.callCount++;
        }

        public static bool TryGetFromCache(ref string name, out Type result, out (string originalTypeName, bool wasCacheHit) state)
        {
            var watch = Stopwatch.StartNew();
            try
            {
                Interlocked.Increment(ref LoadingActions.ReflectionTypeCacheProgress.total);
                var originalName = name;
                if (FasterGameLoadingSettings.loadedTypesByFullNameSinceLastSession.TryGetValue(name, out var fullName))
                {
                    name = fullName;
                }

                if (FoundTypes.TryGetValue(name, out result))
                {
                    Interlocked.Increment(ref LoadingActions.ReflectionTypeCacheProgress.processed);
                    state = (originalName, true);
                    return true;
                }
                state = (originalName, false);
                return false;
            }
            finally
            {
                watch.Stop();
                LoadingActions.TryGetFromCacheTracker.totalTicks += watch.ElapsedTicks;
                LoadingActions.TryGetFromCacheTracker.callCount++;
            }
        }

        public static void StartPreloading()
        {
            var tasks = new List<Task>();
            tasks.Add(Task.Run(() => AccessTools_AllTypes_Patch.DoCache()));
            tasks.Add(Task.Run(() => DoTypeLookupsCache()));
            PreloadTask = Task.WhenAll(tasks);
        }

        private static void DoTypeLookupsCache()
        {
            var typeNames = FasterGameLoadingSettings.loadedTypesSinceLastSession.ToList();
            AccessTools_TypeByName_Patch.ignore = true;
            GenThreading.ParallelFor(0, typeNames.Count, i =>
            {
                var typeName = typeNames[i];
                if (FoundTypes.ContainsKey(typeName) is false)
                {
                    FoundTypes[typeName] = AccessTools.TypeByName(typeName);
                }
            });
            Utils.Log($"Preloaded {FoundTypes.Count} types, {FoundTypes.Count - typeNames.Count} were not found.");
            AccessTools_TypeByName_Patch.ignore = false;
        }
    }
}
