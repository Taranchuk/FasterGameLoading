using HarmonyLib;
using System.Reflection;

namespace FasterGameLoading
{
    [HarmonyPatch]
    public static class DisableLogObsoleteMethodPatchErrors
    {
        public static MethodBase targetMethod;
        public static bool Prepare()
        {
            targetMethod = AccessTools.Method("HugsLib.Utils.HarmonyUtility:LogObsoleteMethodPatchErrors");
            return targetMethod != null;
        }
        public static MethodBase TargetMethod()
        {
            return targetMethod;
        }

        public static bool Prefix()
        {
            return false;
        }
    }
}

