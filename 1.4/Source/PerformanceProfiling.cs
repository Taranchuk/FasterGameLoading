using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Verse;

namespace PerformanceProfiling
{
    [HarmonyPatch(typeof(StaticConstructorOnStartupUtility), nameof(StaticConstructorOnStartupUtility.CallAll))]
    public static class StartupProfiler
    {
        public static void Prefix()
        {
            foreach (var type in PerformanceProfiling.profileUponStartup)
            {
                PerformanceProfiling.ParseType(type);
            }
        }
    }
    [HarmonyPatch(typeof(EditWindow_Log), MethodType.Constructor)]
    internal static class EditWindow_Log_Patch
    {
        private static void Postfix()
        {
            if (PerformanceProfiling.stopwatches.Any())
            {
                foreach (var stopwatch in PerformanceProfiling.stopwatches.OrderByDescending(x => x.Value.totalTime))
                {
                    stopwatch.Value.LogTime();
                }
            }
        }
    }

    public class StopwatchData
    {
        public float totalTime;
        public float count;
        public MethodBase targetMethod;
        public string name;
        public Stopwatch stopwatch;
        public StopwatchData(MethodBase targetMethod)
        {
            this.targetMethod = targetMethod;
            this.stopwatch = new Stopwatch();
        }

        public StopwatchData(string name)
        {
            this.name = name;
            this.stopwatch = new Stopwatch();
        }
        public void Start()
        {
            stopwatch.Restart();
        }

        public void Stop()
        {
            stopwatch.Stop();
            var elapsed = (float)stopwatch.ElapsedTicks / Stopwatch.Frequency;
            count++;
            totalTime += elapsed;
        }

        public void LogTime()
        {
            //if (totalTime >= 0.01f)
            {
                if (targetMethod != null)
                {
                    Log.Message(targetMethod.DeclaringType.FullName + "." + targetMethod.Name + " - " + string.Join(", ", targetMethod.GetParameters().Select(x => x.ParameterType)) + " took " + totalTime + ", run count: " + count);
                }
                else
                {
                    if (count > 1)
                    {
                        Log.Message(name + " took " + totalTime + ", run count: " + count + " - average time: " + totalTime / count);
                    }
                    else
                    {
                        Log.Message(name + " took " + totalTime);
                    }
                }
            }
        }
    }
    public static class PerformanceProfiling
    {
        public static Harmony harmony;
        public static HashSet<Type> profileUponStartup = new HashSet<Type>();
        static PerformanceProfiling()
        {
            Log.TryOpenLogWindow();
        }

        public static void ProfileMod(string packageID)
        {
            var mod = LoadedModManager.RunningMods.FirstOrDefault(x => x.PackageIdPlayerFacing == packageID);
            if (mod != null)
            {
                foreach (var assembly in mod.assemblies.loadedAssemblies)
                {
                    var types = assembly.GetTypes().Where(x => x.HasAttribute<CompilerGeneratedAttribute>() is false).ToHashSet();
                    ProfileTypes(types);
                }
            }
        }

        public static void ProfileTypes(HashSet<Type> typesToParse)
        {
            typesToParse = typesToParse.Except(typeof(PerformanceProfiling))
                .Except(typeof(StopwatchData)).ToHashSet();
            foreach (var type in typesToParse)
            {
                try
                {
                    if (type.HasAttribute<StaticConstructorOnStartup>())
                    {
                        profileUponStartup.Add(type);
                    }
                    else
                    {
                        ParseType(type);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Exception parsing " + type);
                }
            }
        }

        public static void ParseType(Type type)
        {
            HashSet<MethodBase> methods = new HashSet<MethodBase>();
            try
            {
                foreach (var mi in AccessTools.GetDeclaredMethods(type))
                {
                    methods.Add(mi);
                }
                foreach (var mi in AccessTools.GetDeclaredConstructors(type, true))
                {
                    methods.Add(mi);
                }
                foreach (var mi in AccessTools.GetDeclaredConstructors(type, false))
                {
                    methods.Add(mi);
                }
                foreach (var mi in methods)
                {
                    TryProfileMethod(mi);
                }
            }
            catch (Exception ex)
            {

            }
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

        public static void TryProfileMethod(MethodBase mi)
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
                Log.Message("Profiling " + mi.FullDescription());
                ProfileMethod(mi);
            }
            else
            {
                Log.Message("Cannot profile " + mi.FullDescription());
            }
        }


        public static ConcurrentDictionary<MethodBase, StopwatchData> stopwatches = new ConcurrentDictionary<MethodBase, StopwatchData>();
        private static HarmonyMethod profilePrefix = new HarmonyMethod(AccessTools.Method(typeof(PerformanceProfiling), nameof(ProfileMethodPrefix)));
        private static HarmonyMethod profilePostfix = new HarmonyMethod(AccessTools.Method(typeof(PerformanceProfiling), nameof(ProfileMethodPostfix)));
        private static void ProfileMethod(MethodBase methodInfo)
        {
            try
            {
                if (methodInfo.IsStatic && methodInfo.Name.Contains("cctor"))
                {
                    ProfileMethodPrefix(methodInfo, out var state);
                    RuntimeHelpers.RunClassConstructor(methodInfo.DeclaringType.TypeHandle);
                    ProfileMethodPostfix(state);
                }
                else
                {
                    harmony.Patch(methodInfo, prefix: profilePrefix, postfix: profilePostfix);
                }
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

