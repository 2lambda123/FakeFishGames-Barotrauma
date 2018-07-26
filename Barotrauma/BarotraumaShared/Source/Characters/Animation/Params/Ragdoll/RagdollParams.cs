﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using System.IO;
using Barotrauma.Extensions;

namespace Barotrauma
{
    class HumanRagdollParams : RagdollParams
    {
        public static HumanRagdollParams GetRagdollParams() => GetRagdollParams<HumanRagdollParams>("human");
    }

    class FishRagdollParams : RagdollParams { }

    class RagdollParams : EditableParams
    {
        [Serialize(1.0f, true), Editable(0.5f, 2f)]
        public float LimbScale { get; set; }

        [Serialize(1.0f, true), Editable(0.5f, 2f)]
        public float JointScale { get; set; }

        private static Dictionary<string, Dictionary<string, RagdollParams>> allRagdolls = new Dictionary<string, Dictionary<string, RagdollParams>>();

        public List<LimbParams> Limbs { get; private set; } = new List<LimbParams>();
        public List<JointParams> Joints { get; private set; } = new List<JointParams>();
        protected IEnumerable<RagdollSubParams> GetAllSubParams() => Limbs.Select(j => j as RagdollSubParams).Concat(Joints.Select(j => j as RagdollSubParams));

        public XElement MainElement => Doc.Root;

        protected static string GetDefaultFileName(string speciesName) => $"{speciesName.CapitaliseFirstInvariant()}DefaultRagdoll.xml";
        protected static string GetDefaultFolder(string speciesName) => $"Content/Characters/{speciesName.CapitaliseFirstInvariant()}/Ragdolls/";
        protected static string GetDefaultFile(string speciesName) => GetDefaultFolder(speciesName) + GetDefaultFileName(speciesName);

        protected static string GetFolder(string speciesName)
        {
            var folder = XMLExtensions.TryLoadXml(Character.GetConfigFile(speciesName)).Root?.Element("ragdolls")?.GetAttributeString("folder", string.Empty);
            if (string.IsNullOrEmpty(folder) || folder.ToLowerInvariant() == "default")
            {
                folder = GetDefaultFolder(speciesName);
            }
            return folder;
        }   

        /// <summary>
        /// The file name can be partial. If left null, will select randomly. If fails, will select the default file.
        /// </summary>
        public static T GetRagdollParams<T>(string speciesName, string fileName = null) where T : RagdollParams, new()
        {
            if (!allRagdolls.TryGetValue(speciesName, out Dictionary<string, RagdollParams> ragdolls))
            {
                ragdolls = new Dictionary<string, RagdollParams>();
                allRagdolls.Add(speciesName, ragdolls);
            }
            string defaultFileName = GetDefaultFileName(speciesName);
            fileName = fileName ?? defaultFileName;
            if (!ragdolls.TryGetValue(fileName, out RagdollParams ragdoll))
            {
                string selectedFile = null;
                string folder = GetFolder(speciesName);
                if (Directory.Exists(folder))
                {
                    var files = Directory.GetFiles(folder);
                    if (files.None())
                    {
                        DebugConsole.ThrowError($"[RagdollParams] Could not find any ragdoll files from the folder: {folder}. Using the default ragdoll.");
                        selectedFile = GetDefaultFile(speciesName);
                    }
                    else if (fileName != defaultFileName)
                    {
                        // First check if a file matches the name exactly
                        selectedFile = files.FirstOrDefault(f => f == fileName);
                        if (selectedFile == null)
                        {
                            // Then check if a file matches the name ignoring the case
                            selectedFile = files.FirstOrDefault(f => f.ToLowerInvariant() == fileName.ToLowerInvariant());
                        }
                        if (selectedFile == null)
                        {
                            // Last, check if a file matches the name partially
                            selectedFile = files.FirstOrDefault(f => f.ToLowerInvariant().Contains(fileName.ToLowerInvariant()));
                        }
                        if (selectedFile == null)
                        {
                            DebugConsole.ThrowError($"[RagdollParams] Could not find a ragdoll file that matches the name {fileName}. Using the default ragdoll.");
                            selectedFile = GetDefaultFile(speciesName);
                        }
                    }
                    else
                    {
                        // Files found, but none specifided
                        selectedFile = files.GetRandom();
                    }
                }
                else
                {
                    DebugConsole.ThrowError($"[RagdollParams] Invalid directory: {folder}. Using the default ragdoll.");
                    selectedFile = GetDefaultFile(speciesName);
                }
                if (selectedFile == null)
                {
                    throw new Exception("[RagdollParams] Selected file null!");
                }
                DebugConsole.NewMessage($"[RagdollParams] Loading ragdoll from {selectedFile}.", Color.Orange);
                T r = new T();
                if (r.Load(selectedFile))
                {
                    ragdolls.Add(fileName, r);
                }
                else
                {
                    DebugConsole.ThrowError($"[RagdollParams] Failed to load ragdoll params {r} at {selectedFile} for the character {speciesName}");
                }
                ragdoll = r;
            }
            return (T)ragdoll;
        }

        protected override bool Load(string file)
        {
            if (base.Load(file))
            {
                CreateLimbs();
                CreateJoints();
                return true;
            }
            return false;
        }

        protected void CreateLimbs()
        {
            Limbs.Clear();
            foreach (var element in MainElement.Elements("limb"))
            {
                Limbs.Add(new LimbParams(element, this));
            }
        }

        protected void CreateJoints()
        {
            Joints.Clear();
            foreach (var element in MainElement.Elements("joint"))
            {
                Joints.Add(new JointParams(element, this));
            }
        }

        protected override bool Deserialize(XElement element)
        {
            if (base.Deserialize(element))
            {
                GetAllSubParams().ForEach(p => p.Deserialize());
                return true;
            }
            return false;
        }

        protected override bool Serialize(XElement element)
        {
            if (base.Serialize(element))
            {
                GetAllSubParams().ForEach(p => p.Serialize());
                return true;
            }
            return false;
        }

#if CLIENT
        public override void AddToEditor(ParamsEditor editor)
        {
            base.AddToEditor(editor);
            GetAllSubParams().ForEach(p => p.AddToEditor(editor));
        }
#endif
    }

    class JointParams : RagdollSubParams
    {
        public JointParams(XElement element, RagdollParams ragdoll) : base(element, ragdoll)
        {
            Name = $"Joint {element.Attribute("limb1").Value} - {element.Attribute("limb2").Value}";
        }

        [Serialize(true, true), Editable]
        public bool CanBeSevered { get; set; }

        [Serialize(-1, true), Editable(0, 255)]
        public int Limb1 { get; set; }

        [Serialize(-1, true), Editable(0, 255)]
        public int Limb2 { get; set; }

        /// <summary>
        /// Should be converted to sim units.
        /// </summary>
        [Serialize("1.0, 1.0", true), Editable]
        public Vector2 Limb1Anchor { get; set; }

        /// <summary>
        /// Should be converted to sim units.
        /// </summary>
        [Serialize("1.0, 1.0", true), Editable]
        public Vector2 Limb2Anchor { get; set; }

        /// <summary>
        /// In degrees.
        /// </summary>
        [Serialize(float.NaN, true), Editable(-360f, 360f)]
        public float UpperLimit { get; set; }

        /// <summary>
        /// In degrees.
        /// </summary>
        [Serialize(float.NaN, true), Editable(-360f, 360f)]
        public float LowerLimit { get; set; }
    }

    class LimbParams : RagdollSubParams
    {
        public LimbParams(XElement element, RagdollParams ragdoll) : base(element, ragdoll)
        {
            Name = $"Limb {element.Attribute("id").Value}";
            var spriteElement = element.Element("sprite");
            if (spriteElement != null)
            {
                normalSpriteParams = new SpriteParams(spriteElement, ragdoll);
                SubParams.Add(normalSpriteParams);
            }
            var damagedElement = element.Element("damagedsprite");
            if (damagedElement != null)
            {
                damagedSpriteParams = new SpriteParams(damagedElement, ragdoll);
                SubParams.Add(damagedSpriteParams);
            }
        }

        public readonly SpriteParams normalSpriteParams;
        public readonly SpriteParams damagedSpriteParams;

        // TODO: decide which properties should be editable in the editor and which only via xml

        [Serialize(-1, true)]
        public int ID { get; set; }

        [Serialize(LimbType.None, true)]
        public LimbType Type { get; set; }

        [Serialize(0f, true)]
        public float Radius { get; set; }

        [Serialize(0f, true)]
        public float Height { get; set; }

        [Serialize(0f, true)]
        public float Mass { get; set; }

        [Serialize(0, true)]
        public int HealthIndex { get; set; }

        [Serialize(0f, true)]
        public float AttackPriority { get; set; }

        [Serialize(false, true)]
        public bool Flip { get; set; }

        //[Serialize(false, true)]
        //public bool IgnoreCollisions { get; set; }
    }

    class SpriteParams : RagdollSubParams
    {
        public SpriteParams(XElement element, RagdollParams ragdoll) : base(element, ragdoll)
        {
            Name = element.Name.ToString();
        }

        // TODO: decide which properties should be editable in the editor and which only via xml

        [Serialize("", true),]
        public string Texture { get; set; }

        [Serialize("0, 0, 0, 0", true)]
        public Vector4 SourceRect { get; set; }

        [Serialize(0f, true)]
        public float Depth { get; set; }

        [Serialize("0.5, 0.5", true), Editable]
        public Vector2 Origin { get; set; }
    }

    abstract class RagdollSubParams : ISerializableEntity
    {
        public string Name { get; protected set; }
        public Dictionary<string, SerializableProperty> SerializableProperties { get; private set; }
        public XElement Element { get; private set; }
        public List<RagdollSubParams> SubParams { get; set; } = new List<RagdollSubParams>();
        public RagdollParams Ragdoll { get; private set; }

        public RagdollSubParams(XElement element, RagdollParams ragdoll)
        {
            Element = element;
            Ragdoll = ragdoll;
            SerializableProperties = SerializableProperty.DeserializeProperties(this, element);
        }

        public virtual bool Deserialize()
        {
            SerializableProperties = SerializableProperty.DeserializeProperties(this, Element);
            SubParams.ForEach(sp => sp.Deserialize());
            return SerializableProperties != null;
        }

        public virtual bool Serialize()
        {
            SerializableProperty.SerializeProperties(this, Element, true);
            SubParams.ForEach(sp => sp.Serialize());
            return true;
        }

     #if CLIENT
        public SerializableEntityEditor SerializableEntityEditor { get; protected set; }
        public virtual void AddToEditor(ParamsEditor editor)
        {
            SerializableEntityEditor = new SerializableEntityEditor(editor.EditorBox.Content.RectTransform, this, false, true);
            SubParams.ForEach(sp => sp.AddToEditor(editor));
        }
     #endif
    }
}
