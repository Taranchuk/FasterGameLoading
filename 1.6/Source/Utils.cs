using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FasterGameLoading
{
    [StaticConstructorOnStartup]
    public static class Utils
    {
        public static readonly Texture2D EmptyBarTex = SolidColorMaterials.NewSolidColorTexture(GenUI.FillableBar_Empty);

        public static readonly Texture2D BarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.1254902f, 46f / 85f, 0f));
        public static T PopFirst<T>(this List<T> list)
        {
            T currentFirst = list[0];
            list.RemoveAt(0);
            return currentFirst;
        }

        public static void Log(string message)
        {
            Verse.Log.Warning($"[FasterGameLoading] {message}");
        }
        public static List<Thing> ThingsOfDefs(this ListerThings listerThings, IEnumerable<ThingDef> defs)
        {
            List<Thing> outThings = [];
            foreach (var def in defs)
            {
                if (listerThings.listsByDef.TryGetValue(def, out var things))
                {
                    outThings.AddRange(things);
                }

            }
            return outThings;
        }
        public static int FloorToPowerOfTwo(this int i)
        {
            int closest = UnityEngine.Mathf.ClosestPowerOfTwo(i);
            return closest <= i ? closest : closest >> 1;

        }
    }
}

