﻿using Barotrauma.Particles;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Barotrauma.Items.Components
{
    partial class RepairTool
    {
        public ParticleEmitter ParticleEmitter
        {
            get;
            private set;
        }

        private List<ParticleEmitter> ParticleEmitterHitStructure = new List<ParticleEmitter>();
        private List<ParticleEmitter> ParticleEmitterHitCharacter = new List<ParticleEmitter>();
        private List<Pair<RelatedItem, ParticleEmitter>> ParticleEmitterHitItem = new List<Pair<RelatedItem, ParticleEmitter>>();

        partial void InitProjSpecific(XElement element)
        {
            foreach (XElement subElement in element.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "particleemitter":
                        ParticleEmitter = new ParticleEmitter(subElement);
                        break;
                    case "particleemitterhititem":
                        string[] identifiers = subElement.GetAttributeStringArray("identifiers", new string[0]);
                        if (identifiers.Length == 0) identifiers = subElement.GetAttributeStringArray("identifier", new string[0]);
                        string[] excludedIdentifiers = subElement.GetAttributeStringArray("excludedidentifiers", new string[0]);
                        if (excludedIdentifiers.Length == 0) excludedIdentifiers = subElement.GetAttributeStringArray("excludedidentifier", new string[0]);
                        
                        ParticleEmitterHitItem.Add(
                            new Pair<RelatedItem, ParticleEmitter>(
                                new RelatedItem(identifiers, excludedIdentifiers), 
                                new ParticleEmitter(subElement)));
                        break;
                    case "particleemitterhitstructure":
                        ParticleEmitterHitStructure.Add(new ParticleEmitter(subElement));
                        break;
                    case "particleemitterhitcharacter":
                        ParticleEmitterHitCharacter.Add(new ParticleEmitter(subElement));
                        break;
                }
            }
        }


        partial void UseProjSpecific(float deltaTime)
        {
            if (ParticleEmitter != null)
            {
                float particleAngle = item.body.Rotation + ((item.body.Dir > 0.0f) ? 0.0f : MathHelper.Pi);
                ParticleEmitter.Emit(
                    deltaTime, item.WorldPosition + TransformedBarrelPos,
                    item.CurrentHull, particleAngle, -particleAngle);
            }
        }

        partial void FixStructureProjSpecific(Character user, float deltaTime, Structure targetStructure, int sectionIndex)
        {
            Vector2 progressBarPos = targetStructure.SectionPosition(sectionIndex);
            if (targetStructure.Submarine != null)
            {
                progressBarPos += targetStructure.Submarine.DrawPosition;
            }

            var progressBar = user.UpdateHUDProgressBar(
                targetStructure,
                progressBarPos,
                1.0f - targetStructure.SectionDamage(sectionIndex) / targetStructure.Health,
                Color.Red, Color.Green);

            if (progressBar != null) progressBar.Size = new Vector2(60.0f, 20.0f);

            Vector2 particlePos = ConvertUnits.ToDisplayUnits(pickedPosition);
            if (targetStructure.Submarine != null) particlePos += targetStructure.Submarine.DrawPosition;
            foreach (var emitter in ParticleEmitterHitStructure)
            {
                float particleAngle = item.body.Rotation + ((item.body.Dir > 0.0f) ? 0.0f : MathHelper.Pi);
                emitter.Emit(deltaTime, particlePos, item.CurrentHull, particleAngle + MathHelper.Pi, -particleAngle + MathHelper.Pi);
            }
        }

        partial void FixCharacterProjSpecific(Character user, float deltaTime, Character targetCharacter)
        {
            Vector2 particlePos = ConvertUnits.ToDisplayUnits(pickedPosition);
            if (targetCharacter.Submarine != null) particlePos += targetCharacter.Submarine.DrawPosition;
            foreach (var emitter in ParticleEmitterHitCharacter)
            {
                float particleAngle = item.body.Rotation + ((item.body.Dir > 0.0f) ? 0.0f : MathHelper.Pi);
                emitter.Emit(deltaTime, particlePos, item.CurrentHull, particleAngle + MathHelper.Pi, -particleAngle + MathHelper.Pi);
            }
        }

        partial void FixItemProjSpecific(Character user, float deltaTime, Item targetItem, float prevCondition)
        {
            if (prevCondition != targetItem.Condition)
            {
                Vector2 progressBarPos = targetItem.DrawPosition;
                var progressBar = user.UpdateHUDProgressBar(
                    targetItem,
                    progressBarPos,
                    targetItem.Condition / 100.0f,
                    Color.Red, Color.Green);
                if (progressBar != null) { progressBar.Size = new Vector2(60.0f, 20.0f); }
            }

            Vector2 particlePos = ConvertUnits.ToDisplayUnits(pickedPosition);
            if (targetItem.Submarine != null) particlePos += targetItem.Submarine.DrawPosition;
            foreach (var emitter in ParticleEmitterHitItem)
            {
                if (!emitter.First.MatchesItem(targetItem)) continue;
                float particleAngle = item.body.Rotation + ((item.body.Dir > 0.0f) ? 0.0f : MathHelper.Pi);
                emitter.Second.Emit(deltaTime, particlePos, item.CurrentHull, particleAngle + MathHelper.Pi, -particleAngle + MathHelper.Pi);
            }            
        }
    }
}
