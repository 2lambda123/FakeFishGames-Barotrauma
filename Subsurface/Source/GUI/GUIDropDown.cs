﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Barotrauma
{
    public class GUIDropDown : GUIComponent
    {

        public delegate bool OnSelectedHandler(GUIComponent selected);
        public OnSelectedHandler OnSelected;

        private GUIButton button;
        private GUIListBox listBox;

        public bool Dropped { get; set; }

        public object SelectedItemData
        {
            get
            {
                if (listBox.Selected == null) return null;
                return listBox.Selected.UserData;
            }
        }
        
        public GUIDropDown(Rectangle rect, string text, GUIStyle style, GUIComponent parent = null)
            : base(style)
        {
            this.rect = rect;

            if (parent != null) parent.AddChild(this);

            button = new GUIButton(Rectangle.Empty, text, Color.White, Alignment.TopLeft, Alignment.TopLeft, null, this);

            button.TextColor = Color.White;
            button.Color = Color.Black * 0.8f;
            button.HoverColor = Color.DarkGray * 0.8f;
            button.OutlineColor = Color.LightGray * 0.8f;
            button.OnClicked = OnClicked;

            listBox = new GUIListBox(new Rectangle(this.rect.X, this.rect.Bottom, this.rect.Width, 200), style, null);
            listBox.OnSelected = SelectItem;
            //listBox.ScrollBarEnabled = false;
        }

        public void AddItem(string text, object userData = null)
        {
            GUITextBlock textBlock = new GUITextBlock(new Rectangle(0,0,0,20), text, GUI.Style, listBox);
            textBlock.UserData = userData;

            //int totalHeight = 0;
            //foreach (GUIComponent child in listBox.children)
            //{
            //    totalHeight += child.Rect.Height;
            //}

            //listBox.Rect = new Rectangle(listBox.Rect.X,listBox.Rect.Y,listBox.Rect.Width,totalHeight);
        }

        private bool SelectItem(GUIComponent component, object obj)
        {
            GUITextBlock textBlock = component as GUITextBlock;
            if (textBlock==null) return false;
            button.Text = textBlock.Text;

            Dropped = false;

            if (OnSelected != null) OnSelected(component);

            return true;
        }

        public void SelectItem(object userData)
        {
            //GUIComponent child = listBox.children.FirstOrDefault(c => c.UserData == userData);

            //if (child == null) return;

            listBox.Select(userData);

            //SelectItem(child, userData);
        }
       

        private bool wasOpened;

        private bool OnClicked(GUIComponent component, object obj)
        {
            if (wasOpened) return false;

            wasOpened = true;
            Dropped = !Dropped;

            if (Dropped && parent.children[parent.children.Count-1]!=this)
            {
                parent.children.Remove(this);
                parent.children.Add(this);
            }

            return true;
        }

        public override void Update(float deltaTime)
        {
            if (!Visible) return;

            wasOpened = false;

            base.Update(deltaTime);

            if (Dropped && PlayerInput.LeftButtonClicked())
            {
                Rectangle listBoxRect = listBox.Rect;
                listBoxRect.Width += 20;
                if (!listBoxRect.Contains(PlayerInput.MousePosition))
                {
                    Dropped = false;
                }
            }


            button.Update(deltaTime);

            if (Dropped) listBox.Update(deltaTime);
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible) return;

            base.Draw(spriteBatch);

            button.Draw(spriteBatch);

            if (!Dropped) return;

            listBox.Draw(spriteBatch);
        }
    }
}
