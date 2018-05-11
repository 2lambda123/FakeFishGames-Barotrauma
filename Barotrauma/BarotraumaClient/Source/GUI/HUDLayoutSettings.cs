﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Barotrauma
{
    static class HUDLayoutSettings
    {
        public static bool DebugDraw;

        public static Rectangle ButtonAreaTop
        {
            get; private set;
        }

        public static Rectangle InventoryAreaUpper
        {
            get; private set;
        }

        public static Rectangle CrewArea
        {
            get; private set;
        }

        public static Rectangle ChatBoxArea
        {
            get; private set;
        }

        public static Alignment ChatBoxAlignment
        {
            get; private set;
        }

        public static Rectangle InventoryAreaLower
        {
            get; private set;
        }

        public static Rectangle HealthBarAreaLeft
        {
            get; private set;
        }
        public static Rectangle AfflictionAreaLeft
        {
            get; private set;
        }

        public static Rectangle HealthBarAreaRight
        {
            get; private set;
        }
        public static Rectangle AfflictionAreaRight
        {
            get; private set;
        }

        public static Rectangle HealthWindowAreaLeft
        {
            get; private set;
        }

        public static Rectangle HealthWindowAreaRight
        {
            get; private set;
        }

        public static Rectangle ReportArea
        {
            get; private set;
        }

        static HUDLayoutSettings()
        {
            CreateAreas();
        }

        public static void CreateAreas()
        {
            int padding = (int)(10 * GUI.Scale);

            //slice from the top of the screen for misc buttons (info, end round, server controls)
            ButtonAreaTop = new Rectangle(padding, padding, GameMain.GraphicsWidth - padding * 2, (int)(50 * GUI.Scale));

            //slice for the upper slots of the inventory (clothes, id card, headset)
            int inventoryAreaUpperWidth = (int)Math.Min(GameMain.GraphicsWidth* 0.2f, 300);
            int inventoryAreaUpperHeight = (int)Math.Min(GameMain.GraphicsHeight * 0.2f, 200);
            InventoryAreaUpper = new Rectangle(GameMain.GraphicsWidth - inventoryAreaUpperWidth - padding, ButtonAreaTop.Bottom + padding, inventoryAreaUpperWidth, inventoryAreaUpperHeight);

            CrewArea = new Rectangle(padding, ButtonAreaTop.Bottom + padding, GameMain.GraphicsWidth - InventoryAreaUpper.Width - padding * 3, InventoryAreaUpper.Height);

            //horizontal slices at the corners of the screen for health bar and affliction icons
            int healthBarWidth = (int)Math.Max(20 * GUI.Scale, 15);
            int afflictionAreaWidth = (int)(60 * GUI.Scale);
            int lowerAreaHeight = (int)Math.Min(GameMain.GraphicsHeight * 0.35f, 280);
            HealthBarAreaLeft = new Rectangle(padding, GameMain.GraphicsHeight - lowerAreaHeight, healthBarWidth, lowerAreaHeight - padding);
            AfflictionAreaLeft = new Rectangle(HealthBarAreaLeft.Right + (int)(5 * GUI.Scale), GameMain.GraphicsHeight - lowerAreaHeight, afflictionAreaWidth, lowerAreaHeight - padding);
            
            HealthBarAreaRight = new Rectangle(GameMain.GraphicsWidth - padding - healthBarWidth, HealthBarAreaLeft.Y, healthBarWidth, HealthBarAreaLeft.Height);
            AfflictionAreaRight = new Rectangle(HealthBarAreaRight.X - afflictionAreaWidth - (int)(5 * GUI.Scale), GameMain.GraphicsHeight - lowerAreaHeight, afflictionAreaWidth, lowerAreaHeight - padding);
            
            //entire bottom side of the screen for inventory, minus health and affliction areas at the sides
            InventoryAreaLower = new Rectangle(AfflictionAreaLeft.Right + padding, GameMain.GraphicsHeight - lowerAreaHeight, AfflictionAreaRight.X - AfflictionAreaLeft.Right - padding * 2, lowerAreaHeight);

            //chatbox between upper and lower inventory areas, can be on either side depending on the alignment
            ChatBoxAlignment = Alignment.Left;
            int chatBoxWidth = (int)Math.Min(300 * GUI.Scale, 300);
            int chatBoxHeight = Math.Min(InventoryAreaLower.Y - InventoryAreaUpper.Bottom - padding * 2, 450);
            ChatBoxArea = ChatBoxAlignment == Alignment.Left ?
                new Rectangle(padding, InventoryAreaUpper.Bottom + padding, chatBoxWidth, chatBoxHeight) :
                new Rectangle(GameMain.GraphicsWidth - padding - chatBoxWidth, InventoryAreaUpper.Bottom + padding, chatBoxWidth, chatBoxHeight);

            //health windows between upper and lower inventory areas, minus the area taken up by the chatbox on either side
            Rectangle healthWindowArea = ChatBoxAlignment == Alignment.Left ?
                new Rectangle(ChatBoxArea.Right + 60, InventoryAreaUpper.Y, GameMain.GraphicsWidth - ChatBoxArea.Width * 2 - 60 - padding, InventoryAreaLower.Y - InventoryAreaUpper.Y - padding * 2) :
                new Rectangle(padding - ChatBoxArea.Width, InventoryAreaUpper.Y, GameMain.GraphicsWidth - ChatBoxArea.Width * 2 - 60 - padding, InventoryAreaLower.Y - InventoryAreaUpper.Y - padding * 2);

            //split the health area vertically, left side for the player's own health and right side for the character they're treating
            int healthWindowWidth = Math.Min((int)(healthWindowArea.Width * 0.75f) - padding / 2, 500);
            HealthWindowAreaLeft = new Rectangle(healthWindowArea.X, healthWindowArea.Y, healthWindowWidth, healthWindowArea.Height);
            HealthWindowAreaRight = new Rectangle(healthWindowArea.Right - healthWindowWidth, healthWindowArea.Y, healthWindowWidth, healthWindowArea.Height);

            //report buttons (report breach etc) appear center right, not visible when health window is open
            int reportAreaWidth = (int)Math.Min(150 * GUI.Scale, 200);
            ReportArea = new Rectangle(GameMain.GraphicsWidth - padding - reportAreaWidth, InventoryAreaUpper.Bottom + padding, reportAreaWidth, InventoryAreaLower.Y - InventoryAreaUpper.Bottom - padding * 2);
        }

        public static void Draw(SpriteBatch spriteBatch)
        {
            GUI.DrawRectangle(spriteBatch, ButtonAreaTop, Color.White * 0.5f);
            GUI.DrawRectangle(spriteBatch, InventoryAreaUpper, Color.Yellow * 0.5f);
            GUI.DrawRectangle(spriteBatch, CrewArea, Color.Blue * 0.5f);
            GUI.DrawRectangle(spriteBatch, ChatBoxArea, Color.Cyan * 0.5f);
            GUI.DrawRectangle(spriteBatch, HealthBarAreaLeft, Color.Red * 0.5f);
            GUI.DrawRectangle(spriteBatch, AfflictionAreaLeft, Color.Red * 0.5f);
            GUI.DrawRectangle(spriteBatch, HealthBarAreaRight, Color.Red * 0.5f);
            GUI.DrawRectangle(spriteBatch, AfflictionAreaRight, Color.Red * 0.5f);
            GUI.DrawRectangle(spriteBatch, InventoryAreaLower, Color.Yellow * 0.5f);
            GUI.DrawRectangle(spriteBatch, HealthWindowAreaLeft, Color.Red * 0.5f);
            GUI.DrawRectangle(spriteBatch, HealthWindowAreaRight, Color.Red * 0.5f);
        }
    }
}
