using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Verse;
namespace FasterGameLoading
{
    public class DelayedActions : MonoBehaviour
    {
        public List<(ThingDef, Action)> graphicsToLoad = new ();
        public List<(BuildableDef, Action)> iconsToLoad = new ();
        public const float MaxTimeToLoadThisFrame = 0.001f;
        public IEnumerator LoadGraphics()
        {
            Stopwatch stopwatch = new();
            stopwatch.Start();
            var totalElapsed = 0f;
            var count = 0;
            Log.Warning("Starting loading graphics: " + graphicsToLoad.Count + " - " + DateTime.Now.ToString());
            while (graphicsToLoad.Any())
            {
                if (UnityData.IsInMainThread is false)
                {
                    yield return 0;
                }
                var entry = graphicsToLoad.Pop();
                if (entry.Item1.graphic == BaseContent.BadGraphic)
                {
                    entry.Item2();
                    count++;
                    float elapsed = (float)stopwatch.ElapsedTicks / Stopwatch.Frequency;
                    totalElapsed += elapsed;
                    if (elapsed >= MaxTimeToLoadThisFrame)
                    {
                        count = 0;
                        yield return 0;
                        stopwatch.Restart();
                    }
                }
            }

            count = 0;
            while (iconsToLoad.Any())
            {
                if (UnityData.IsInMainThread is false)
                {
                    yield return 0;
                }
                var entry = iconsToLoad.Pop();
                if (entry.Item1.uiIcon == BaseContent.BadTex)
                {
                    entry.Item2();
                    count++;
                    float elapsed = (float)stopwatch.ElapsedTicks / Stopwatch.Frequency;
                    totalElapsed += elapsed;
                    if (elapsed >= MaxTimeToLoadThisFrame)
                    {
                        count = 0;
                        yield return 0;
                        stopwatch.Restart();
                    }
                }
            }
            stopwatch.Stop();
            Log.Warning("Finished loading graphics and icons - " + DateTime.Now.ToString());
            yield return null;
        }
    }
}

