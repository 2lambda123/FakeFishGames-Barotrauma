﻿using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma
{
    partial class ItemAssemblyPrefab : MapEntityPrefab
    {
        private readonly XElement configElement;
        private readonly string configPath;

        [Serialize(false, false)]
        public bool HideInMenus { get; set; }
        
        public List<Pair<MapEntityPrefab, Rectangle>> DisplayEntities
        {
            get;
            private set;
        }

        public Rectangle Bounds;

        public ItemAssemblyPrefab(string filePath)
        {
            configPath = filePath;
            XDocument doc = XMLExtensions.TryLoadXml(filePath);
            if (doc == null || doc.Root == null) return;
            
            name = doc.Root.GetAttributeString("name", "");
            identifier = doc.Root.GetAttributeString("identifier", null) ?? name.ToLowerInvariant().Replace(" ", "");
            configElement = doc.Root;

            Category = MapEntityCategory.ItemAssembly;

            SerializableProperty.DeserializeProperties(this, configElement);

            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            DisplayEntities = new List<Pair<MapEntityPrefab, Rectangle>>();
            foreach (XElement entityElement in doc.Root.Elements())
            {
                string entityName = entityElement.GetAttributeString("name", "");
                MapEntityPrefab mapEntity = List.Find(p => p.Name == entityName);
                Rectangle rect = entityElement.GetAttributeRect("rect", Rectangle.Empty);
                if (mapEntity != null && !entityElement.GetAttributeBool("hideinassemblypreview", false))
                {
                    DisplayEntities.Add(new Pair<MapEntityPrefab, Rectangle>(mapEntity, rect));
                    minX = Math.Min(minX, rect.X);
                    minY = Math.Min(minY, rect.Y - rect.Height);
                    maxX = Math.Max(maxX, rect.Right);
                    maxY = Math.Max(maxY, rect.Y);
                }
            }

            Bounds = new Rectangle(minX, minY, maxX - minX, maxY - minY);
            
            List.Add(this);
        }

        public static void Remove(string filePath)
        {
            var matchingAssembly = List.Find(prefab => 
                prefab is ItemAssemblyPrefab assemblyPrefab && 
                assemblyPrefab.configPath == filePath);
            if (matchingAssembly != null)
            {
                List.Remove(matchingAssembly);
            }
        }
        
        protected override void CreateInstance(Rectangle rect)
        {
            CreateInstance(rect.Location.ToVector2(), Submarine.MainSub);
        }

        public List<MapEntity> CreateInstance(Vector2 position, Submarine sub)
        {
            List<MapEntity> entities = MapEntity.LoadAll(sub, configElement, configPath);
            if (entities.Count == 0) return entities;

            Vector2 offset = sub == null ? Vector2.Zero : sub.HiddenSubPosition;

            foreach (MapEntity me in entities)
            {
                me.Move(position);
                Item item = me as Item;
                if (item == null) continue;
                Wire wire = item.GetComponent<Wire>();
                if (wire != null)
                {
                    wire.MoveNodes(position - offset);

                    // Placeholder way of hiding wires in alien ruins for now, until a decision whether unique wiring sprites will be used
                    if (!(Screen.Selected is SubEditorScreen)) wire.Hidden = Name.ToLowerInvariant().Contains("alien");
                }
            }

            MapEntity.MapLoaded(entities, true);
#if CLIENT
            if (Screen.Selected == GameMain.SubEditorScreen)
            {
                MapEntity.SelectedList.Clear();
                MapEntity.SelectedList.AddRange(entities);
            }
#endif   
            return entities;

        }
        
        public void Delete()
        {
            List.Remove(this);
            if (File.Exists(configPath))
            {
                try
                {
                    File.Delete(configPath);
                }
                catch (Exception e)
                {
                    DebugConsole.ThrowError("Deleting item assembly \"" + name + "\" failed.", e);
                }
            }
        }

        public static void LoadAll()
        {
            if (GameSettings.VerboseLogging)
            {
                DebugConsole.Log("Loading item assembly prefabs: ");
            }

            List<string> itemAssemblyFiles = new List<string>();

            //find assembly files in the item assembly folder
            string directoryPath = Path.Combine("Content", "Items", "Assemblies");
            if (Directory.Exists(directoryPath))
            {
                itemAssemblyFiles.AddRange(Directory.GetFiles(directoryPath));
            }

            //find assembly files in selected content packages
            foreach (ContentPackage cp in GameMain.Config.SelectedContentPackages)
            {
                foreach (string filePath in cp.GetFilesOfType(ContentType.ItemAssembly))
                {
                    //ignore files that have already been added (= file saved to item assembly folder)
                    if (itemAssemblyFiles.Any(f => Path.GetFullPath(f) == Path.GetFullPath(filePath))) { continue; }
                    itemAssemblyFiles.Add(filePath);
                }
            }

            foreach (string file in itemAssemblyFiles)
            {
                new ItemAssemblyPrefab(file);
            }
        }
    }
}
