﻿using Microsoft.Xna.Framework;
using System.Xml.Linq;

namespace Barotrauma.SpriteDeformations
{
    class NoiseDeformationParams : SpriteDeformationParams
    {
        [Serialize(0.0f, true), Editable(MinValueFloat = 0.0f, MaxValueFloat = 100.0f,
            ToolTip = "The frequency of the noise.")]
        public override float Frequency { get; set; }

        [Serialize(1.0f, true), Editable(MinValueFloat = 0.0f, MaxValueFloat = 10.0f,
            ToolTip = "How much the noise distorts the sprite.")]
        public float Amplitude { get; set; }

        [Serialize(0.0f, true), Editable(MinValueFloat = 0.0f, MaxValueFloat = 10.0f,
            ToolTip = "How fast the noise changes.")]
        public float ChangeSpeed { get; set; }

        public NoiseDeformationParams(XElement element) : base(element)
        {
        }
    }

    class NoiseDeformation : SpriteDeformation
    {
        private NoiseDeformationParams NoiseDeformationParams => deformationParams as NoiseDeformationParams;

        private float phase;

        public NoiseDeformation(XElement element) : base(element, new NoiseDeformationParams(element))
        {
            phase = Rand.Range(0.0f, 255.0f);
            UpdateNoise();
        }

        private void UpdateNoise()
        {
            for (int x = 0; x < Resolution.X; x++)
            {
                float normalizedX = x / (float)(Resolution.X - 1) * NoiseDeformationParams.Frequency;
                for (int y = 0; y < Resolution.Y; y++)
                {
                    float normalizedY = y / (float)(Resolution.X - 1) * NoiseDeformationParams.Frequency;
                    
                    Deformation[x, y] = new Vector2(
                        PerlinNoise.GetPerlin(normalizedX + phase, normalizedY + phase) - 0.5f,
                        PerlinNoise.GetPerlin(normalizedY - phase, normalizedX - phase) - 0.5f);
                }
            }
        }

        protected override void GetDeformation(out Vector2[,] deformation, out float multiplier)
        {
            deformation = Deformation;
            multiplier = NoiseDeformationParams.Amplitude;
        }

        public override void Update(float deltaTime)
        {
            if (NoiseDeformationParams.ChangeSpeed > 0.0f)
            {
                phase += deltaTime * NoiseDeformationParams.ChangeSpeed / 100.0f;
                phase %= 1.0f;
                UpdateNoise();
            }
        }
    }
}
