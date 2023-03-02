using HarmonyLib;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Xml;
using UnityEngine;
using Verse;
namespace FasterGameLoading
{
    public class FasterGameLoadingMod : Mod
    {
        public static Harmony harmony;
        public static FasterGameLoadingSettings settings;
        public static Thread threadPostLoad;
        public static DelayedActions delayedActions;
        public FasterGameLoadingMod(ModContentPack pack) : base(pack)
        {
            var gameObject = new GameObject("FasterGameLoadingMod");
            Object.DontDestroyOnLoad(gameObject);
            delayedActions = gameObject.AddComponent<DelayedActions>();
            settings = this.GetSettings<FasterGameLoadingSettings>();
            harmony = new Harmony("FasterGameLoadingMod");
            harmony.PatchAll();
            if (FasterGameLoadingSettings.delayLongEventActionsLoading)
            {
                harmony.Patch(AccessTools.DeclaredMethod(typeof(LongEventHandler), nameof(LongEventHandler.ExecuteWhenFinished)),
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(Startup), nameof(Startup.DelayExecuteWhenFinished))));
                Startup.doNotDelayLongEventsWhenFinished = false;
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
    }
}

