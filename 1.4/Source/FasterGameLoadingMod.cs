using HarmonyLib;
using System;
using System.Collections.Generic;
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

            ProfileStartupPerformance();

            // an attempt to put harmony patchings into another thread, didn't work out by some reason
            //thread = new Thread(new ThreadStart(() =>
            //{
            //    var threadedHarmony = new ThreadedHarmony();
            //    threadedHarmony.Run();
            //}));
            //thread.Start();
        }

        private static void ProfileStartupPerformance()
        {
            PerformanceProfiling.PerformanceProfiling.harmony = harmony;
            PerformanceProfiling.PerformanceProfiling.ProfileTypes(GetTypesToProfile());

            //PerformanceProfiling.ProfileMod("OskarPotocki.VanillaFactionsExpanded.Core");
            //PerformanceProfiling.ProfileMod("CETeam.CombatExtended");
        }

        private static HashSet<Type> GetTypesToProfile()
        {
            HashSet<Type> typesToParse = new HashSet<Type>
            {
                //typeof(XmlNode),
                //typeof(LoadableXmlAsset),
                //typeof(DirectXmlCrossRefLoader),
                //typeof(TextureAtlasHelper),
                //typeof(GenDefDatabase),
                //typeof(ParseHelper),
                //typeof(DirectXmlLoader),
                //typeof(GraphicData),
                //typeof(DirectXmlToObject),
                //typeof(GenGeneric),
                //typeof(ConvertHelper),
                //typeof(Graphic),
                //typeof(ModAssemblyHandler),
                //typeof(ModContentPack),
                //typeof(ModsConfig),
                //typeof(ModLister),
                //typeof(BackCompatibility),
                //typeof(MaterialPool),
                //typeof(GlobalTextureAtlasManager),
                //typeof(GraphicDatabase),
                //typeof(GenTypes),
                //typeof(XmlInheritance),
                //typeof(LoadedLanguage),
            };

            //foreach (var subType in typeof(Def).AllSubclasses())
            //{
            //    typesToParse.Add(subType);
            //}
            //foreach (var subType in typeof(Graphic).AllSubclasses())
            //{
            //    typesToParse.Add(subType);
            //}

            return typesToParse;
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

