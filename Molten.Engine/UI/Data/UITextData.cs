﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Molten.Graphics;

namespace Molten.UI
{
    public struct UITextData : IUIRenderData
    {
        [DataMember]
        public Color Color;

        [DataMember]
        public string Text;

        public SpriteFont Font;

        [DataMember]
        public Vector2F Position;

        public IMaterial Material;

        public void Render(SpriteBatcher sb, UIRenderData data)
        {
            if (Font != null && Color.A > 0)
                sb.DrawString(Font, 16, Text, Position, Color, Material);
        }
    }
}
