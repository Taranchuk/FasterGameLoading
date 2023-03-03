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
        public List<Action> actionsToPerform = new();
        public List<(Harmony harmony, Assembly assembly)> harmonyPatchesToPerform = new();
        public List<(ThingDef def, Action action)> graphicsToLoad = new();
        public List<(BuildableDef def, Action action)> iconsToLoad = new();
        private List<Type> curTypes;
        private Harmony curHarmony;
        private Assembly curAssembly;
        private Stopwatch stopwatch = new();
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
            Log.Warning("Starting actions: " + actionsToPerform.Count + " - " + DateTime.Now.ToString());
            while (actionsToPerform.Any())
            {
                if (UnityData.IsInMainThread is false)
                {
                    yield return 0;
                }
                var action = actionsToPerform.PopFirst();
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Error("Error loading action for " + action.Target + " - " + action.Method.FullDescription(), ex);
                }
                count++;
                float elapsed = (float)stopwatch.ElapsedTicks / Stopwatch.Frequency;
                if (elapsed >= MaxImpactThisFrame)
                {
                    count = 0;
                    yield return 0;
                    stopwatch.Restart();
                }
            }
            Startup.doNotDelayLongEventsWhenFinished = true;
            count = 0;
            Log.Warning("Finished actions - " + DateTime.Now.ToString());
            Log.Warning("Starting performing harmony patches: " + harmonyPatchesToPerform.Count + " - " + DateTime.Now.ToString());
            while (harmonyPatchesToPerform.Any())
            {
                if (curTypes is null || !curTypes.Any())
                {
                    var (harmony, assembly) = harmonyPatchesToPerform.PopFirst();
                    curHarmony = harmony;
                    curAssembly = assembly;
                    curTypes = AccessTools.GetTypesFromAssembly(curAssembly).ToList();
                }
                if (UnityData.IsInMainThread is false)
                {
                    yield return 0;
                }

                if (curTypes.Any())
                {
                    try
                    {
                        var curType = curTypes.PopFirst();
                        var patchProcessor = curHarmony.CreateClassProcessor(curType);
                        patchProcessor.Patch();
                    }
                    catch (Exception ex)
                    {
                        Error("Error performing harmony patches for " + curAssembly, ex);
                    }
                }
                count++;
                float elapsed = (float)stopwatch.ElapsedTicks / Stopwatch.Frequency;
                if (elapsed >= MaxImpactThisFrame)
                {
                    count = 0;
                    yield return 0;
                    stopwatch.Restart();
                }
            }
            Startup.doNotDelayHarmonyPatches = true;
            count = 0;
            Log.Warning("Finished performing harmony patches: " + DateTime.Now.ToString());
            Log.Warning("Starting loading graphics: " + graphicsToLoad.Count + " - " + DateTime.Now.ToString());
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
                }
                catch (Exception ex)
                {
                    Error("Error loading graphic for " + def, ex);
                }
                count++;
                float elapsed = (float)stopwatch.ElapsedTicks / Stopwatch.Frequency;
                if (elapsed >= MaxImpactThisFrame)
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

            count = 0; 
            Log.Warning("Finished loading graphics - " + DateTime.Now.ToString());
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
                    float elapsed = (float)stopwatch.ElapsedTicks / Stopwatch.Frequency;
                    if (elapsed >= MaxImpactThisFrame)
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

