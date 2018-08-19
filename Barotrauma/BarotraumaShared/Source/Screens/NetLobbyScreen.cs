﻿using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using System;

namespace Barotrauma
{
    partial class NetLobbyScreen : Screen
    {
        public string ServerName = "Server";

        private UInt16 lastUpdateID;
        public UInt16 LastUpdateID
        {
            get
            {
#if SERVER
                if (GameMain.Server != null && lastUpdateID < 1) lastUpdateID++;
#endif
                return lastUpdateID;
            }
            set { lastUpdateID = value; }
        }

        //for guitextblock delegate
        public string GetServerName()
        {
            return ServerName;
        }
        
        private string levelSeed = "";

        public void SetLevelDifficulty(float difficulty)
        {
            difficulty = MathHelper.Clamp(difficulty, 0.0f, 100.0f);
#if SERVER
            if (GameMain.Server != null)
            {
                GameMain.Server.SelectedLevelDifficulty = difficulty;
                lastUpdateID++;
            }
#endif
#if CLIENT
            levelDifficultyScrollBar.BarScroll = difficulty / 100.0f;
#endif
        }
        
        public void ToggleTraitorsEnabled(int dir)
        {
#if SERVER
            if (GameMain.Server == null) return;

            lastUpdateID++;
            
            int index = (int)GameMain.Server.TraitorsEnabled + dir;
            if (index < 0) index = 2;
            if (index > 2) index = 0;

            SetTraitorsEnabled((YesNoMaybe)index);
#endif
        }

        public void SetBotCount(int botCount)
        {
#if SERVER
            if (GameMain.Server != null)
            {
                if (botCount < 0) botCount = GameMain.Server.MaxBotCount;
                if (botCount > GameMain.Server.MaxBotCount) botCount = 0;

                GameMain.Server.BotCount = botCount;
                lastUpdateID++;
            }
#endif
#if CLIENT
            (botCountText as GUITextBlock).Text = botCount.ToString();
#endif
        }

        public void SetBotSpawnMode(BotSpawnMode botSpawnMode)
        {
#if SERVER
            if (GameMain.Server != null)
            {
                GameMain.Server.BotSpawnMode = botSpawnMode;
                lastUpdateID++;
            }
#endif
#if CLIENT
            (botSpawnModeText as GUITextBlock).Text = botSpawnMode.ToString();
#endif
        }

        public void SetTraitorsEnabled(YesNoMaybe enabled)
        {
#if SERVER
            if (GameMain.Server != null) GameMain.Server.TraitorsEnabled = enabled;
#endif
#if CLIENT
            (traitorProbabilityText as GUITextBlock).Text = enabled.ToString();
#endif
        }
    }
}
