﻿using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Barotrauma.Items.Components
{
    partial class Pump : Powered, IServerSerializable, IClientSerializable
    {
        private GUIScrollBar isActiveSlider;
        private GUIScrollBar pumpSpeedSlider;
        private GUITickBox powerIndicator;

        partial void InitProjSpecific()
        {
            GUIFrame paddedFrame = new GUIFrame(new RectTransform(new Vector2(0.9f, 0.8f), GuiFrame.RectTransform, Anchor.Center), style: null);

            isActiveSlider = new GUIScrollBar(new RectTransform(new Point(50, 100), paddedFrame.RectTransform, Anchor.CenterLeft),
                barSize: 0.2f, style: "OnOffLever")
            {
                IsBooleanSwitch = true,
                MinValue = 0.25f,
                MaxValue = 0.75f
            };
            var sliderHandle = isActiveSlider.GetChild<GUIButton>();
            sliderHandle.RectTransform.NonScaledSize = new Point(84, sliderHandle.Rect.Height);
            
            isActiveSlider.OnMoved = (GUIScrollBar scrollBar, float barScroll) =>
            {
                bool active = scrollBar.BarScroll < 0.5f;
                if (active == IsActive) return false;

                targetLevel = null;
                IsActive = active;
                if (!IsActive) currPowerConsumption = 0.0f;

                if (GameMain.Client != null)
                {
                    correctionTimer = CorrectionDelay;
                    item.CreateClientEvent(this);
                }

                return true;
            };

            var rightArea = new GUILayoutGroup(new RectTransform(new Vector2(0.75f, 1.0f), paddedFrame.RectTransform, Anchor.CenterRight)) { RelativeSpacing = 0.1f };

            powerIndicator = new GUITickBox(new RectTransform(new Point(30, 30), rightArea.RectTransform), TextManager.Get("PumpPowered"), style: "IndicatorLightGreen")
            {
                CanBeFocused = false
            };

            var pumpSpeedText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.2f), rightArea.RectTransform) { RelativeOffset = new Vector2(0.25f, 0.0f) },
                "", textAlignment: Alignment.BottomLeft);
            string pumpSpeedStr = TextManager.Get("PumpSpeed");
            pumpSpeedText.TextGetter = () => { return pumpSpeedStr + ": " + (int)flowPercentage + " %"; };

            var sliderArea = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.3f), rightArea.RectTransform, Anchor.CenterLeft), isHorizontal: true)
            {
                Stretch = true,
                RelativeSpacing = 0.05f
            };

            new GUITextBlock(new RectTransform(new Vector2(0.15f, 1.0f), sliderArea.RectTransform), 
                TextManager.Get("PumpOut"), textAlignment: Alignment.Center);
            pumpSpeedSlider = new GUIScrollBar(new RectTransform(new Vector2(0.8f, 1.0f), sliderArea.RectTransform), barSize: 0.25f, style: "GUISlider")
            {
                Step = 0.05f,
                OnMoved = (GUIScrollBar scrollBar, float barScroll) =>
                {
                    float newValue = barScroll * 200.0f - 100.0f;
                    if (Math.Abs(newValue - FlowPercentage) < 0.1f) return false;

                    FlowPercentage = newValue;

                    if (GameMain.Client != null)
                    {
                        correctionTimer = CorrectionDelay;
                        item.CreateClientEvent(this);
                    }
                    return true;
                }
            };

            new GUITextBlock(new RectTransform(new Vector2(0.15f, 1.0f), sliderArea.RectTransform), 
                TextManager.Get("PumpIn"), textAlignment: Alignment.Center);            
        }

        public override void UpdateHUD(Character character, float deltaTime)
        {
            powerIndicator.Selected = hasPower && IsActive;

            if (!PlayerInput.LeftButtonHeld())
            {
                isActiveSlider.BarScroll += (IsActive ? -10.0f : 10.0f) * deltaTime;

                float pumpSpeedScroll = (FlowPercentage + 100.0f) / 200.0f;
                if (Math.Abs(pumpSpeedScroll - pumpSpeedSlider.BarScroll) > 0.01f)
                {
                    pumpSpeedSlider.BarScroll = pumpSpeedScroll;
                }
            }
        }
        
        public override void AddToGUIUpdateList()
        {
            GuiFrame.AddToGUIUpdateList();
        }
        
        public void ClientWrite(Lidgren.Network.NetBuffer msg, object[] extraData = null)
        {
            //flowpercentage can only be adjusted at 10% intervals -> no need for more accuracy than this
            msg.WriteRangedInteger(-10, 10, (int)(flowPercentage / 10.0f));
            msg.Write(IsActive);
        }

        public void ClientRead(ServerNetObject type, Lidgren.Network.NetBuffer msg, float sendingTime)
        {
            if (correctionTimer > 0.0f)
            {
                StartDelayedCorrection(type, msg.ExtractBits(5 + 1), sendingTime);
                return;
            }

            FlowPercentage = msg.ReadRangedInteger(-10, 10) * 10.0f;
            IsActive = msg.ReadBoolean();
        }
    }
}
