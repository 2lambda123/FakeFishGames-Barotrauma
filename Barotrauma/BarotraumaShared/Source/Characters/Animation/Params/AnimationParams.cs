﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Linq;

namespace Barotrauma
{
    public enum AnimationType
    {
        Walk,
        Run,
        SwimSlow,
        SwimFast
    }

    abstract class GroundedMovementParams : AnimationParams
    {
        [Serialize("1.0,1.0", true), Editable]
        public Vector2 StepSize
        {
            get;
            set;
        }

        [Serialize(float.NaN, true), Editable]
        public float HeadPosition { get; set; }

        [Serialize(float.NaN, true), Editable]
        public float TorsoPosition { get; set; }
    }

    // TODO: add some of the ragdoll params here?
    abstract class AnimationParams : ISerializableEntity
    {
        private bool isLoaded;
        protected bool Load(string file, AnimationType type)
        {
            FilePath = file;
            AnimationType = type;
            XDocument doc = XMLExtensions.TryLoadXml(file);
            if (doc == null || doc.Root == null) return false;
            Name = doc.Root.Name.ToString();
            SerializableProperties = SerializableProperty.DeserializeProperties(this, doc.Root);
            isLoaded = true;
            return SerializableProperties != null;
        }

        protected string FilePath { get; private set; }
        public virtual AnimationType AnimationType { get; private set; }
        public bool IsGroundedAnimation => AnimationType == AnimationType.Walk || AnimationType == AnimationType.Run;
        public bool IsSwimAnimation => AnimationType == AnimationType.SwimSlow || AnimationType == AnimationType.SwimFast;

        protected static Dictionary<string, Dictionary<AnimationType, AnimationParams>> animations = new Dictionary<string, Dictionary<AnimationType, AnimationParams>>();

        public static T GetAnimParams<T>(Character character, AnimationType type) where T : AnimationParams, new()
        {
            string speciesName = character.SpeciesName;
            if (!animations.TryGetValue(speciesName, out Dictionary<AnimationType, AnimationParams> anims))
            {
                anims = new Dictionary<AnimationType, AnimationParams>();
                animations.Add(character.SpeciesName, anims);
            }
            if (!anims.TryGetValue(type, out AnimationParams anim))
            {
                XDocument doc = XMLExtensions.TryLoadXml(character.ConfigPath);
                string firstLetter = speciesName.First().ToString().ToUpperInvariant();
                speciesName = firstLetter + speciesName.ToLowerInvariant().Substring(1);
                string animType = type.ToString();
                string defaultPath = $"Content/Characters/{speciesName}/{speciesName}{animType}.xml";
                string animPath = doc.Root.Element("animation").GetAttributeString("path", defaultPath);
                animPath = animPath.Replace("[ANIMTYPE]", animType);
                T a = new T();
                if (a.Load(animPath, type))
                {
                    anims.Add(type, a);
                }
                else
                {
                    DebugConsole.ThrowError($"Failed to load an animation {a} of type {type} at {animPath}");
                }
                anim = a;
            }
            return anim as T;
        }

        public string Name
        {
            get;
            private set;
        }

        public Dictionary<string, SerializableProperty> SerializableProperties
        {
            get;
            private set;
        }

        [Serialize(1.0f, true), Editable]
        public float Speed
        {
            get;
            set;
        }

        /// <summary>
        /// In degrees.
        /// </summary>
        [Serialize(float.NaN, true), Editable]
        public float HeadAngle
        {
            get => float.IsNaN(HeadAngleInRadians) ? float.NaN : MathHelper.ToDegrees(HeadAngleInRadians);
            set
            {
                if (!float.IsNaN(value))
                {
                    HeadAngleInRadians = MathHelper.ToRadians(value);
                }
            }
        }
        public float HeadAngleInRadians { get; private set; } = float.NaN;

        /// <summary>
        /// In degrees.
        /// </summary>
        [Serialize(float.NaN, true), Editable]
        public float TorsoAngle
        {
            get => float.IsNaN(TorsoAngleInRadians) ? float.NaN : MathHelper.ToDegrees(TorsoAngleInRadians);
            set
            {
                if (!float.IsNaN(value))
                {
                    TorsoAngleInRadians = MathHelper.ToRadians(value);
                }
            }
        }
        public float TorsoAngleInRadians { get; private set; } = float.NaN;

#if CLIENT
        private static GUIListBox editor;
        public static GUIListBox Editor
        {
            get
            {
                if (editor == null)
                {
                    CreateEditor();
                }
                return editor;
            }
        }

        public static void CreateEditor()
        {
            editor = new GUIListBox(new RectTransform(new Vector2(0.25f, 1), GUI.Canvas) { MinSize = new Point(200, GameMain.GraphicsHeight) });
        }

        public SerializableEntityEditor SerializableEntityEditor { get; private set; }

        public void AddToEditor()
        {
            if (!isLoaded)
            {
                DebugConsole.ThrowError("Animation params not loaded!");
                return;
            }
            SerializableEntityEditor = new SerializableEntityEditor(Editor.Content.RectTransform, this, false, true);
        }

        public bool Save()
        {
             if (!isLoaded)
            {
                DebugConsole.ThrowError("Animation params not loaded!");
                return false;
            }
            XDocument doc = XMLExtensions.TryLoadXml(FilePath);
            if (doc == null || doc.Root == null) return false;       
            SerializableProperty.SerializeProperties(this, doc.Root, true);
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = true,
                NewLineOnAttributes = true
            };
            using (var writer = XmlWriter.Create(FilePath, settings))
            {
                doc.WriteTo(writer);
                writer.Flush();
            }
            return true;
        }

        public bool Reset()
        {
            if (!isLoaded)
            {
                DebugConsole.ThrowError("Animation params not loaded!");
                return false;
            }
            XDocument doc = XMLExtensions.TryLoadXml(FilePath);
            if (doc == null || doc.Root == null) return false;
            SerializableProperties = SerializableProperty.DeserializeProperties(this, doc.Root);
            return true;
        }
#endif
    }
}
