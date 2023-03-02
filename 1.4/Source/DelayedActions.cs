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
        public IEnumerator PerformActions()
        {
            Stopwatch stopwatch = new();
            stopwatch.Start();
            var count = 0;
            Log.Warning("Starting actions: " + actionsToPerform.Count + " - " + DateTime.Now.ToString());
            while (actionsToPerform.Any())
            {
                if (UnityData.IsInMainThread is false)
                {
                    yield return 0;
                }
                var action = actionsToPerform.Pop();
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Log.Error("Error loading action for " + action.Method.FullDescription() + " - " + ex.Message);
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
                    var (harmony, assembly) = harmonyPatchesToPerform.Pop();
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
                        var curType = curTypes.Pop();
                        var patchProcessor = curHarmony.CreateClassProcessor(curType);
                        patchProcessor.Patch();
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error performing harmony patches for " + curAssembly + " - " + ex.Message);
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
                var (def, action) = graphicsToLoad.Pop();
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Log.Error("Error loading graphic for " + def + " - " + ex.Message);
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
                var (def, action) = iconsToLoad.Pop();
                if (def.uiIcon == BaseContent.BadTex)
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error loading icon for " + def + " - " + ex.Message);
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
            MedicalCareUtility.Reset();
            yield return null;
        }
    }
}

