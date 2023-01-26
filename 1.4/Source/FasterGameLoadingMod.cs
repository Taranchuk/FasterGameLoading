using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public FasterGameLoadingMod(ModContentPack pack) : base(pack)
        {
            settings = this.GetSettings<FasterGameLoadingSettings>();
            harmony = new Harmony("FasterGameLoadingMod");
            harmony.PatchAll();

            // an attempt to put harmony patchings into another thread, didn't work out by some reason
            //thread = new Thread(new ThreadStart(() =>
            //{
            //    var threadedHarmony = new ThreadedHarmony();
            //    threadedHarmony.Run();
            //}));
            //thread.Start();
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

