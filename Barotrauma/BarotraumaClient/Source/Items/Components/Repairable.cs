﻿using Barotrauma.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Barotrauma.Items.Components
{
    partial class Repairable : ItemComponent
    {
        private GUIButton repairButton;
        private GUIProgressBar progressBar;

        private List<ParticleEmitter> particleEmitters = new List<ParticleEmitter>();
        //the corresponding particle emitter is active when the condition is within this range
        private List<Vector2> particleEmitterConditionRanges = new List<Vector2>();

        private string repairButtonText, repairingText;

        [Serialize("", false)]
        public string Description
        {
            get;
            set;
        }
        
        public override bool ShouldDrawHUD(Character character)
        {
            if (!HasRequiredItems(character, false) || character.SelectedConstruction != item) return false;
            return (item.Condition < ShowRepairUIThreshold || (currentFixer == character && !item.IsFullCondition));
        }

        partial void InitProjSpecific(XElement element)
        {
            var paddedFrame = new GUILayoutGroup(new RectTransform(new Vector2(0.9f, 0.85f), GuiFrame.RectTransform, Anchor.Center), childAnchor: Anchor.TopCenter)
            {
                Stretch = true,
                RelativeSpacing = 0.05f
            };

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.15f), paddedFrame.RectTransform),
                header, textAlignment: Alignment.TopCenter, font: GUI.LargeFont);

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.0f), paddedFrame.RectTransform),
                Description, font: GUI.SmallFont, wrap: true);

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.15f), paddedFrame.RectTransform),
                TextManager.Get("RequiredRepairSkills"));
            for (int i = 0; i < requiredSkills.Count; i++)
            {
                var skillText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.15f), paddedFrame.RectTransform),
                    "   - " + TextManager.Get("SkillName." + requiredSkills[i].Identifier) + ": " + ((int)requiredSkills[i].Level), font: GUI.SmallFont)
                {
                    UserData = requiredSkills[i]
                };
            }

            progressBar = new GUIProgressBar(new RectTransform(new Vector2(1.0f, 0.15f), paddedFrame.RectTransform), 
                color: Color.Green, barSize: 0.0f);

            repairButtonText = TextManager.Get("RepairButton");
            repairingText = TextManager.Get("Repairing");
            repairButton = new GUIButton(new RectTransform(new Vector2(0.8f, 0.15f), paddedFrame.RectTransform, Anchor.TopCenter),
                repairButtonText)
            {
                OnClicked = (btn, obj) =>
                {
                    currentFixer = Character.Controlled;
                    item.CreateClientEvent(this);
                    return true;
                }
            };

            foreach (XElement subElement in element.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "emitter":
                    case "particleemitter":
                        particleEmitters.Add(new ParticleEmitter(subElement));
                        particleEmitterConditionRanges.Add(new Vector2(
                            subElement.GetAttributeFloat("mincondition", 0.0f), 
                            subElement.GetAttributeFloat("maxcondition", 100.0f)));
                        break;
                }
            }
        }

        partial void UpdateProjSpecific(float deltaTime)
        {
            for (int i = 0; i < particleEmitters.Count; i++)
            {
                if (item.ConditionPercentage >= particleEmitterConditionRanges[i].X && item.ConditionPercentage <= particleEmitterConditionRanges[i].Y)
                {
                    particleEmitters[i].Emit(deltaTime, item.WorldPosition, item.CurrentHull);
                }
            }
        }

        public override void DrawHUD(SpriteBatch spriteBatch, Character character)
        {
            IsActive = true;

            progressBar.BarSize = item.Condition / item.MaxCondition;
            progressBar.Color = ToolBox.GradientLerp(progressBar.BarSize, Color.Red, Color.Orange, Color.Green);

            repairButton.Enabled = currentFixer == null;
            repairButton.Text = currentFixer == null ? 
                repairButtonText : 
                repairingText + new string('.', ((int)(Timing.TotalTime * 2.0f) % 3) + 1);

            System.Diagnostics.Debug.Assert(GuiFrame.GetChild(0) is GUILayoutGroup, "Repair UI hierarchy has changed, could not find skill texts");
            foreach (GUIComponent c in GuiFrame.GetChild(0).Children)
            {
                Skill skill = c.UserData as Skill;
                if (skill == null) continue;

                GUITextBlock textBlock = (GUITextBlock)c;
                if (character.GetSkillLevel(skill.Identifier) < skill.Level)
                {
                    textBlock.TextColor = Color.Red;
                }
                else
                {
                    textBlock.TextColor = Color.White;
                }
            }
        }
    }
}
