using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
using Verse;

namespace FasterGameLoading
{
    public class FasterGameLoadingMod : Mod
    {
        public static Harmony harmony;
        public static FasterGameLoadingSettings settings;
        public static Thread threadPostLoad;
        public FasterGameLoadingMod(ModContentPack pack) : base(pack)
        {
            settings = this.GetSettings<FasterGameLoadingSettings>();
            harmony = new Harmony("FasterGameLoadingMod");
            harmony.PatchAll();
            //ProfileMethods();

            // an attempt to put harmony patchings into another thread, didn't work out by some reason
            //thread = new Thread(new ThreadStart(() =>
            //{
            //    var threadedHarmony = new ThreadedHarmony();
            //    threadedHarmony.Run();
            //}));
            //thread.Start();
        }
        private static void ProfileMethods()
        {
            //ParseType(typeof(DirectXmlCrossRefLoader));
            //ParseType(typeof(GenDefDatabase));
            //ParseType(typeof(ParseHelper));
            //ParseType(typeof(DirectXmlLoader));
            //ParseType(typeof(GraphicData));
            //ParseType(typeof(DirectXmlToObject));
            //ParseType(typeof(GenGeneric));
            //ParseType(typeof(ConvertHelper));
            //foreach (var subType in typeof(Def).AllSubclasses())
            //{
            //    ParseType(subType);
            //}
            //ParseType(typeof(Graphic));
            //foreach (var subType in typeof(Graphic).AllSubclasses())
            //{
            //    ParseType(subType);
            //}
            //ParseType(typeof(ModAssemblyHandler));
            //ParseType(typeof(ModContentPack));
            //ParseType(typeof(ModsConfig));
            //ParseType(typeof(GenTypes));
            //ParseType(typeof(LoadableXmlAsset));
            //ParseType(typeof(XmlInheritance));
            //ParseType(typeof(LoadedLanguage));

        }

        public static void ParseType(Type type)
        {
            HashSet<MethodInfo> methods = new HashSet<MethodInfo>();
            foreach (var mi in AccessTools.GetDeclaredMethods(type))
            {
                methods.Add(mi);
            }
            foreach (var mi in methods)
            {
                TryProfileMethod(mi);
            }
            //foreach (var subType in type.GetNestedTypes(AccessTools.all))
            //{
            //    ParseType(subType);
            //}
        }
        public static void ParseMethod(MethodInfo method)
        {
            HashSet<MethodInfo> methods = new HashSet<MethodInfo>();
            HashSet<Type> types = new HashSet<Type>();
            try
            {

                List<CodeInstruction> instructions = PatchProcessor.GetCurrentInstructions(method);
                foreach (CodeInstruction instr in instructions)
                {
                    if (instr.operand is MethodInfo mi)
                    {
                        _ = mi.GetParameters().Length;
                        _ = mi.GetUnderlyingType();
                        methods.Add(mi);
                        types.Add(mi.DeclaringType);
                    }
                }
            }
            catch (Exception ex)
            {
            }

            foreach (var type in types)
            {
                foreach (var mi in AccessTools.GetDeclaredMethods(type))
                {
                    methods.Add(mi);
                }
            }

            foreach (var mi in methods)
            {
                if (mi.DeclaringType.Assembly == typeof(Game).Assembly && mi.DeclaringType != typeof(PawnApparelGenerator) && mi.Name.Contains("Reset") is false)
                {
                    TryProfileMethod(mi);
                }
            }
        }

        public static void TryProfileMethod(MethodInfo mi)
        {
            if (mi.HasMethodBody() && mi.DeclaringType.IsConstructedGenericType is false &&
                    mi.IsGenericMethod is false && mi.ContainsGenericParameters is false && mi.IsGenericMethodDefinition is false)
            {
                var desc = mi.FullDescription();
                if (desc.Contains("LoadoutGenericDef")
                    || desc.Contains("Harmony_WarmupComplete")
                    || desc.Contains("Harmony_Fire_SpreadInterval")
                    || desc.Contains("Harmony_RegenerateLayers"))
                {
                    return;
                }
                ProfileMethod(mi);
            }
        }

        public override string SettingsCategory()
        {
            return this.Content.Name;
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            var ls = new Listing_Standard();
            ls.Begin(new Rect(inRect.x, inRect.y, inRect.width, 500));
            ls.CheckboxLabeled("Disable static atlases backing. Will cut some time off during loading, but might make map rendering perform a bit slower.", ref FasterGameLoadingSettings.disableStaticAtlasesBaking);
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
                TextureResize.DoTextureResizing();
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

        public static ConcurrentDictionary<MethodBase, StopwatchData> stopwatches = new ConcurrentDictionary<MethodBase, StopwatchData>();
        private static HarmonyMethod profilePrefix = new HarmonyMethod(AccessTools.Method(typeof(FasterGameLoadingMod), nameof(ProfileMethodPrefix)));
        private static HarmonyMethod profilePostfix = new HarmonyMethod(AccessTools.Method(typeof(FasterGameLoadingMod), nameof(ProfileMethodPostfix)));
        private static void ProfileMethod(MethodInfo methodInfo)
        {
            try
            {
                //Log.Message("Profiling " + methodInfo.FullDescription());
                harmony.Patch(methodInfo, prefix: profilePrefix, postfix: profilePostfix);
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }
        public static void ProfileMethodPrefix(MethodBase __originalMethod, out StopwatchData __state)
        {
            if (stopwatches.TryGetValue(__originalMethod, out __state) is false)
            {
                stopwatches[__originalMethod] = __state = new StopwatchData(__originalMethod);
            }
            __state.Start();
        }
        public static void ProfileMethodPostfix(StopwatchData __state)
        {
            __state.Stop();
        }
    }

    public class FasterGameLoadingSettings : ModSettings
    {
        public static Dictionary<string, string> loadedTexturesSinceLastSession = new Dictionary<string, string>();
        public static Dictionary<string, ModContentPack> modsByPackageIds = new Dictionary<string, ModContentPack>();
        public static Dictionary<string, string> loadedTypesByFullNameSinceLastSession = new Dictionary<string, string>();

        public static List<string> modsInLastSession = new List<string>();
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
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref loadedTexturesSinceLastSession, "loadedTexturesSinceLastSession", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref loadedTypesByFullNameSinceLastSession, "loadedTypesByFullNameSinceLastSession", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref modsInLastSession, "modsInLastSession", LookMode.Value);
            Scribe_Values.Look(ref disableStaticAtlasesBaking, "disableStaticAtlasesBaking");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                loadedTexturesSinceLastSession ??= new Dictionary<string, string>();
                loadedTypesByFullNameSinceLastSession ??= new Dictionary<string, string>();
                modsInLastSession ??= new List<string>();
                if (!modsInLastSession.SequenceEqual(ModsConfig.ActiveModsInLoadOrder.Select(x => x.packageIdLowerCase)))
                {
                    loadedTexturesSinceLastSession.Clear();
                    loadedTypesByFullNameSinceLastSession.Clear();
                }
            }
        }
    }
}

