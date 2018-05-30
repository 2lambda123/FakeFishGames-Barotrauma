﻿using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
#if CLIENT
using Microsoft.Xna.Framework.Graphics;
using Barotrauma.Tutorials;
#endif
using System;

namespace Barotrauma
{
    public enum WindowMode
    {
        Windowed, Fullscreen, BorderlessWindowed
    }

    public partial class GameSettings
    {
        const string FilePath = "config.xml";

        public int GraphicsWidth { get; set; }
        public int GraphicsHeight { get; set; }

        public bool VSyncEnabled { get; set; }

        public bool EnableSplashScreen { get; set; }

        public int ParticleLimit { get; set; }
                
        private KeyOrMouse[] keyMapping;

        private WindowMode windowMode;

        public List<string> jobNamePreferences;
        
        public bool UseSteamMatchmaking { get; set; }

#if DEBUG
        //steam functionality can be enabled/disabled in debug builds
        public bool UseSteam;
#endif

        public WindowMode WindowMode
        {
            get { return windowMode; }
            set { windowMode = value; }
        }

        public List<string> JobNamePreferences
        {
            get { return jobNamePreferences; }
            set { jobNamePreferences = value; }
        }

        private bool unsavedSettings;

        public bool UnsavedSettings
        {
            get
            {
                return unsavedSettings;
            }
            private set
            {
                unsavedSettings = value;
#if CLIENT
                if (applyButton != null)
                {
                    //applyButton.Selected = unsavedSettings;
                    applyButton.Enabled = unsavedSettings;
                    applyButton.Text = unsavedSettings ? "Apply*" : "Apply";
                }
#endif
            }
        }

        private float soundVolume, musicVolume;

        public float SoundVolume
        {
            get { return soundVolume; }
            set
            {
                soundVolume = MathHelper.Clamp(value, 0.0f, 1.0f);
#if CLIENT
                if (GameMain.SoundManager!=null)
                {
                    GameMain.SoundManager.SetCategoryGainMultiplier("default",soundVolume);
                    GameMain.SoundManager.SetCategoryGainMultiplier("ui",soundVolume);
                    GameMain.SoundManager.SetCategoryGainMultiplier("waterambience",soundVolume);
                }
#endif
            }
        }

        public float MusicVolume
        {
            get { return musicVolume; }
            set
            {
                musicVolume = MathHelper.Clamp(value, 0.0f, 1.0f);
#if CLIENT
                SoundPlayer.MusicVolume = musicVolume;
#endif
            }
        }

        public ContentPackage SelectedContentPackage { get; set; }

        public string   MasterServerUrl { get; set; }
        public bool     AutoCheckUpdates { get; set; }
        public bool     WasGameUpdated { get; set; }

        private string defaultPlayerName;
        public string   DefaultPlayerName
        {
            get
            {
                return defaultPlayerName ?? "";
            }
            set
            {
                if (defaultPlayerName != value)
                {
                    defaultPlayerName = value;
                    Save();
                }
            }
        }

        public List<string> CompletedTutorialNames { get; private set; }

        public static bool VerboseLogging { get; set; }
        public static bool SaveDebugConsoleLogs { get; set; }

        public GameSettings(string filePath)
        {
            ContentPackage.LoadAll(ContentPackage.Folder);
            CompletedTutorialNames = new List<string>();
            Load(filePath);
        }

        public void Load(string filePath)
        {
            XDocument doc = XMLExtensions.TryLoadXml(filePath);
            
            MasterServerUrl = doc.Root.GetAttributeString("masterserverurl", "");

            AutoCheckUpdates = doc.Root.GetAttributeBool("autocheckupdates", true);
            WasGameUpdated = doc.Root.GetAttributeBool("wasgameupdated", false);

            VerboseLogging = doc.Root.GetAttributeBool("verboselogging", false);
            SaveDebugConsoleLogs = doc.Root.GetAttributeBool("savedebugconsolelogs", false);

#if CLIENT
            UseSteam = doc.Root.GetAttributeBool("usesteam", true);
#endif

            if (doc == null)
            {
                GraphicsWidth = 1024;
                GraphicsHeight = 678;

                MasterServerUrl = "";

                SelectedContentPackage = ContentPackage.list.Any() ? ContentPackage.list[0] : new ContentPackage("");

                jobNamePreferences = new List<string>();
                foreach (JobPrefab job in JobPrefab.List)
                {
                    jobNamePreferences.Add(job.Name);
                }
                return;
            }

            XElement graphicsMode = doc.Root.Element("graphicsmode");
            GraphicsWidth = graphicsMode.GetAttributeInt("width", 0);
            GraphicsHeight = graphicsMode.GetAttributeInt("height", 0);
            VSyncEnabled = graphicsMode.GetAttributeBool("vsync", true);

            XElement graphicsSettings = doc.Root.Element("graphicssettings");
            ParticleLimit = graphicsSettings.GetAttributeInt("particlelimit", 1500);

#if CLIENT
            if (GraphicsWidth == 0 || GraphicsHeight == 0)
            {
                GraphicsWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
                GraphicsHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            }
#endif

            //FullScreenEnabled = ToolBox.GetAttributeBool(graphicsMode, "fullscreen", true);

            var windowModeStr = graphicsMode.GetAttributeString("displaymode", "Fullscreen");
            if (!Enum.TryParse<WindowMode>(windowModeStr, out windowMode))
            {
                windowMode = WindowMode.Fullscreen;
            }

            SoundVolume = doc.Root.GetAttributeFloat("soundvolume", 1.0f);
            MusicVolume = doc.Root.GetAttributeFloat("musicvolume", 0.3f);

#if DEBUG
            UseSteamMatchmaking = doc.Root.GetAttributeBool("usesteammatchmaking", true) && UseSteam;
#else
            UseSteamMatchmaking = doc.Root.GetAttributeBool("usesteammatchmaking", true) && Steam.SteamManager.USE_STEAM;
#endif

            EnableSplashScreen = doc.Root.GetAttributeBool("enablesplashscreen", true);

            keyMapping = new KeyOrMouse[Enum.GetNames(typeof(InputType)).Length];
            keyMapping[(int)InputType.Up] = new KeyOrMouse(Keys.W);
            keyMapping[(int)InputType.Down] = new KeyOrMouse(Keys.S);
            keyMapping[(int)InputType.Left] = new KeyOrMouse(Keys.A);
            keyMapping[(int)InputType.Right] = new KeyOrMouse(Keys.D);
            keyMapping[(int)InputType.Run] = new KeyOrMouse(Keys.LeftShift);

            keyMapping[(int)InputType.Chat] = new KeyOrMouse(Keys.Tab);
            keyMapping[(int)InputType.RadioChat] = new KeyOrMouse(Keys.OemPipe);
            keyMapping[(int)InputType.CrewOrders] = new KeyOrMouse(Keys.C);

            keyMapping[(int)InputType.Select] = new KeyOrMouse(Keys.E);

            keyMapping[(int)InputType.Use] = new KeyOrMouse(0);
            keyMapping[(int)InputType.Aim] = new KeyOrMouse(1);

            foreach (XElement subElement in doc.Root.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "keymapping":
                        foreach (XAttribute attribute in subElement.Attributes())
                        {
                            if (Enum.TryParse(attribute.Name.ToString(), true, out InputType inputType))
                            {
                                if (int.TryParse(attribute.Value.ToString(), out int mouseButton))
                                {
                                    keyMapping[(int)inputType] = new KeyOrMouse(mouseButton);
                                }
                                else
                                {
                                    if (Enum.TryParse(attribute.Value.ToString(), true, out Keys key))
                                    {
                                        keyMapping[(int)inputType] = new KeyOrMouse(key);
                                    }
                                }
                            }
                        }
                        break;
                    case "gameplay":
                        jobNamePreferences = new List<string>();
                        foreach (XElement ele in subElement.Element("jobpreferences").Elements("job"))
                        {
                            jobNamePreferences.Add(ele.GetAttributeString("name", ""));
                        }
                        break;
                    case "player":
                        defaultPlayerName = subElement.GetAttributeString("name", "");
                        break;
                    case "tutorials":
                        foreach (XElement tutorialElement in subElement.Elements())
                        {
                            CompletedTutorialNames.Add(tutorialElement.GetAttributeString("name", ""));
                        }
                        break;
                }
            }

            foreach (InputType inputType in Enum.GetValues(typeof(InputType)))
            {
                if (keyMapping[(int)inputType] == null)
                {
                    DebugConsole.ThrowError("Key binding for the input type \"" + inputType + " not set!");
                    keyMapping[(int)inputType] = new KeyOrMouse(Keys.D1);
                }
            }
            
            UnsavedSettings = false;

            foreach (XElement subElement in doc.Root.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "contentpackage":
                        string path = subElement.GetAttributeString("path", "");
                        
                        SelectedContentPackage = ContentPackage.list.Find(cp => cp.Path == path);

                        if (SelectedContentPackage == null) SelectedContentPackage = new ContentPackage(path);
                        break;
                }
            }
        }
        
        public void Save()
        {
            UnsavedSettings = false;

            XDocument doc = new XDocument();

            if (doc.Root == null)
            {
                doc.Add(new XElement("config"));
            }

            doc.Root.Add(
                new XAttribute("masterserverurl", MasterServerUrl),
                new XAttribute("autocheckupdates", AutoCheckUpdates),
                new XAttribute("musicvolume", musicVolume),
                new XAttribute("soundvolume", soundVolume),
                new XAttribute("verboselogging", VerboseLogging),
                new XAttribute("savedebugconsolelogs", SaveDebugConsoleLogs),
                new XAttribute("enablesplashscreen", EnableSplashScreen));

            if (WasGameUpdated)
            {
                doc.Root.Add(new XAttribute("wasgameupdated", true));
            }

            XElement gMode = doc.Root.Element("graphicsmode");
            if (gMode == null)
            {
                gMode = new XElement("graphicsmode");
                doc.Root.Add(gMode);
            }

            if (GraphicsWidth == 0 || GraphicsHeight == 0)
            {
                gMode.ReplaceAttributes(new XAttribute("displaymode", windowMode));
            }
            else
            {
                gMode.ReplaceAttributes(
                    new XAttribute("width", GraphicsWidth),
                    new XAttribute("height", GraphicsHeight),
                    new XAttribute("vsync", VSyncEnabled),
                    new XAttribute("displaymode", windowMode));
            }

            XElement gSettings = doc.Root.Element("graphicssettings");
            if (gSettings == null)
            {
                gSettings = new XElement("graphicssettings");
                doc.Root.Add(gSettings);
            }

            gSettings.ReplaceAttributes(new XAttribute("particlelimit", ParticleLimit));

            if (SelectedContentPackage != null)
            {
                doc.Root.Add(new XElement("contentpackage",
                    new XAttribute("path", SelectedContentPackage.Path)));
            }

            var keyMappingElement = new XElement("keymapping");
            doc.Root.Add(keyMappingElement);
            for (int i = 0; i < keyMapping.Length; i++)
            {
                if (keyMapping[i].MouseButton == null)
                {
                    keyMappingElement.Add(new XAttribute(((InputType)i).ToString(), keyMapping[i].Key));
                }
                else
                {
                    keyMappingElement.Add(new XAttribute(((InputType)i).ToString(), keyMapping[i].MouseButton));
                }
            }

            var gameplay = new XElement("gameplay");
            var jobPreferences = new XElement("jobpreferences");
            foreach (string jobName in JobNamePreferences)
            {
                jobPreferences.Add(new XElement("job", new XAttribute("name", jobName)));
            }
            gameplay.Add(jobPreferences);
            doc.Root.Add(gameplay);

            var playerElement = new XElement("player");
            playerElement.Add(new XAttribute("name", defaultPlayerName ?? ""));
            doc.Root.Add(playerElement);

#if CLIENT
            if (Tutorial.Tutorials != null)
            {
                foreach (Tutorial tutorial in Tutorial.Tutorials)
                {
                    if (tutorial.Completed && !CompletedTutorialNames.Contains(tutorial.Name))
                    {
                        CompletedTutorialNames.Add(tutorial.Name);
                    }
                }
            }
#endif
            var tutorialElement = new XElement("tutorials");
            foreach (string tutorialName in CompletedTutorialNames)
            {
                tutorialElement.Add(new XElement("Tutorial", new XAttribute("name", tutorialName)));
            }
            doc.Root.Add(tutorialElement);

            doc.Save(FilePath);
        }
    }
}
