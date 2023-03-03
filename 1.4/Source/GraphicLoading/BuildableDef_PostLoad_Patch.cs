using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(BuildableDef), "PostLoad")]
    public static class BuildableDef_PostLoad_Patch
    {
        public static bool Prepare() => FasterGameLoadingSettings.delayGraphicLoading;
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codeInstructions)
        {
            var execute = AccessTools.Method(typeof(LongEventHandler), nameof(LongEventHandler.ExecuteWhenFinished));
            var executeDelayed = AccessTools.Method(typeof(BuildableDef_PostLoad_Patch), nameof(ExecuteDelayed));
            foreach (var code in codeInstructions)
            {
                if (code.Calls(execute))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, executeDelayed);
                }
                else
                {
                    yield return code;
                }
            }
        }
    
        public static void ExecuteDelayed(Action action, BuildableDef def)
        {
            if (def is ThingDef thingDef && thingDef.ShouldBeLoadedImmediately())
            {
                var oldValue = Startup.doNotDelayLongEventsWhenFinished;
                Startup.doNotDelayLongEventsWhenFinished = true;
                LongEventHandler.ExecuteWhenFinished(action);
                Startup.doNotDelayLongEventsWhenFinished = oldValue;
            }
            else
            {
                FasterGameLoadingMod.delayedActions.iconsToLoad.Add((def, action));
            }
        }

        public static bool ShouldBeLoadedImmediately(this ThingDef thingDef)
        {
            return thingDef.graphicData != null && thingDef.graphicData.Linked 
                || thingDef.thingClass != null && thingDef.thingClass.Name == "Building_Pipe"
                || typeof(Medicine).IsAssignableFrom(thingDef.thingClass) 
                || thingDef.orderedTakeGroup?.defName == "Medicine";
        }
    }
}
