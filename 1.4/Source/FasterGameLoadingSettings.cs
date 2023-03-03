﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FasterGameLoading
{
    public class FasterGameLoadingSettings : ModSettings
    {
        public static Dictionary<string, string> loadedTexturesSinceLastSession = new Dictionary<string, string>();
        public static Dictionary<string, ModContentPack> modsByPackageIds = new Dictionary<string, ModContentPack>();
        public static Dictionary<string, string> loadedTypesByFullNameSinceLastSession = new Dictionary<string, string>();
        public static List<string> modsInLastSession = new List<string>();
        public static HashSet<string> successfulXMLPathesSinceLastSession = new HashSet<string>();
        public static HashSet<string> failedXMLPathesSinceLastSession = new HashSet<string>();
        public static bool delayLongEventActionsLoading = true;
        public static bool delayHarmonyPatchesLoading = true;
        public static bool delayGraphicLoading = true;
        public static bool earlyModContentLoading = true;
        public static bool disableStaticAtlasesBaking;
        public static ModContentPack GetModContent(string packageId)
        {
            var packageLower = packageId.ToLower();
            if (!modsByPackageIds.TryGetValue(packageLower, out var mod))
            {
                modsByPackageIds[packageLower] = mod = LoadedModManager.RunningModsListForReading.FirstOrDefault(x =>
                    x.PackageIdPlayerFacing.ToLower() == packageLower);
            }
            return mod;
        }

        public static void DoSettingsWindowContents(Rect inRect)
        {
            var ls = new Listing_Standard();
            ls.Begin(new Rect(inRect.x, inRect.y, inRect.width, 500));
            ls.CheckboxLabeled("Load mod content early during game idling periods. When enabled, the game might become not responsive during loading, but it's expected. Disable this if you will encounter any issues.", ref earlyModContentLoading);
            ls.CheckboxLabeled("Prevent long event loading during startup and load them gradually during playing. Will cut some time off during loading, however it might be not stable and error prone. Disable this if you will encounter any issues.", ref delayLongEventActionsLoading);
            ls.CheckboxLabeled("Prevent harmony patches loading during startup and load them gradually during playing. Will cut some time off during loading, however it might be not stable and error prone. Disable this if you will encounter any issues.", ref delayHarmonyPatchesLoading);
            ls.CheckboxLabeled("Prevent graphic and icon loading during startup and load them gradually during playing. Will cut some time off during loading, however it might be not stable and error prone. Disable this if you will encounter any issues.", ref delayGraphicLoading);
            ls.CheckboxLabeled("Disable static atlases backing. Will cut some time off during loading, but might make map rendering perform a bit slower.", ref disableStaticAtlasesBaking);
            ls.GapLine();
            var explanation = "Some mods may contain a lot of high-res textures that take a long time to load. Use this to downscale hi-res textures. " +
                "Additionally, dds files generated by RimPy will be deleted alongside, so you can perform texture compression by this tool again. " +
                "Following textures will be reduced down to target size: " +
                "\nBuilding - 256px" +
                "\nPawn - 256px" +
                "\nApparel - 128px " +
                "\nWeapon - 128px" +
                "\nItem - 128px" +
                "\nPlant - 128px" +
                "\nTree - 256px" +
                "\nTerrain - 1024px";
            if (ls.ButtonTextLabeled(explanation, "Downscale textures"))
            {
                Find.WindowStack.Add(new Dialog_MessageBox("Perform texture downscaling? It can be reverted by redownloading mods.", "Confirm".Translate(), delegate
                {
                    TextureResize.DoTextureResizing();
                }, "GoBack".Translate()));
            }
            ls.End();
        }

        public bool ButtonText(Listing_Standard ls, string label, string tooltip = null, float widthPct = 1f)
        {
            Rect rect = ls.GetRect(30f, widthPct);
            bool result = false;
            if (!ls.BoundingRectCached.HasValue || rect.Overlaps(ls.BoundingRectCached.Value))
            {
                result = Widgets.ButtonText(rect, label);
                if (tooltip != null)
                {
                    TooltipHandler.TipRegion(rect, tooltip);
                }
            }
            ls.Gap(ls.verticalSpacing);
            return result;
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref loadedTexturesSinceLastSession, "loadedTexturesSinceLastSession", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref loadedTypesByFullNameSinceLastSession, "loadedTypesByFullNameSinceLastSession", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref successfulXMLPathesSinceLastSession, "successfulXMLPathesSinceLastSession", LookMode.Value);
            Scribe_Collections.Look(ref failedXMLPathesSinceLastSession, "failedXMLPathesSinceLastSession", LookMode.Value);
            Scribe_Collections.Look(ref modsInLastSession, "modsInLastSession", LookMode.Value);
            Scribe_Values.Look(ref disableStaticAtlasesBaking, "disableStaticAtlasesBaking");
            Scribe_Values.Look(ref delayGraphicLoading, "delayGraphicLoading", true);
            Scribe_Values.Look(ref delayLongEventActionsLoading, "delayLongEventActionsLoading", true);
            Scribe_Values.Look(ref delayHarmonyPatchesLoading, "delayHarmonyPatchesLoading", true);
            Scribe_Values.Look(ref earlyModContentLoading, "earlyModContentLoading", true);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                loadedTexturesSinceLastSession ??= new Dictionary<string, string>();
                loadedTypesByFullNameSinceLastSession ??= new Dictionary<string, string>();
                failedXMLPathesSinceLastSession ??= new HashSet<string>();
                successfulXMLPathesSinceLastSession ??= new HashSet<string>();
                modsInLastSession ??= new List<string>();
                if (!modsInLastSession.SequenceEqual(ModsConfig.ActiveModsInLoadOrder.Select(x => x.packageIdLowerCase)))
                {
                    loadedTexturesSinceLastSession.Clear();
                    loadedTypesByFullNameSinceLastSession.Clear();
                    failedXMLPathesSinceLastSession.Clear();
                    successfulXMLPathesSinceLastSession.Clear();
                }
            }
        }
    }
}

