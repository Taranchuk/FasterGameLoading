using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace FasterGameLoading
{
    public static class TextureResize
    {
        public enum TextureType
        {
            None, Building, Pawn, Weapon, Apparel, Item, Plant, Tree, Terrain, Mote, Filth, Projectile, UI, Other
        }
        public static Dictionary<TextureType, int> targetSizes = new Dictionary<TextureType, int> 
        {
            { TextureType.Building, 256 },
            { TextureType.Pawn, 256 },
            { TextureType.Apparel, 128 },
            { TextureType.Weapon, 128 },
            { TextureType.Item, 128 },
            { TextureType.Plant, 128 },
            { TextureType.Tree, 256 },
            { TextureType.Terrain, 1024 },
        };

        public static Dictionary<TextureType, List<KeyValuePair<BuildableDef, string>>> textures = new();
        public static Dictionary<Texture, string> texturesByPaths = new();
        public static Dictionary<Texture, KeyValuePair<BuildableDef, string>> texturesByDefs = new();
        public static Dictionary<Texture, ModContentPack> texturesByMods = new();

        public static void DoTextureResizing()
        {
            foreach (var value in Enum.GetValues(typeof(TextureType)).Cast<TextureType>())
            {
                textures[value] = new();
            }

            foreach (var contentList in ModContentLoaderTexture2D_LoadTexture_Patch.savedTextures)
            {
                texturesByPaths[contentList.Value] = contentList.Key;
            }

            foreach (var mod in LoadedModManager.RunningMods)
            {
                foreach (var texture in mod.textures.contentList.Values)
                {
                    texturesByMods[texture] = mod;
                }
            }

            foreach (var pawnKind in DefDatabase<PawnKindDef>.AllDefs)
            {
                var modContent = pawnKind.modContentPack;
                if (modContent != null && modContent.IsOfficialMod)
                {
                    continue;
                }
                if (pawnKind.lifeStages != null)
                {
                    foreach (var lifeStage in pawnKind.lifeStages)
                    {
                        if (lifeStage.bodyGraphicData != null)
                        {
                            AddEntry(TextureType.Pawn, pawnKind.race, lifeStage.bodyGraphicData.Graphic);
                            if (lifeStage.dessicatedBodyGraphicData != null)
                            {
                                AddEntry(TextureType.Pawn, pawnKind.race, lifeStage.dessicatedBodyGraphicData.Graphic);
                            }
                        }
                    }
                }

            }

            foreach (var styleDef in DefDatabase<StyleCategoryDef>.AllDefs)
            {
                var modContent = styleDef.modContentPack;
                if (modContent != null && modContent.IsOfficialMod)
                {
                    continue;
                }
                foreach (var style in styleDef.thingDefStyles)
                {
                    var type = GetTextureType(style.thingDef);
                    AddEntry(type, style.thingDef, style.styleDef.Graphic);
                    if (style.styleDef.wornGraphicPath.NullOrEmpty() is false)
                    {
                        foreach (var bodyType in DefDatabase<BodyTypeDef>.AllDefs)
                        {
                            if (TryGetGraphicApparel(style.thingDef, style.styleDef.wornGraphicPath, bodyType, out var graphic))
                            {
                                AddEntry(type, style.thingDef, graphic);
                            }
                        }
                    }
                }
            }

            foreach (var def in DefDatabase<BuildableDef>.AllDefs)
            {
                var modContent = def.modContentPack;
                if (modContent != null && modContent.IsOfficialMod)
                {
                    continue;
                }
                if (def is TerrainDef terrain)
                {
                    FillEntry(TextureType.Terrain, def);
                }
                else if (def is ThingDef thingDef)
                {
                    var type = GetTextureType(thingDef);
                    FillEntry(type, thingDef);
                    if (type == TextureType.Apparel)
                    {
                        foreach (var bodyType in DefDatabase<BodyTypeDef>.AllDefs)
                        {
                            if (TryGetGraphicApparel(thingDef, thingDef.apparel.wornGraphicPath, bodyType, out var graphic))
                            {
                                AddEntry(type, def, graphic);
                            }
                            if (thingDef.apparel.wornGraphicPaths != null)
                            {
                                foreach (var path in thingDef.apparel.wornGraphicPaths)
                                {
                                    if (TryGetGraphicApparel(thingDef, thingDef.apparel.wornGraphicPath, bodyType, out var graphic2))
                                    {
                                        AddEntry(type, def, graphic2);
                                    }
                                }
                            }
                        }
                    }
                    else if (type == TextureType.Plant || type == TextureType.Tree)
                    {
                        if (thingDef.plant.leaflessGraphic != null)
                        {
                            AddEntry(type, def, thingDef.plant.leaflessGraphic);
                        }
                        if (thingDef.plant.immatureGraphic != null)
                        {
                            AddEntry(type, def, thingDef.plant.immatureGraphic);
                        }
                        if (thingDef.plant.pollutedGraphic != null)
                        {
                            AddEntry(type, def, thingDef.plant.pollutedGraphic);
                        }
                    }
                }
            }

            List<KeyValuePair<string, int>> texturesToResize = new List<KeyValuePair<string, int>>();
            foreach (var texture in texturesByPaths)
            {
                if (texturesByMods.TryGetValue(texture.Key, out var mod))
                {
                    if (mod.PackageIdPlayerFacing == "DerekBickley.LTOColonyGroupsFinal")
                    {
                        continue;
                    }
                }
                if (texture.Key.width > 128)
                {
                    if (texturesByDefs.TryGetValue(texture.Key, out var value))
                    {
                        if (value.Key is TerrainDef)
                        {
                            if (texture.Key.width > targetSizes[TextureType.Terrain] || texture.Key.height > targetSizes[TextureType.Terrain])
                            {
                                texturesToResize.Add(new(texture.Value, targetSizes[TextureType.Terrain]));
                            }
                        }
                        else if (value.Key is ThingDef thingDef)
                        {
                            if (thingDef.graphicData.drawSize.x + thingDef.graphicData.drawSize.y <= 8)
                            {
                                var type = GetTextureType(thingDef);
                                if (targetSizes.TryGetValue(type, out var targetSize) && (texture.Key.width > targetSize || texture.Key.height > targetSize))
                                {
                                    texturesToResize.Add(new(texture.Value, targetSize));
                                }
                            }

                        }
                    }
                }
            }
            if (texturesToResize.Any())
            {
                GenThreading.ParallelForEach(texturesToResize, delegate (KeyValuePair<string, int> entry)
                {
                    ResizeTexture(entry.Key, entry.Value);
                });
                Log.Warning("Downscaled " + texturesToResize.Count + " textures");
            }
        }

        public static void ResizeTexture(string path, int targetSize)
        {
            try
            {
                var image = System.Drawing.Image.FromFile(path);
                double ratio = image.Height > image.Width ? (double)targetSize / image.Height : (double)targetSize / image.Width;
                int newWidth = (int)(image.Width * ratio);
                int newHeight = (int)(image.Height * ratio);
                System.Drawing.Bitmap newImage = new System.Drawing.Bitmap(newWidth, newHeight);
                var g = System.Drawing.Graphics.FromImage(newImage);
                g.DrawImage(image, 0, 0, newWidth, newHeight);
                g.Dispose();
                image.Dispose();
                newImage.Save(path);
                newImage.Dispose();
                var ddsPath = Path.ChangeExtension(path, ".dds");
                if (File.Exists(ddsPath))
                {
                    File.Delete(ddsPath);
                }
            }
            catch (Exception ex)
            {
                //Log.Error("Error resizing " + path + " - " + ex.ToString());
            }

        }
        public static bool TryGetGraphicApparel(ThingDef def, string wornGraphicPath, BodyTypeDef bodyType, out Graphic rec)
        {
            if (bodyType == BodyTypeDefOf.Baby && def.apparel.developmentalStageFilter.HasFlag(DevelopmentalStage.Baby) is false 
                || bodyType == BodyTypeDefOf.Child && def.apparel.developmentalStageFilter.HasFlag(DevelopmentalStage.Child) is false)
            {
                rec = null;
                return false;
            }
            if (wornGraphicPath.NullOrEmpty())
            {
                rec = null;
                return false;
            }
            string path = ((def.apparel.LastLayer != ApparelLayerDefOf.Overhead && def.apparel.LastLayer != ApparelLayerDefOf.EyeCover 
                && !RenderAsPack(def) && !(wornGraphicPath == BaseContent.PlaceholderImagePath) 
                && !(wornGraphicPath == BaseContent.PlaceholderGearImagePath)) ? (wornGraphicPath + "_" + bodyType.defName) 
                : wornGraphicPath);
            Shader shader = ShaderDatabase.Cutout;
            if (def.apparel.useWornGraphicMask)
            {
                shader = ShaderDatabase.CutoutComplex;
            }
            Log_Error_Patch.suppressErrorMessages = true;
            rec = GraphicDatabase.Get<Graphic_Multi>(path, shader, def.graphicData.drawSize, Color.white);
            Log_Error_Patch.suppressErrorMessages = false;
            return true;
        }

        public static bool RenderAsPack(ThingDef def)
        {
            if (def.apparel.LastLayer.IsUtilityLayer)
            {
                if (def.apparel.wornGraphicData != null)
                {
                    return def.apparel.wornGraphicData.renderUtilityAsPack;
                }
                return true;
            }
            return false;
        }

        private static TextureType GetTextureType(ThingDef thingDef)
        {
            if (thingDef.building != null)
            {
                return TextureType.Building;
            }
            else if (thingDef.IsWeapon)
            {
                return TextureType.Weapon;
            }
            else if (thingDef.IsApparel)
            {
                return TextureType.Apparel;
            }
            else if (thingDef.IsPlant)
            {
                if (thingDef.plant.IsTree)
                {
                    return TextureType.Tree;
                }
                return TextureType.Plant;
            }
            else if (thingDef.projectile != null)
            {
                return TextureType.Projectile;
            }
            else if (thingDef.category == ThingCategory.Mote)
            {
                return TextureType.Mote;
            }
            else if (thingDef.category == ThingCategory.Filth)
            {
                return TextureType.Filth;
            }
            else if (thingDef.category == ThingCategory.Item)
            {
                return TextureType.Item;
            }
            else if (thingDef.race != null)
            {
                return TextureType.Pawn;
            }
            return TextureType.None;
        }
    
        private static void FillEntry(TextureType type, BuildableDef def, Graphic graphicOverride = null)
        {
            var graphic = graphicOverride ?? def.graphic;
            AddEntry(type, def, graphic);
            if (def.uiIconPath.NullOrEmpty() is false)
            {
                if (def.uiIcon != null)
                {
                    if (texturesByPaths.TryGetValue(def.uiIcon, out var fullPath))
                    {
                        textures[type].Add(new KeyValuePair<BuildableDef, string>(def, fullPath));
                    }
                }
            }
        }
        private static void AddEntry(TextureType type, BuildableDef def, Graphic graphic)
        {
            if (graphic is Graphic_Multi multi)
            {
                foreach (var mat in multi.mats)
                {
                    GetMatTexture(type, def, mat);
                }
            }
            else if (graphic is Graphic_Appearances appearances)
            {
                foreach (var subGraphic in appearances.subGraphics)
                {
                    AddEntry(type, def, subGraphic);
                }
            }
            else if (graphic is Graphic_Single single)
            {
                GetMatTexture(type, def, single.mat);
            }
            else if (graphic is Graphic_RandomRotated randomRotated)
            {
                AddEntry(type, def, randomRotated.subGraphic);
            }
            else if (graphic is Graphic_Linked linked)
            {
                AddEntry(type, def, linked.subGraphic);
            }
            else if (def.graphic is Graphic_Collection collection)
            {
                foreach (var subGraphic in collection.subGraphics)
                {
                    AddEntry(type, def, subGraphic);
                }
            }
        }
        private static void GetMatTexture(TextureType type, BuildableDef def, Material mat)
        {
            if (mat?.mainTexture != null && texturesByPaths.TryGetValue(mat.mainTexture, out var fullPath))
            {
                AddEntry(type, def, fullPath, mat.mainTexture);
                Texture2D mask = null;
                if (mat.HasProperty(ShaderPropertyIDs.MaskTex))
                {
                    mask = (Texture2D)mat.GetTexture(ShaderPropertyIDs.MaskTex);
                }
                if (mask != null && texturesByPaths.TryGetValue(mask, out var maskPath))
                {
                    AddEntry(type, def, maskPath, mask);
                }
            }
        }
        private static void AddEntry(TextureType type, BuildableDef def, string fullPath, Texture texture)
        {
            var entry = new KeyValuePair<BuildableDef, string>(def, fullPath);
            textures[type].Add(entry);
            texturesByDefs[texture] = entry;
        }
    }
}

