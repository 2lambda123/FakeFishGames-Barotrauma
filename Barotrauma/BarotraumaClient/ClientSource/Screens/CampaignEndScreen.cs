﻿using Barotrauma.Media;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Barotrauma
{
    class CampaignEndScreen : Screen
    {
        private readonly CreditsPlayer creditsPlayer;

        private readonly Camera cam;

        public Action OnFinished;

        protected SlideshowPlayer slideshowPlayer;

        public CampaignEndScreen()
        {
            creditsPlayer = new CreditsPlayer(new RectTransform(Vector2.One, Frame.RectTransform), "Content/Texts/Credits.xml")
            {
                AutoRestart = false,
                ScrollBarEnabled = false,
                AllowMouseWheelScroll = false
            };
            new GUIButton(new RectTransform(new Vector2(0.1f), creditsPlayer.RectTransform, Anchor.BottomRight, maxSize: new Point(300, 50)) { AbsoluteOffset = new Point(GUI.IntScale(20)) },
                TextManager.Get("close"))
            {
                OnClicked = (btn, userdata) =>
                {
                    creditsPlayer.Scroll = 1.0f;
                    return true;
                }
            };
            cam = new Camera();
        }

        public override void Select()
        {
            base.Select();
            if (SlideshowPrefab.Prefabs.TryGet("campaignending".ToIdentifier(), out var slideshow))
            {
                slideshowPlayer = new SlideshowPlayer(GUICanvas.Instance, slideshow);
            }
            creditsPlayer.Restart();
            creditsPlayer.Visible = false;
            SteamAchievementManager.UnlockAchievement("campaigncompleted".ToIdentifier(), unlockClients: true);
        }

        public override void Deselect()
        {
            GUI.HideCursor = false;
            SoundPlayer.OverrideMusicType = Identifier.Empty;
        }

        public override void Update(double deltaTime)
        {
            slideshowPlayer?.UpdateManually((float)deltaTime);
            if (creditsPlayer.Finished)
            {
                OnFinished?.Invoke();
                SoundPlayer.OverrideMusicType = Identifier.Empty;
            }
        }

        public override void Draw(double deltaTime, GraphicsDevice graphics, SpriteBatch spriteBatch)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, null, GUI.SamplerState, null, GameMain.ScissorTestEnable);
            graphics.Clear(Color.Black);
            SoundPlayer.OverrideMusicType = "ending".ToIdentifier();
            if (slideshowPlayer != null && !slideshowPlayer.Finished)
            {
                slideshowPlayer.DrawManually(spriteBatch);
            }
            else
            {
                GUI.HideCursor = false;
                creditsPlayer.Visible = true;
            }
            GUI.Draw(cam, spriteBatch);
            spriteBatch.End();
        }
    }
}
