﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Barotrauma
{
    public class GUITextBlock : GUIComponent
    {
        protected string text;

        protected Alignment textAlignment;

        private float textScale = 1;

        protected Vector2 textPos;
        protected Vector2 origin;

        protected Vector2 caretPos;

        protected Color textColor;

        private string wrappedText;

        public delegate string TextGetterHandler();
        public TextGetterHandler TextGetter;

        public bool Wrap;

        private bool overflowClipActive;
        public bool OverflowClip;

        private float textDepth;

        public Vector2 TextOffset { get; set; }

        public override Vector4 Padding
        {
            get { return padding; }
            set 
            { 
                padding = value;
                SetTextPos();
            }
        }

        public string Text
        {
            get { return text; }
            set
            {
                if (Text == value) return;

                text = value;
                wrappedText = value;
                SetTextPos();
            }
        }

        public string WrappedText
        {
            get { return wrappedText; }
        }

        public override Rectangle Rect
        {
            get
            {
                return base.Rect;
            }
            set
            {
                if (RectTransform != null) { return; }
                if (base.Rect == value) return;
                foreach (GUIComponent child in Children)
                {
                    child.Rect = new Rectangle(child.Rect.X + value.X - rect.X, child.Rect.Y + value.Y - rect.Y, child.Rect.Width, child.Rect.Height);
                }

                Point moveAmount = value.Location - rect.Location;

                rect = value;
                if (value.Width != rect.Width || value.Height != rect.Height)
                {
                    SetTextPos();
                }
                else if (moveAmount != Point.Zero)
                {
                    caretPos += moveAmount.ToVector2();
                }
            }
        }

        public float TextDepth
        {
            get { return textDepth; }
            set { textDepth = MathHelper.Clamp(value, 0.0f, 1.0f); }
        }
        
        public Vector2 TextPos
        {
            get { return textPos; }
        }

        public float TextScale
        {
            get { return textScale; }
            set
            {
                if (value != textScale)
                {
                    textScale = value;
                    SetTextPos();
                }
            }
        }

        public Vector2 Origin
        {
            get { return origin; }
        }

        public Color TextColor
        {
            get { return textColor; }
            set { textColor = value; }
        }

        public Vector2 CaretPos
        {
            get { return caretPos; }
        }

        [System.Obsolete("Use RectTransform instead of Rectangle")]
        public GUITextBlock(Rectangle rect, string text, string style, GUIComponent parent, ScalableFont font)
            : this(rect, text, style, Alignment.TopLeft, Alignment.TopLeft, parent, false, font)
        {
        }


        [System.Obsolete("Use RectTransform instead of Rectangle")]
        public GUITextBlock(Rectangle rect, string text, string style, GUIComponent parent = null, bool wrap = false)
            : this(rect, text, style, Alignment.TopLeft, Alignment.TopLeft, parent, wrap)
        {
        }

        [System.Obsolete("Use RectTransform instead of Rectangle")]
        public GUITextBlock(Rectangle rect, string text, Color? color, Color? textColor, Alignment textAlignment = Alignment.Left, string style = null, GUIComponent parent = null, bool wrap = false)
            : this(rect, text,color, textColor, Alignment.TopLeft, textAlignment, style, parent, wrap)
        {
        }

        /// <summary>
        /// This is the new constructor.
        /// </summary>
        public GUITextBlock(RectTransform rectT, string text, Color? textColor = null, ScalableFont font = null, 
            Alignment textAlignment = Alignment.Left, bool wrap = false, string style = "", Color? color = null) 
            : base(style, rectT)
        {
            if (color.HasValue)
            {
                this.color = color.Value;
            }
            if (textColor.HasValue)
            {
                this.textColor = textColor.Value;
            }
            this.Font = font ?? GUI.Font;
            this.text = text;
            this.textAlignment = textAlignment;
            this.Wrap = wrap;
            SetTextPos();
        }

        protected override void UpdateDimensions(GUIComponent parent = null)
        {
            base.UpdateDimensions(parent);

            SetTextPos();
        }

        public override void ApplyStyle(GUIComponentStyle style)
        {
            if (style == null) return;
            base.ApplyStyle(style);

            textColor = style.textColor;
        }


        public GUITextBlock(Rectangle rect, string text, Color? color, Color? textColor, Alignment alignment, Alignment textAlignment = Alignment.Left, string style = null, GUIComponent parent = null, bool wrap = false, ScalableFont font = null)
            : this (rect, text, style, alignment, textAlignment, parent, wrap, font)
        {
            if (color != null) this.color = (Color)color;
            if (textColor != null) this.textColor = (Color)textColor;
        }

        public GUITextBlock(Rectangle rect, string text, string style, Alignment alignment = Alignment.TopLeft, Alignment textAlignment = Alignment.TopLeft, GUIComponent parent = null, bool wrap = false, ScalableFont font = null)
            : base(style)        
        {
            this.Font = font == null ? GUI.Font : font;

            this.rect = rect;

            this.text = text;

            this.alignment = alignment;

            this.padding = new Vector4(5.0f, 5.0f, 5.0f, 5.0f);

            this.textAlignment = textAlignment;
            
            if (parent != null)
                parent.AddChild(this);

            this.Wrap = wrap;

            SetTextPos();

            TextScale = 1.0f;

            if (rect.Height == 0 && !string.IsNullOrEmpty(Text))
            {
                this.rect.Height = (int)Font.MeasureString(wrappedText).Y;
            }
        }

        public void SetTextPos()
        {
            if (text == null) return;

            var rect = Rect;

            overflowClipActive = false;

            wrappedText = text;

            Vector2 size = MeasureText(text);           

            if (Wrap && rect.Width > 0)
            {
                wrappedText = ToolBox.WrapText(text, rect.Width - padding.X - padding.Z, Font, textScale);
                size = MeasureText(wrappedText);
            }
            else if (OverflowClip)
            {
                overflowClipActive = size.X > rect.Width - padding.X - padding.Z;
            }
                     
            textPos = new Vector2(rect.Width / 2.0f, rect.Height / 2.0f);
            origin = size * 0.5f;

            if (textAlignment.HasFlag(Alignment.Left) && !overflowClipActive)
                origin.X += (rect.Width / 2.0f - padding.X) - size.X / 2;
            
            if (textAlignment.HasFlag(Alignment.Right) || overflowClipActive)
                origin.X -= (rect.Width / 2.0f - padding.Z) - size.X / 2;

            if (textAlignment.HasFlag(Alignment.Top))
                origin.Y += (rect.Height / 2.0f - padding.Y) - size.Y / 2;

            if (textAlignment.HasFlag(Alignment.Bottom))
                origin.Y -= (rect.Height / 2.0f - padding.W) - size.Y / 2;
            
            origin.X = (int)origin.X;
            origin.Y = (int)origin.Y;

            textPos.X = (int)textPos.X;
            textPos.Y = (int)textPos.Y;

            if (wrappedText.Contains("\n"))
            {
                string[] lines = wrappedText.Split('\n');
                Vector2 lastLineSize = MeasureText(lines[lines.Length-1]);
                caretPos = new Vector2(rect.X + lastLineSize.X, rect.Y + size.Y - lastLineSize.Y) + textPos - origin;
            }
            else
            {
                caretPos = new Vector2(rect.X + size.X, rect.Y) + textPos - origin;
            }
        }

        private Vector2 MeasureText(string text) 
        {
            if (Font == null) return Vector2.Zero;

            Vector2 size = Vector2.Zero;
            while (size == Vector2.Zero)
            {
                try { size = Font.MeasureString((text == "") ? " " : text); }
                catch { text = text.Substring(0, text.Length - 1); }
            }

            return size;
        }

        protected override void SetAlpha(float a)
        {
            base.SetAlpha(a);
            textColor = new Color(textColor.R, textColor.G, textColor.B, a);
        }

        protected override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible) return;

            Color currColor = color;
            if (state == ComponentState.Hover) currColor = hoverColor;
            if (state == ComponentState.Selected) currColor = selectedColor;

            var rect = Rect;

            base.Draw(spriteBatch);

            if (TextGetter != null) Text = TextGetter();

            Rectangle prevScissorRect = spriteBatch.GraphicsDevice.ScissorRectangle;
            if (overflowClipActive)
            {
                Rectangle scissorRect = new Rectangle(rect.X + (int)padding.X, rect.Y, rect.Width - (int)padding.X - (int)padding.Z, rect.Height);
                spriteBatch.GraphicsDevice.ScissorRectangle = scissorRect;
            }

            if (!string.IsNullOrEmpty(text))
            {
                Font.DrawString(spriteBatch,
                    Wrap ? wrappedText : text,
                    rect.Location.ToVector2() + textPos + TextOffset,
                    textColor * (textColor.A / 255.0f),
                    0.0f, origin, TextScale,
                    SpriteEffects.None, textDepth);
            }

            if (overflowClipActive)
            {
                spriteBatch.GraphicsDevice.ScissorRectangle = prevScissorRect;
            }

            if (OutlineColor.A * currColor.A > 0.0f) GUI.DrawRectangle(spriteBatch, rect, OutlineColor * (currColor.A / 255.0f), false);
        }
    }
}
