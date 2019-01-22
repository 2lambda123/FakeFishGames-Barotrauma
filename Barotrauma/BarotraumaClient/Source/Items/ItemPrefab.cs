﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Barotrauma
{
    class BrokenItemSprite
    {
        //sprite will be rendered if the condition of the item is below this
        public readonly float MaxCondition;
        public readonly Sprite Sprite;
        public readonly bool FadeIn;

        public BrokenItemSprite(Sprite sprite, float maxCondition, bool fadeIn)
        {
            Sprite = sprite;
            MaxCondition = MathHelper.Clamp(maxCondition, 0.0f, 100.0f);
            FadeIn = fadeIn;
        }
    }

    partial class ItemPrefab : MapEntityPrefab
    {
        public class DecorativeSprite
        {
            public Sprite Sprite { get; private set; }

            public enum AnimationType
            {
                None,
                Sine,
                Noise
            }

            [Serialize("0,0", false)]
            public Vector2 Offset { get; private set; }

            [Serialize(AnimationType.None, false)]
            public AnimationType OffsetAnim { get; private set; }

            [Serialize(0.0f, false)]
            public float OffsetAnimSpeed { get; private set; }

            private float rotationSpeedRadians;
            [Serialize(0.0f, false)]
            public float RotationSpeed
            {
                get
                {
                    return MathHelper.ToDegrees(rotationSpeedRadians);
                }
                private set
                {
                    rotationSpeedRadians = MathHelper.ToRadians(value);
                }
            }

            [Serialize(0.0f, false)]
            public float Rotation { get; private set; }

            [Serialize(AnimationType.None, false)]
            public AnimationType RotationAnim { get; private set; }

            /// <summary>
            /// Should the sprite be hidden when the sprite is inactive (otherwise animations are just disabled)
            /// </summary>
            [Serialize(true, false)]
            public bool HideWhenInactive { get; private set; }

            /// <summary>
            /// If > 0, only one sprite of the same group is used (chosen randomly)
            /// </summary>
            [Serialize(0, false)]
            public int RandomGroupID { get; private set; }

            /// <summary>
            /// The sprite is only drawn if these conditions are fulfilled
            /// </summary>
            public List<PropertyConditional> Conditionals { get; private set; } = new List<PropertyConditional>();

            public DecorativeSprite(XElement element, string path = "")
            {
                Sprite = new Sprite(element, path);
                SerializableProperty.DeserializeProperties(this, element);

                foreach (XElement subElement in element.Elements())
                {
                    if (subElement.Name.ToString().ToLowerInvariant() == "conditional")
                    {
                        foreach (XAttribute attribute in subElement.Attributes())
                        {
                            if (attribute.Name.ToString().ToLowerInvariant() == "targetitemcomponent") { continue; }
                            Conditionals.Add(new PropertyConditional(attribute));
                        }
                    }
                }
            }

            public Vector2 GetOffset(ref float offsetState)
            {
                if (OffsetAnimSpeed <= 0.0f)
                {
                    return Offset;
                }
                switch (OffsetAnim)
                {                    
                    case AnimationType.Sine:
                        offsetState = offsetState % (MathHelper.TwoPi / OffsetAnimSpeed);
                        return Offset * (float)Math.Sin(offsetState * OffsetAnimSpeed);
                    case AnimationType.Noise:
                        offsetState = offsetState % (1.0f / (OffsetAnimSpeed * 0.1f));

                        float t = offsetState * 0.1f * OffsetAnimSpeed;
                        return new Vector2(
                            Offset.X * (PerlinNoise.GetPerlin(t, t) - 0.5f),
                            Offset.Y * (PerlinNoise.GetPerlin(t + 0.5f, t + 0.5f) - 0.5f));
                    default:
                        return Offset;
                }
            }
            
            public float GetRotation(ref float rotationState)
            {
                if (rotationSpeedRadians <= 0.0f)
                {
                    return Rotation;
                }
                switch (OffsetAnim)
                {
                    case AnimationType.Sine:
                        rotationState = rotationState % (MathHelper.TwoPi / rotationSpeedRadians);
                        return Rotation * (float)Math.Sin(rotationState * rotationSpeedRadians);
                    case AnimationType.Noise:
                        rotationState = rotationState % (1.0f / rotationSpeedRadians);
                        return Rotation * PerlinNoise.GetPerlin(rotationState * rotationSpeedRadians, rotationState * rotationSpeedRadians);
                    default:
                        return rotationState * rotationSpeedRadians;
                }
            }

            public void Remove()
            {
                Sprite?.Remove();
                Sprite = null;
            }
        }

        public List<BrokenItemSprite> BrokenSprites = new List<BrokenItemSprite>();
        public List<DecorativeSprite> DecorativeSprites = new List<DecorativeSprite>();
        public Dictionary<int, List<DecorativeSprite>> DecorativeSpriteGroups = new Dictionary<int, List<DecorativeSprite>>();
        public Sprite InventoryIcon;

        //only used to display correct color in the sub editor, item instances have their own property that can be edited on a per-item basis
        [Serialize("1.0,1.0,1.0,1.0", false)]
        public Color InventoryIconColor
        {
            get;
            protected set;
        }

        public override void DrawPlacing(SpriteBatch spriteBatch, Camera cam)
        {
            Vector2 position = Submarine.MouseToWorldGrid(cam, Submarine.MainSub);

            if (PlayerInput.RightButtonClicked())
            {
                selected = null;
                return;
            }

            if (!ResizeHorizontal && !ResizeVertical)
            {
                sprite.Draw(spriteBatch, new Vector2(position.X, -position.Y) + sprite.size / 2.0f * Scale, SpriteColor, scale: Scale);

            }
            else
            {
                Vector2 placeSize = size;
                if (placePosition == Vector2.Zero)
                {
                    if (PlayerInput.LeftButtonHeld()) placePosition = position;
                }
                else
                {
                    if (ResizeHorizontal)
                        placeSize.X = Math.Max(position.X - placePosition.X, size.X);
                    if (ResizeVertical)
                        placeSize.Y = Math.Max(placePosition.Y - position.Y, size.Y);

                    position = placePosition;
                }

                if (sprite != null) sprite.DrawTiled(spriteBatch, new Vector2(position.X, -position.Y), placeSize, color: SpriteColor);
            }
        }

        public override void DrawPlacing(SpriteBatch spriteBatch, Rectangle placeRect, float scale = 1.0f)
        {
            if (!ResizeHorizontal && !ResizeVertical)
            {
                sprite.Draw(spriteBatch, new Vector2(placeRect.Center.X, -(placeRect.Y - placeRect.Height / 2)), SpriteColor, scale: Scale * scale);
            }
            else
            {
                if (sprite != null) sprite.DrawTiled(spriteBatch, new Vector2(placeRect.X, -placeRect.Y), placeRect.Size.ToVector2(), null, SpriteColor);
            }
        }
    }
}
