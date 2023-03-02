using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(ThingDef), "PostLoad")]
    public static class ThingDef_PostLoad_Patch
    {
        public static bool Prepare() => FasterGameLoadingSettings.delayGraphicLoading;
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codeInstructions)
        {
            var execute = AccessTools.Method(typeof(LongEventHandler), nameof(LongEventHandler.ExecuteWhenFinished));
            var executeDelayed = AccessTools.Method(typeof(ThingDef_PostLoad_Patch), nameof(ExecuteDelayed));
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
        

        public static void ExecuteDelayed(Action action, ThingDef def)
        {
            if (def.graphicData.Linked || def.IsMedicine)
            {
                var oldValue = Startup.doNotDelayLongEventsWhenFinished;
                Startup.doNotDelayLongEventsWhenFinished = true;
                LongEventHandler.ExecuteWhenFinished(action);
                Startup.doNotDelayLongEventsWhenFinished = oldValue;
            }
            else
            {
                FasterGameLoadingMod.delayedActions.graphicsToLoad.Add((def, action));
            }
        }
    }
}
