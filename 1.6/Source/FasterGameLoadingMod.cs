using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld.IO;
using UnityEngine;
using Verse;
using Object = UnityEngine.Object;
namespace FasterGameLoading
{
    public class FasterGameLoadingMod : Mod
    {
        public static Harmony harmony;
        public static FasterGameLoadingSettings settings;
        public static LoadingActions loadingActions;
        public FasterGameLoadingMod(ModContentPack pack) : base(pack)
        {
            var gameObject = new GameObject("FasterGameLoadingMod");
            Object.DontDestroyOnLoad(gameObject);
            loadingActions = gameObject.AddComponent<LoadingActions>();
            settings = this.GetSettings<FasterGameLoadingSettings>();
            harmony = new Harmony("FasterGameLoadingMod");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            //if (FasterGameLoadingSettings.debugMode)
            //{
            //    ProfileTypes();
            //}
        }

        private void ProfileTypes()
        {
            var typesToProfile = new List<Type>
            {
                //typeof(ParseHelper),
                //typeof(StaticConstructorOnStartupUtility),
                //typeof(Harmony),
                //typeof(ImageConversion),
                //typeof(FilesystemFile),
                //typeof(Texture2D),
                //typeof(AccessTools),
                //typeof(AssetBundle),
                //typeof(ModContentPack),
                //typeof(GlobalTextureAtlasManager),
                //typeof(GenTypes),
                //typeof(XmlInheritance),
                //typeof(LoadedLanguage),
                //typeof(DirectXmlCrossRefLoader),
                //typeof(GenDefDatabase),
                //typeof(ParseHelper),
                //typeof(PlayDataLoader),
                typeof(DefDatabase<ThingDef>),
                typeof(DirectXmlLoader),
                typeof(GraphicDatabase),
                typeof(GraphicData),
            };
            PerformanceProfiling.harmony = harmony;
            PerformanceProfiling.ProfileTypes(typesToProfile.Distinct().ToHashSet());
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

