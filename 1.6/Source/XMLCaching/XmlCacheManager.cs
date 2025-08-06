using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Verse;
using System.Diagnostics;
using System.Xml.Linq;
using System.Threading.Tasks;

namespace FasterGameLoading
{
    public static class XmlCacheManager
    {
        private static bool cacheActive;
        private const long MinFreeSpaceForCaching = 1L * 1024 * 1024 * 1024;
        private static bool loggedLowSpaceWarning = false;
        private static Dictionary<string, XmlNode> _inheritanceCache;
        private static HashSet<string> _existingInheritanceCacheHashes;
        private static Dictionary<string, XmlDocument> _sessionCache = new Dictionary<string, XmlDocument>();
        public static bool XMLCacheLoaded { get; set; }
        public static bool CacheIsActive => cacheActive;
        public static string CacheDirectory => Path.Combine(GenFilePaths.SaveDataFolderPath, "FGL_Cache");
        public static string XMLCachePath => Path.Combine(CacheDirectory, "XmlCache.xml");
        public static string XMLMetadataCachePath => Path.Combine(CacheDirectory, "XmlMetadataCache.xml");
        public static string XMLInheritanceCachePath => Path.Combine(CacheDirectory, "XmlInheritanceCache.xml");
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
            if (File.Exists(XMLCachePath)) File.Delete(XMLCachePath);
            if (File.Exists(XMLMetadataCachePath)) File.Delete(XMLMetadataCachePath);
            if (File.Exists(XMLInheritanceCachePath)) File.Delete(XMLInheritanceCachePath);
            Reset();
        }

        public static void BuildAndSaveInheritanceCache()
        {
            var totalSw = Stopwatch.StartNew();
            var loadingSw = new Stopwatch();
            var hashingSw = new Stopwatch();
            var importSw = new Stopwatch();

            loadingSw.Start();
            var inheritanceCacheDoc = LoadXmlDocument(XMLInheritanceCachePath, true);
            loadingSw.Stop();

            if (_existingInheritanceCacheHashes == null)
            {
                _existingInheritanceCacheHashes = new HashSet<string>();
                if (inheritanceCacheDoc?.DocumentElement != null)
                {
                    foreach (XmlNode node in inheritanceCacheDoc.DocumentElement.ChildNodes)
                    {
                        var hash = node.Attributes["FGL_OriginalHash"]?.Value;
                        if (!string.IsNullOrEmpty(hash))
                        {
                            _existingInheritanceCacheHashes.Add(hash);
                        }
                    }
                }
            }

            var newDoc = (XmlDocument)inheritanceCacheDoc.CloneNode(true);
            var root = newDoc.DocumentElement;
            var initialCount = root.ChildNodes.Count;
            var newCount = 0;

            hashingSw.Start();
            var nodesToProcess = new System.Collections.Concurrent.ConcurrentDictionary<string, KeyValuePair<XmlNode, XmlInheritance.XmlInheritanceNode>>();
            Parallel.ForEach(XmlInheritance.resolvedNodes, (kvp) =>
            {
                var originalNodeHash = GetNodeHash(kvp.Key);
                if (!_existingInheritanceCacheHashes.Contains(originalNodeHash))
                {
                    nodesToProcess[originalNodeHash] = kvp;
                }
            });
            hashingSw.Stop();

            importSw.Start();
            foreach (var kvp in nodesToProcess)
            {
                _existingInheritanceCacheHashes.Add(kvp.Key);
                var resolvedNode = (XmlElement)kvp.Value.Value.resolvedXmlNode;
                var importedNode = (XmlElement)newDoc.ImportNode(resolvedNode, true);
                importedNode.SetAttribute("FGL_OriginalHash", kvp.Key);
                root.AppendChild(importedNode);
                newCount++;
            }
            importSw.Stop();

            if (newCount > 0)
            {
                var docToSave = newDoc.OuterXml;
                _sessionCache[XMLInheritanceCachePath] = newDoc;
                Task.Run(() =>
                {
                    try
                    {
                        File.WriteAllText(XMLInheritanceCachePath, docToSave);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[FasterGameLoading] Error in background inheritance cache saving: {e}");
                    }
                });
            }

            totalSw.Stop();
            Log.Warning($"[FasterGameLoading] Built inheritance cache. Initial: {initialCount}, New: {newCount}, Total: {root.ChildNodes.Count}. Took {totalSw.ElapsedMilliseconds}ms (Load: {loadingSw.ElapsedMilliseconds}ms, Hash: {hashingSw.ElapsedMilliseconds}ms, Import: {importSw.ElapsedMilliseconds}ms).");
        }

        public static bool TryApplyInheritanceCache()
        {
            var totalSw = Stopwatch.StartNew();
            try
            {
                if (_inheritanceCache == null)
                {
                    _inheritanceCache = new Dictionary<string, XmlNode>();
                    var doc = LoadXmlDocument(XMLInheritanceCachePath);
                    if (doc?.DocumentElement != null)
                    {
                        var concurrentCache = new System.Collections.Concurrent.ConcurrentDictionary<string, XmlNode>();
                        Parallel.ForEach(doc.DocumentElement.ChildNodes.Cast<XmlNode>(), node =>
                        {
                            concurrentCache[node.Attributes["FGL_OriginalHash"].Value] = node;
                        });
                        _inheritanceCache = new Dictionary<string, XmlNode>(concurrentCache);
                    }
                }

                if (_inheritanceCache.Count == 0) return false;

                var concurrentNodeHashes = new System.Collections.Concurrent.ConcurrentDictionary<XmlNode, string>();
                Parallel.ForEach(XmlInheritance.unresolvedNodes, (inheritanceNode) =>
                {
                    concurrentNodeHashes[inheritanceNode.xmlNode] = GetNodeHash(inheritanceNode.xmlNode);
                });
                var nodeHashes = new Dictionary<XmlNode, string>(concurrentNodeHashes);

                foreach (var inheritanceNode in XmlInheritance.unresolvedNodes)
                {
                    if (!_inheritanceCache.ContainsKey(nodeHashes[inheritanceNode.xmlNode]))
                    {
                        Log.Warning($"[FasterGameLoading] Inheritance cache is incomplete (missing {inheritanceNode.xmlNode.Name}), falling back to vanilla resolver.");
                        return false;
                    }
                }

                var newResolvedNodes = new Dictionary<XmlNode, XmlInheritance.XmlInheritanceNode>();
                foreach (var inheritanceNode in XmlInheritance.unresolvedNodes)
                {
                    var originalNode = inheritanceNode.xmlNode;
                    inheritanceNode.resolvedXmlNode = _inheritanceCache[nodeHashes[originalNode]];
                    newResolvedNodes[originalNode] = inheritanceNode;
                }

                XmlInheritance.resolvedNodes = newResolvedNodes;
                XmlInheritance.unresolvedNodes.Clear();
                totalSw.Stop();
                Log.Warning($"[FasterGameLoading] Applied inheritance cache. Resolved: {newResolvedNodes.Count}. Took {totalSw.ElapsedMilliseconds}ms.");
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"Error loading inheritance cache, rebuilding. Error: {e}");
                InvalidateCache();
                return false;
            }
        }

        private static string GetNodeHash(XmlNode node)
        {
            return GenerateSha256Hash(node.OuterXml);
        }

        public static void Reset()
        {
            DeactivateCache();
            currentFileHashes.Clear();
            XMLCacheLoaded = false;
            _inheritanceCache = null;
            _existingInheritanceCacheHashes = null;
            _sessionCache.Clear();
        }

        public static void HydrateLookupsFrom(XmlDocument doc, Dictionary<XmlNode, LoadableXmlAsset> assetLookup)
        {
            DeepProfiler.Start("FGL: HydrateLookupsFrom");
            assetLookup.Clear();

            if (doc?.DocumentElement == null)
            {
                DeepProfiler.End();
                return;
            }
            var metadataDoc = LoadXmlDocument(XMLMetadataCachePath);
            if (metadataDoc?.DocumentElement == null)
            {
                DeepProfiler.End();
                return;
            }

            var nodeList = doc.DocumentElement.ChildNodes.Cast<XmlNode>().ToList();
            foreach (XmlNode metaNode in metadataDoc.DocumentElement.ChildNodes)
            {
                if (metaNode is XmlElement metaElement)
                {
                    int index = int.Parse(metaElement.GetAttribute("Index"));
                    if (index < nodeList.Count)
                    {
                        var originalNode = nodeList[index];
                        string modId = metaElement.GetAttribute("FGL_SourceMod");
                        string filePath = metaElement.GetAttribute("FGL_SourceFile");
                        string assetName = metaElement.GetAttribute("FGL_AssetName");
                        assetLookup[originalNode] = CreateFauxAsset(assetName, modId, filePath);
                    }
                }
            }
            DeepProfiler.End();
        }

        public static void SaveXMLCache(XmlDocument doc, Dictionary<XmlNode, LoadableXmlAsset> assetLookup)
        {
            var totalSw = Stopwatch.StartNew();
            if (!VerifyDiskSpaceForCaching()) return;

            var buildMetaSw = Stopwatch.StartNew();
            var metadataDoc = new XmlDocument();
            var root = metadataDoc.CreateElement("FGL_Metadata");
            metadataDoc.AppendChild(root);

            var nodeList = doc.DocumentElement.ChildNodes.Cast<XmlNode>().ToList();
            var nodeToIndexMap = new Dictionary<XmlNode, int>(nodeList.Count);
            for (int i = 0; i < nodeList.Count; i++)
            {
                nodeToIndexMap[nodeList[i]] = i;
            }

            foreach (var kvp in assetLookup)
            {
                if (nodeToIndexMap.TryGetValue(kvp.Key, out int index))
                {
                    var asset = kvp.Value;
                    var metaElement = metadataDoc.CreateElement("meta");
                    metaElement.SetAttribute("Index", index.ToString());
                    metaElement.SetAttribute("FGL_SourceMod", asset.mod?.PackageIdPlayerFacing ?? "");
                    metaElement.SetAttribute("FGL_SourceFile", asset.FullFilePath ?? "");
                    metaElement.SetAttribute("FGL_AssetName", asset.name);
                    root.AppendChild(metaElement);
                }
            }
            buildMetaSw.Stop();

            var docString = doc.OuterXml;
            var metaString = metadataDoc.OuterXml;

            _sessionCache[XMLCachePath] = doc;
            _sessionCache[XMLMetadataCachePath] = metadataDoc;

            Task.Run(() => SaveXMLCache_Threaded(docString, metaString, buildMetaSw.ElapsedMilliseconds));
            totalSw.Stop();
            Log.Warning($"[FasterGameLoading] SaveXMLCache took {totalSw.ElapsedMilliseconds}ms.");
        }

        private static void SaveXMLCache_Threaded(string doc, string metadataDoc, long buildMetaMs)
        {
            try
            {
                Directory.CreateDirectory(CacheDirectory);
                File.WriteAllText(XMLCachePath, doc);
                File.WriteAllText(XMLMetadataCachePath, metadataDoc);
            }
            catch (Exception e)
            {
                Log.Error($"[FasterGameLoading] Error in background XML cache saving: {e}");
            }
        }

        public static Task StartPreloadingCache()
        {
            return Task.Run(() =>
            {
                LoadXmlDocument(XMLCachePath);
                LoadXmlDocument(XMLMetadataCachePath);
                LoadXmlDocument(XMLInheritanceCachePath);
            });
        }
        public static bool TryLoadXMLCache(out XmlDocument doc)
        {
            DeepProfiler.Start("FGL: TryLoadXMLCache");
            doc = LoadXmlDocument(XMLCachePath);
            if (doc != null)
            {
                DeepProfiler.End();
                return true;
            }
            DeepProfiler.End();
            return false;
        }

        private static XmlDocument LoadXmlDocument(string path, bool createIfMissing = false)
        {
            if (_sessionCache.TryGetValue(path, out var cachedDoc))
            {
                return cachedDoc;
            }
            if (!File.Exists(path))
            {
                if (createIfMissing)
                {
                    var newDoc = new XmlDocument();
                    var root = newDoc.CreateElement("FGL_InheritanceData");
                    newDoc.AppendChild(root);
                    _sessionCache[path] = newDoc;
                    return newDoc;
                }
                return null;
            }

            try
            {
                var doc = new XmlDocument();
                var readerSettings = new XmlReaderSettings
                {
                    IgnoreComments = true,
                    IgnoreWhitespace = true,
                    CheckCharacters = false,
                    DtdProcessing = DtdProcessing.Ignore
                };
                using (var stream = File.OpenRead(path))
                {
                    using (var xmlReader = XmlReader.Create(stream, readerSettings))
                    {
                        doc.Load(xmlReader);
                    }
                }
                _sessionCache[path] = doc;
                return doc;
            }
            catch (Exception e)
            {
                Log.Warning($"[FasterGameLoading] Could not load XML document from {path}. Error: {e}");
                return null;
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
    }
}
