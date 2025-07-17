using HarmonyLib;
using System;
using System.Collections.Generic;
using Verse;
using Verse.Sound;

namespace FasterGameLoading
{
  [HarmonyPatchCategory("SoundStarter")]
  [HarmonyPatch(typeof(SoundStarter))]
  internal static class SoundStarter_Patch
  {
    [HarmonyPatch("PlayOneShotOnCamera")]
    [HarmonyPrefix]
    static bool PlayOneShotOnCamera_Patch()
    {
      return false;
    }
    [HarmonyPatch("PlayOneShot")]
    [HarmonyPrefix]
    static bool PlayOneShot_Patch()
    {
      return false;
    }
    [HarmonyPatch("TrySpawnSustainer")]
    [HarmonyPrefix]
    static bool TrySpawnSustainer_Patch(ref Sustainer __result)
    {
      __result = null;
      return false;
    }
    internal static void Unpatch()
    {
      FasterGameLoadingMod.harmony.UnpatchCategory("SoundStarter");
    }
  } 
}