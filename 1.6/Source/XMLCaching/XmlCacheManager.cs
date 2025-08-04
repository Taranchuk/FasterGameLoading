using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Verse;
using System.Diagnostics;

namespace FasterGameLoading
{
    public static class XmlCacheManager
    {
        private static bool cacheActive;
        private const long MinFreeSpaceForCaching = 1L * 1024 * 1024 * 1024;
        private static bool loggedLowSpaceWarning = false;
        public static bool XMLCacheLoaded { get; set; }
        public static bool CacheIsActive => cacheActive;
        public static string CacheDirectory => Path.Combine(GenFilePaths.SaveDataFolderPath, "FGL_Cache");
        public static string XMLCachePath => Path.Combine(CacheDirectory, "XmlCache.xml");
        public static Dictionary<string, string> currentFileHashes = new Dictionary<string, string>();

        public static void ActivateCache()
        {
            cacheActive = true;
        }

        public static void DeactivateCache()
        {
            cacheActive = false;
        }

        public static void InvalidateCache()
        {
            DeactivateCache();
            if (File.Exists(XMLCachePath))
            {
                File.Delete(XMLCachePath);
            }
        }

        public static void Reset()
        {
            DeactivateCache();
            currentFileHashes.Clear();
            XMLCacheLoaded = false;
        }

        public static void HydrateLookupsFrom(XmlDocument doc, Dictionary<XmlNode, LoadableXmlAsset> assetLookup)
        {
            DeepProfiler.Start("FGL: HydrateLookupsFrom");
            assetLookup.Clear();
            if (doc.DocumentElement == null)
            {
                DeepProfiler.End();
                return;
            }
            foreach (XmlNode node in doc.DocumentElement.ChildNodes)
            {
                if (node is XmlElement element)
                {
                    string modId = element.GetAttribute("FGL_SourceMod");
                    string filePath = element.GetAttribute("FGL_SourceFile");
                    string assetName = element.GetAttribute("FGL_AssetName");
                    if (!string.IsNullOrEmpty(modId) || !string.IsNullOrEmpty(filePath) || !string.IsNullOrEmpty(assetName))
                    {
                        assetLookup[node] = CreateFauxAsset(assetName, modId, filePath);
                    }
                }
            }
            DeepProfiler.End();
        }

        public static void SaveXMLCache(XmlDocument doc, Dictionary<XmlNode, LoadableXmlAsset> assetLookup)
        {
            DeepProfiler.Start("FGL: SaveXMLCache");
            if (!VerifyDiskSpaceForCaching()) return;

            Directory.CreateDirectory(CacheDirectory);
            var sw = Stopwatch.StartNew();
            AddFGLAttributes(doc, assetLookup);
            sw.Stop();
            Log.Warning($"[FasterGameLoading] Took {sw.ElapsedMilliseconds}ms to add cache attributes to doc.");
            var writerSettings = new XmlWriterSettings { CheckCharacters = false, Indent = false, NewLineHandling = NewLineHandling.None };
            using (var writer = XmlWriter.Create(XMLCachePath, writerSettings))
            {
                doc.Save(writer);
            }
            sw.Restart();
            RemoveFGLAttributes(doc);
            sw.Stop();
            Log.Warning($"[FasterGameLoading] Took {sw.ElapsedMilliseconds}ms to remove cache attributes from doc.");
            DeepProfiler.End();
        }

        public static bool TryLoadXMLCache(out XmlDocument doc)
        {
            DeepProfiler.Start("FGL: TryLoadXMLCache");
            doc = new XmlDocument();
            if (!File.Exists(XMLCachePath))
            {
                DeepProfiler.End();
                return false;
            }
            try
            {
                using (var stream = File.OpenRead(XMLCachePath))
                {
                    var readerSettings = new XmlReaderSettings
                    {
                        IgnoreComments = true,
                        IgnoreWhitespace = true,
                        CheckCharacters = false,
                        DtdProcessing = DtdProcessing.Ignore
                    };
                    using (var xmlReader = XmlReader.Create(stream, readerSettings))
                    {
                        doc.Load(xmlReader);
                    }
                }

                DeepProfiler.End();
                return true;
            }
            catch (System.Exception e)
            {
                Log.Error($"Error loading XML cache, rebuilding. Error: {e}");
                File.Delete(XMLCachePath);
                DeepProfiler.End();
                return false;
            }
        }

        private static readonly AccessTools.FieldRef<LoadableXmlAsset, ModContentPack> FauxAssetModSetter =
            AccessTools.FieldRefAccess<LoadableXmlAsset, ModContentPack>("mod");
        private static readonly AccessTools.FieldRef<LoadableXmlAsset, string> FauxAssetPathSetter =
            AccessTools.FieldRefAccess<LoadableXmlAsset, string>("fullFolderPath");

        public static LoadableXmlAsset CreateFauxAsset(string name, string modId, string fullPath)
        {
            ModContentPack mod = FasterGameLoadingSettings.GetModContent(modId);
            var fauxAsset = new LoadableXmlAsset(name, "<Def/>");
            FauxAssetModSetter(fauxAsset) = mod;
            FauxAssetPathSetter(fauxAsset) = string.IsNullOrEmpty(fullPath) ? null : Path.GetDirectoryName(fullPath);
            return fauxAsset;
        }

        public static bool VerifyDiskSpaceForCaching()
        {
            try
            {
                DriveInfo drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(CacheDirectory)));
                if (drive.AvailableFreeSpace < MinFreeSpaceForCaching)
                {
                    if (!loggedLowSpaceWarning)
                    {
                        Log.Warning($"[FasterGameLoading] Not enough free space on drive {drive.Name} to cache XML files. Invalidating and deactivating cache. Available: {drive.AvailableFreeSpace / (1024 * 1024)} MB, Required: {MinFreeSpaceForCaching / (1024 * 1024)} MB.");
                        InvalidateCache();
                        loggedLowSpaceWarning = true;
                    }
                    return false;
                }
            }
            catch (Exception e)
            {
                Log.Error($"[FasterGameLoading] Error checking for free disk space: {e}");
                return false;
            }
            return true;
        }
        public static string GenerateSha256Hash(string input)
        {
            DeepProfiler.Start("FGL: GenerateSha256Hash");
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                var builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                DeepProfiler.End();
                return builder.ToString();
            }
        }

        private static void AddFGLAttributes(XmlDocument doc, Dictionary<XmlNode, LoadableXmlAsset> assetLookup)
        {
            if (doc.DocumentElement == null) return;
            foreach (XmlNode node in doc.DocumentElement.ChildNodes)
            {
                if (assetLookup.TryGetValue(node, out var asset) && asset != null && node is XmlElement element)
                {
                    element.SetAttribute("FGL_SourceMod", asset.mod?.PackageIdPlayerFacing ?? "");
                    element.SetAttribute("FGL_SourceFile", asset.FullFilePath ?? "");
                    element.SetAttribute("FGL_AssetName", asset.name);
                }
            }
        }

        private static void RemoveFGLAttributes(XmlDocument doc)
        {
            if (doc.DocumentElement == null) return;
            foreach (XmlNode node in doc.DocumentElement.ChildNodes)
            {
                if (node is XmlElement element)
                {
                    element.RemoveAttribute("FGL_SourceMod");
                    element.RemoveAttribute("FGL_SourceFile");
                    element.RemoveAttribute("FGL_AssetName");
                }
            }
        }
    }
}
