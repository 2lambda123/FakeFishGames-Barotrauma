﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Extensions;

namespace Barotrauma
{
    class SpriteEditorScreen : Screen
    {
        private GUIListBox textureList, spriteList;

        private GUIFrame topPanel;
        private GUIFrame leftPanel;
        private GUIFrame rightPanel;

        private GUIFrame topPanelContents;
        private GUITextBlock texturePathText;
        private GUITextBlock xmlPathText;
        private GUIScrollBar zoomBar;
        private string xmlPath;
        private List<Sprite> selectedSprites = new List<Sprite>();
        private Texture2D selectedTexture;
        private Rectangle viewArea;
        private Rectangle textureRect;
        private float zoom = 1;
        private float minZoom = 0.25f;
        private float maxZoom;
        private int spriteCount;

        private readonly Camera cam;
        public override Camera Cam
        {
            get { return cam; }
        }

        public GUIComponent TopPanel
        {
            get { return topPanel; }
        }

        public SpriteEditorScreen()
        {
            cam = new Camera();

            topPanel = new GUIFrame(new RectTransform(new Vector2(1.0f, 0.1f), Frame.RectTransform) { MinSize = new Point(0, 60) }, "GUIFrameTop");
            topPanelContents = new GUIFrame(new RectTransform(new Vector2(0.95f, 0.8f), topPanel.RectTransform, Anchor.Center), style: null);

            new GUIButton(new RectTransform(new Vector2(0.12f, 0.4f), topPanelContents.RectTransform, Anchor.TopLeft)
            {
                RelativeOffset = new Vector2(0, 0.1f)
            }, "Reload Texture")
            {
                OnClicked = (button, userData) =>
                {
                    if (!(textureList.SelectedData is Texture2D selectedTexture)) { return false; }
                    object selectedSprite = spriteList.SelectedData;
                    Sprite matchingSprite = Sprite.LoadedSprites.First(s => s.Texture == selectedTexture);
                    matchingSprite.ReloadTexture();
                    RefreshLists();
                    textureList.Select(matchingSprite.Texture);
                    spriteList.Select(selectedSprite);
                    texturePathText.Text = "Texture reloaded from " + matchingSprite.FilePath;
                    texturePathText.TextColor = Color.LightGreen;
                    return true;
                }
            };
            new GUIButton(new RectTransform(new Vector2(0.12f, 0.4f), topPanelContents.RectTransform, Anchor.BottomLeft)
            {
                RelativeOffset = new Vector2(0, 0.1f)
            }, "Reset Changes")
            {
                OnClicked = (button, userData) =>
                {
                    if (selectedTexture == null) { return false; }
                    foreach (Sprite sprite in Sprite.LoadedSprites)
                    {
                        if (sprite.Texture != selectedTexture) { continue; }
                        var element = sprite.SourceElement;
                        if (element == null) { continue; }
                        sprite.SourceRect = element.GetAttributeRect("sourcerect", sprite.SourceRect);
                        sprite.Origin = element.GetAttributeVector2("origin", sprite.RelativeOrigin);
                    }
                    ResetWidgets();
                    xmlPathText.Text = "Changes successfully reset";
                    xmlPathText.TextColor = Color.LightGreen;
                    return true;
                }
            };
            new GUIButton(new RectTransform(new Vector2(0.12f, 0.4f), topPanelContents.RectTransform, Anchor.TopLeft)
            {
                RelativeOffset = new Vector2(0.15f, 0.1f)
            }, "Save Selected Sprites")
            {
                OnClicked = (button, userData) =>
                {
                    if (selectedSprites.None()) { return false; }
                    foreach (var sprite in selectedSprites)
                    {
                        var element = sprite.SourceElement;
                        if (element == null)
                        {
                            xmlPathText.Text = "No xml element defined for the sprite";
                            xmlPathText.TextColor = Color.Red;
                            return false;
                        }
                        element.SetAttributeValue("sourcerect", XMLExtensions.RectToString(sprite.SourceRect));
                        element.SetAttributeValue("origin", XMLExtensions.Vector2ToString(sprite.RelativeOrigin));
                    }
                    var firstSprite = selectedSprites.First();
                    XElement e = firstSprite.SourceElement;
                    var d = XMLExtensions.TryLoadXml(xmlPath);
                    if (d == null || d.BaseUri != e.Document.BaseUri)
                    {
                        xmlPathText.Text = "Failed to save to " + xmlPath;
                        xmlPathText.TextColor = Color.Red;
                        return false;
                    }
                    e.Document.Save(xmlPath);
                    xmlPathText.Text = "Selected sprites saved to " + xmlPath;
                    xmlPathText.TextColor = Color.LightGreen;
                    return true;
                }
            };
            new GUIButton(new RectTransform(new Vector2(0.12f, 0.4f), topPanelContents.RectTransform, Anchor.BottomLeft)
            {
                RelativeOffset = new Vector2(0.15f, 0.1f)
            }, "Save All Sprites")
            {
                OnClicked = (button, userData) =>
                {
                    if (selectedTexture == null) { return false; }
                    XDocument doc = null;
                    foreach (Sprite sprite in Sprite.LoadedSprites)
                    {
                        if (sprite.Texture != selectedTexture) { continue; }
                        var element = sprite.SourceElement;
                        if (element == null) { continue; }
                        element.SetAttributeValue("sourcerect", XMLExtensions.RectToString(sprite.SourceRect));
                        element.SetAttributeValue("origin", XMLExtensions.Vector2ToString(sprite.RelativeOrigin));
                        doc = element.Document;
                    }
                    if (doc != null)
                    {
                        var d = XMLExtensions.TryLoadXml(xmlPath);
                        if (d == null || d.BaseUri != doc.BaseUri)
                        {
                            xmlPathText.Text = "Failed to save to " + xmlPath;
                            xmlPathText.TextColor = Color.Red;
                            return false;
                        }
                        doc.Save(xmlPath);
                        xmlPathText.Text = "All changes saved to " + xmlPath;
                        xmlPathText.TextColor = Color.LightGreen;
                        return true;
                    }
                    else
                    {
                        xmlPathText.Text = "Failed to save to " + xmlPath;
                        xmlPathText.TextColor = Color.Red;
                        return false;
                    }
                }
            };
            new GUITextBlock(new RectTransform(new Vector2(0.2f, 0.2f), topPanelContents.RectTransform, Anchor.TopCenter, Pivot.CenterRight) { RelativeOffset = new Vector2(0, 0.3f) }, "Zoom: ");
            zoomBar = new GUIScrollBar(new RectTransform(new Vector2(0.2f, 0.35f), topPanelContents.RectTransform, Anchor.TopCenter, Pivot.CenterRight)
            {
                RelativeOffset = new Vector2(0.05f, 0.3f)
            }, barSize: 0.1f)
            {
                BarScroll = GetBarScrollValue(),
                Step = 0.01f,
                OnMoved = (scrollBar, value) =>
                {
                    zoom = MathHelper.Lerp(minZoom, maxZoom, value);
                    ResetWidgets();
                    return true;
                }
            };
            new GUIButton(new RectTransform(new Vector2(0.05f, 0.35f), topPanelContents.RectTransform, Anchor.TopCenter, Pivot.CenterLeft) { RelativeOffset = new Vector2(0.055f, 0.3f) }, "Reset Zoom")
            {
                OnClicked = (box, data) =>
                {
                    ResetScale();
                    return true;
                }
            };

            texturePathText = new GUITextBlock(new RectTransform(new Vector2(0.5f, 0.4f), topPanelContents.RectTransform, Anchor.Center, Pivot.BottomCenter)
                { RelativeOffset = new Vector2(0.4f, 0) }, "", Color.LightGray);
            xmlPathText = new GUITextBlock(new RectTransform(new Vector2(0.5f, 0.4f), topPanelContents.RectTransform, Anchor.Center, Pivot.TopCenter)
                { RelativeOffset = new Vector2(0.4f, 0) }, "", Color.LightGray);

            leftPanel = new GUIFrame(new RectTransform(new Vector2(0.25f, 1.0f - topPanel.RectTransform.RelativeSize.Y), Frame.RectTransform, Anchor.BottomLeft)
                { MinSize = new Point(150, 0) }, style: "GUIFrameLeft");
            var paddedLeftPanel = new GUILayoutGroup(new RectTransform(new Vector2(0.9f, 0.95f), leftPanel.RectTransform, Anchor.CenterLeft)
                { RelativeOffset = new Vector2(0.02f, 0.0f) })
                    { Stretch = true };
            textureList = new GUIListBox(new RectTransform(new Vector2(1.0f, 1.0f), paddedLeftPanel.RectTransform))
            {
                OnSelected = (listBox, userData) =>
                {
                    var previousTexture = selectedTexture;
                    selectedTexture = userData as Texture2D;
                    if (previousTexture != selectedTexture)
                    {
                        ResetScale();
                    }
                    foreach (GUIComponent child in spriteList.Content.Children)
                    {
                        var textBlock = (GUITextBlock)child;
                        var sprite = (Sprite)textBlock.UserData;
                        textBlock.TextColor = new Color(textBlock.TextColor, sprite.Texture == selectedTexture ? 1.0f : 0.4f);
                    }
                    if (selectedSprites.None(s => s.Texture == selectedTexture))
                    {
                        spriteList.Select(Sprite.LoadedSprites.First(s => s.Texture == selectedTexture));
                    }
                    var firstSprite = selectedSprites.First();
                    texturePathText.Text = firstSprite.FilePath;
                    texturePathText.TextColor = Color.LightGray;
                    var element = firstSprite.SourceElement;
                    if (element == null)
                    {
                        xmlPathText.Text = string.Empty;
                    }
                    else
                    {
                        string[] splitted = element.BaseUri.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
                        IEnumerable<string> filtered = splitted.SkipWhile(part => part != "Content");
                        string parsed = string.Join("/", filtered);
                        xmlPath = parsed;
                        xmlPathText.Text = xmlPath;
                        xmlPathText.TextColor = Color.LightGray;
                    }
                    topPanelContents.Visible = true;
                    return true;
                }
            };
            
            rightPanel = new GUIFrame(new RectTransform(new Vector2(0.25f, 1.0f - topPanel.RectTransform.RelativeSize.Y), Frame.RectTransform, Anchor.BottomRight)
                { MinSize = new Point(150, 0) },
                style: "GUIFrameRight");
            var paddedRightPanel = new GUILayoutGroup(new RectTransform(new Vector2(0.95f, 0.95f), rightPanel.RectTransform, Anchor.Center) { RelativeOffset = new Vector2(0.02f, 0.0f) })
            {
                Stretch = true,
                RelativeSpacing = 0.01f
            };
            spriteList = new GUIListBox(new RectTransform(new Vector2(1.0f, 1.0f), paddedRightPanel.RectTransform))
            {
                OnSelected = (listBox, userData) =>
                {
                    Sprite sprite = userData as Sprite;
                    if (sprite == null) return false;
                    if (selectedSprites.Any(s => s.Texture != selectedTexture))
                    {
                        ResetWidgets();
                    }
                    if (Widget.EnableMultiSelect)
                    {
                        if (selectedSprites.Contains(sprite))
                        {
                            selectedSprites.Remove(sprite);
                        }
                        else
                        {
                            selectedSprites.Add(sprite);
                        }
                    }
                    else
                    {
                        selectedSprites.Clear();
                        selectedSprites.Add(sprite);
                    }
                    textureList.Select(sprite.Texture);
                    return true;
                }
            };
       
            RefreshLists();
        }

        private void ResetWidgets()
        {
            widgets.Clear();
            Widget.selectedWidgets.Clear();
        }

        public override void Select()
        {
            base.Select();
            RefreshLists();
        }

        public void SelectSprite(Sprite sprite)
        {
            ResetWidgets();
            textureList.Select(sprite.Texture);
            ResetScale();
            selectedSprites.Clear();
            selectedSprites.Add(sprite);
        }

        public override void Update(double deltaTime)
        {
            base.Update(deltaTime);
            for (int i = 0; i < widgets.Count; i++)
            {
                widgets.ElementAt(i).Value.Update((float)deltaTime);
            }
            Widget.EnableMultiSelect = PlayerInput.KeyDown(Keys.LeftControl);
            // Select rects with the mouse
            if (Widget.selectedWidgets.None() || Widget.EnableMultiSelect)
            {
                if (selectedTexture != null)
                {
                    foreach (Sprite sprite in Sprite.LoadedSprites)
                    {
                        if (sprite.Texture != selectedTexture) continue;
                        if (PlayerInput.LeftButtonClicked())
                        {
                            var scaledRect = new Rectangle(textureRect.Location + sprite.SourceRect.Location.Multiply(zoom), sprite.SourceRect.Size.Multiply(zoom));
                            if (scaledRect.Contains(PlayerInput.MousePosition))
                            {
                                spriteList.Select(sprite);
                            }
                        }
                    }
                }
            }
            if (PlayerInput.ScrollWheelSpeed != 0 && viewArea.Contains(PlayerInput.MousePosition))
            {
                zoom = MathHelper.Clamp(zoom + PlayerInput.ScrollWheelSpeed * (float)deltaTime * 0.05f * zoom, minZoom, maxZoom);
                zoomBar.BarScroll = GetBarScrollValue();
                ResetWidgets();
            }
        }

        public override void Draw(double deltaTime, GraphicsDevice graphics, SpriteBatch spriteBatch)
        {
            graphics.Clear(new Color(0.051f, 0.149f, 0.271f, 1.0f));
            spriteBatch.Begin(SpriteSortMode.Deferred, rasterizerState: GameMain.ScissorTestEnable, samplerState: SamplerState.PointClamp);

            int margin = 20;
            viewArea = new Rectangle(leftPanel.Rect.Right + margin, topPanel.Rect.Bottom + margin, rightPanel.Rect.Left - leftPanel.Rect.Right - margin * 2, Frame.Rect.Height - topPanel.Rect.Height - margin * 2);

            if (selectedTexture != null)
            {
                textureRect = new Rectangle(
                    (int)(viewArea.Center.X - selectedTexture.Bounds.Width / 2f * zoom),
                    (int)(viewArea.Center.Y - selectedTexture.Bounds.Height / 2f * zoom),
                    (int)(selectedTexture.Bounds.Width * zoom),
                    (int)(selectedTexture.Bounds.Height * zoom));

                spriteBatch.Draw(selectedTexture,
                    viewArea.Center.ToVector2(), 
                    sourceRectangle: null, 
                    color: Color.White, 
                    rotation: 0.0f,
                    origin: new Vector2(selectedTexture.Bounds.Width / 2.0f, selectedTexture.Bounds.Height / 2.0f), 
                    scale: zoom, 
                    effects: SpriteEffects.None, 
                    layerDepth: 0);

                //GUI.DrawRectangle(spriteBatch, viewArea, Color.Green, isFilled: false);
                GUI.DrawRectangle(spriteBatch, textureRect, Color.Gray, isFilled: false);

                foreach (GUIComponent element in spriteList.Content.Children)
                {
                    Sprite sprite = element.UserData as Sprite;
                    if (sprite == null) { continue; }
                    if (sprite.Texture != selectedTexture) continue;
                    spriteCount++;

                    Rectangle sourceRect = new Rectangle(
                        textureRect.X + (int)(sprite.SourceRect.X * zoom),
                        textureRect.Y + (int)(sprite.SourceRect.Y * zoom),
                        (int)(sprite.SourceRect.Width * zoom),
                        (int)(sprite.SourceRect.Height * zoom));

                    bool isSelected = selectedSprites.Contains(sprite);
                    GUI.DrawRectangle(spriteBatch, sourceRect, isSelected ? Color.Yellow : Color.Red * 0.5f, thickness: isSelected ? 2 : 1);

                    string identifier = null;
                    var sourceElement = sprite.SourceElement;
                    if (sourceElement != null)
                    {
                        var parentElement = sourceElement.Parent;
                        identifier = parentElement != null ? sourceElement.ToString() + parentElement.ToString() : sourceElement.ToString();
                    }
                    if (!string.IsNullOrEmpty(identifier))
                    {
                        int widgetSize = 10;
                        Vector2 halfSize = new Vector2(widgetSize) / 2;
                        Vector2 tooltipOffset = new Vector2(15, -10);
                        Vector2 GetTopLeft() => sprite.SourceRect.Location.ToVector2();
                        Vector2 GetTopRight() => new Vector2(GetTopLeft().X + sprite.SourceRect.Width, GetTopLeft().Y);
                        Vector2 GetBottomRight() => new Vector2(GetTopRight().X, GetTopRight().Y + sprite.SourceRect.Height);
                        var originWidget = GetWidget($"{identifier}_origin", widgetSize, Widget.Shape.Cross, initMethod: w =>
                        {
                            w.color = Color.Yellow;
                            w.secondaryColor = Color.Gray;
                            w.tooltipOffset = tooltipOffset;
                            w.tooltip = $"Origin: {sprite.RelativeOrigin.FormatDoubleDecimal()}";
                            w.inputAreaMargin = new Point(widgetSize / 2);
                            w.refresh = () =>
                                w.DrawPos = (textureRect.Location.ToVector2() + (sprite.Origin + sprite.SourceRect.Location.ToVector2()) * zoom)
                                    .Clamp(textureRect.Location.ToVector2() + GetTopLeft() * zoom, textureRect.Location.ToVector2() + GetBottomRight() * zoom);
                            w.refresh();
                            w.MouseDown += () => spriteList.Select(sprite);
                            w.MouseHeld += dTime =>
                            {
                                w.DrawPos = PlayerInput.MousePosition.Clamp(textureRect.Location.ToVector2() + GetTopLeft() * zoom, textureRect.Location.ToVector2() + GetBottomRight() * zoom);
                                sprite.Origin = (w.DrawPos + halfSize - textureRect.Location.ToVector2() - sprite.SourceRect.Location.ToVector2() * zoom) / zoom;
                                w.tooltip = $"Origin: {sprite.RelativeOrigin.FormatDoubleDecimal()}";
                            };
                            w.Deselected += w.refresh;
                            w.MouseUp += w.refresh;
                            w.PreUpdate += dTime => w.Enabled = isSelected;
                        });
                        var positionWidget = GetWidget($"{identifier}_position", widgetSize, Widget.Shape.Rectangle, initMethod: w =>
                        {
                            w.color = Color.Yellow;
                            w.secondaryColor = Color.Gray;
                            w.tooltipOffset = tooltipOffset;
                            w.tooltip = $"Position: {sprite.SourceRect.Location}";
                            w.DrawPos = textureRect.Location.ToVector2() + GetTopLeft() * zoom - halfSize;
                            w.inputAreaMargin = new Point(widgetSize / 2);
                            w.MouseDown += () => spriteList.Select(sprite);
                            w.MouseHeld += dTime =>
                            {
                                w.DrawPos = PlayerInput.MousePosition;
                                sprite.SourceRect = new Rectangle(((w.DrawPos + halfSize - textureRect.Location.ToVector2()) / zoom).ToPoint(), sprite.SourceRect.Size);
                                if (widgets.TryGetValue($"{identifier}_size", out Widget sizeW))
                                {
                                    sizeW.refresh();
                                }
                                if (widgets.TryGetValue($"{identifier}_origin", out Widget originW))
                                {
                                    originW.refresh();
                                }
                                if (spriteList.SelectedComponent is GUITextBlock textBox)
                                {
                                    textBox.Text = GetSpriteName(sprite) + " " + sprite.SourceRect;
                                }
                                w.tooltip = $"Position: {sprite.SourceRect.Location}";
                            };
                            w.refresh = () => w.DrawPos = textureRect.Location.ToVector2() + sprite.SourceRect.Location.ToVector2() * zoom - halfSize;
                            w.Deselected += w.refresh;
                            w.MouseUp += w.refresh;
                            w.PreUpdate += dTime => w.Enabled = isSelected;
                        });
                        var sizeWidget = GetWidget($"{identifier}_size", widgetSize, Widget.Shape.Rectangle, initMethod: w =>
                        {
                            w.color = Color.Yellow;
                            w.secondaryColor = Color.Gray;
                            w.tooltipOffset = tooltipOffset;
                            w.tooltip = $"Size: {sprite.SourceRect.Size}";
                            w.DrawPos = textureRect.Location.ToVector2() + GetBottomRight() * zoom + halfSize;
                            w.inputAreaMargin = new Point(widgetSize / 2);
                            w.MouseDown += () => spriteList.Select(sprite);
                            w.MouseHeld += dTime =>
                            {
                                w.DrawPos = PlayerInput.MousePosition;
                                sprite.SourceRect = new Rectangle(sprite.SourceRect.Location, ((w.DrawPos - new Vector2(widgetSize) - positionWidget.DrawPos) / zoom).ToPoint());
                                sprite.RelativeOrigin = sprite.RelativeOrigin;
                                if (widgets.TryGetValue($"{identifier}_origin", out Widget originW))
                                {
                                    originW.refresh();
                                }
                                if (spriteList.SelectedComponent is GUITextBlock textBox)
                                {
                                    textBox.Text = GetSpriteName(sprite) + " " + sprite.SourceRect;
                                }
                                w.tooltip = $"Size: {sprite.SourceRect.Size}";
                            };
                            w.refresh = () => w.DrawPos = textureRect.Location.ToVector2() + new Vector2(sprite.SourceRect.Right, sprite.SourceRect.Bottom) * zoom + halfSize;
                            w.MouseUp += w.refresh;
                            w.Deselected += w.refresh;
                            w.PreUpdate += dTime => w.Enabled = isSelected;
                        });
                        if (isSelected)
                        {
                            positionWidget.Draw(spriteBatch, (float)deltaTime);
                            sizeWidget.Draw(spriteBatch, (float)deltaTime);
                            originWidget.Draw(spriteBatch, (float)deltaTime);
                        }
                    }
                }
            }

            GUI.Draw(Cam, spriteBatch);

            //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth - 100, 0), "widgets: " + widgets.Count, Color.LightGreen);
            //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth - 100, 20), "sprites: " + spriteCount, Color.LightGreen);
            spriteCount = 0;

            spriteBatch.End();
        }

        private void ResetScale()
        {
            float width = viewArea.Width / (float)selectedTexture.Width;
            float height = viewArea.Height / (float)selectedTexture.Height;
            maxZoom = Math.Min(width, height);
            zoom = Math.Min(1, maxZoom);
            zoomBar.BarScroll = GetBarScrollValue();
            ResetWidgets();
        }

        private float GetBarScrollValue() => MathHelper.Lerp(0, 1, MathUtils.InverseLerp(minZoom, maxZoom, zoom));

        private string GetIdentifier(Sprite sprite)
        {
            var element = sprite.SourceElement;
            if (element == null) { return string.Empty; }
            string identifier = element.Parent.GetAttributeString("identifier", string.Empty);
            if (string.IsNullOrEmpty(identifier))
            {
                return element.Parent.GetAttributeString("name", string.Empty);
            }
            return identifier;
        }

        private string GetSpriteName(Sprite sprite)
        {
            string identifier = GetIdentifier(sprite);
            return string.IsNullOrEmpty(identifier) ? Path.GetFileNameWithoutExtension(sprite.FilePath) : identifier;
        }

        public void RefreshLists()
        {
            textureList.ClearChildren();
            spriteList.ClearChildren();
            ResetWidgets();
            HashSet<string> textures = new HashSet<string>();
            foreach (Sprite sprite in Sprite.LoadedSprites.OrderBy(s => Path.GetFileNameWithoutExtension(s.FilePath)))
            {
                //ignore sprites that don't have a file path (e.g. submarine pics)
                if (string.IsNullOrEmpty(sprite.FilePath)) continue;
                new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.05f), spriteList.Content.RectTransform) { MinSize = new Point(0, 20) }, GetSpriteName(sprite) + " " + sprite.SourceRect)
                {
                    Padding = Vector4.Zero,
                    UserData = sprite
                };

                string normalizedFilePath = Path.GetFullPath(sprite.FilePath);
                if (!textures.Contains(normalizedFilePath))
                {
                    new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.05f), textureList.Content.RectTransform) { MinSize = new Point(0, 20) },
                        Path.GetFileName(sprite.FilePath))
                    {
                        Padding = Vector4.Zero,
                        ToolTip = sprite.FilePath,
                        UserData = sprite.Texture
                    };
                    textures.Add(normalizedFilePath);
                }
            }

            topPanelContents.Visible = false;
        }

        #region Widgets
        private Dictionary<string, Widget> widgets = new Dictionary<string, Widget>();

        private Widget GetWidget(string id, int size = 5, Widget.Shape shape = Widget.Shape.Rectangle, Action<Widget> initMethod = null)
        {
            if (!widgets.TryGetValue(id, out Widget widget))
            {
                widget = new Widget(id, size, shape);
                initMethod?.Invoke(widget);
                widgets.Add(id, widget);
            }
            return widget;
        }
        #endregion
    }
}
