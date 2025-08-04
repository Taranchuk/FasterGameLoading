using RimWorld.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Verse;

namespace FasterGameLoading
{
    public static class TexturePreloader
    {
        public static ConcurrentDictionary<string, byte[]> preloadedTextures = new ConcurrentDictionary<string, byte[]>();

        public static void Start()
        {
            Task.Run(() =>
            {
                var textureFiles = new List<FileInfo>();
                foreach (var mod in LoadedModManager.RunningMods)
                {
                    var modFiles = ModContentPack.GetAllFilesForMod(mod, "Textures/", (string e) => e.ToLower() == ".png");
                    textureFiles.AddRange(modFiles.Values);
                }

                Parallel.ForEach(textureFiles, file =>
                {
                    preloadedTextures[file.FullName] = File.ReadAllBytes(file.FullName);
                });
            });
        }
    }
}
