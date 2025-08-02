using HarmonyLib;
using RimWorld.Planet;
using System;
using System.Linq;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(World), "FinalizeInit")]
    public class World_FinalizeInit_Patch
    {
        public static void Postfix()
        {
            LongEventHandler.ExecuteWhenFinished(delegate
            {
                FasterGameLoadingMod.loadingActions.FinalizeLoading();
            });
        }
    }
}

