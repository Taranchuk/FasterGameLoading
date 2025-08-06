using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Verse;

namespace FasterGameLoading
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
            PerformanceProfiling.stopProfiling = true;
        }
    }

    public static class PerformanceProfiling
    {
        public static bool stopProfiling;
        public static Harmony harmony;
        public static HashSet<Type> profileUponStartup = new HashSet<Type>();
        static PerformanceProfiling()
        {
            Log.TryOpenLogWindow();
        }
        public static bool ValidAssembly(Assembly assembly)
        {
            if (assembly.FullName.Contains("0Harmony")) return false;
            if (assembly.FullName.Contains("Cecil")) return false;
            if (assembly.FullName.Contains("Multiplayer")) return false;
            if (assembly.FullName.Contains("UnityEngine")) return false;

            return true;
        }
        public static void ProfileMod(string packageID)
        {
            var mod = LoadedModManager.RunningMods.FirstOrDefault(x => x.PackageIdPlayerFacing == packageID);
            if (mod != null)
            {
                foreach (var assembly in mod.assemblies.loadedAssemblies)
                {
                    if (ValidAssembly(assembly))
                    {
                        var types = assembly.GetTypes().Where(x => x.HasAttribute<CompilerGeneratedAttribute>() is false).ToHashSet();
                        ProfileTypes(types);
                    }
                }
            }
        }

        public static void ProfileTypes(HashSet<Type> typesToParse)
        {
            typesToParse = typesToParse.Except(typeof(PerformanceProfiling)).ToHashSet();
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
                catch (Exception)
                {
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
            catch (Exception)
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
            catch (Exception)
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

                if (mi.IsAbstract || mi.IsGenericMethod || mi.IsSpecialName) return;

                try
                {
                    var tracker = new LoadingActions.ProfilingTracker
                    {
                        methodName = mi.Name,
                        declaringType = mi.DeclaringType
                    };
                    if (LoadingActions.profiledMethods.TryAdd(mi, tracker))
                    {
                        LoadingActions.profilingTrackers.Add(tracker);
                        ProfileMethod(mi);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to patch method {mi.DeclaringType.Name}.{mi.Name}: {ex.Message}");
                }
            }
        }

        private static HarmonyMethod profilePrefix = new HarmonyMethod(AccessTools.Method(typeof(PerformanceProfiling), nameof(MethodProfilingPrefix)));
        private static HarmonyMethod profilePostfix = new HarmonyMethod(AccessTools.Method(typeof(PerformanceProfiling), nameof(MethodProfilingPostfix)));
        [ThreadStatic]
        private static Stack<LoadingActions.ProfilingTracker> callStack;
        private static void ProfileMethod(MethodBase methodInfo)
        {
            if (methodInfo.IsStatic && methodInfo.Name.Contains("cctor"))
            {
                long state = 0;
                MethodProfilingPrefix(methodInfo, ref state);
                RuntimeHelpers.RunClassConstructor(methodInfo.DeclaringType.TypeHandle);
                MethodProfilingPostfix(methodInfo, state);
            }
            else
            {
                harmony.Patch(methodInfo, prefix: profilePrefix, postfix: profilePostfix);
            }
        }
        private static void MethodProfilingPrefix(MethodBase __originalMethod, ref long __state)
        {
            if (callStack is null)
            {
                callStack = new Stack<LoadingActions.ProfilingTracker>();
            }
            if (!stopProfiling && LoadingActions.profiledMethods.TryGetValue(__originalMethod, out var tracker))
            {
                callStack.Push(tracker);
                __state = Stopwatch.GetTimestamp();
            }
        }

        private static void MethodProfilingPostfix(MethodBase __originalMethod, long __state)
        {
            if (callStack != null && !stopProfiling && LoadingActions.profiledMethods.TryGetValue(__originalMethod, out var tracker))
            {
                long elapsedTicks = Stopwatch.GetTimestamp() - __state;
                tracker.totalTicks += elapsedTicks;
                tracker.callCount++;
                callStack.Pop();
                if (callStack.Count > 0)
                {
                    var parent = callStack.Peek();
                    parent.childrenTicks += elapsedTicks;
                }
            }
        }
    }
}
