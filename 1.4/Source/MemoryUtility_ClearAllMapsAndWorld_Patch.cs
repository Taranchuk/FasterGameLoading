using HarmonyLib;
using System;
using System.Diagnostics;
using System.Linq;
using Verse;
using Verse.Profile;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(MemoryUtility), "ClearAllMapsAndWorld")]
    public static class MemoryUtility_ClearAllMapsAndWorld_Patch
    {
        [HarmonyPriority(int.MaxValue)]
        public static void Prefix()
        {
            PerformPatchesIfAny();
        }
        public static void PerformPatchesIfAny()
        {
            Log.Warning("Loading game, starting performing actions (" + FasterGameLoadingMod.delayedActions.actionsToPerform.Count() 
                + ") and harmony patches (" + FasterGameLoadingMod.delayedActions.harmonyPatchesToPerform.Count + ") : " 
                + DateTime.Now.ToString() + " - " + UnityData.IsInMainThread);
            while (FasterGameLoadingMod.delayedActions.actionsToPerform.Any())
            {
                var entry = FasterGameLoadingMod.delayedActions.actionsToPerform.Pop();
                try
                {
                    entry();
                }
                catch (Exception ex)
                {
                    Log.Error("Error performing action for " + entry.Method.FullDescription() + " - " + ex.Message);
                }
            }
            while (FasterGameLoadingMod.delayedActions.harmonyPatchesToPerform.Any())
            {
                var entry = FasterGameLoadingMod.delayedActions.harmonyPatchesToPerform.Pop();
                try
                {
                    var curTypes = AccessTools.GetTypesFromAssembly(entry.Item2).ToList();
                    foreach (var curType in curTypes)
                    {
                        var patchProcessor = entry.Item1.CreateClassProcessor(curType);
                        patchProcessor.Patch();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Error performing harmony patches for " + entry.Item1 + " - " + entry.Item2 + " - " + ex.Message);
                }
            }
        }
    }
}

