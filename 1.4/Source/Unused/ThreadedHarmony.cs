using HarmonyLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Verse;

namespace FasterGameLoading
{
    [HarmonyPatch(typeof(PatchProcessor), nameof(PatchProcessor.Patch))]
    public static class PatchProcessor_Patch_Test
    {
        public static bool Prefix(PatchProcessor __instance, ref MethodInfo __result)
        {
            if (ThreadedHarmony.preventRecursion2 || ThreadedHarmony.shouldStop)
            {
                return true;
            }
            ThreadedHarmony.patchProcessorQueue.Enqueue(new(__instance, __result));
            return false;
        }
    }
    
    [HarmonyPatch(typeof(PatchClassProcessor), nameof(PatchClassProcessor.Patch))]
    public static class PatchClassProcessor_Threaded
    {
        public static bool Prefix(PatchClassProcessor __instance, ref List<MethodInfo> __result)
        {
            if (ThreadedHarmony.preventRecursion2 || ThreadedHarmony.shouldStop)
            {
                return true;
            }
            ThreadedHarmony.patchProcessorClassQueue.Enqueue(new(__instance, __result));
            return false;
        }
    }
    
    [HarmonyPatch(typeof(StaticConstructorOnStartupUtility), "CallAll")]
    public static class StaticConstructorOnStartupUtility_CallAll_Patch
    {
        public static void Postfix()
        {
            ThreadedHarmony.shouldStop = true;
        }
    }
    public class ThreadedHarmony
    {
        public static bool shouldStop;
        internal static readonly object locker = new();
        public static ConcurrentQueue<(PatchProcessor, MethodInfo)> patchProcessorQueue = new();
        public static ConcurrentQueue<(PatchClassProcessor, List<MethodInfo>)> patchProcessorClassQueue = new();
        public static bool preventRecursion1;
        public static bool preventRecursion2;
        public void Run()
        {
            while (true)
            {
                while (patchProcessorQueue.TryDequeue(out var patchProcessor))
                {
                    preventRecursion1 = true;
                    try
                    {
                        patchProcessor.Item2 = Patch(patchProcessor.Item1);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Exception " + ex);
                    }
                    preventRecursion1 = false;
                }
                while (patchProcessorClassQueue.TryDequeue(out var patchClassProcessor))
                {
                    preventRecursion2 = true;
                    try
                    {
                        patchClassProcessor.Item2 = Patch(patchClassProcessor.Item1);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Exception " + ex);
                    }
                    preventRecursion2 = false;
                }
    
                if (shouldStop)
                {
                    Thread.CurrentThread.Abort();
                    break;
                }
            }
        }
    
        public MethodInfo Patch(PatchProcessor processor)
        {
            lock (locker)
            {
                var patchInfo = HarmonySharedState.GetPatchInfo(processor.original) ?? new PatchInfo();
                patchInfo.AddPrefixes(processor.instance.Id, processor.prefix);
                patchInfo.AddPostfixes(processor.instance.Id, processor.postfix);
                patchInfo.AddTranspilers(processor.instance.Id, processor.transpiler);
                patchInfo.AddFinalizers(processor.instance.Id, processor.finalizer);
                var replacement = PatchFunctions.UpdateWrapper(processor.original, patchInfo);
                HarmonySharedState.UpdatePatchInfo(processor.original, replacement, patchInfo);
                return replacement;
            }
        }
    
        public List<MethodInfo> Patch(PatchClassProcessor processor)
        {
            if (processor.containerAttributes is null)
                return null;
    
            Exception exception = null;
    
            var mainPrepareResult = processor.RunMethod<HarmonyPrepare, bool>(true, false);
            if (mainPrepareResult is false)
            {
                processor.RunMethod<HarmonyCleanup>(ref exception);
                processor.ReportException(exception, null);
                return new List<MethodInfo>();
            }
    
            var replacements = new List<MethodInfo>();
            MethodBase lastOriginal = null;
            try
            {
                var originals = processor.GetBulkMethods();
    
                if (originals.Count == 1)
                    lastOriginal = originals[0];
                processor.ReversePatch(ref lastOriginal);
    
                replacements = originals.Count > 0 ? processor.BulkPatch(originals, ref lastOriginal) 
                    : processor.PatchWithAttributes(ref lastOriginal);
            }
            catch (Exception ex)
            {
                exception = ex;
            }
    
            processor.RunMethod<HarmonyCleanup>(ref exception, exception);
            processor.ReportException(exception, lastOriginal);
            return replacements;
        }
    }
}