﻿using Barotrauma.Networking;
using Lidgren.Network;
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

        partial void InitProjSpecific(XElement element)
        {
            uiElements.Clear();

            var visibleElements = customInterfaceElementList.Where(ciElement => !string.IsNullOrEmpty(ciElement.Label));

            GUILayoutGroup paddedFrame = new GUILayoutGroup(new RectTransform(new Vector2(0.9f, 0.8f), GuiFrame.RectTransform, Anchor.Center),
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
                    var tickBox = new GUITickBox(new RectTransform(new Vector2(1.0f, elementSize), paddedFrame.RectTransform),
                        TextManager.Get(ciElement.Label, returnNull: true) ?? ciElement.Label)
                    {
                        UserData = ciElement
                    };
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
                    uiElements.Add(tickBox);
                }
                else
                {
                    var btn = new GUIButton(new RectTransform(new Vector2(1.0f, elementSize), paddedFrame.RectTransform), 
                        TextManager.Get(ciElement.Label, returnNull: true) ?? ciElement.Label, style: "GUIButtonLarge")
                    {
                        UserData = ciElement
                    };
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
            for (int i = 0; i< customInterfaceElementList.Count; i++)
            {
                editor.CreateStringField(customInterfaceElementList[i],
                    new SerializableProperty(labelProperty, customInterfaceElementList[i]),
                    customInterfaceElementList[i].Label, "Label #" + (i + 1), "");
                editor.CreateStringField(customInterfaceElementList[i],
                    new SerializableProperty(signalProperty, customInterfaceElementList[i]),
                    customInterfaceElementList[i].Signal, "Signal #" + (i + 1), "");
            }
        }

        partial void UpdateLabelsProjSpecific()
        {
            for (int i = 0; i < labels.Length && i < uiElements.Count; i++)
            {
                if (uiElements[i] is GUIButton button)
                {
                    button.Text = labels[i];
                }
                else if (uiElements[i] is GUITickBox tickBox)
                {
                    tickBox.Text = labels[i];
                }
            }
        }

        public void ClientWrite(NetBuffer msg, object[] extraData = null)
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

        public void ClientRead(ServerNetObject type, NetBuffer msg, float sendingTime)
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
