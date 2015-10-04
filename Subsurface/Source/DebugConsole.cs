﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Subsurface.Networking;

namespace Subsurface
{
    struct ColoredText
    {
        public string Text;
        public Color Color;

        public readonly string Time;

        public ColoredText(string text, Color color)
        {
            this.Text = text;
            this.Color = color;

            Time = DateTime.Now.ToString();
        }
    }

    static class DebugConsole
    {
        public static List<ColoredText> messages = new List<ColoredText>();

        static bool isOpen;

        static GUITextBox textBox;
        
        //used for keeping track of the message entered when pressing up/down
        static int selectedIndex;

        public static bool IsOpen
        {
            get { return isOpen; }
        }

        public static void Init(GameWindow window)
        {            
            textBox = new GUITextBox(new Rectangle(30, 480,780, 30), Color.Black, Color.White, Alignment.Left, Alignment.Left);
            NewMessage("Press F3 to open/close the debug console", Color.Green);        
        }

        public static void Update(GameMain game, float deltaTime)
        {
            if (PlayerInput.KeyHit(Keys.F3))
            {
                isOpen = !isOpen;
                if (isOpen)
                {
                    textBox.Select();
                }
                else
                {
                    textBox.Deselect();
                }

                //keyboardDispatcher.Subscriber = (isOpen) ? textBox : null;
            }

            if (isOpen)
            {
                Character.DisableControls = true;

                if (PlayerInput.KeyHit(Keys.Up))
                {
                    SelectMessage(-1);
                }
                else if (PlayerInput.KeyHit(Keys.Down))
                {
                    SelectMessage(1);
                }
                
                textBox.Update(deltaTime);

                if (PlayerInput.GetKeyboardState.IsKeyDown(Keys.Enter) && textBox.Text != "")
                {
                    messages.Add(new ColoredText(textBox.Text, Color.White));
                    ExecuteCommand(textBox.Text, game);
                    textBox.Text = "";

                    selectedIndex = messages.Count;
                }
            }
        }

        private static void SelectMessage(int direction)
        {
            int messageCount = messages.Count;
            if (messageCount == 0) return;

            direction = Math.Min(Math.Max(-1, direction), 1);
            
            selectedIndex += direction;
            if (selectedIndex < 0) selectedIndex = messageCount - 1;
            selectedIndex = selectedIndex % messageCount;

            textBox.Text = messages[selectedIndex].Text;    
        }

        public static void Draw(SpriteBatch spriteBatch)
        {
            if (!isOpen) return;

            int x = 20, y = 20;
            int width = 800, height = 500;

            int margin = 5;

            GUI.DrawRectangle(spriteBatch,
                new Vector2(x, y),
                new Vector2(width, height),
                new Color(0.4f, 0.4f, 0.4f, 0.6f), true);

            GUI.DrawRectangle(spriteBatch,
                new Vector2(x + margin, y + margin),
                new Vector2(width - margin * 2, height - margin * 2),
                new Color(0.0f, 0.0f, 0.0f, 0.6f), true);

            //remove messages that won't fit on the screen
            while (messages.Count() * 20 > height-70)
            {
                messages.RemoveAt(0);
            }

            Vector2 messagePos = new Vector2(x + margin * 2, y + height - 70 - messages.Count()*20);
            foreach (ColoredText message in messages)
            {
                spriteBatch.DrawString(GUI.Font, message.Text, messagePos, message.Color); 
                messagePos.Y += 20;
            }

            textBox.Draw(spriteBatch);
        }

        public static void ExecuteCommand(string command, GameMain game)
        {
#if !DEBUG
            if (Game1.Client!=null)
            {
                ThrowError("Console commands are disabled in multiplayer mode");
                return;
            }
#endif

            if (command == "") return;
            string[] commands = command.Split(' ');

            switch (commands[0].ToLower())
            {
                case "createfilelist":
                    UpdaterUtil.SaveFileList("filelist.xml");
                    break;
                case "spawn":
                    if (commands.Length == 1) return;
                    
                    if (commands[1].ToLower()=="human")
                    {
                        WayPoint spawnPoint = WayPoint.GetRandom(SpawnType.Human);
                        Character.Controlled = new Character(Character.HumanConfigFile, (spawnPoint == null) ? Vector2.Zero : spawnPoint.SimPosition);
                        if (GameMain.GameSession != null)
                        {
                            SinglePlayerMode mode = GameMain.GameSession.gameMode as SinglePlayerMode;
                            if (mode == null) break;
                            mode.CrewManager.AddCharacter(Character.Controlled);
                            mode.CrewManager.SelectCharacter(null, Character.Controlled);
                        }
                    }
                    else
                    {
                        WayPoint spawnPoint = WayPoint.GetRandom(SpawnType.Enemy);
                        new AICharacter("Content/Characters/" + commands[1] + "/" + commands[1] + ".xml", (spawnPoint == null) ? Vector2.Zero : spawnPoint.SimPosition);
                    }

                    break;
                //case "startserver":
                //    if (Game1.Server==null)
                //        Game1.NetworkMember = new GameServer();
                //    break;
                case "kick":
                    if (GameMain.Server == null) break;
                    GameMain.Server.KickPlayer(commands[1]);
                    break;
                case "startclient":
                    if (commands.Length == 1) return;
                    if (GameMain.Client == null)
                    {
                        GameMain.NetworkMember = new GameClient("Name");
                        GameMain.Client.ConnectToServer(commands[1]);
                    }
                    break;
                case "mainmenuscreen":
                case "mainmenu":
                case "menu":
                    GameMain.MainMenuScreen.Select();
                    break;
                case "gamescreen":
                case "game":
                    GameMain.GameScreen.Select();
                    break;
                case "editmapscreen":
                case "editmap":
                case "edit":              
                    GameMain.EditMapScreen.Select();
                    break;
                case "editcharacter":
                case "editchar":
                    GameMain.EditCharacterScreen.Select();
                    break;
                case "freecamera":
                case "freecam":
                    Character.Controlled = null;
                    GameMain.GameScreen.Cam.TargetPos = Vector2.Zero;
                    break;
                case "editwater":
                case "water":
                    if (GameMain.Client== null)
                    {
                        Hull.EditWater = !Hull.EditWater;
                    }
                    break;
                case "generatelevel":
                    GameMain.Level = new Level("asdf", 50.0f, 500,500, 50);
                    GameMain.Level.Generate(100.0f);
                    break;
                case "fixitems":
                    foreach (Item it in Item.itemList)
                    {
                        it.Condition = 100.0f;
                    }
                    break;
                case "fixhull":
                case "fixwalls":
                    foreach (Structure w in Structure.wallList)
                    {
                        for (int i = 0 ; i < w.SectionCount; i++)
                        {
                            w.AddDamage(i, -100000.0f);
                        }
                    }
                    break;
                case "shake":
                    GameMain.GameScreen.Cam.Shake = 10.0f;
                    break;
                case "losenabled":
                case "los":
                case "drawlos":
                    GameMain.LightManager.LosEnabled = !GameMain.LightManager.LosEnabled;
                    break;
                case "lighting":
                case "lightingenabled":
                case "light":
                case "lights":
                    GameMain.LightManager.LightingEnabled = !GameMain.LightManager.LightingEnabled;
                    break;
                case "oxygen":
                case "air":
                    foreach (Hull hull in Hull.hullList)
                    {
                        hull.OxygenPercentage = 100.0f;
                    }
                    break;
                case "tutorial":
                    TutorialMode.Start();
                    break;
                case "lobbyscreen":
                case "lobby":
                    GameMain.LobbyScreen.Select();
                    break;
                case "savemap":
                case "savesub":
                    if (commands.Length < 2) break;

                    string fileName = string.Join(" ", commands.Skip(1));
                    if (fileName.Contains("../"))
                    {
                        DebugConsole.ThrowError("Illegal symbols in filename (../)");
                        return;
                    }
                    Submarine.SaveCurrent(fileName +".gz");
                    NewMessage("map saved", Color.Green);
                    break;
                case "loadmap":
                case "loadsub":
                    if (commands.Length < 2) break;
                    Submarine.Load(string.Join(" ", commands.Skip(1)));
                    break;
                case "cleansub":
                    for (int i = MapEntity.mapEntityList.Count-1; i>=0; i--)
                    {
                        MapEntity me = MapEntity.mapEntityList[i];

                        if (me.SimPosition.Length()>200.0f)
                        {
                            DebugConsole.NewMessage("Removed "+me.Name+" (simposition "+me.SimPosition+")", Color.Orange);
                            MapEntity.mapEntityList.RemoveAt(i);
                        }
                        else if (me.MoveWithLevel)
                        {
                            DebugConsole.NewMessage("Removed " + me.Name + " (MoveWithLevel==true)", Color.Orange);
                            MapEntity.mapEntityList.RemoveAt(i);
                        }
                    }
                    break;
                case "messagebox":
                    if (commands.Length < 3) break;
                    new GUIMessageBox(commands[1], commands[2]);
                    break;
                case "debugdraw":
                    //Hull.DebugDraw = !Hull.DebugDraw;
                    //Ragdoll.DebugDraw = !Ragdoll.DebugDraw;
                    GameMain.DebugDraw = !GameMain.DebugDraw;
                    break;
                default:
                    NewMessage("Command not found", Color.Red);
                    break;
            }
        }

        public static void NewMessage(string msg, Color color)
        {
            if (String.IsNullOrEmpty((msg))) return;
            messages.Add(new ColoredText(msg, color));

            if (textBox != null && textBox.Text == "") selectedIndex = messages.Count;
        }

        public static void ThrowError(string error, Exception e = null)
        {            
            if (e != null) error += " {" + e.Message + "}";
            System.Diagnostics.Debug.WriteLine(error);
            NewMessage(error, Color.Red);
            isOpen = true;
        }
    }
}
