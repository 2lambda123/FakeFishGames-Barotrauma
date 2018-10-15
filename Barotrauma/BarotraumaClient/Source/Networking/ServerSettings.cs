﻿using Lidgren.Network;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace Barotrauma.Networking
{
    partial class ServerSettings : ISerializableEntity
    {
        partial class NetPropertyData
        {
            public GUIComponent GUIComponent;
            public object TempValue;

            public void AssignGUIComponent(GUIComponent component)
            {
                GUIComponent = component;
                GUIComponentValue = property.GetValue();
                TempValue = GUIComponentValue;
            }

            public object GUIComponentValue
            {
                get
                {
                    if (GUIComponent == null) return null;
                    else if (GUIComponent is GUITickBox tickBox) return tickBox.Selected;
                    else if (GUIComponent is GUITextBox textBox) return textBox.Text;
                    else if (GUIComponent is GUIScrollBar scrollBar) return scrollBar.BarScrollValue;
                    else if (GUIComponent is GUIRadioButtonGroup radioButtonGroup) return radioButtonGroup.Selected;
                    return null;
                }
                set
                {
                    if (GUIComponent == null) return;
                    else if (GUIComponent is GUITickBox tickBox) tickBox.Selected = (bool)value;
                    else if (GUIComponent is GUITextBox textBox) textBox.Text = (string)value;
                    else if (GUIComponent is GUIScrollBar scrollBar) scrollBar.BarScrollValue = (float)value;
                    else if (GUIComponent is GUIRadioButtonGroup radioButtonGroup) radioButtonGroup.Selected = (Enum)value;
                }
            }

            public bool ChangedLocally
            {
                get
                {
                    if (GUIComponent == null) return false;
                    return !PropEquals(TempValue, GUIComponentValue);
                }
            }

            public bool PropEquals(object a,object b)
            {
                switch (typeString)
                {
                    case "float":
                        if (!(a is float?)) return false;
                        if (!(b is float?)) return false;
                        return (float)a == (float)b;
                    case "int":
                        if (!(a is int?)) return false;
                        if (!(b is int?)) return false;
                        return (int)a == (int)b;
                    case "bool":
                        if (!(a is bool?)) return false;
                        if (!(b is bool?)) return false;
                        return (bool)a == (bool)b;
                    case "Enum":
                        if (!(a is Enum)) return false;
                        if (!(b is Enum)) return false;
                        return ((Enum)a).Equals((Enum)b);
                    default:
                        return a.ToString().Equals(b.ToString(),StringComparison.InvariantCulture);
                }
            }
        }

        partial void InitProjSpecific()
        {
            var properties = TypeDescriptor.GetProperties(GetType()).Cast<PropertyDescriptor>();

            SerializableProperties = new Dictionary<string, SerializableProperty>();

            foreach (var property in properties)
            {
                SerializableProperty objProperty = new SerializableProperty(property, this);
                SerializableProperties.Add(property.Name.ToLowerInvariant(), objProperty);
            }
        }

        public void ClientRead(NetBuffer incMsg)
        {
            SharedRead(incMsg);

            Voting.ClientRead(incMsg);

            if (incMsg.ReadBoolean())
            {
                isPublic = incMsg.ReadBoolean();
                EnableUPnP = incMsg.ReadBoolean();
                incMsg.ReadPadBits();
                QueryPort = incMsg.ReadUInt16();

                int count = incMsg.ReadUInt16();
                for (int i=0;i<count;i++)
                {
                    UInt32 key = incMsg.ReadUInt32();
                    if (netProperties.ContainsKey(key))
                    {
                        bool changedLocally = netProperties[key].ChangedLocally;
                        netProperties[key].Read(incMsg);
                        netProperties[key].TempValue = netProperties[key].Value;

                        if (netProperties[key].GUIComponent!=null)
                        {
                            if (!changedLocally)
                            {
                                netProperties[key].GUIComponentValue = netProperties[key].Value;
                            }
                        }
                    }
                    else
                    {
                        UInt32 size = incMsg.ReadVariableUInt32();
                        incMsg.Position += 8 * size;
                    }
                }

                ReadMonsterEnabled(incMsg);
            }
            else
            {
                incMsg.ReadPadBits();
            }
        }

        public void ClientWrite()
        {
            IEnumerable<KeyValuePair<UInt32, NetPropertyData>> changedProperties = netProperties.Where(kvp => kvp.Value.ChangedLocally);
            UInt32 count = (UInt32)changedProperties.Count();

            if (count == 0) return;

            NetOutgoingMessage outMsg = GameMain.NetworkMember.NetPeer.CreateMessage();

            outMsg.Write((byte)ClientPacketHeader.SERVER_SETTINGS);

            outMsg.Write(count);
            DebugConsole.NewMessage("COUNT: " + count.ToString(), Color.Yellow);
            foreach (KeyValuePair<UInt32,NetPropertyData> prop in changedProperties)
            {
                DebugConsole.NewMessage(prop.Value.Name, Color.Lime);
                outMsg.Write(prop.Key);
                prop.Value.Write(outMsg,prop.Value.GUIComponentValue);
            }
            
            (GameMain.NetworkMember.NetPeer as NetClient).SendMessage(outMsg, NetDeliveryMethod.ReliableUnordered);
        }

        //GUI stuff
        private GUIFrame settingsFrame;
        private GUIFrame[] settingsTabs;
        private int settingsTabIndex;
        
        enum SettingsTab
        {
            Rounds,
            Server,
            Banlist,
            Whitelist
        }

        private NetPropertyData GetPropertyData(string name)
        {
            return netProperties.First(p => p.Value.Name == name).Value;
        }

        public void AddToGUIUpdateList()
        {
            settingsFrame?.AddToGUIUpdateList();
        }

        private void CreateSettingsFrame()
        {
            foreach (NetPropertyData prop in netProperties.Values)
            {
                prop.TempValue = prop.Value;
            }

            //background frame
            settingsFrame = new GUIFrame(new RectTransform(Vector2.One, GUI.Canvas), style: null, color: Color.Black * 0.5f);
            new GUIButton(new RectTransform(Vector2.One, settingsFrame.RectTransform), "", style: null).OnClicked += (btn, userData) =>
            {
                if (GUI.MouseOn == btn || GUI.MouseOn == btn.TextBlock) ToggleSettingsFrame(btn, userData);
                return true;
            };
            
            new GUIButton(new RectTransform(Vector2.One, settingsFrame.RectTransform), "", style: null)
            {
                OnClicked = ToggleSettingsFrame
            };

            //center frames
            GUIFrame innerFrame = new GUIFrame(new RectTransform(new Vector2(0.3f, 0.7f), settingsFrame.RectTransform, Anchor.Center) { MinSize = new Point(400, 430) });
            GUIFrame paddedFrame = new GUIFrame(new RectTransform(new Vector2(0.9f, 0.9f), innerFrame.RectTransform, Anchor.Center), style: null);

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.1f), paddedFrame.RectTransform), "Settings", font: GUI.LargeFont);

            var buttonArea = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.07f), paddedFrame.RectTransform) { RelativeOffset = new Vector2(0.0f, 0.1f) }, isHorizontal: true)
            {
                Stretch = true,
                RelativeSpacing = 0.01f
            };

            //tabs
            var tabValues = Enum.GetValues(typeof(SettingsTab)).Cast<SettingsTab>().ToArray();
            string[] tabNames = new string[tabValues.Count()];
            for (int i = 0; i < tabNames.Length; i++)
            {
                tabNames[i] = TextManager.Get("ServerSettings" + tabValues[i] + "Tab");
            }
            settingsTabs = new GUIFrame[tabNames.Length];
            for (int i = 0; i < tabNames.Length; i++)
            {
                settingsTabs[i] = new GUIFrame(new RectTransform(new Vector2(1.0f, 0.79f), paddedFrame.RectTransform, Anchor.Center) { RelativeOffset = new Vector2(0.0f, 0.05f) },
                    style: "InnerFrame");

                var tabButton = new GUIButton(new RectTransform(new Vector2(0.2f, 1.0f), buttonArea.RectTransform), tabNames[i])
                {
                    UserData = i,
                    OnClicked = SelectSettingsTab
                };
            }

            SelectSettingsTab(null, 0);

            //"Close"
            var closeButton = new GUIButton(new RectTransform(new Vector2(0.25f, 0.05f), paddedFrame.RectTransform, Anchor.BottomRight), TextManager.Get("Close"))
            {
                OnClicked = ToggleSettingsFrame
            };

            //--------------------------------------------------------------------------------
            //                              game settings 
            //--------------------------------------------------------------------------------

            var roundsTab = new GUILayoutGroup(new RectTransform(new Vector2(0.95f, 0.95f), settingsTabs[(int)SettingsTab.Rounds].RectTransform, Anchor.Center))
            {
                Stretch = true,
                RelativeSpacing = 0.02f
            };
            
            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.05f), roundsTab.RectTransform), TextManager.Get("ServerSettingsSubSelection"));
            var selectionFrame = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.05f), roundsTab.RectTransform), isHorizontal: true)
            {
                Stretch = true,
                RelativeSpacing = 0.05f
            };

            GUIRadioButtonGroup selectionMode = new GUIRadioButtonGroup();
            for (int i = 0; i < 3; i++)
            {
                var selectionTick = new GUITickBox(new RectTransform(new Vector2(0.3f, 1.0f), selectionFrame.RectTransform), ((SelectionMode)i).ToString(), font: GUI.SmallFont);
                selectionMode.AddRadioButton((SelectionMode)i, selectionTick);
            }
            DebugConsole.NewMessage(SubSelectionMode.ToString(),Color.White);
            GetPropertyData("SubSelectionMode").AssignGUIComponent(selectionMode);

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.05f), roundsTab.RectTransform), TextManager.Get("ServerSettingsModeSelection"));
            selectionFrame = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.05f), roundsTab.RectTransform), isHorizontal: true)
            {
                Stretch = true,
                RelativeSpacing = 0.05f
            };

            selectionMode = new GUIRadioButtonGroup();
            for (int i = 0; i < 3; i++)
            {
                var selectionTick = new GUITickBox(new RectTransform(new Vector2(0.3f, 1.0f), selectionFrame.RectTransform), ((SelectionMode)i).ToString(), font: GUI.SmallFont);
                selectionMode.AddRadioButton((SelectionMode)i, selectionTick);
            }
            GetPropertyData("ModeSelectionMode").AssignGUIComponent(selectionMode);
            
            var endBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.05f), roundsTab.RectTransform),
                TextManager.Get("ServerSettingsEndRoundWhenDestReached"))
            {
                OnSelected = (GUITickBox) => { return true; }
            };
            GetPropertyData("EndRoundAtLevelEnd").AssignGUIComponent(endBox);
            
            var endVoteBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.05f), roundsTab.RectTransform),
                TextManager.Get("ServerSettingsEndRoundVoting"));
            GetPropertyData("AllowEndVoting").AssignGUIComponent(endVoteBox);

            GUIScrollBar slider;
            GUITextBlock sliderLabel;
            CreateLabeledSlider(roundsTab, "ServerSettingsEndRoundVotesRequired", out slider, out sliderLabel);

            string endRoundLabel = sliderLabel.Text;
            slider.Step = 0.2f;
            slider.Range = new Vector2(0.5f, 1.0f);
            GetPropertyData("EndVoteRequiredRatio").AssignGUIComponent(slider);
            slider.OnMoved = (GUIScrollBar scrollBar, float barScroll) =>
            {
                ((GUITextBlock)scrollBar.UserData).Text = endRoundLabel + (int)MathUtils.Round(scrollBar.BarScrollValue * 100.0f, 10.0f) + " %";
                return true;
            };
            slider.OnMoved(slider, slider.BarScroll);

            var respawnBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.05f), roundsTab.RectTransform),
                TextManager.Get("ServerSettingsAllowRespawning"))
            {
                OnSelected = (GUITickBox) =>
                {
                    return true;
                }
            };
            GetPropertyData("AllowRespawn").AssignGUIComponent(respawnBox);

            CreateLabeledSlider(roundsTab, "ServerSettingsRespawnInterval", out slider, out sliderLabel);
            string intervalLabel = sliderLabel.Text;
            slider.Step = 0.05f;
            slider.Range = new Vector2(10.0f, 600.0f);
            GetPropertyData("RespawnInterval").AssignGUIComponent(slider);
            slider.OnMoved = (GUIScrollBar scrollBar, float barScroll) =>
            {
                GUITextBlock text = scrollBar.UserData as GUITextBlock;
                text.Text = intervalLabel + ToolBox.SecondsToReadableTime(scrollBar.BarScrollValue);
                return true;
            };
            slider.OnMoved(slider, slider.BarScroll);

            var minRespawnText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.05f), roundsTab.RectTransform), "")
            {
                ToolTip = TextManager.Get("ServerSettingsMinRespawnToolTip")
            };

            string minRespawnLabel = TextManager.Get("ServerSettingsMinRespawn");
            CreateLabeledSlider(roundsTab, "", out slider, out sliderLabel);
            slider.ToolTip = minRespawnText.ToolTip;
            slider.UserData = minRespawnText;
            slider.Step = 0.1f;
            slider.Range = new Vector2(0.0f, 1.0f);
            GetPropertyData("MinRespawnRatio").AssignGUIComponent(slider);
            slider.OnMoved = (GUIScrollBar scrollBar, float barScroll) =>
            {
                ((GUITextBlock)scrollBar.UserData).Text = minRespawnLabel + (int)MathUtils.Round(scrollBar.BarScrollValue * 100.0f, 10.0f) + " %";
                return true;
            };
            slider.OnMoved(slider, MinRespawnRatio);

            var respawnDurationText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.05f), roundsTab.RectTransform), "")
            {
                ToolTip = TextManager.Get("ServerSettingsRespawnDurationToolTip")
            };

            string respawnDurationLabel = TextManager.Get("ServerSettingsRespawnDuration");
            CreateLabeledSlider(roundsTab, "", out slider, out sliderLabel);
            slider.ToolTip = minRespawnText.ToolTip;
            slider.UserData = respawnDurationText;
            slider.Step = 0.1f;
            slider.Range = new Vector2(60.0f, 660.0f);
            slider.ScrollToValue = (GUIScrollBar scrollBar, float barScroll) =>
            {
                return barScroll >= 1.0f ? 0.0f : barScroll * (scrollBar.Range.Y - scrollBar.Range.X) + scrollBar.Range.X;
            };
            slider.ValueToScroll = (GUIScrollBar scrollBar, float value) =>
            {
                return value <= 0.0f ? 1.0f : (value - scrollBar.Range.X) / (scrollBar.Range.Y - scrollBar.Range.X);
            };
            GetPropertyData("MaxTransportTime").AssignGUIComponent(slider);
            slider.OnMoved = (GUIScrollBar scrollBar, float barScroll) =>
            {
                if (barScroll == 1.0f)
                {
                    ((GUITextBlock)scrollBar.UserData).Text = respawnDurationLabel + "unlimited";
                }
                else
                {
                    ((GUITextBlock)scrollBar.UserData).Text = respawnDurationLabel + ToolBox.SecondsToReadableTime(scrollBar.BarScrollValue);
                }

                return true;
            };
            slider.OnMoved(slider, slider.BarScroll);

            var buttonHolder = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.07f), roundsTab.RectTransform), isHorizontal: true)
            {
                Stretch = true,
                RelativeSpacing = 0.05f
            };

            var monsterButton = new GUIButton(new RectTransform(new Vector2(0.5f, 1.0f), buttonHolder.RectTransform),
                TextManager.Get("ServerSettingsMonsterSpawns"))
            {
                Enabled = !GameMain.NetworkMember.GameStarted
            };
            var monsterFrame = new GUIListBox(new RectTransform(new Vector2(0.6f, 0.7f), settingsTabs[(int)SettingsTab.Rounds].RectTransform, Anchor.BottomLeft, Pivot.BottomRight))
            {
                Visible = false
            };
            monsterButton.UserData = monsterFrame;
            monsterButton.OnClicked = (button, obj) =>
            {
                if (GameMain.NetworkMember.GameStarted)
                {
                    ((GUIComponent)obj).Visible = false;
                    button.Enabled = false;
                    return true;
                }
                ((GUIComponent)obj).Visible = !((GUIComponent)obj).Visible;
                return true;
            };
            
            List<string> monsterNames = MonsterEnabled.Keys.ToList();
            foreach (string s in monsterNames)
            {
                var monsterEnabledBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.1f), monsterFrame.Content.RectTransform) { MinSize = new Point(0, 25) },
                    label: s)
                {
                    Selected = MonsterEnabled[s],
                    OnSelected = (GUITickBox) =>
                    {
                        if (GameMain.NetworkMember.GameStarted)
                        {
                            monsterFrame.Visible = false;
                            monsterButton.Enabled = false;
                            return true;
                        }
                        MonsterEnabled[s] = !MonsterEnabled[s];
                        return true;
                    }
                };
            }

            var cargoButton = new GUIButton(new RectTransform(new Vector2(0.5f, 1.0f), buttonHolder.RectTransform),
                TextManager.Get("ServerSettingsAdditionalCargo"))
            {
                Enabled = !GameMain.NetworkMember.GameStarted
            };
            var cargoFrame = new GUIListBox(new RectTransform(new Vector2(0.6f, 0.7f), settingsTabs[(int)SettingsTab.Rounds].RectTransform, Anchor.BottomRight, Pivot.BottomLeft))
            {
                Visible = false
            };
            cargoButton.UserData = cargoFrame;
            cargoButton.OnClicked = (button, obj) =>
            {
                if (GameMain.NetworkMember.GameStarted)
                {
                    ((GUIComponent)obj).Visible = false;
                    button.Enabled = false;
                    return true;
                }
                ((GUIComponent)obj).Visible = !((GUIComponent)obj).Visible;
                return true;
            };

            /* TODO: fix
            foreach (MapEntityPrefab pf in MapEntityPrefab.List)
            {
                ItemPrefab ip = pf as ItemPrefab;

                if (ip == null || (!ip.CanBeBought && !ip.Tags.Contains("smallitem"))) continue;

                GUITextBlock textBlock = new GUITextBlock(new RectTransform(new Vector2(0.9f, 0.15f), cargoFrame.Content.RectTransform) { MinSize = new Point(0, 30) },
                    ip.Name, font: GUI.SmallFont)
                {
                    Padding = new Vector4(40.0f, 3.0f, 0.0f, 0.0f),
                    UserData = cargoFrame,
                    CanBeFocused = false
                };

                if (ip.sprite != null)
                {
                    GUIImage img = new GUIImage(new RectTransform(new Point(textBlock.Rect.Height), textBlock.RectTransform), ip.sprite, scaleToFit: true)
                    {
                        Color = ip.SpriteColor
                    };
                }
                
                extraCargo.TryGetValue(ip, out int cargoVal);
                var amountInput = new GUINumberInput(new RectTransform(new Vector2(0.3f, 1.0f), textBlock.RectTransform, Anchor.CenterRight),
                    GUINumberInput.NumberType.Int)
                {
                    MinValueInt = 0,
                    MaxValueInt = 100,
                    IntValue = cargoVal
                };
                amountInput.OnValueChanged += (numberInput) =>
                {
                    if (extraCargo.ContainsKey(ip))
                    {
                        extraCargo[ip] = numberInput.IntValue;
                    }
                    else
                    {
                        extraCargo.Add(ip, numberInput.IntValue);
                    }
                };
            }
            */

            //--------------------------------------------------------------------------------
            //                              server settings 
            //--------------------------------------------------------------------------------

            var serverTab = new GUILayoutGroup(new RectTransform(new Vector2(0.95f, 0.95f), settingsTabs[(int)SettingsTab.Server].RectTransform, Anchor.Center))
            {
                Stretch = true,
                RelativeSpacing = 0.02f
            };

            string autoRestartDelayLabel = TextManager.Get("ServerSettingsAutoRestartDelay");
            var startIntervalText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.05f), serverTab.RectTransform), autoRestartDelayLabel);
            var startIntervalSlider = new GUIScrollBar(new RectTransform(new Vector2(1.0f, 0.05f), serverTab.RectTransform), barSize: 0.1f)
            {
                UserData = startIntervalText,
                Step = 0.05f,
                OnMoved = (GUIScrollBar scrollBar, float barScroll) =>
                {
                    GUITextBlock text = scrollBar.UserData as GUITextBlock;
                    text.Text = autoRestartDelayLabel + ToolBox.SecondsToReadableTime(scrollBar.BarScrollValue);
                    return true;
                }
            };
            startIntervalSlider.Range = new Vector2(10.0f, 300.0f);
            GetPropertyData("AutoRestartInterval").AssignGUIComponent(startIntervalSlider);
            startIntervalSlider.OnMoved(startIntervalSlider, startIntervalSlider.BarScroll);

            //***********************************************

            var startWhenClientsReady = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.05f), serverTab.RectTransform),
                TextManager.Get("ServerSettingsStartWhenClientsReady"));
            GetPropertyData("StartWhenClientsReady").AssignGUIComponent(startWhenClientsReady);

            CreateLabeledSlider(serverTab, "ServerSettingsStartWhenClientsReadyRatio", out slider, out sliderLabel);
            string clientsReadyRequiredLabel = sliderLabel.Text;
            slider.Step = 0.2f;
            slider.Range = new Vector2(0.5f, 1.0f);
            slider.OnMoved = (GUIScrollBar scrollBar, float barScroll) =>
            {
                ((GUITextBlock)scrollBar.UserData).Text = clientsReadyRequiredLabel.Replace("[percentage]", ((int)MathUtils.Round(scrollBar.BarScrollValue * 100.0f, 10.0f)).ToString());
                return true;
            };
            GetPropertyData("StartWhenClientsReadyRatio").AssignGUIComponent(slider);
            slider.OnMoved(slider, slider.BarScroll);

            //***********************************************

            var allowSpecBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.05f), serverTab.RectTransform), TextManager.Get("ServerSettingsAllowSpectating"))
            {
                OnSelected = (GUITickBox) =>
                {
                    return true;
                }
            };
            GetPropertyData("AllowSpectating").AssignGUIComponent(allowSpecBox);

            var voteKickBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.05f), serverTab.RectTransform), TextManager.Get("ServerSettingsAllowVoteKick"));
            GetPropertyData("AllowVoteKick").AssignGUIComponent(voteKickBox);

            CreateLabeledSlider(serverTab, "ServerSettingsKickVotesRequired", out slider, out sliderLabel);
            string votesRequiredLabel = sliderLabel.Text;
            slider.Step = 0.2f;
            slider.Range = new Vector2(0.5f, 1.0f);
            slider.OnMoved = (GUIScrollBar scrollBar, float barScroll) =>
            {
                ((GUITextBlock)scrollBar.UserData).Text = votesRequiredLabel + (int)MathUtils.Round(scrollBar.BarScrollValue * 100.0f, 10.0f) + " %";
                return true;
            };
            GetPropertyData("KickVoteRequiredRatio").AssignGUIComponent(slider);
            slider.OnMoved(slider, slider.BarScroll);

            CreateLabeledSlider(serverTab, "ServerSettingsAutobanTime", out slider, out sliderLabel);
            string autobanLabel = sliderLabel.Text;
            slider.Step = 0.05f;
            slider.Range = new Vector2(0.0f, MaxAutoBanTime);
            slider.OnMoved = (GUIScrollBar scrollBar, float barScroll) =>
            {
                ((GUITextBlock)scrollBar.UserData).Text = autobanLabel + ToolBox.SecondsToReadableTime(scrollBar.BarScrollValue);
                return true;
            };
            GetPropertyData("AutoBanTime").AssignGUIComponent(slider);
            slider.OnMoved(slider, slider.BarScroll);

            var shareSubsBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.05f), serverTab.RectTransform), TextManager.Get("ServerSettingsShareSubFiles"))
            {
                OnSelected = (GUITickBox) =>
                {
                    return true;
                }
            };
            GetPropertyData("AllowFileTransfers").AssignGUIComponent(shareSubsBox);

            var randomizeLevelBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.05f), serverTab.RectTransform), TextManager.Get("ServerSettingsRandomizeSeed"))
            {
                OnSelected = (GUITickBox) =>
                {
                    return true;
                }
            };
            GetPropertyData("RandomizeSeed").AssignGUIComponent(randomizeLevelBox);

            var saveLogsBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.05f), serverTab.RectTransform), TextManager.Get("ServerSettingsSaveLogs"))
            {
                OnSelected = (GUITickBox) =>
                {
                    //TODO: fix?
                    //showLogButton.Visible = SaveServerLogs;
                    return true;
                }
            };
            GetPropertyData("SaveServerLogs").AssignGUIComponent(saveLogsBox);

            var ragdollButtonBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.05f), serverTab.RectTransform), TextManager.Get("ServerSettingsAllowRagdollButton"))
            {
                OnSelected = (GUITickBox) =>
                {
                    return true;
                }
            };
            GetPropertyData("AllowRagdollButton").AssignGUIComponent(ragdollButtonBox);

            var traitorRatioBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.05f), serverTab.RectTransform), TextManager.Get("ServerSettingsUseTraitorRatio"));

            CreateLabeledSlider(serverTab, "", out slider, out sliderLabel);
            /*var traitorRatioText = new GUITextBlock(new Rectangle(20, y + 20, 20, 20), "Traitor ratio: 20 %", "", settingsTabs[1], GUI.SmallFont);
            var traitorRatioSlider = new GUIScrollBar(new Rectangle(150, y + 22, 100, 15), "", 0.1f, settingsTabs[1]);*/
            var traitorRatioSlider = slider;
            traitorRatioBox.OnSelected = (GUITickBox) =>
            {
                traitorRatioSlider.OnMoved(traitorRatioSlider, traitorRatioSlider.BarScroll);
                return true;
            };
            
            if (TraitorUseRatio)
            {
                traitorRatioSlider.Range = new Vector2(0.1f, 1.0f);
            }
            else
            {
                traitorRatioSlider.Range = new Vector2(1.0f, maxPlayers);
            }

            string traitorRatioLabel = TextManager.Get("ServerSettingsTraitorRatio");
            string traitorCountLabel = TextManager.Get("ServerSettingsTraitorCount");

            traitorRatioSlider.Range = new Vector2(0.1f, 1.0f);
            traitorRatioSlider.OnMoved = (GUIScrollBar scrollBar, float barScroll) =>
            {
                GUITextBlock traitorText = scrollBar.UserData as GUITextBlock;
                if (traitorRatioBox.Selected)
                {
                    scrollBar.Step = 0.01f;
                    scrollBar.Range = new Vector2(0.1f, 1.0f);
                    traitorText.Text = traitorRatioLabel + (int)MathUtils.Round(scrollBar.BarScrollValue * 100.0f, 1.0f) + " %";
                }
                else
                {
                    scrollBar.Step = 1f / (maxPlayers - 1);
                    scrollBar.Range = new Vector2(1.0f, maxPlayers);
                    traitorText.Text = traitorCountLabel + scrollBar.BarScrollValue;
                }
                return true;
            };
            
            GetPropertyData("TraitorUseRatio").AssignGUIComponent(traitorRatioBox);
            GetPropertyData("TraitorRatio").AssignGUIComponent(traitorRatioSlider);

            traitorRatioSlider.OnMoved(traitorRatioSlider, traitorRatioSlider.BarScroll);
            traitorRatioBox.OnSelected(traitorRatioBox);


            var karmaBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.05f), serverTab.RectTransform), TextManager.Get("ServerSettingsUseKarma"))
            {
                OnSelected = (GUITickBox) =>
                {
                    return true;
                }
            };
            GetPropertyData("KarmaEnabled").AssignGUIComponent(karmaBox);

            //--------------------------------------------------------------------------------
            //                              banlist
            //--------------------------------------------------------------------------------

            BanList.CreateBanFrame(settingsTabs[2]);

            //--------------------------------------------------------------------------------
            //                              whitelist
            //--------------------------------------------------------------------------------

            //Whitelist.CreateWhiteListFrame(settingsTabs[3]); //TODO: fix

        }

        private void CreateLabeledSlider(GUIComponent parent, string labelTag, out GUIScrollBar slider, out GUITextBlock label)
        {
            var container = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.05f), parent.RectTransform), isHorizontal: true)
            {
                Stretch = true,
                RelativeSpacing = 0.05f
            };

            slider = new GUIScrollBar(new RectTransform(new Vector2(0.5f, 0.8f), container.RectTransform), barSize: 0.1f);
            label = new GUITextBlock(new RectTransform(new Vector2(0.5f, 0.8f), container.RectTransform),
                string.IsNullOrEmpty(labelTag) ? "" : TextManager.Get(labelTag), font: GUI.SmallFont);

            //slider has a reference to the label to change the text when it's used
            slider.UserData = label;
        }
        
        private bool SelectSettingsTab(GUIButton button, object obj)
        {
            settingsTabIndex = (int)obj;

            for (int i = 0; i < settingsTabs.Length; i++)
            {
                settingsTabs[i].Visible = i == settingsTabIndex;
            }

            return true;
        }
        
        public bool ToggleSettingsFrame(GUIButton button, object obj)
        {
            if (settingsFrame == null)
            {
                CreateSettingsFrame();
            }
            else
            {
                ClientWrite();
                foreach (NetPropertyData prop in netProperties.Values)
                {
                    prop.GUIComponent = null;
                }
                settingsFrame = null;
            }

            return false;
        }

        public void ManagePlayersFrame(GUIFrame infoFrame)
        {
            GUIListBox cList = new GUIListBox(new RectTransform(Vector2.One, infoFrame.RectTransform));
            /*foreach (Client c in ConnectedClients)
            {
                var frame = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.15f), cList.Content.RectTransform),
                    c.Name + " (" + c.Connection.RemoteEndPoint.Address.ToString() + ")", style: "ListBoxElement")
                {
                    Color = (c.InGame && c.Character != null && !c.Character.IsDead) ? Color.Gold * 0.2f : Color.Transparent,
                    HoverColor = Color.LightGray * 0.5f,
                    SelectedColor = Color.Gold * 0.5f
                };

                var buttonArea = new GUILayoutGroup(new RectTransform(new Vector2(0.45f, 0.85f), frame.RectTransform, Anchor.CenterRight) { RelativeOffset = new Vector2(0.05f, 0.0f) },
                    isHorizontal: true);

                var kickButton = new GUIButton(new RectTransform(new Vector2(0.25f, 1.0f), buttonArea.RectTransform),
                    TextManager.Get("Kick"))
                {
                    UserData = c.Name,
                    OnClicked = GameMain.NetLobbyScreen.KickPlayer
                };

                var banButton = new GUIButton(new RectTransform(new Vector2(0.25f, 1.0f), buttonArea.RectTransform),
                    TextManager.Get("Ban"))
                {
                    UserData = c.Name,
                    OnClicked = GameMain.NetLobbyScreen.BanPlayer
                };

                var rangebanButton = new GUIButton(new RectTransform(new Vector2(0.25f, 1.0f), buttonArea.RectTransform),
                    TextManager.Get("BanRange"))
                {
                    UserData = c.Name,
                    OnClicked = GameMain.NetLobbyScreen.BanPlayerRange
                };
            }*/ //TODO: reimplement
        }
    }
}