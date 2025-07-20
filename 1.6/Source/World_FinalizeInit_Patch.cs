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
                while (FasterGameLoadingMod.delayedActions.subSoundDefToResolve.Any())
                {
                    var (def, action) = FasterGameLoadingMod.delayedActions.subSoundDefToResolve.Dequeue();
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        FasterGameLoadingMod.delayedActions.Error("Error resolving AudioGrain for " + def, ex);
                    }
                }
                SoundStarter_Patch.Unpatch();
            });
        }
    }
}

