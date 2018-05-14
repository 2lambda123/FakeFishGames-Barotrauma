﻿using Barotrauma.Items.Components;
using Barotrauma.Lights;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma
{
    partial class Limb
    {
        //minimum duration between hit/attack sounds
        public const float SoundInterval = 0.4f;
        public float LastAttackSoundTime, LastImpactSoundTime;

        public LightSource LightSource
        {
            get;
            private set;
        }

        private string hitSoundTag;

        public string HitSoundTag
        {
            get { return hitSoundTag; }
        }

        partial void InitProjSpecific(XElement element)
        {
            foreach (XElement subElement in element.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "lightsource":
                        LightSource = new LightSource(subElement);
                        break;
                    case "sound":
                        hitSoundTag = subElement.GetAttributeString("tag", "");
                        if (string.IsNullOrWhiteSpace(hitSoundTag))
                        {
                            //legacy support
                            hitSoundTag = subElement.GetAttributeString("file", "");
                        }
                        break;
                }
            }
        }

        partial void UpdateProjSpecific()
        {
            if (LightSource != null)
            {
                LightSource.ParentSub = body.Submarine;
                LightSource.Rotation = (dir == Direction.Right) ? body.Rotation : body.Rotation - MathHelper.Pi;
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            float brightness = 1.0f - (burnOverLayStrength / 100.0f) * 0.5f;
            Color color = new Color(brightness, brightness, brightness);

            if (isSevered)
            {
                if (severedFadeOutTimer > SeveredFadeOutTime)
                {
                    return;
                }
                else if (severedFadeOutTimer > SeveredFadeOutTime - 1.0f)
                {
                    color *= SeveredFadeOutTime - severedFadeOutTimer;
                }
            }

            body.Dir = Dir;

            bool hideLimb = wearingItems.Any(w => w != null && w.HideLimb);
                body.UpdateDrawPosition();
            if (!hideLimb)
            {
                body.Draw(spriteBatch, sprite, color, null, scale);
            }
            else
            {
            }

            if (LightSource != null)
            {
                LightSource.Position = body.DrawPosition;
                LightSource.LightSpriteEffect = (dir == Direction.Right) ? SpriteEffects.None : SpriteEffects.FlipVertically;
            }

            WearableSprite onlyDrawable = wearingItems.Find(w => w.HideOtherWearables);

            foreach (WearableSprite wearable in wearingItems)
            {
                if (onlyDrawable != null && onlyDrawable != wearable) continue;

                SpriteEffects spriteEffect = (dir == Direction.Right) ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

                Vector2 origin = wearable.Sprite.Origin;
                if (body.Dir == -1.0f) origin.X = wearable.Sprite.SourceRect.Width - origin.X;
                
                float depth = wearable.Sprite.Depth;

                if (wearable.InheritLimbDepth)
                {
                    depth = sprite.Depth - 0.000001f;
                    if (wearable.DepthLimb != LimbType.None)
                    {
                        Limb depthLimb = character.AnimController.GetLimb(wearable.DepthLimb);
                        if (depthLimb != null)
                        {
                            depth = depthLimb.sprite.Depth - 0.000001f;
                        }
                    }
                }

                wearable.Sprite.Draw(spriteBatch,
                    new Vector2(body.DrawPosition.X, -body.DrawPosition.Y),
                    color, origin,
                    -body.DrawRotation,
                    scale, spriteEffect, depth);
            }

            if (damageOverlayStrength > 0.0f && damagedSprite != null && !hideLimb)
            {
                SpriteEffects spriteEffect = (dir == Direction.Right) ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

                float depth = sprite.Depth - 0.0000015f;

                damagedSprite.Draw(spriteBatch,
                    new Vector2(body.DrawPosition.X, -body.DrawPosition.Y),
                    color * Math.Min(damageOverlayStrength / 50.0f, 1.0f), sprite.Origin,
                    -body.DrawRotation,
                    1.0f, spriteEffect, depth);
            }

            if (!GameMain.DebugDraw) return;

            if (pullJoint != null)
            {
                Vector2 pos = ConvertUnits.ToDisplayUnits(pullJoint.WorldAnchorB);
                GUI.DrawRectangle(spriteBatch, new Rectangle((int)pos.X, (int)-pos.Y, 5, 5), Color.Red, true);
            }
        }
    }
}
