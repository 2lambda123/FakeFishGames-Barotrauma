﻿using Microsoft.Xna.Framework;
using System;

namespace Subsurface
{
    class GUIFrame : GUIComponent
    {        
        public GUIFrame(Rectangle rect, GUIStyle style = null, GUIComponent parent = null)
            : this(rect, null, (Alignment.Left | Alignment.Top), style, parent)
        {
        }


        public GUIFrame(Rectangle rect, Color color, GUIStyle style = null, GUIComponent parent = null)
            : this(rect, color, (Alignment.Left | Alignment.Top), style, parent)
        {
        }

        public GUIFrame(Rectangle rect, Color? color, Alignment alignment, GUIStyle style = null, GUIComponent parent = null)
            : base(style)
        {
            this.rect = rect;

            this.alignment = alignment;
            
            if (color!=null) this.color = (Color)color;

            if (parent != null)
            {
                parent.AddChild(this);
            }
            else
            {
                UpdateDimensions();
            }

            //if (style != null) ApplyStyle(style);
        }
                
        public override void Draw(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch)
        {
            
                           
            Color currColor = color;
            if (state == ComponentState.Selected) currColor = selectedColor;
            if (state == ComponentState.Hover) currColor = hoverColor;

            GUI.DrawRectangle(spriteBatch, rect, currColor * (currColor.A/255.0f), true);
            base.Draw(spriteBatch);

            if (OutlineColor != Color.Transparent)
            {
                GUI.DrawRectangle(spriteBatch, rect, OutlineColor * (OutlineColor.A/255.0f), false);
            }


            DrawChildren(spriteBatch);
        }

    }
}
