using HarmonyLib;
using System;
using System.Collections.Generic;
using Verse;
using Verse.Sound;

namespace FasterGameLoading
{
  // Blocks play sound errors before SubSoundDef Resolved
  [HarmonyPatchCategory("SoundStarter")]
  [HarmonyPatch]
  internal static class SoundStarter_Patch
  {
    [HarmonyPatch(typeof(SoundStarter))]
    [HarmonyPatch("PlayOneShotOnCamera")]
    [HarmonyPrefix]
    static bool PlayOneShotOnCamera_Patch()
    {
      return false;
    }
    [HarmonyPatch(typeof(SoundStarter))]
    [HarmonyPatch("PlayOneShot")]
    [HarmonyPrefix]
    static bool PlayOneShot_Patch()
    {
      return false;
    }
    [HarmonyPatch(typeof(SoundStarter))]
    [HarmonyPatch("TrySpawnSustainer")]
    [HarmonyPrefix]
    static bool TrySpawnSustainer_Patch(ref Sustainer __result)
    {
      __result = null;
      return false;
    }
    [HarmonyPatch(typeof(SubSoundDef), nameof(SubSoundDef.TryPlay))]
    [HarmonyPrefix]
    static bool TryPlay_Patch()
    {
      return false;
    }
    internal static void Unpatch()
    {
      FasterGameLoadingMod.harmony.UnpatchCategory("SoundStarter");
    }
  }


}