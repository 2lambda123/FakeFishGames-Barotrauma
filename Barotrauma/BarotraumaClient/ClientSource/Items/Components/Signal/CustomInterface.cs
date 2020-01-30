﻿using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma.Items.Components
{
    partial class CustomInterface
    {
        private List<GUIComponent> uiElements = new List<GUIComponent>();
        private GUILayoutGroup uiElementContainer;

        partial void InitProjSpecific(XElement element)
        {
            uiElements.Clear();

            var visibleElements = customInterfaceElementList.Where(ciElement => !string.IsNullOrEmpty(ciElement.Label));

            uiElementContainer = new GUILayoutGroup(new RectTransform(new Vector2(0.75f, 0.65f), GuiFrame.RectTransform, Anchor.Center) { RelativeOffset = new Vector2(0.0f, 0.025f) },
                childAnchor: customInterfaceElementList.Count > 1 ? Anchor.TopCenter : Anchor.Center)
            {
                RelativeSpacing = 0.05f,
                Stretch = visibleElements.Count() > 2
            };

            float elementSize = Math.Min(1.0f / visibleElements.Count(), 0.5f);
            foreach (CustomInterfaceElement ciElement in visibleElements)
            {
                if (ciElement.ContinuousSignal)
                {
                    var tickBox = new GUITickBox(new RectTransform(new Vector2(1.0f, elementSize), uiElementContainer.RectTransform),
                        TextManager.Get(ciElement.Label, returnNull: true) ?? ciElement.Label)
                    {
                        UserData = ciElement
                    };
                    tickBox.TextBlock.AutoScale = true;
                    tickBox.OnSelected += (tBox) =>
                    {
                        if (GameMain.Client == null)
                        {
                            TickBoxToggled(tBox.UserData as CustomInterfaceElement, tBox.Selected);
                        }
                        else
                        {
                            item.CreateClientEvent(this);
                        }
                        return true;
                    };
                    //reset size restrictions set by the Style to make sure the elements can fit the interface
                    tickBox.RectTransform.MinSize = new Point(0, 0);
                    tickBox.RectTransform.MaxSize = new Point(int.MaxValue, int.MaxValue);
                    uiElements.Add(tickBox);
                }
                else
                {
                    var btn = new GUIButton(new RectTransform(new Vector2(1.0f, elementSize), uiElementContainer.RectTransform), 
                        TextManager.Get(ciElement.Label, returnNull: true) ?? ciElement.Label, style: "DeviceButton")
                    {
                        UserData = ciElement
                    };
                    btn.TextBlock.AutoScale = true;
                    btn.OnClicked += (_, userdata) =>
                    {
                        if (GameMain.Client == null)
                        {
                            ButtonClicked(userdata as CustomInterfaceElement);
                        }
                        else
                        {
                            GameMain.Client.CreateEntityEvent(item, new object[] { NetEntityEvent.Type.ComponentState, item.GetComponentIndex(this), userdata as CustomInterfaceElement });
                        }
                        return true;
                    };

                    //reset size restrictions set by the Style to make sure the elements can fit the interface
                    btn.RectTransform.MinSize = btn.Frame.RectTransform.MinSize = new Point(0, 0);
                    btn.RectTransform.MaxSize = btn.Frame.RectTransform.MaxSize = new Point(int.MaxValue, int.MaxValue);
                    btn.TextBlock.Wrap = true;

                    uiElements.Add(btn);
                }
            }
        }

        public override void CreateEditingHUD(SerializableEntityEditor editor)
        {
            base.CreateEditingHUD(editor);

            PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(customInterfaceElementList[0]);
            PropertyDescriptor labelProperty = properties.Find("Label", false);
            PropertyDescriptor signalProperty = properties.Find("Signal", false);
            for (int i = 0; i < customInterfaceElementList.Count; i++)
            {
                editor.CreateStringField(customInterfaceElementList[i],
                    new SerializableProperty(labelProperty),
                    customInterfaceElementList[i].Label, "Label #" + (i + 1), "");
                editor.CreateStringField(customInterfaceElementList[i],
                    new SerializableProperty(signalProperty),
                    customInterfaceElementList[i].Signal, "Signal #" + (i + 1), "");
            }
        }

        public void HighlightElement(int index, Color color, float duration, float pulsateAmount = 0.0f)
        {
            if (index < 0 || index >= uiElements.Count) { return; }
            uiElements[index].Flash(color, duration);

            if (pulsateAmount > 0.0f)
            {
                if (uiElements[index] is GUIButton button)
                {
                    button.Frame.Pulsate(Vector2.One, Vector2.One * (1.0f + pulsateAmount), duration);
                    button.Frame.RectTransform.SetPosition(Anchor.Center);
                }
                else
                {
                    uiElements[index].Pulsate(Vector2.One, Vector2.One * (1.0f + pulsateAmount), duration);
                }
            }
        }

        partial void UpdateProjSpecific()
        {
            bool elementVisibilityChanged = false;
            int visibleElementCount = 0;
            foreach (var uiElement in uiElements)
            {
                CustomInterfaceElement element = uiElement.UserData as CustomInterfaceElement;
                if (element == null) { continue; }
                bool visible =
                    Screen.Selected == GameMain.SubEditorScreen ||
                    element.StatusEffects.Any() ||
                    (element.Connection != null && element.Connection.Wires.Any(w => w != null));
                if (visible) { visibleElementCount++; }
                if (uiElement.Visible != visible)
                {
                    uiElement.Visible = visible;
                    elementVisibilityChanged = true;
                }
            }

            if (elementVisibilityChanged)
            {
                uiElementContainer.Stretch = visibleElementCount > 2;
                uiElementContainer.ChildAnchor = visibleElementCount > 1 ? Anchor.TopCenter : Anchor.Center;
                float elementSize = Math.Min(1.0f / visibleElementCount, 0.5f);
                foreach (var uiElement in uiElements)
                {
                    uiElement.RectTransform.RelativeSize = new Vector2(1.0f, elementSize);
                }
            }
        }

        partial void UpdateLabelsProjSpecific()
        {
            for (int i = 0; i < labels.Length && i < uiElements.Count; i++)
            {
                if (uiElements[i] is GUIButton button)
                {
                    button.Text = string.IsNullOrWhiteSpace(customInterfaceElementList[i].Label) ?
                        TextManager.GetWithVariable("connection.signaloutx", "[num]", (i + 1).ToString()) :
                        customInterfaceElementList[i].Label;
                }
                else if (uiElements[i] is GUITickBox tickBox)
                {
                    tickBox.Text = string.IsNullOrWhiteSpace(customInterfaceElementList[i].Label) ?
                        TextManager.GetWithVariable("connection.signaloutx", "[num]", (i + 1).ToString()) :
                        customInterfaceElementList[i].Label;
                }
            }
        }

        public void ClientWrite(IWriteMessage msg, object[] extraData = null)
        {
            //extradata contains an array of buttons clicked by the player (or nothing if the player didn't click anything)
            for (int i = 0; i < customInterfaceElementList.Count; i++)
            {
                if (customInterfaceElementList[i].ContinuousSignal)
                {
                    msg.Write(((GUITickBox)uiElements[i]).Selected);
                }
                else
                {
                    msg.Write(extraData != null && extraData.Any(d => d as CustomInterfaceElement == customInterfaceElementList[i]));
                }
            }
        }

        public void ClientRead(ServerNetObject type, IReadMessage msg, float sendingTime)
        {
            for (int i = 0; i < customInterfaceElementList.Count; i++)
            {
                bool elementState = msg.ReadBoolean();
                if (customInterfaceElementList[i].ContinuousSignal)
                {
                    ((GUITickBox)uiElements[i]).Selected = elementState;
                    TickBoxToggled(customInterfaceElementList[i], elementState);
                }
                else if (elementState)
                {
                    ButtonClicked(customInterfaceElementList[i]);
                }
            }
        }
    }
}
