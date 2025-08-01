using HarmonyLib;
using System;
using System.Collections.Generic;
using Verse;
using Verse.Sound;

namespace FasterGameLoading
{
    [HarmonyPatch]
    static class SubSoundDef_ResolvePatch
    {
        [HarmonyPatch(typeof(SubSoundDef), "ResolveReferences")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> LateExecute(IEnumerable<CodeInstruction> codeInstructions)
        {
            var execute = AccessTools.Method(typeof(LongEventHandler), nameof(LongEventHandler.ExecuteWhenFinished));
            foreach (var ci in codeInstructions)
            {
                if (ci.Calls(execute))
                {
                    yield return CodeInstruction.LoadArgument(0);
                    yield return CodeInstruction.Call(typeof(SubSoundDef_ResolvePatch), nameof(ExecuteDelayed));
                    continue;
                }
                yield return ci;
            }
        }

        static void ExecuteDelayed(Action action,SubSoundDef def)
        {
            FasterGameLoadingMod.delayedActions.subSoundDefToResolve.Enqueue((def, action));
        }
    }
}
