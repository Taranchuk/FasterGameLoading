using HarmonyLib;
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
            if (def is ThingDef thingDef && thingDef.graphicData != null && (typeof(Graphic_Linked).IsAssignableFrom(thingDef.graphicData.graphicClass) 
                || thingDef.graphicData.Linked))
            {
                var oldValue = Startup.doNotDelayLongEventsWhenFinished;
                Startup.doNotDelayLongEventsWhenFinished = true;
                Log.Message("Allowing: " + action.Method.FullDescription());
                LongEventHandler.ExecuteWhenFinished(action);
                Startup.doNotDelayLongEventsWhenFinished = oldValue;
            }
            else
            {
                FasterGameLoadingMod.delayedActions.iconsToLoad.Add((def, action));
            }
        }
    }
}
