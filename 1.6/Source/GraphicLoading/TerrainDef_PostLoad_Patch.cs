using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(TerrainDef), "PostLoad")]
    public static class TerrainDef_PostLoad_Patch
    {
        public static bool Prepare() => FasterGameLoadingSettings.delayGraphicLoading;
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codeInstructions)
        {
            var execute = AccessTools.Method(typeof(LongEventHandler), nameof(LongEventHandler.ExecuteWhenFinished));
            var executeDelayed = AccessTools.Method(typeof(TerrainDef_PostLoad_Patch), nameof(ExecuteDelayed));
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

        public static void ExecuteDelayed(Action action, TerrainDef def)
        {
            FasterGameLoadingMod.loadingActions.terrainGraphicsToLoad.Enqueue((def, action));
        }
    }
}
