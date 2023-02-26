using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Xml;
using UnityEngine;
using Verse;
namespace FasterGameLoading
{
    public class DelayedActions : MonoBehaviour
    {
        public List<Action> graphicsToLoad = new List<Action>();
        public List<Action> iconsToLoad = new List<Action>();
        public IEnumerator LoadGraphics()
        {
            Stopwatch stopwatch = new();
            stopwatch.Start();
            var totalElapsed = 0f;
            var count = 0;
            Log.Warning("Starting loading graphics: " + graphicsToLoad.Count + " - " + DateTime.Now.ToString());
            while (graphicsToLoad.Any())
            {
                var entry = graphicsToLoad.Pop();
                entry();
                count++;
                float elapsed = (float)stopwatch.ElapsedTicks / Stopwatch.Frequency;
                totalElapsed += elapsed;
                if (elapsed >= 0.01f)
                {
                    Log.Warning("Done loading graphic: " + count);
                    count = 0;
                    yield return new WaitForSeconds(0.01f);
                    stopwatch.Restart();
                }
            }

            count = 0;
            while (iconsToLoad.Any())
            {
                var entry = iconsToLoad.Pop();
                entry();
                count++;
                float elapsed = (float)stopwatch.ElapsedTicks / Stopwatch.Frequency;
                totalElapsed += elapsed;
                if (elapsed >= 0.01f)
                {
                    Log.Warning("Done loading icon: " + count);
                    count = 0;
                    yield return new WaitForSeconds(0.01f);
                    stopwatch.Restart();
                }
            }
            stopwatch.Stop();
            Log.Warning("Finished loading graphics and icons - " + DateTime.Now.ToString());
            yield return null;
        }

        public void OnDestroy()
        {
            Log.Message("On destroy");
        }
    }
    public class FasterGameLoadingMod : Mod
    {
        public static Harmony harmony;
        public static FasterGameLoadingSettings settings;
        public static Thread threadPostLoad;
        public static DelayedActions loadGraphicsPerFrames;
        public FasterGameLoadingMod(ModContentPack pack) : base(pack)
        {
            var gameObject = new GameObject("FasterGameLoadingMod");
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            loadGraphicsPerFrames = gameObject.AddComponent<DelayedActions>();
            settings = this.GetSettings<FasterGameLoadingSettings>();
            harmony = new Harmony("FasterGameLoadingMod");
            harmony.PatchAll();

            // an attempt to put harmony patchings into another thread, didn't work out by some reason
            //thread = new Thread(new ThreadStart(() =>
            //{
            //    var threadedHarmony = new ThreadedHarmony();
            //    threadedHarmony.Run();
            //}));
            //thread.Start();
        }

        public override string SettingsCategory()
        {
            return this.Content.Name;
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            FasterGameLoadingSettings.DoSettingsWindowContents(inRect);
        }
    }
}

