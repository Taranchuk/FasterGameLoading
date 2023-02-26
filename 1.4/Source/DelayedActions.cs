using RimWorld;
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
                    try
                    {
                        entry.Item2();
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error loading graphic for " + entry.Item1 + " - " + ex.Message);
                    }
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
                else
                {
                    if (entry.Item1.graphicData.shaderType == null)
                    {
                        entry.Item1.graphicData.shaderType = ShaderTypeDefOf.Cutout;
                    }
                    if (entry.Item1.drawerType != DrawerType.RealtimeOnly)
                    {
                        TextureAtlasGroup textureAtlasGroup = entry.Item1.category.ToAtlasGroup();
                        entry.Item1.graphic.TryInsertIntoAtlas(textureAtlasGroup);
                        if (textureAtlasGroup == TextureAtlasGroup.Building && entry.Item1.Minifiable)
                        {
                            entry.Item1.graphic.TryInsertIntoAtlas(TextureAtlasGroup.Item);
                        }
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
                    try
                    {
                        entry.Item2();
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error loading icon for " + entry.Item1 + " - " + ex.Message);
                    }
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

