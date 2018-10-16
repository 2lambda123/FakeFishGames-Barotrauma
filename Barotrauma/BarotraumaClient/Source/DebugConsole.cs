﻿using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Barotrauma
{
    static partial class DebugConsole
    {
        public partial class Command
        {
            /// <summary>
            /// Executed when a client uses the command. If not set, the command is relayed to the server as-is.
            /// </summary>
            public Action<string[]> OnClientExecute;

            public bool RelayToServer
            {
                get { return OnClientExecute == null; }
            }

            public void ClientExecute(string[] args)
            {
                if (!CheatsEnabled && IsCheat)
                {
                    NewMessage("You need to enable cheats using the command \"enablecheats\" before you can use the command \"" + names[0] + "\".", Color.Red);
                    if (GameMain.Config.UseSteam)
                    {
                        NewMessage("Enabling cheats will disable Steam achievements during this play session.", Color.Red);
                    }
                    return;
                }

                if (OnClientExecute != null)
                {
                    OnClientExecute(args);
                }
                else
                {
                    OnExecute(args);
                }
            }
        }

        private static bool isOpen;
        public static bool IsOpen => isOpen;

        private static Queue<ColoredText> queuedMessages = new Queue<ColoredText>();

        private static GUITextBlock activeQuestionText;
        
        private static GUIFrame frame;
        private static GUIListBox listBox;
        private static GUITextBox textBox;

        public static GUITextBox TextBox => textBox;

        public static void Init()
        {
            frame = new GUIFrame(new RectTransform(new Vector2(0.5f, 0.45f), GUI.Canvas) { MinSize = new Point(400, 300), AbsoluteOffset = new Point(10, 10) },
                color: new Color(0.4f, 0.4f, 0.4f, 0.8f));
            var paddedFrame = new GUIFrame(new RectTransform(new Vector2(0.95f, 0.9f), frame.RectTransform, Anchor.Center), style: null);

            listBox = new GUIListBox(new RectTransform(new Point(paddedFrame.Rect.Width, paddedFrame.Rect.Height - 30), paddedFrame.RectTransform)
            {
                IsFixedSize = false
            }, color: Color.Black * 0.9f);

            textBox = new GUITextBox(new RectTransform(new Point(paddedFrame.Rect.Width, 20), paddedFrame.RectTransform, Anchor.BottomLeft)
            {
                IsFixedSize = false
            });
            textBox.OnTextChanged += (textBox, text) =>
            {
                ResetAutoComplete();
                return true;
            };

            NewMessage("Press F3 to open/close the debug console", Color.Cyan);
            NewMessage("Enter \"help\" for a list of available console commands", Color.Cyan);
        }

        public static void AddToGUIUpdateList()
        {
            if (isOpen)
            {
                frame.AddToGUIUpdateList();
            }
        }

        public static void Update(GameMain game, float deltaTime)
        {
            lock (queuedMessages)
            {
                while (queuedMessages.Count > 0)
                {
                    var newMsg = queuedMessages.Dequeue();
                    AddMessage(newMsg);

                    if (GameSettings.SaveDebugConsoleLogs)
                    {
                        unsavedMessages.Add(newMsg);
                        if (unsavedMessages.Count >= messagesPerFile)
                        {
                            SaveLogs();
                            unsavedMessages.Clear();
                        }
                    }
                }
            }

            activeQuestionText?.SetAsLastChild();

            if (PlayerInput.KeyHit(Keys.F3))
            {
                isOpen = !isOpen;
                if (isOpen)
                {
                    textBox.Select();
                    AddToGUIUpdateList();
                }
                else
                {
                    GUI.ForceMouseOn(null);
                    textBox.Deselect();
                }
            }

            if (isOpen)
            {
                frame.UpdateManually(deltaTime);

                Character.DisableControls = true;

                if (PlayerInput.KeyHit(Keys.Up))
                {
                    textBox.Text = SelectMessage(-1, textBox.Text);
                }
                else if (PlayerInput.KeyHit(Keys.Down))
                {
                    textBox.Text = SelectMessage(1, textBox.Text);
                }
                else if (PlayerInput.KeyHit(Keys.Tab))
                {
                     textBox.Text = AutoComplete(textBox.Text);
                }

                if (PlayerInput.KeyHit(Keys.Enter))
                {
                    ExecuteCommand(textBox.Text);
                    textBox.Text = "";
                }
            }
        }

        public static void Draw(SpriteBatch spriteBatch)
        {
            if (!isOpen) return;

            frame.DrawManually(spriteBatch);
        }

        private static bool IsCommandPermitted(string command, GameClient client)
        {
            switch (command)
            {
                case "kick":
                    return client.HasPermission(ClientPermissions.Kick);
                case "ban":
                case "banip":
                    return client.HasPermission(ClientPermissions.Ban);
                case "netstats":
                case "help":
                case "dumpids":
                case "admin":
                case "entitylist":
                    return true;
                default:
                    return client.HasConsoleCommandPermission(command);
            }
        }

        public static void DequeueMessages()
        {
            while (queuedMessages.Count > 0)
            {
                var newMsg = queuedMessages.Dequeue();
                AddMessage(newMsg);

                if (GameSettings.SaveDebugConsoleLogs) unsavedMessages.Add(newMsg);
            }
        }

        private static void AddMessage(ColoredText msg)
        {
            //listbox not created yet, don't attempt to add
            if (listBox == null) return;

            if (listBox.Content.CountChildren > MaxMessages)
            {
                listBox.RemoveChild(listBox.Content.Children.First());
            }

            Messages.Add(msg);
            if (Messages.Count > MaxMessages)
            {
                Messages.RemoveRange(0, Messages.Count - MaxMessages);
            }

            try
            {
                var textBlock = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.0f), listBox.Content.RectTransform),
                    msg.Text, font: GUI.SmallFont, wrap: true)
                {
                    CanBeFocused = false,
                    TextColor = msg.Color
                };
                listBox.UpdateScrollBarSize();
                listBox.BarScroll = 1.0f;
            }
            catch (Exception e)
            {
                ThrowError("Failed to add a message to the debug console.", e);
            }

            selectedIndex = Messages.Count;
        }

        static partial void AddHelpMessage(Command command)
        {
            if (listBox.Content.CountChildren > MaxMessages)
            {
                listBox.RemoveChild(listBox.Content.Children.First());
            }

            var textContainer = new GUIFrame(new RectTransform(new Vector2(1.0f, 0.0f), listBox.Content.RectTransform),
                style: "InnerFrame", color: Color.White * 0.6f)
            {
                CanBeFocused = false
            };
            var textBlock = new GUITextBlock(new RectTransform(new Point(listBox.Content.Rect.Width - 170, 0), textContainer.RectTransform, Anchor.TopRight) { AbsoluteOffset = new Point(20, 0) },
                command.help, textAlignment: Alignment.TopLeft, font: GUI.SmallFont, wrap: true)
            {
                CanBeFocused = false,
                TextColor = Color.White
            };
            textContainer.RectTransform.NonScaledSize = new Point(textContainer.RectTransform.NonScaledSize.X, textBlock.RectTransform.NonScaledSize.Y + 5);
            textBlock.SetTextPos();
            var nameBlock = new GUITextBlock(new RectTransform(new Point(150, textContainer.Rect.Height), textContainer.RectTransform),
                command.names[0], textAlignment: Alignment.TopLeft);

            listBox.UpdateScrollBarSize();
            listBox.BarScroll = 1.0f;

            selectedIndex = Messages.Count;
        }

        private static void AssignOnClientExecute(string names, Action<string[]> onClientExecute)
        {
            commands.First(c => c.names.Intersect(names.Split('|')).Count() > 0).OnClientExecute = onClientExecute;
        }

        private static void InitProjectSpecific()
        {
#if WINDOWS
            commands.Add(new Command("copyitemnames", "", (string[] args) =>
            {
                StringBuilder sb = new StringBuilder();
                foreach (MapEntityPrefab mp in MapEntityPrefab.List)
                {
                    if (!(mp is ItemPrefab)) continue;
                    sb.AppendLine(mp.Name);
                }
                System.Windows.Clipboard.SetText(sb.ToString());
            }));
#endif

            commands.Add(new Command("autohull", "", (string[] args) =>
            {
                if (Screen.Selected != GameMain.SubEditorScreen) return;

                if (MapEntity.mapEntityList.Any(e => e is Hull || e is Gap))
                {
                    ShowQuestionPrompt("This submarine already has hulls and/or gaps. This command will delete them. Do you want to continue? Y/N",
                        (option) =>
                        {
                            if (option.ToLower() == "y") GameMain.SubEditorScreen.AutoHull();
                        });
                }
                else
                {
                    GameMain.SubEditorScreen.AutoHull();
                }
            }));

            commands.Add(new Command("startclient", "", (string[] args) =>
            {
                if (args.Length == 0) return;

                if (GameMain.Client == null)
                {
                    GameMain.Client = new GameClient("Name", args[0]);
                }
            }));

            commands.Add(new Command("mainmenuscreen|mainmenu|menu", "mainmenu/menu: Go to the main menu.", (string[] args) =>
            {
                GameMain.GameSession = null;

                List<Character> characters = new List<Character>(Character.CharacterList);
                foreach (Character c in characters)
                {
                    c.Remove();
                }

                GameMain.MainMenuScreen.Select();
            }));

            commands.Add(new Command("gamescreen|game", "gamescreen/game: Go to the \"in-game\" view.", (string[] args) =>
            {
                GameMain.GameScreen.Select();
            }));

            commands.Add(new Command("editsubscreen|editsub|subeditor", "editsub/subeditor: Switch to the submarine editor.", (string[] args) =>
            {
                if (args.Length > 0)
                {
                    Submarine.Load(string.Join(" ", args), true);
                }
                GameMain.SubEditorScreen.Select();
            }));

            commands.Add(new Command("editparticles", "", (string[] args) =>
            {
                GameMain.ParticleEditorScreen.Select();
            }));

            commands.Add(new Command("editlevels", "", (string[] args) =>
            {
                GameMain.LevelEditorScreen.Select();
            }));

            commands.Add(new Command("editsprites|editsprite|spriteeditor|spriteedit", "", (string[] args) =>
            {
                GameMain.SpriteEditorScreen.Select();
            }));

            commands.Add(new Command("charactereditor|editcharacter|editcharacters|editanimation|editanimations|animedit|animationeditor|animeditor|animationedit", "charactereditor: Edit characters, animations, ragdolls....", (string[] args) =>
            {
                GameMain.CharacterEditorScreen.Select();
            }));

            commands.Add(new Command("control|controlcharacter", "control [character name]: Start controlling the specified character.", (string[] args) =>
            {
                if (args.Length < 1) return;

                var character = FindMatchingCharacter(args, true);

                if (character != null)
                {
                    Character.Controlled = character;
                }
            },
            () =>
            {
                return new string[][]
                {
                    Character.CharacterList.Select(c => c.Name).Distinct().ToArray()
                };
            }, isCheat: true));

            commands.Add(new Command("shake", "", (string[] args) =>
            {
                GameMain.GameScreen.Cam.Shake = 10.0f;
            }));

            commands.Add(new Command("los", "los: Toggle the line of sight effect on/off.", (string[] args) =>
            {
                GameMain.LightManager.LosEnabled = !GameMain.LightManager.LosEnabled;
                NewMessage("Line of sight effect " + (GameMain.LightManager.LosEnabled ? "enabled" : "disabled"), Color.White);
            }, isCheat: true));

            commands.Add(new Command("lighting|lights", "Toggle lighting on/off.", (string[] args) =>
            {
                GameMain.LightManager.LightingEnabled = !GameMain.LightManager.LightingEnabled;
                NewMessage("Lighting " + (GameMain.LightManager.LightingEnabled ? "enabled" : "disabled"), Color.White);
            }, isCheat: true));

            commands.Add(new Command("multiplylights [color]", "Multiplies the colors of all the static lights in the sub with the given color value.", (string[] args) =>
            {
                if (Screen.Selected != GameMain.SubEditorScreen || args.Length < 1) return;

                Color color = XMLExtensions.ParseColor(args[0]);
                foreach (Item item in Item.ItemList)
                {
                    if (item.ParentInventory != null || item.body != null) continue;
                    var lightComponent = item.GetComponent<LightComponent>();
                    if (lightComponent != null) lightComponent.LightColor =
                        new Color(
                            (lightComponent.LightColor.R / 255.0f) * (color.R / 255.0f),
                            (lightComponent.LightColor.G / 255.0f) * (color.G / 255.0f),
                            (lightComponent.LightColor.B / 255.0f) * (color.B / 255.0f),
                            (lightComponent.LightColor.A / 255.0f) * (color.A / 255.0f));
                }
            }, isCheat: false));

            commands.Add(new Command("tutorial", "", (string[] args) =>
            {
                TutorialMode.StartTutorial(Tutorials.Tutorial.Tutorials[0]);
            }));

            commands.Add(new Command("lobby|lobbyscreen", "", (string[] args) =>
            {
                GameMain.LobbyScreen.Select();
            }));

            commands.Add(new Command("save|savesub", "save [submarine name]: Save the currently loaded submarine using the specified name.", (string[] args) =>
            {
                if (args.Length < 1) return;

                if (GameMain.SubEditorScreen.CharacterMode)
                {
                    GameMain.SubEditorScreen.SetCharacterMode(false);
                }

                string fileName = string.Join(" ", args);
                if (fileName.Contains("../"))
                {
                    ThrowError("Illegal symbols in filename (../)");
                    return;
                }

                if (Submarine.SaveCurrent(System.IO.Path.Combine(Submarine.SavePath, fileName + ".sub")))
                {
                    NewMessage("Sub saved", Color.Green);
                }
            }));

            commands.Add(new Command("load|loadsub", "load [submarine name]: Load a submarine.", (string[] args) =>
            {
                if (args.Length == 0) return;
                Submarine.Load(string.Join(" ", args), true);
            }));

            commands.Add(new Command("cleansub", "", (string[] args) =>
            {
                for (int i = MapEntity.mapEntityList.Count - 1; i >= 0; i--)
                {
                    MapEntity me = MapEntity.mapEntityList[i];

                    if (me.SimPosition.Length() > 2000.0f)
                    {
                        NewMessage("Removed " + me.Name + " (simposition " + me.SimPosition + ")", Color.Orange);
                        MapEntity.mapEntityList.RemoveAt(i);
                    }
                    else if (!me.ShouldBeSaved)
                    {
                        NewMessage("Removed " + me.Name + " (!ShouldBeSaved)", Color.Orange);
                        MapEntity.mapEntityList.RemoveAt(i);
                    }
                    else if (me is Item)
                    {
                        Item item = me as Item;
                        var wire = item.GetComponent<Wire>();
                        if (wire == null) continue;

                        if (wire.GetNodes().Count > 0 && !wire.Connections.Any(c => c != null))
                        {
                            wire.Item.Drop(null);
                            NewMessage("Dropped wire (ID: " + wire.Item.ID + ") - attached on wall but no connections found", Color.Orange);
                        }
                    }
                }
            }, isCheat: true));

            commands.Add(new Command("messagebox", "", (string[] args) =>
            {
                new GUIMessageBox("", string.Join(" ", args));
            }));

            commands.Add(new Command("debugdraw", "debugdraw: Toggle the debug drawing mode on/off.", (string[] args) =>
            {
                GameMain.DebugDraw = !GameMain.DebugDraw;
                NewMessage("Debug draw mode " + (GameMain.DebugDraw ? "enabled" : "disabled"), Color.White);
            }, isCheat: true));

            commands.Add(new Command("fpscounter", "fpscounter: Toggle the FPS counter.", (string[] args) =>
            {
                GameMain.ShowFPS = !GameMain.ShowFPS;
                NewMessage("FPS counter " + (GameMain.DebugDraw ? "enabled" : "disabled"), Color.White);
            }));
            commands.Add(new Command("showperf", "showperf: Toggle performance statistics on/off.", (string[] args) =>
            {
                GameMain.ShowPerf = !GameMain.ShowPerf;
                NewMessage("Performance statistics " + (GameMain.ShowPerf ? "enabled" : "disabled"), Color.White);
            }));

            commands.Add(new Command("hudlayoutdebugdraw|debugdrawhudlayout", "hudlayoutdebugdraw: Toggle the debug drawing mode of HUD layout areas on/off.", (string[] args) =>
            {
                HUDLayoutSettings.DebugDraw = !HUDLayoutSettings.DebugDraw;
                NewMessage("HUD layout debug draw mode " + (HUDLayoutSettings.DebugDraw ? "enabled" : "disabled"), Color.White);
            }));

            commands.Add(new Command("interactdebugdraw|debugdrawinteract", "interactdebugdraw: Toggle the debug drawing mode of item interaction ranges on/off.", (string[] args) =>
            {
                Character.DebugDrawInteract = !Character.DebugDrawInteract;
                NewMessage("Interact debug draw mode " + (Character.DebugDrawInteract ? "enabled" : "disabled"), Color.White);
            }, isCheat: true));

            commands.Add(new Command("togglehud|hud", "togglehud/hud: Toggle the character HUD (inventories, icons, buttons, etc) on/off.", (string[] args) =>
            {
                GUI.DisableHUD = !GUI.DisableHUD;
                GameMain.Instance.IsMouseVisible = !GameMain.Instance.IsMouseVisible;
                NewMessage(GUI.DisableHUD ? "Disabled HUD" : "Enabled HUD", Color.White);
            }));

            commands.Add(new Command("followsub", "followsub: Toggle whether the camera should follow the nearest submarine.", (string[] args) =>
            {
                Camera.FollowSub = !Camera.FollowSub;
                NewMessage(Camera.FollowSub ? "Set the camera to follow the closest submarine" : "Disabled submarine following.", Color.White);
            }));

            commands.Add(new Command("toggleaitargets|aitargets", "toggleaitargets/aitargets: Toggle the visibility of AI targets (= targets that enemies can detect and attack/escape from).", (string[] args) =>
            {
                AITarget.ShowAITargets = !AITarget.ShowAITargets;
                NewMessage(AITarget.ShowAITargets ? "Enabled AI target drawing" : "Disabled AI target drawing", Color.White);
            }, isCheat: true));
#if DEBUG
            commands.Add(new Command("spamchatmessages", "", (string[] args) =>
            {
                int msgCount = 1000;
                if (args.Length > 0) int.TryParse(args[0], out msgCount);
                int msgLength = 50;
                if (args.Length > 1) int.TryParse(args[1], out msgLength);

                for (int i = 0; i < msgCount; i++)
                {
                    if (GameMain.Client != null)
                    {
                        GameMain.Client.SendChatMessage(ToolBox.RandomSeed(msgLength));
                    }
                }
            }));
#endif

                    commands.Add(new Command("dumptexts", "dumptexts [filepath]: Extracts all the texts from the given text xml and writes them into a file (using the same filename, but with the .txt extension). If the filepath is omitted, the EnglishVanilla.xml file is used.", (string[] args) =>
            {
                string filePath = args.Length > 0 ? args[0] : "Content/Texts/EnglishVanilla.xml";
                var doc = XMLExtensions.TryLoadXml(filePath);
                if (doc?.Root == null) return;
                List<string> lines = new List<string>();
                foreach (XElement element in doc.Root.Elements())
                {
                    lines.Add(element.ElementInnerText());
                }
                File.WriteAllLines(Path.GetFileNameWithoutExtension(filePath) + ".txt", lines);
            },
            () =>
            {
                var files = TextManager.GetTextFiles().Select(f => f.Replace("\\", "/"));
                return new string[][]
                {
                    TextManager.GetTextFiles().Where(f => Path.GetExtension(f)==".xml").ToArray()
                };
            }));

            commands.Add(new Command("loadtexts", "loadtexts [sourcefile] [destinationfile]: Loads all lines of text from a given .txt file and inserts them sequientially into the elements of an xml file. If the file paths are omitted, EnglishVanilla.txt and EnglishVanilla.xml are used.", (string[] args) =>
            {
                string sourcePath = args.Length > 0 ? args[0] : "Content/Texts/EnglishVanilla.txt";
                string destinationPath = args.Length > 1 ? args[1] : "Content/Texts/EnglishVanilla.xml";

                string[] lines;
                try
                {
                    lines = File.ReadAllLines(sourcePath);
                }
                catch (Exception e)
                {
                    ThrowError("Reading the file \"" + sourcePath + "\" failed.", e);
                    return;
                }
                var doc = XMLExtensions.TryLoadXml(destinationPath);
                int i = 0;
                foreach (XElement element in doc.Root.Elements())
                {
                    if (i >= lines.Length)
                    {
                        ThrowError("Error while loading texts to the xml file. The xml has more elements than the number of lines in the text file.");
                        return;
                    }
                    element.Value = lines[i];
                    i++;
                }
                doc.Save(destinationPath);
            },
            () =>
            {
                var files = TextManager.GetTextFiles().Select(f => f.Replace("\\", "/"));
                return new string[][]
                {
                    files.Where(f => Path.GetExtension(f)==".txt").ToArray(),
                    files.Where(f => Path.GetExtension(f)==".xml").ToArray()
                };
            }));

            commands.Add(new Command("updatetextfile", "updatetextfile [sourcefile] [destinationfile]: Inserts all the xml elements that are only present in the source file into the destination file. Can be used to update outdated translation files more easily.", (string[] args) =>
            {
                if (args.Length < 2) return;
                string sourcePath = args[0];
                string destinationPath = args[1];

                var sourceDoc = XMLExtensions.TryLoadXml(sourcePath);
                var destinationDoc = XMLExtensions.TryLoadXml(destinationPath);

                XElement destinationElement = destinationDoc.Root.Elements().First();
                foreach (XElement element in sourceDoc.Root.Elements())
                {
                    if (destinationDoc.Root.Element(element.Name) == null)
                    {
                        element.Value = "!!!!!!!!!!!!!" + element.Value;
                        destinationElement.AddAfterSelf(element);
                    }
                    XNode nextNode = destinationElement.NextNode;
                    while ((!(nextNode is XElement) || nextNode == element) && nextNode != null) nextNode = nextNode.NextNode;
                    destinationElement = nextNode as XElement;
                }
                destinationDoc.Save(destinationPath);
            },
            () =>
            {
                var files = TextManager.GetTextFiles().Where(f => Path.GetExtension(f) == ".xml").Select(f => f.Replace("\\", "/")).ToArray();
                return new string[][]
                {
                    files,
                    files
                };
            }));

            commands.Add(new Command("dumpentitytexts", "dumpentitytexts [filepath]: gets the names and descriptions of all entity prefabs and writes them into a file along with xml tags that can be used in translation files. If the filepath is omitted, the file is written to Content/Texts/EntityTexts.txt", (string[] args) =>
            {
                string filePath = args.Length > 0 ? args[0] : "Content/Texts/EntityTexts.txt";
                List<string> lines = new List<string>();
                foreach (MapEntityPrefab me in MapEntityPrefab.List)
                {
                    lines.Add("<EntityName." + me.Identifier + ">" + me.Name + "</" + me.Identifier + ".Name>");
                    lines.Add("<EntityDescription." + me.Identifier + ">" + me.Description + "</" + me.Identifier + ".Description>");
                }
                File.WriteAllLines(filePath, lines);
            }));


            commands.Add(new Command("cleanbuild", "", (string[] args) =>
            {
                GameMain.Config.MusicVolume = 0.5f;
                GameMain.Config.SoundVolume = 0.5f;
                NewMessage("Music and sound volume set to 0.5", Color.Green);

                GameMain.Config.GraphicsWidth = 0;
                GameMain.Config.GraphicsHeight = 0;
                GameMain.Config.WindowMode = WindowMode.Fullscreen;
                NewMessage("Resolution set to 0 x 0 (screen resolution will be used)", Color.Green);
                NewMessage("Fullscreen enabled", Color.Green);

                GameSettings.ShowUserStatisticsPrompt = true;

                GameSettings.VerboseLogging = false;

                if (GameMain.Config.MasterServerUrl != "http://www.undertowgames.com/baromaster")
                {
                    ThrowError("MasterServerUrl \"" + GameMain.Config.MasterServerUrl + "\"!");
                }

                GameMain.Config.Save();

                var saveFiles = System.IO.Directory.GetFiles(SaveUtil.SaveFolder);

                foreach (string saveFile in saveFiles)
                {
                    System.IO.File.Delete(saveFile);
                    NewMessage("Deleted " + saveFile, Color.Green);
                }

                if (System.IO.Directory.Exists(System.IO.Path.Combine(SaveUtil.SaveFolder, "temp")))
                {
                    System.IO.Directory.Delete(System.IO.Path.Combine(SaveUtil.SaveFolder, "temp"), true);
                    NewMessage("Deleted temp save folder", Color.Green);
                }

                if (System.IO.Directory.Exists(ServerLog.SavePath))
                {
                    var logFiles = System.IO.Directory.GetFiles(ServerLog.SavePath);

                    foreach (string logFile in logFiles)
                    {
                        System.IO.File.Delete(logFile);
                        NewMessage("Deleted " + logFile, Color.Green);
                    }
                }

                if (System.IO.File.Exists("filelist.xml"))
                {
                    System.IO.File.Delete("filelist.xml");
                    NewMessage("Deleted filelist", Color.Green);
                }

                if (System.IO.File.Exists("Data/bannedplayers.txt"))
                {
                    System.IO.File.Delete("Data/bannedplayers.txt");
                    NewMessage("Deleted bannedplayers.txt", Color.Green);
                }

                if (System.IO.File.Exists("Submarines/TutorialSub.sub"))
                {
                    System.IO.File.Delete("Submarines/TutorialSub.sub");

                    NewMessage("Deleted TutorialSub from the submarine folder", Color.Green);
                }

                /*if (System.IO.File.Exists(GameServer.SettingsFile))
                {
                    System.IO.File.Delete(GameServer.SettingsFile);
                    NewMessage("Deleted server settings", Color.Green);
                }

                if (System.IO.File.Exists(GameServer.ClientPermissionsFile))
                {
                    System.IO.File.Delete(GameServer.ClientPermissionsFile);
                    NewMessage("Deleted client permission file", Color.Green);
                }*/

                if (System.IO.File.Exists("crashreport.log"))
                {
                    System.IO.File.Delete("crashreport.log");
                    NewMessage("Deleted crashreport.log", Color.Green);
                }

                if (!System.IO.File.Exists("Content/Map/TutorialSub.sub"))
                {
                    ThrowError("TutorialSub.sub not found!");
                }
            }));

            AssignOnClientExecute(
                "giveperm",
                (string[] args) =>
                {
                    if (args.Length < 1) return;

                    if (!int.TryParse(args[0], out int id))
                    {
                        ThrowError("\"" + id + "\" is not a valid client ID.");
                        return;
                    }

                    NewMessage("Valid permissions are:", Color.White);
                    foreach (ClientPermissions permission in Enum.GetValues(typeof(ClientPermissions)))
                    {
                        NewMessage(" - " + permission.ToString(), Color.White);
                    }
                    ShowQuestionPrompt("Permission to grant to client #" + id + "?", (perm) =>
                    {
                        GameMain.Client.SendConsoleCommand("giveperm " + id + " " + perm);
                    });
                }
            );

            AssignOnClientExecute(
                "revokeperm",
                (string[] args) =>
                {
                    if (args.Length < 1) return;

                    if (!int.TryParse(args[0], out int id))
                    {
                        ThrowError("\"" + id + "\" is not a valid client ID.");
                        return;
                    }

                    NewMessage("Valid permissions are:", Color.White);
                    foreach (ClientPermissions permission in Enum.GetValues(typeof(ClientPermissions)))
                    {
                        NewMessage(" - " + permission.ToString(), Color.White);
                    }

                    ShowQuestionPrompt("Permission to revoke from client #" + id + "?", (perm) =>
                    {
                        GameMain.Client.SendConsoleCommand("revokeperm " + id + " " + perm);
                    });
                }
            );

            AssignOnClientExecute(
                "giverank",
                (string[] args) =>
                {
                    if (args.Length < 1) return;

                    if (!int.TryParse(args[0], out int id))
                    {
                        ThrowError("\"" + id + "\" is not a valid client ID.");
                        return;
                    }

                    NewMessage("Valid ranks are:", Color.White);
                    foreach (PermissionPreset permissionPreset in PermissionPreset.List)
                    {
                        NewMessage(" - " + permissionPreset.Name, Color.White);
                    }
                    ShowQuestionPrompt("Rank to grant to client #" + id + "?", (rank) =>
                    {
                        GameMain.Client.SendConsoleCommand("giverank " + id + " " + rank);
                    });
                }
            );

            AssignOnClientExecute(
                "givecommandperm",
                (string[] args) =>
                {
                    if (args.Length < 1) return;

                    if (!int.TryParse(args[0], out int id))
                    {
                        ThrowError("\"" + id + "\" is not a valid client ID.");
                        return;
                    }

                    ShowQuestionPrompt("Console command permissions to grant to client #" + id + "? You may enter multiple commands separated with a space.", (commandNames) =>
                    {
                        GameMain.Client.SendConsoleCommand("givecommandperm " + id + " " + commandNames);
                    });
                }
            );

            AssignOnClientExecute(
                "revokecommandperm",
                (string[] args) =>
                {
                    //TODO: revoke lol
                    if (args.Length < 1) return;

                    if (!int.TryParse(args[0], out int id))
                    {
                        ThrowError("\"" + id + "\" is not a valid client ID.");
                        return;
                    }

                    ShowQuestionPrompt("Console command permissions to grant to client #" + id + "? You may enter multiple commands separated with a space.", (commandNames) =>
                    {
                        GameMain.Client.SendConsoleCommand("givecommandperm " + id + " " + commandNames);
                    });
                }
            );

            AssignOnClientExecute(
                "showperm",
                (string[] args) =>
                {
                    if (args.Length < 1) return;

                    if (!int.TryParse(args[0], out int id))
                    {
                        ThrowError("\"" + id + "\" is not a valid client ID.");
                        return;
                    }

                    GameMain.Client.SendConsoleCommand("showperm " + id);
                }
            );

            AssignOnClientExecute(
                "banip",
                (string[] args) =>
                {
                    if (GameMain.Client == null || args.Length == 0) return;
                    ShowQuestionPrompt("Reason for banning the ip \"" + args[0] + "\"?", (reason) =>
                    {
                        ShowQuestionPrompt("Enter the duration of the ban (leave empty to ban permanently, or use the format \"[days] d [hours] h\")", (duration) =>
                        {
                            TimeSpan? banDuration = null;
                            if (!string.IsNullOrWhiteSpace(duration))
                            {
                                if (!TryParseTimeSpan(duration, out TimeSpan parsedBanDuration))
                                {
                                    ThrowError("\"" + duration + "\" is not a valid ban duration. Use the format \"[days] d [hours] h\", \"[days] d\" or \"[hours] h\".");
                                    return;
                                }
                                banDuration = parsedBanDuration;
                            }

                            GameMain.Client.SendConsoleCommand(
                                "banip " +
                                args[0] + " " +
                                (banDuration.HasValue ? banDuration.Value.TotalSeconds.ToString() : "0") + " " +
                                reason);
                        });
                    });
                }
            );

            AssignOnClientExecute(
                "campaigndestination|setcampaigndestination",
                (string[] args) =>
                {
                    var campaign = GameMain.GameSession?.GameMode as CampaignMode;
                    if (campaign == null)
                    {
                        ThrowError("No campaign active!");
                        return;
                    }

                    if (args.Length == 0)
                    {
                        int i = 0;
                        foreach (LocationConnection connection in campaign.Map.CurrentLocation.Connections)
                        {
                            NewMessage("     " + i + ". " + connection.OtherLocation(campaign.Map.CurrentLocation).Name, Color.White);
                            i++;
                        }
                        ShowQuestionPrompt("Select a destination (0 - " + (campaign.Map.CurrentLocation.Connections.Count - 1) + "):", (string selectedDestination) =>
                        {
                            int destinationIndex = -1;
                            if (!int.TryParse(selectedDestination, out destinationIndex)) return;
                            if (destinationIndex < 0 || destinationIndex >= campaign.Map.CurrentLocation.Connections.Count)
                            {
                                NewMessage("Index out of bounds!", Color.Red);
                                return;
                            }
                            GameMain.Client.SendConsoleCommand("campaigndestination " + destinationIndex);
                        });
                    }
                    else
                    {
                        int destinationIndex = -1;
                        if (!int.TryParse(args[0], out destinationIndex)) return;
                        if (destinationIndex < 0 || destinationIndex >= campaign.Map.CurrentLocation.Connections.Count)
                        {
                            NewMessage("Index out of bounds!", Color.Red);
                            return;
                        }
                        GameMain.Client.SendConsoleCommand("campaigndestination " + destinationIndex);
                    }
                }
            );
        }
    }
}
