using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
            ProfileMethods();

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
            //ParseType(typeof(ModContentLoaderTexture2D_LoadTexture_Patch));
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
            FasterGameLoadingSettings.DoSettingsWindowContents(inRect);
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
}

