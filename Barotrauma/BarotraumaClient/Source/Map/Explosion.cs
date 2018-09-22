﻿using Barotrauma.Lights;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Barotrauma
{
    partial class Explosion
    {
        partial void ExplodeProjSpecific(Vector2 worldPosition, Hull hull)
        {
            if (shockwave)
            {
                GameMain.ParticleManager.CreateParticle("shockwave", worldPosition,
                    Vector2.Zero, 0.0f, hull);
            }

            hull = hull ?? Hull.FindHull(worldPosition, useWorldCoordinates: true);
            bool underwater = hull == null;

            if (underwater)
            {
                var underwaterExplosion = GameMain.ParticleManager.CreateParticle("underwaterexplosion", worldPosition, Vector2.Zero, 0.0f, hull);
                if (underwaterExplosion != null)
                {
                    underwaterExplosion.Size *= MathHelper.Clamp(attack.Range / 150.0f, 0.5f, 10.0f);
                    underwaterExplosion.StartDelay = 0.0f;
                }
            }

            for (int i = 0; i < attack.Range * 0.1f; i++)
            {
                if (underwater)
                {
                    Vector2 bubblePos = Rand.Vector(Rand.Range(0.0f, attack.Range * 0.5f));
                    GameMain.ParticleManager.CreateParticle("risingbubbles", worldPosition + bubblePos,
                        Vector2.Zero, 0.0f, hull);

                    if (i < attack.Range * 0.02f)
                    {
                        var underwaterExplosion = GameMain.ParticleManager.CreateParticle("underwaterexplosion", worldPosition + bubblePos,
                            Vector2.Zero, 0.0f, hull);
                        if (underwaterExplosion != null)
                        {
                            underwaterExplosion.Size *= MathHelper.Clamp(attack.Range / 300.0f, 0.5f, 2.0f) * Rand.Range(0.8f, 1.2f);
                        }
                    }
                }
                else
                {
                    float particleSpeed = Rand.Range(0.0f, 1.0f);
                    particleSpeed = particleSpeed * particleSpeed * attack.Range;

                    if (flames)
                    {
                        float particleScale = MathHelper.Clamp(attack.Range * 0.0025f, 0.5f, 2.0f);
                        var flameParticle = GameMain.ParticleManager.CreateParticle("explosionfire", 
                            ClampParticlePos(worldPosition + Rand.Vector((float)System.Math.Sqrt(Rand.Range(0.0f, attack.Range))), hull),
                            Rand.Vector(Rand.Range(0.0f, particleSpeed)), 0.0f, hull);
                        if (flameParticle != null) flameParticle.Size *= particleScale;
                    }
                    if (smoke)
                    {
                        var smokeParticle = GameMain.ParticleManager.CreateParticle(Rand.Range(0.0f, 1.0f) < 0.5f ? "explosionsmoke" : "smoke",
                            ClampParticlePos(worldPosition + Rand.Vector((float)System.Math.Sqrt(Rand.Range(0.0f, attack.Range))), hull),
                            Rand.Vector(Rand.Range(0.0f, particleSpeed)), 0.0f, hull);
                    }
                }

                if (sparks)
                {
                    GameMain.ParticleManager.CreateParticle("spark", worldPosition,
                        Rand.Vector(Rand.Range(500.0f, 800.0f)), 0.0f, hull);
                }
            }

            if (hull != null && !string.IsNullOrWhiteSpace(decal) && decalSize > 0.0f)
            {
                hull.AddDecal(decal, worldPosition, decalSize);
            }

            if (flash)
            {
                float displayRange = attack.Range;
                if (displayRange < 0.1f) return;

                var light = new LightSource(worldPosition, displayRange, Color.LightYellow, null);
                CoroutineManager.StartCoroutine(DimLight(light));
            }
        }

        private IEnumerable<object> DimLight(LightSource light)
        {
            float currBrightness = 1.0f;
            float startRange = light.Range;

            while (light.Color.A > 0.0f)
            {
                light.Color = new Color(light.Color.R, light.Color.G, light.Color.B, currBrightness);
                light.Range = startRange * currBrightness;

                currBrightness -= CoroutineManager.DeltaTime * 20.0f;

                yield return CoroutineStatus.Running;
            }

            light.Remove();

            yield return CoroutineStatus.Success;
        }
    }
}
