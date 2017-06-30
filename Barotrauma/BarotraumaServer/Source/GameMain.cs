﻿using System;
using System.Diagnostics;
using System.Reflection;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using Barotrauma.Networking;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System.Threading;

namespace Barotrauma
{
    class GameMain
    {
        public static readonly Version Version = Assembly.GetEntryAssembly().GetName().Version;

        public static World World;
        public static GameSettings Config;

        public static GameServer Server;
        public const GameClient Client = null;
        public static NetworkMember NetworkMember
        {
            get { return Server as NetworkMember; }
        }

        public static GameSession GameSession;

        public static GameMain Instance
        {
            get;
            private set;
        }

        //only screens the server implements
        public static GameScreen GameScreen;
        public static NetLobbyScreen NetLobbyScreen;

        //null screens because they are not implemented by the server,
        //but they're checked for all over the place
        //TODO: maybe clean up instead of having these constants
        public static readonly Screen MainMenuScreen = UnimplementedScreen.Instance;
        public static readonly Screen LobbyScreen = UnimplementedScreen.Instance;

        public static readonly Screen ServerListScreen = UnimplementedScreen.Instance;

        public static readonly Screen EditMapScreen = UnimplementedScreen.Instance;
        public static readonly Screen EditCharacterScreen = UnimplementedScreen.Instance;

        //
        public static bool ShouldRun = true;

        public static ContentPackage SelectedPackage
        {
            get { return Config.SelectedContentPackage; }
        }

        public GameMain()
        {
            Instance = this;

            World = new World(new Vector2(0, -9.82f));
            FarseerPhysics.Settings.AllowSleep = true;
            FarseerPhysics.Settings.ContinuousPhysics = false;
            FarseerPhysics.Settings.VelocityIterations = 1;
            FarseerPhysics.Settings.PositionIterations = 1;

            Config = new GameSettings("serverconfig.xml");
            if (Config.WasGameUpdated)
            {
                UpdaterUtil.CleanOldFiles();
                Config.WasGameUpdated = false;
                Config.Save("serverconfig.xml");
            }

            GameScreen = new GameScreen();
        }

        public void Init()
        {
            Mission.Init();
            MapEntityPrefab.Init();
            LevelGenerationParams.LoadPresets();

            JobPrefab.LoadAll(SelectedPackage.GetFilesOfType(ContentType.Jobs));
            StructurePrefab.LoadAll(SelectedPackage.GetFilesOfType(ContentType.Structure));

            ItemPrefab.LoadAll(SelectedPackage.GetFilesOfType(ContentType.Item));

            GameModePreset.Init();

            LocationType.Init();

            Submarine.RefreshSavedSubs();

            Screen.SelectNull();

            NetLobbyScreen = new NetLobbyScreen();
        }

        public void StartServer()
        {
            Server = new GameServer("Dedicated Server Test", 14242, false, "asd", false, 10);
        }

        public void CloseServer()
        {
            Server.Disconnect();
            Server = null;
        }

        public void Run()
        {
            Hyper.ComponentModel.HyperTypeDescriptionProvider.Add(typeof(Character));
            Hyper.ComponentModel.HyperTypeDescriptionProvider.Add(typeof(Item));
            Hyper.ComponentModel.HyperTypeDescriptionProvider.Add(typeof(Items.Components.ItemComponent));
            Hyper.ComponentModel.HyperTypeDescriptionProvider.Add(typeof(Hull));

            Init();
            StartServer();

            DateTime prevTime = DateTime.Now;

            while (ShouldRun)
            {
                prevTime = DateTime.Now;

                DebugConsole.Update();
                if (Screen.Selected != null) Screen.Selected.Update((float)Timing.Step);
                Server.Update((float)Timing.Step);
                CoroutineManager.Update((float)Timing.Step, (float)Timing.Step);
                
                int frameTime = DateTime.Now.Subtract(prevTime).Milliseconds;
                Thread.Sleep(Math.Max((int)(Timing.Step * 1000.0) - frameTime,0));
            }

            CloseServer();

        }
        
        public void ProcessInput()
        {
            while (true)
            {
                string input = Console.ReadLine();
                lock (DebugConsole.QueuedCommands)
                {
                    DebugConsole.QueuedCommands.Add(input);
                }
            }
        }

        public CoroutineHandle ShowLoading(IEnumerable<object> loader, bool waitKeyHit = true)
        {
            return CoroutineManager.StartCoroutine(loader);
        }
    }
}
