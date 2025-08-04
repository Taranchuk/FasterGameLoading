using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.IO;
using RuntimeAudioClipLoader;
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
            PreloadingManager.StartPreloading();
            //if (FasterGameLoadingSettings.debugMode)
            //{
            //    ProfileTypes();
            //}
        }

        private void ProfileTypes()
        {
            var typesToProfile = new List<Type>
            {
                typeof(ParseHelper),
                typeof(StaticConstructorOnStartupUtility),
                typeof(Harmony),
                typeof(ImageConversion),
                typeof(FilesystemFile),
                typeof(Texture2D),
                typeof(AccessTools),
                typeof(AssetBundle),
                typeof(GlobalTextureAtlasManager),
                typeof(GenTypes),
                typeof(XmlInheritance),
                typeof(LoadedLanguage),
                typeof(DirectXmlCrossRefLoader),
                typeof(GenDefDatabase),
                typeof(PlayDataLoader),
                typeof(ModLister),
                typeof(Mod),
                typeof(DirectXmlLoader),
                typeof(DefInjectionPackage),
                typeof(DefGenerator),
                typeof(PlayerKnowledgeDatabase),
                typeof(KeyPrefs),
                typeof(Prefs),
                typeof(ShortHashGiver),
                typeof(ModContentLoader<AudioClip>),
                typeof(Manager),
                typeof(BackstoryTranslationUtility),
            };
            typesToProfile.AddRange(GenTypes.AllSubclasses(typeof(Def)));
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

