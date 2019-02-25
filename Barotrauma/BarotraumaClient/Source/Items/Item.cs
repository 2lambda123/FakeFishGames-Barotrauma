﻿using Barotrauma.Items.Components;
using Barotrauma.Networking;
using FarseerPhysics;
using Lidgren.Network;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using Barotrauma.Extensions;

namespace Barotrauma
{
    partial class Item : MapEntity, IDamageable, ISerializableEntity, IServerSerializable, IClientSerializable
    {
        public static bool ShowItems = true;
        
        private readonly List<PosInfo> positionBuffer = new List<PosInfo>();

        private List<ItemComponent> activeHUDs = new List<ItemComponent>();

        public IEnumerable<ItemComponent> ActiveHUDs => activeHUDs;

        class SpriteState
        {
            public float RotationState;
            public float OffsetState;
            public bool IsActive = true;
        }

        private Dictionary<ItemPrefab.DecorativeSprite, SpriteState> spriteAnimState = new Dictionary<ItemPrefab.DecorativeSprite, SpriteState>();

        private Sprite activeSprite;
        public override Sprite Sprite
        {
            get { return activeSprite; }
        }

        private GUITextBlock itemInUseWarning;
        private GUITextBlock ItemInUseWarning
        {
            get
            {
                if (itemInUseWarning == null)
                {
                    itemInUseWarning = new GUITextBlock(new RectTransform(new Point(10), GUI.Canvas), "", 
                        textColor: Color.Orange, color: Color.Black, 
                        textAlignment:Alignment.Center, style: "OuterGlow");
                }
                return itemInUseWarning;
            }
        }

        public override bool SelectableInEditor
        {
            get
            {
                return parentInventory == null && (body == null || body.Enabled) && ShowItems;
            }
        }
              
        public float SpriteRotation;

        public Color GetSpriteColor()
        {
            Color color = spriteColor;
            if (Prefab.UseContainedSpriteColor && ownInventory != null)
            {
                for (int i = 0; i < ownInventory.Items.Length; i++)
                {
                    if (ownInventory.Items[i] != null)
                    {
                        color = ownInventory.Items[i].ContainerColor;
                        break;
                    }
                }
            }
            return color;
        }

        public Color GetInventoryIconColor()
        {
            Color color = InventoryIconColor;
            if (Prefab.UseContainedInventoryIconColor && ownInventory != null)
            {
                for (int i = 0; i < ownInventory.Items.Length; i++)
                {
                    if (ownInventory.Items[i] != null)
                    {
                        color = ownInventory.Items[i].ContainerColor;
                        break;
                    }
                }
            }
            return color;
        }

        partial void SetActiveSprite()
        {
            activeSprite = prefab.sprite;
            if (Container != null)
            {
                foreach (ContainedItemSprite containedSprite in Prefab.ContainedSprites)
                {
                    if (containedSprite.MatchesContainer(Container))
                    {
                        activeSprite = containedSprite.Sprite;
                        return;
                    }
                }
            }

            foreach (BrokenItemSprite brokenSprite in Prefab.BrokenSprites)
            {
                if (condition <= brokenSprite.MaxCondition)
                {
                    activeSprite = brokenSprite.Sprite;
                    break;
                }
            }
        }

        partial void InitProjSpecific()
        {
            foreach (var decorativeSprite in ((ItemPrefab)prefab).DecorativeSprites)
            {
                spriteAnimState.Add(decorativeSprite, new SpriteState());
            }
        }

        public override bool IsVisible(Rectangle worldView)
        {
            //no drawable components and the body has been disabled = nothing to draw
            if (drawableComponents.Count == 0 && body != null && !body.Enabled)
            {
                return false;
            }

            float padding = 100.0f;
            Vector2 size = new Vector2(rect.Width + padding, rect.Height + padding);
            foreach (IDrawableComponent drawable in drawableComponents)
            {
                size.X = Math.Max(drawable.DrawSize.X, size.X);
                size.Y = Math.Max(drawable.DrawSize.Y, size.Y);
            }
            size *= 0.5f;

            Vector2 worldPosition = WorldPosition;
            if (worldPosition.X - size.X > worldView.Right || worldPosition.X + size.X < worldView.X) return false;
            if (worldPosition.Y + size.Y < worldView.Y - worldView.Height || worldPosition.Y - size.Y > worldView.Y) return false;

            return true;
        }

        public override void Draw(SpriteBatch spriteBatch, bool editing, bool back = true)
        {
            if (!Visible || (!editing && hiddenInGame)) return;
            if (editing && !ShowItems) return;
            
            Color color = isHighlighted && !GUI.DisableItemHighlights ? Color.Orange : GetSpriteColor();
            //if (IsSelected && editing) color = Color.Lerp(color, Color.Gold, 0.5f);

            Sprite activeSprite = prefab.sprite;
            BrokenItemSprite fadeInBrokenSprite = null;
            float fadeInBrokenSpriteAlpha = 0.0f;
            if (condition < 100.0f)
            {
                for (int i = 0; i < Prefab.BrokenSprites.Count; i++)
                {
                    if (condition <= Prefab.BrokenSprites[i].MaxCondition)
                    {
                        activeSprite = Prefab.BrokenSprites[i].Sprite;
                        break;
                    }

                    if (Prefab.BrokenSprites[i].FadeIn)
                    {
                        float min = i > 0 ? Prefab.BrokenSprites[i].MaxCondition : 0.0f;
                        float max = i < Prefab.BrokenSprites.Count - 1 ? Prefab.BrokenSprites[i + 1].MaxCondition : 100.0f;
                        fadeInBrokenSpriteAlpha = 1.0f - ((condition - min) / (max - min));
                        if (fadeInBrokenSpriteAlpha > 0.0f && fadeInBrokenSpriteAlpha < 1.0f)
                        {
                            fadeInBrokenSprite = Prefab.BrokenSprites[i];
                        }
                    }
                }
            }

            if (activeSprite != null)
            {
                SpriteEffects oldEffects = activeSprite.effects;
                activeSprite.effects ^= SpriteEffects;
                SpriteEffects oldBrokenSpriteEffects = SpriteEffects.None;
                if (fadeInBrokenSprite != null)
                {
                    oldBrokenSpriteEffects = fadeInBrokenSprite.Sprite.effects;
                    fadeInBrokenSprite.Sprite.effects ^= SpriteEffects;
                }

                float depth = GetDrawDepth();
                if (body == null)
                {
                    bool flipHorizontal = (SpriteEffects & SpriteEffects.FlipHorizontally) != 0;
                    bool flipVertical = (SpriteEffects & SpriteEffects.FlipVertically) != 0;

                    if (prefab.ResizeHorizontal || prefab.ResizeVertical)
                    {
                        activeSprite.DrawTiled(spriteBatch, new Vector2(DrawPosition.X - rect.Width / 2, -(DrawPosition.Y + rect.Height / 2)), new Vector2(rect.Width, rect.Height), color: color,
                            depth: depth);
                        fadeInBrokenSprite?.Sprite.DrawTiled(spriteBatch, new Vector2(DrawPosition.X - rect.Width / 2, -(DrawPosition.Y + rect.Height / 2)), new Vector2(rect.Width, rect.Height), color: color * fadeInBrokenSpriteAlpha,
                            depth: depth - 0.000001f);
                        foreach (var decorativeSprite in Prefab.DecorativeSprites)
                        {
                            if (!spriteAnimState[decorativeSprite].IsActive) { continue; }                            
                            Vector2 offset = decorativeSprite.GetOffset(ref spriteAnimState[decorativeSprite].OffsetState) * Scale;
                            decorativeSprite.Sprite.DrawTiled(spriteBatch, 
                                new Vector2(DrawPosition.X + offset.X - rect.Width / 2, -(DrawPosition.Y + offset.Y + rect.Height / 2)), 
                                new Vector2(rect.Width, rect.Height), color: color,
                                depth: depth + (decorativeSprite.Sprite.Depth - activeSprite.Depth));
                        }
                    }
                    else
                    {
                        activeSprite.Draw(spriteBatch, new Vector2(DrawPosition.X, -DrawPosition.Y), color, SpriteRotation, Scale, activeSprite.effects, depth);
                        fadeInBrokenSprite?.Sprite.Draw(spriteBatch, new Vector2(DrawPosition.X, -DrawPosition.Y), color * fadeInBrokenSpriteAlpha, SpriteRotation, Scale, activeSprite.effects, depth - 0.000001f);
                        foreach (var decorativeSprite in Prefab.DecorativeSprites)
                        {
                            if (!spriteAnimState[decorativeSprite].IsActive) { continue; }
                            float rotation = decorativeSprite.GetRotation(ref spriteAnimState[decorativeSprite].RotationState);
                            Vector2 offset = decorativeSprite.GetOffset(ref spriteAnimState[decorativeSprite].OffsetState) * Scale;
                            decorativeSprite.Sprite.Draw(spriteBatch, new Vector2(DrawPosition.X + offset.X, -(DrawPosition.Y + offset.Y)), color, 
                                SpriteRotation + rotation, Scale, activeSprite.effects,
                                depth: depth + (decorativeSprite.Sprite.Depth - activeSprite.Depth));
                        }
                    }
                }
                else if (body.Enabled)
                {
                    var holdable = GetComponent<Holdable>();
                    if (holdable != null && holdable.Picker?.AnimController != null)
                    {
                        if (holdable.Picker.SelectedItems[0] == this)
                        {
                            Limb holdLimb = holdable.Picker.AnimController.GetLimb(LimbType.RightHand);
                            depth = holdLimb.ActiveSprite.Depth + 0.000001f;
                            foreach (WearableSprite wearableSprite in holdLimb.WearingItems)
                            {
                                if (!wearableSprite.InheritLimbDepth && wearableSprite.Sprite != null) depth = Math.Min(wearableSprite.Sprite.Depth, depth);
                            }
                        }
                        else if (holdable.Picker.SelectedItems[1] == this)
                        {
                            Limb holdLimb = holdable.Picker.AnimController.GetLimb(LimbType.LeftHand);
                            depth = holdLimb.ActiveSprite.Depth - 0.000001f;
                            foreach (WearableSprite wearableSprite in holdLimb.WearingItems)
                            {
                                if (!wearableSprite.InheritLimbDepth && wearableSprite.Sprite != null) depth = Math.Max(wearableSprite.Sprite.Depth, depth);
                            }
                        }
                    }
                    body.Draw(spriteBatch, activeSprite, color, depth, Scale);
                    if (fadeInBrokenSprite != null) body.Draw(spriteBatch, fadeInBrokenSprite.Sprite, color * fadeInBrokenSpriteAlpha, depth - 0.000001f, Scale);

                    foreach (var decorativeSprite in Prefab.DecorativeSprites)
                    {
                        if (!spriteAnimState[decorativeSprite].IsActive) { continue; }
                        float rotation = decorativeSprite.GetRotation(ref spriteAnimState[decorativeSprite].RotationState);
                        Vector2 offset = decorativeSprite.GetOffset(ref spriteAnimState[decorativeSprite].OffsetState) * Scale;

                        var ca = (float)Math.Cos(-body.Rotation);
                        var sa = (float)Math.Sin(-body.Rotation);
                        Vector2 transformedOffset = new Vector2(ca * offset.X + sa * offset.Y, -sa * offset.X + ca * offset.Y);

                        decorativeSprite.Sprite.Draw(spriteBatch, new Vector2(DrawPosition.X + transformedOffset.X, -(DrawPosition.Y + transformedOffset.Y)), color,
                            -body.Rotation + rotation, Scale, activeSprite.effects,
                            depth: depth + (decorativeSprite.Sprite.Depth - activeSprite.Depth));
                    }
                }

                activeSprite.effects = oldEffects;
                if (fadeInBrokenSprite != null)
                {
                    fadeInBrokenSprite.Sprite.effects = oldEffects;
                }
            }

            //use a backwards for loop because the drawable components may disable drawing, 
            //causing them to be removed from the list
            for (int i = drawableComponents.Count - 1; i >= 0; i--)
            {
                drawableComponents[i].Draw(spriteBatch, editing);
            }

            if (GameMain.DebugDraw)
            {
                aiTarget?.Draw(spriteBatch);
                var containedItems = ContainedItems;
                if (containedItems != null)
                {
                    foreach (Item item in containedItems)
                    {
                        item.AiTarget?.Draw(spriteBatch);
                    }
                }
            }

            if (!editing || (body != null && !body.Enabled))
            {
                return;
            }

            if (IsSelected || IsHighlighted)
            {
                GUI.DrawRectangle(spriteBatch, new Vector2(DrawPosition.X - rect.Width / 2, -(DrawPosition.Y + rect.Height / 2)), new Vector2(rect.Width, rect.Height), 
                    Color.White, false, 0, thickness: Math.Max(1, (int)(2 / Screen.Selected.Cam.Zoom)));

                foreach (Rectangle t in Prefab.Triggers)
                {
                    Rectangle transformedTrigger = TransformTrigger(t);

                    Vector2 rectWorldPos = new Vector2(transformedTrigger.X, transformedTrigger.Y);
                    if (Submarine != null) rectWorldPos += Submarine.Position;
                    rectWorldPos.Y = -rectWorldPos.Y;

                    GUI.DrawRectangle(spriteBatch,
                        rectWorldPos,
                        new Vector2(transformedTrigger.Width, transformedTrigger.Height),
                        Color.Green,
                        false,
                        0,
                        (int)Math.Max((1.5f / GameScreen.Selected.Cam.Zoom), 1.0f));
                }
            }

            if (!ShowLinks) return;

            foreach (MapEntity e in linkedTo)
            {
                bool isLinkAllowed = prefab.IsLinkAllowed(e.prefab);
                Color lineColor = Color.Red * 0.5f;
                if (isLinkAllowed)
                {
                    lineColor = e is Item i && (DisplaySideBySideWhenLinked || i.DisplaySideBySideWhenLinked) ? Color.Purple * 0.5f : Color.LightGreen * 0.5f;
                }
                Vector2 from = new Vector2(WorldPosition.X, -WorldPosition.Y);
                Vector2 to = new Vector2(e.WorldPosition.X, -e.WorldPosition.Y);
                GUI.DrawLine(spriteBatch, from, to, lineColor * 0.25f, width: 3);
                GUI.DrawLine(spriteBatch, from, to, lineColor, width: 1);
                //GUI.DrawString(spriteBatch, from, $"Linked to {e.Name}", lineColor, Color.Black * 0.5f);
            }
        }

        public void UpdateSpriteStates(float deltaTime)
        {
            foreach (int spriteGroup in Prefab.DecorativeSpriteGroups.Keys)
            {
                for (int i = 0; i < Prefab.DecorativeSpriteGroups[spriteGroup].Count; i++)
                {
                    var decorativeSprite = Prefab.DecorativeSpriteGroups[spriteGroup][i];
                    if (decorativeSprite == null) { continue; }
                    if (spriteGroup > 0)
                    {
                        int activeSpriteIndex = ID % Prefab.DecorativeSpriteGroups[spriteGroup].Count;
                        if (i != activeSpriteIndex)
                        {
                            spriteAnimState[decorativeSprite].IsActive = false;
                            continue;
                        }
                    }

                    //check if the sprite is active (whether it should be drawn or not)
                    var spriteState = spriteAnimState[decorativeSprite];
                    spriteState.IsActive = true;
                    foreach (PropertyConditional conditional in decorativeSprite.IsActiveConditionals)
                    {
                        if (!ConditionalMatches(conditional))
                        {
                            spriteState.IsActive = false;
                            break;
                        }
                    }
                    if (!spriteState.IsActive) { continue; }

                    //check if the sprite should be animated
                    bool animate = true;
                    foreach (PropertyConditional conditional in decorativeSprite.AnimationConditionals)
                    {
                        if (!ConditionalMatches(conditional)) { animate = false; break; }
                    }
                    if (!animate) { continue; }
                    spriteState.OffsetState += deltaTime;
                    spriteState.RotationState += deltaTime;

                    bool ConditionalMatches(PropertyConditional conditional)
                    {
                        if (string.IsNullOrEmpty(conditional.TargetItemComponentName))
                        {
                            if (!conditional.Matches(this)) { return false; }
                        }
                        else
                        {
                            foreach (ItemComponent component in components)
                            {
                                if (component.Name != conditional.TargetItemComponentName) { continue; }
                                if (!conditional.Matches(component)) { return false; }
                            }
                        }
                        return true;
                    }
                }
            }
            
        }

        public override void UpdateEditing(Camera cam)
        {
            if (editingHUD == null || editingHUD.UserData as Item != this)
            {
                editingHUD = CreateEditingHUD(Screen.Selected != GameMain.SubEditorScreen);
            }

            if (Screen.Selected != GameMain.SubEditorScreen) return;

            if (Character.Controlled == null) activeHUDs.Clear();

            if (!Linkable) return;

            if (!PlayerInput.KeyDown(Keys.Space)) return;
            bool lClick = PlayerInput.LeftButtonClicked();
            bool rClick = PlayerInput.RightButtonClicked();
            if (!lClick && !rClick) return;

            Vector2 position = cam.ScreenToWorld(PlayerInput.MousePosition);
            var otherEntity = mapEntityList.FirstOrDefault(e => e != this && e.IsHighlighted && e.IsMouseOn(position));
            if (otherEntity != null)
            {
                if (linkedTo.Contains(otherEntity))
                {
                    linkedTo.Remove(otherEntity);
                    if (otherEntity.linkedTo != null && otherEntity.linkedTo.Contains(this)) otherEntity.linkedTo.Remove(this);
                }
                else
                {
                    linkedTo.Add(otherEntity);
                    if (otherEntity.Linkable && otherEntity.linkedTo != null) otherEntity.linkedTo.Add(this);
                }
            }
        }
        
        private GUIComponent CreateEditingHUD(bool inGame = false)
        {
            editingHUD = new GUIFrame(new RectTransform(new Vector2(0.3f, 0.25f), GUI.Canvas, Anchor.CenterRight) { MinSize = new Point(400, 0) }) { UserData = this };
            GUIListBox listBox = new GUIListBox(new RectTransform(new Vector2(0.95f, 0.8f), editingHUD.RectTransform, Anchor.Center), style: null)
            {
                Spacing = 5
            };

            var itemEditor = new SerializableEntityEditor(listBox.Content.RectTransform, this, inGame, showName: true);

            if (!inGame && Linkable)
            {
                var linkText = new GUITextBlock(new RectTransform(new Point(editingHUD.Rect.Width, 20)), TextManager.Get("HoldToLink"), font: GUI.SmallFont);
                var itemsText = new GUITextBlock(new RectTransform(new Point(editingHUD.Rect.Width, 20)), TextManager.Get("AllowedLinks") + ": ", font: GUI.SmallFont);
                if (AllowedLinks.None())
                {
                    itemsText.Text += TextManager.Get("None");
                }
                else
                {
                    for (int i = 0; i < AllowedLinks.Count; i++)
                    {
                        itemsText.Text += AllowedLinks[i];
                        if (i < AllowedLinks.Count - 1)
                        {
                            itemsText.Text += ", ";
                        }
                    }
                }
                itemEditor.AddCustomContent(linkText, 1);
                itemEditor.AddCustomContent(itemsText, 2);
                linkText.TextColor = Color.Yellow;
                itemsText.TextColor = Color.Yellow;
            }
            if (!inGame && Sprite != null)
            {
                var reloadTextureButton = new GUIButton(new RectTransform(new Point(editingHUD.Rect.Width / 2, 20)), "Reload Texture");
                reloadTextureButton.OnClicked += (button, data) =>
                {
                    Sprite.ReloadTexture();
                    Sprite.ReloadXML();
                    return true;
                };
                itemEditor.AddCustomContent(reloadTextureButton, itemEditor.ContentCount);
            }            

            foreach (ItemComponent ic in components)
            {
                if (inGame)
                {
                    if (!ic.AllowInGameEditing) continue;
                    if (SerializableProperty.GetProperties<InGameEditable>(ic).Count == 0) continue;
                }
                else
                {
                    if (ic.requiredItems.Count == 0 && SerializableProperty.GetProperties<Editable>(ic).Count == 0) continue;
                }

                var componentEditor = new SerializableEntityEditor(listBox.Content.RectTransform, ic, inGame, showName: !inGame);
                
                if (inGame) continue;

                foreach (var kvp in ic.requiredItems)
                {
                    foreach (RelatedItem relatedItem in kvp.Value)
                    {
                        var textBlock = new GUITextBlock(new RectTransform(new Point(editingHUD.Rect.Width, 20)),
                            relatedItem.Type.ToString() + " required", font: GUI.SmallFont)
                        {
                            Padding = new Vector4(10.0f, 0.0f, 10.0f, 0.0f)
                        };
                        componentEditor.AddCustomContent(textBlock, 1);

                        GUITextBox namesBox = new GUITextBox(new RectTransform(new Vector2(0.5f, 1.0f), textBlock.RectTransform, Anchor.CenterRight))
                        {
                            Font = GUI.SmallFont,
                            Text = relatedItem.JoinedIdentifiers
                        };

                        namesBox.OnDeselected += (textBox, key) =>
                        {
                            relatedItem.JoinedIdentifiers = textBox.Text;
                            textBox.Text = relatedItem.JoinedIdentifiers;
                        };

                        namesBox.OnEnterPressed += (textBox, text) =>
                        {
                            relatedItem.JoinedIdentifiers = text;
                            textBox.Text = relatedItem.JoinedIdentifiers;
                            return true;
                        };
                    }
                }
            }

            PositionEditingHUD();
            SetHUDLayout();

            return editingHUD;
        }
        
        /// <summary>
        /// Reposition currently active item interfaces to make sure they don't overlap with each other
        /// </summary>
        private void SetHUDLayout()
        {
            //reset positions first
            List<GUIComponent> elementsToMove = new List<GUIComponent>();

            if (editingHUD != null && editingHUD.UserData == this)
            {
                elementsToMove.Add(editingHUD);
            }       
            
            foreach (ItemComponent ic in activeHUDs)
            {
                if (ic.GuiFrame == null || ic.AllowUIOverlap || ic.GetLinkUIToComponent() != null) continue;
                ic.GuiFrame.RectTransform.ScreenSpaceOffset = Point.Zero;
                elementsToMove.Add(ic.GuiFrame);
            }

            List<Rectangle> disallowedAreas = new List<Rectangle>();
            if (GameMain.GameSession?.CrewManager != null)
            {
                disallowedAreas.Add(GameMain.GameSession.CrewManager.GetCharacterListArea());
            }

            GUI.PreventElementOverlap(elementsToMove, disallowedAreas,
                new Rectangle(
                    20, 20, 
                    GameMain.GraphicsWidth - 40, 
                    HUDLayoutSettings.InventoryTopY > 0 ? HUDLayoutSettings.InventoryTopY - 20 : GameMain.GraphicsHeight - 80));

            foreach (ItemComponent ic in activeHUDs)
            {
                if (ic.GuiFrame == null) continue;
                var linkUIToComponent = ic.GetLinkUIToComponent();
                if (linkUIToComponent == null) continue;
                
                ic.GuiFrame.RectTransform.ScreenSpaceOffset = linkUIToComponent.GuiFrame.RectTransform.ScreenSpaceOffset;
            }
        }

        public void UpdateHUD(Camera cam, Character character, float deltaTime)
        {
            bool editingHUDCreated = false;
            if (HasInGameEditableProperties ||
                Screen.Selected == GameMain.SubEditorScreen)
            {
                UpdateEditing(cam);
                editingHUDCreated = editingHUD != null;
            }

            List<ItemComponent> prevActiveHUDs = new List<ItemComponent>(activeHUDs);
            List<ItemComponent> activeComponents = new List<ItemComponent>(components);
            foreach (MapEntity entity in linkedTo)
            {
                if (prefab.IsLinkAllowed(entity.prefab) && entity is Item i)
                {
                    if (!i.DisplaySideBySideWhenLinked) continue;
                    activeComponents.AddRange(i.components);
                }
            }

            activeHUDs.Clear();
            //the HUD of the component with the highest priority will be drawn
            //if all components have a priority of 0, all of them are drawn
            List<ItemComponent> maxPriorityHUDs = new List<ItemComponent>();
            foreach (ItemComponent ic in activeComponents)
            {
                if (ic.CanBeSelected && ic.HudPriority > 0 && ic.ShouldDrawHUD(character) &&
                    (maxPriorityHUDs.Count == 0 || ic.HudPriority >= maxPriorityHUDs[0].HudPriority))
                {
                    if (maxPriorityHUDs.Count > 0 && ic.HudPriority > maxPriorityHUDs[0].HudPriority) maxPriorityHUDs.Clear();
                    maxPriorityHUDs.Add(ic);
                }
            }

            if (maxPriorityHUDs.Count > 0)
            {
                activeHUDs.AddRange(maxPriorityHUDs);
            }
            else
            {
                foreach (ItemComponent ic in activeComponents)
                {
                    if (ic.CanBeSelected && ic.ShouldDrawHUD(character)) activeHUDs.Add(ic);
                }
            }

            //active HUDs have changed, need to reposition
            if (!prevActiveHUDs.SequenceEqual(activeHUDs) || editingHUDCreated)
            {
                SetHUDLayout();
            }

            Rectangle mergedHUDRect = Rectangle.Empty;
            foreach (ItemComponent ic in activeHUDs)
            {
                ic.UpdateHUD(character, deltaTime, cam);
                if (ic.GuiFrame != null && ic.GuiFrame.Rect.Height < GameMain.GraphicsHeight)
                {
                    mergedHUDRect = mergedHUDRect == Rectangle.Empty ?
                        ic.GuiFrame.Rect :
                        Rectangle.Union(mergedHUDRect, ic.GuiFrame.Rect);
                }
            }

            if (itemInUseWarning != null && mergedHUDRect != Rectangle.Empty)
            {
                itemInUseWarning.Visible = false;
                foreach (Character otherCharacter in Character.CharacterList)
                {
                    if (otherCharacter != character &&
                        otherCharacter.SelectedConstruction == character.SelectedConstruction)
                    {
                        ItemInUseWarning.Visible = true;
                        if (mergedHUDRect.Width > GameMain.GraphicsWidth / 2) { mergedHUDRect.Inflate(-GameMain.GraphicsWidth / 4, 0); }
                        itemInUseWarning.RectTransform.ScreenSpaceOffset = new Point(mergedHUDRect.X, mergedHUDRect.Bottom);
                        itemInUseWarning.RectTransform.NonScaledSize = new Point(mergedHUDRect.Width, (int)(50 * GUI.Scale));
                        if (itemInUseWarning.UserData != otherCharacter)
                        {
                            itemInUseWarning.Text = TextManager.Get("ItemInUse").Replace("[character]", otherCharacter.Name);
                            itemInUseWarning.UserData = otherCharacter;
                        }
                        break;
                    }
                }
            }
        }
        
        public void DrawHUD(SpriteBatch spriteBatch, Camera cam, Character character)
        {
            if (HasInGameEditableProperties)
            {
                DrawEditing(spriteBatch, cam);
            }
            
            foreach (ItemComponent ic in activeHUDs)
            {
                if (ic.CanBeSelected)
                {
                    ic.DrawHUD(spriteBatch, character);
                }
            }
        }

        public override void AddToGUIUpdateList()
        {
            AddToGUIUpdateList(addLinkedHUDs: true);
        }

        private void AddToGUIUpdateList(bool addLinkedHUDs)
        {
            if (Screen.Selected is SubEditorScreen)
            {
                if (editingHUD != null && editingHUD.UserData == this) editingHUD.AddToGUIUpdateList();
            }
            else
            {
                if (HasInGameEditableProperties)
                {
                    if (editingHUD != null && editingHUD.UserData == this) editingHUD.AddToGUIUpdateList();
                }
            }

            if (Character.Controlled != null && Character.Controlled?.SelectedConstruction != this) return;

            bool needsLayoutUpdate = false;
            foreach (ItemComponent ic in activeHUDs)
            {
                if (!ic.CanBeSelected) { continue; }

                bool useAlternativeLayout = ic.Item != this;
                bool wasUsingAlternativeLayout = ic.UseAlternativeLayout;
                ic.UseAlternativeLayout = useAlternativeLayout;
                needsLayoutUpdate |= ic.UseAlternativeLayout != wasUsingAlternativeLayout;
                ic.AddToGUIUpdateList();
            }

            if (itemInUseWarning != null && itemInUseWarning.Visible)
            {
                itemInUseWarning.AddToGUIUpdateList();
            }

            if (needsLayoutUpdate)
            {
                SetHUDLayout();
            }
        }

        public void ClientRead(ServerNetObject type, NetBuffer msg, float sendingTime)
        {
            if (type == ServerNetObject.ENTITY_POSITION)
            {
                ClientReadPosition(type, msg, sendingTime);
                return;
            }

            NetEntityEvent.Type eventType =
                (NetEntityEvent.Type)msg.ReadRangedInteger(0, Enum.GetValues(typeof(NetEntityEvent.Type)).Length - 1);

            switch (eventType)
            {
                case NetEntityEvent.Type.ComponentState:
                    {
                        int componentIndex = msg.ReadRangedInteger(0, components.Count - 1);
                        (components[componentIndex] as IServerSerializable).ClientRead(type, msg, sendingTime);
                    }
                    break;
                    
                case NetEntityEvent.Type.InventoryState:
                    { 
                        int containerIndex = msg.ReadRangedInteger(0, components.Count - 1);
                        (components[containerIndex] as ItemContainer).Inventory.ClientRead(type, msg, sendingTime);
                    }
                    break;
                case NetEntityEvent.Type.Status:
                    float prevCondition = condition;
                    condition = msg.ReadSingle();
                    if (prevCondition > 0.0f && condition <= 0.0f)
                    {
                        ApplyStatusEffects(ActionType.OnBroken, 1.0f);
                        foreach (ItemComponent ic in components)
                        {
                            ic.PlaySound(ActionType.OnBroken, WorldPosition);
                        }
                    }
                    break;
                case NetEntityEvent.Type.ApplyStatusEffect:
                    {
                        ActionType actionType = (ActionType)msg.ReadRangedInteger(0, Enum.GetValues(typeof(ActionType)).Length - 1);
                        byte componentIndex = msg.ReadByte();
                        ushort targetID = msg.ReadUInt16();
                        byte targetLimbID = msg.ReadByte();

                        ItemComponent targetComponent = componentIndex < components.Count ? components[componentIndex] : null;
                        Character target = FindEntityByID(targetID) as Character;
                        Limb targetLimb = target != null && targetLimbID < target.AnimController.Limbs.Length ? target.AnimController.Limbs[targetLimbID] : null;
                        
                        if (targetComponent == null)
                        {
                            ApplyStatusEffects(actionType, 1.0f, target, targetLimb, true);
                        }
                        else
                        {
                            targetComponent.ApplyStatusEffects(actionType, 1.0f, target, targetLimb);
                        }                        
                    }
                    break;
                case NetEntityEvent.Type.ChangeProperty:
                    ReadPropertyChange(msg, false);
                    break;
                case NetEntityEvent.Type.Invalid:
                    break;
            }
        }

        public void ClientWrite(NetBuffer msg, object[] extraData = null)
        {
            if (extraData == null || extraData.Length == 0 || !(extraData[0] is NetEntityEvent.Type))
            {
                return;
            }

            NetEntityEvent.Type eventType = (NetEntityEvent.Type)extraData[0];
            msg.WriteRangedInteger(0, Enum.GetValues(typeof(NetEntityEvent.Type)).Length - 1, (int)eventType);
            switch (eventType)
            {
                case NetEntityEvent.Type.ComponentState:
                    int componentIndex = (int)extraData[1];
                    msg.WriteRangedInteger(0, components.Count - 1, componentIndex);
                    (components[componentIndex] as IClientSerializable).ClientWrite(msg, extraData);
                    break;
                case NetEntityEvent.Type.InventoryState:
                    int containerIndex = (int)extraData[1];
                    msg.WriteRangedInteger(0, components.Count - 1, containerIndex);
                    (components[containerIndex] as ItemContainer).Inventory.ClientWrite(msg, extraData);
                    break;
                case NetEntityEvent.Type.Treatment:
                    UInt16 characterID = (UInt16)extraData[1];
                    Limb targetLimb = (Limb)extraData[2];

                    Character targetCharacter = FindEntityByID(characterID) as Character;

                    msg.Write(characterID);
                    msg.Write(targetCharacter == null ? (byte)255 : (byte)Array.IndexOf(targetCharacter.AnimController.Limbs, targetLimb));               
                    break;
                case NetEntityEvent.Type.ChangeProperty:
                    WritePropertyChange(msg, extraData, true);
                    break;
            }
            msg.WritePadBits();
        }

        partial void ClientUpdatePosition()
        {
            if (GameMain.Client == null) { return; }

            Vector2 newVelocity = body.LinearVelocity;
            Vector2 newPosition = body.SimPosition;
            float newAngularVelocity = body.AngularVelocity;
            float newRotation = body.Rotation;
            body.CorrectPosition(positionBuffer, out newPosition, out newVelocity, out newRotation, out newAngularVelocity);

            body.LinearVelocity = newVelocity;
            body.AngularVelocity = newAngularVelocity;
            if (Vector2.DistanceSquared(newPosition, body.SimPosition) > 0.0001f ||
                Math.Abs(newRotation - body.Rotation) > 0.01f)
            {
                body.TargetPosition = newPosition;
                body.TargetRotation = newRotation;
                body.MoveToTargetPosition(lerp: true);
            }

            Vector2 displayPos = ConvertUnits.ToDisplayUnits(body.SimPosition);
            rect.X = (int)(displayPos.X - rect.Width / 2.0f);
            rect.Y = (int)(displayPos.Y + rect.Height / 2.0f);
        }

        public void ClientReadPosition(ServerNetObject type, NetBuffer msg, float sendingTime)
        {
            if (body == null)
            {
                DebugConsole.ThrowError("Received a position update for an item with no physics body (" + Name + ")");
                return;
            }

            var posInfo = body.ClientRead(type, msg, sendingTime, parentDebugName: Name);
            msg.ReadPadBits();
            if (posInfo != null)
            {
                int index = 0;
                while (index < positionBuffer.Count && sendingTime > positionBuffer[index].Timestamp)
                {
                    index++;
                }

                positionBuffer.Insert(index, posInfo);
            }
            /*body.FarseerBody.Awake = awake;
            if (body.FarseerBody.Awake)
            {
                if ((newVelocity - body.LinearVelocity).LengthSquared() > 8.0f * 8.0f) body.LinearVelocity = newVelocity;
            }
            else
            {
                try
                {
                    body.FarseerBody.Enabled = false;
                }
                catch (Exception e)
                {
                    DebugConsole.ThrowError("Exception in PhysicsBody.Enabled = false (" + body.PhysEnabled + ")", e);
                    if (body.UserData != null) DebugConsole.NewMessage("PhysicsBody UserData: " + body.UserData.GetType().ToString(), Color.Red);
                    if (GameMain.World.ContactManager == null) DebugConsole.NewMessage("ContactManager is null!", Color.Red);
                    else if (GameMain.World.ContactManager.BroadPhase == null) DebugConsole.NewMessage("Broadphase is null!", Color.Red);
                    if (body.FarseerBody.FixtureList == null) DebugConsole.NewMessage("FixtureList is null!", Color.Red);
                }
            }

            if ((newPosition - SimPosition).Length() > body.LinearVelocity.Length() * 2.0f)
            {
                if (body.SetTransform(newPosition, newRotation))
                {
                    Vector2 displayPos = ConvertUnits.ToDisplayUnits(body.SimPosition);
                    rect.X = (int)(displayPos.X - rect.Width / 2.0f);
                    rect.Y = (int)(displayPos.Y + rect.Height / 2.0f);
                }
            }*/
        }

        public void CreateClientEvent<T>(T ic) where T : ItemComponent, IClientSerializable
        {
            if (GameMain.Client == null) return;

            int index = components.IndexOf(ic);
            if (index == -1) return;

            GameMain.Client.CreateEntityEvent(this, new object[] { NetEntityEvent.Type.ComponentState, index });
        }
        
        public static Item ReadSpawnData(NetBuffer msg, bool spawn = true)
        {
            string itemName = msg.ReadString();
            string itemIdentifier = msg.ReadString();
            bool descriptionChanged = msg.ReadBoolean();
            string itemDesc = "";
            if (descriptionChanged)
            {
                itemDesc = msg.ReadString();
            }
            ushort itemId = msg.ReadUInt16();
            ushort inventoryId = msg.ReadUInt16();

            DebugConsole.Log("Received entity spawn message for item " + itemName + ".");

            Vector2 pos = Vector2.Zero;
            Submarine sub = null;
            int itemContainerIndex = -1;
            int inventorySlotIndex = -1;

            if (inventoryId > 0)
            {
                itemContainerIndex = msg.ReadByte();
                inventorySlotIndex = msg.ReadByte();
            }
            else
            {
                pos = new Vector2(msg.ReadSingle(), msg.ReadSingle());

                ushort subID = msg.ReadUInt16();
                if (subID > 0)
                {
                    sub = Submarine.Loaded.Find(s => s.ID == subID);
                }
            }

            byte teamID = msg.ReadByte();
            bool tagsChanged = msg.ReadBoolean();
            string tags = "";
            if (tagsChanged)
            {
                tags = msg.ReadString();
            }

            if (!spawn) return null;

            //----------------------------------------

            var itemPrefab = MapEntityPrefab.Find(itemName, itemIdentifier) as ItemPrefab;
            if (itemPrefab == null) return null;

            Inventory inventory = null;

            var inventoryOwner = FindEntityByID(inventoryId);
            if (inventoryOwner != null)
            {
                if (inventoryOwner is Character)
                {
                    inventory = (inventoryOwner as Character).Inventory;
                }
                else if (inventoryOwner is Item)
                {
                    if ((inventoryOwner as Item).components[itemContainerIndex] is ItemContainer container)
                    {
                        inventory = container.Inventory;
                    }
                }
            }

            var item = new Item(itemPrefab, pos, sub)
            {
                ID = itemId
            };

            foreach (WifiComponent wifiComponent in item.GetComponents<WifiComponent>())
            {
                wifiComponent.TeamID = teamID;
            }
            if (descriptionChanged) item.Description = itemDesc;
            if (tagsChanged) item.Tags = tags;

            if (sub != null)
            {
                item.CurrentHull = Hull.FindHull(pos + sub.Position, null, true);
                item.Submarine = item.CurrentHull?.Submarine;
            }

            if (inventory != null)
            {
                if (inventorySlotIndex >= 0 && inventorySlotIndex < 255 &&
                    inventory.TryPutItem(item, inventorySlotIndex, false, false, null, false))
                {
                    return null;
                }
                inventory.TryPutItem(item, null, item.AllowedSlots, false);
            }

            return item;
        }

        partial void RemoveProjSpecific()
        {
            if (Inventory.draggingItem == this)
            {
                Inventory.draggingItem = null;
                Inventory.draggingSlot = null;
            }
        }
    }
}