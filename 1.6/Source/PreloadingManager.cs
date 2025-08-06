using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FasterGameLoading
{
    public static class PreloadingManager
    {
        public static Task PreloadTask;
        public static void StartPreloading()
        {
            ReflectionCacheManager.StartPreloading();
            PreloadTask = XmlCacheManager.StartPreloadingCache();
            TexturePreloader.Start();
        }
    }
}
