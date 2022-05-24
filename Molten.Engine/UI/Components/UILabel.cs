﻿using Molten.Graphics;
using System.Runtime.Serialization;

namespace Molten.UI
{
    /// <summary>
    /// Provides text rendering capabilty for the UI system, with the aim of labeling elements to the user.
    /// </summary>
    public class UILabel : UIElement<UITextData>
    {
        UIHorizonalAlignment _hAlign;
        UIVerticalAlignment _vAlign;

        protected override void OnInitialize(Engine engine, UISettings settings, UITheme theme)
        {
            base.OnInitialize(engine, settings, theme);

            Properties.Color = theme.TextColor;
            Properties.Text = Name;
            theme.RequestFont(engine, LoadFont_Request);
        }

        private void LoadFont_Request(ContentRequest cr)
        {
            Font = cr.Get<TextFont>(0);
        }

        protected override void OnUpdateBounds()
        {
            base.OnUpdateBounds();

            if (Properties.Font == null)
                return;

            Properties.Position = (Vector2F)LocalBounds.TopLeft;
            Vector2F textSize = Properties.Font.MeasureString(Properties.Text);

            switch (_hAlign)
            {
                case UIHorizonalAlignment.Center:
                    Properties.Position.X = RenderBounds.Center.X - (textSize.X / 2);
                    break;

                case UIHorizonalAlignment.Right:
                    Properties.Position.X = RenderBounds.Right - textSize.X;
                    break;
            }

            switch (_vAlign)
            {
                case UIVerticalAlignment.Center:
                    Properties.Position.Y = RenderBounds.Center.Y - (textSize.Y / 2);
                    break;

                case UIVerticalAlignment.Bottom:
                    Properties.Position.Y = RenderBounds.Bottom - textSize.Y;
                    break;
            }
        }

        /// <summary>
        /// Gets or sets the horizontal alignment.
        /// </summary>
        public UIHorizonalAlignment HorizontalAlign
        {
            get => _hAlign;
            set
            {
                if(_hAlign != value)
                {
                    _hAlign = value;
                    OnUpdateBounds();
                }
            }
        }

        /// <summary>
        /// Gets or sets the vertical alignment.
        /// </summary>
        public UIVerticalAlignment VerticalAlign
        {
            get => _vAlign;
            set
            {
                if (_vAlign != value)
                {
                    _vAlign = value;
                    OnUpdateBounds();
                }
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="TextFont"/> of the current <see cref="UILabel"/>.
        /// </summary>
        public TextFont Font
        {
            get => Properties.Font;
            set
            {
                if(Properties.Font != value)
                {
                    Properties.Font = value;
                    OnUpdateBounds();
                }
            }
        }
    }
}
