﻿using System.Runtime.CompilerServices;

namespace Molten.Graphics
{
    /// <summary>
    /// A base class for sprite batcher implementations.
    /// </summary>
    public abstract partial class SpriteBatcher : IDisposable
    {
        static Vector2F DEFAULT_ORIGIN_CENTER = new Vector2F(0.5f);

        protected enum SpriteFormat
        {
            Sprite = 0, // Textured or untextured (rectangle) sprites

            MSDF = 1, // Multi-channel signed-distance field.

            Line = 2, // Untextured lines

            Triangle = 3, // Untextured triangles

            Ellipse = 4, // Untextured circles - Uses a geometry shader to handle this
        }

        protected struct SpriteItem
        {
            public SpriteGpuData Vertex;
            public SpriteFormat Format;
            public ITexture2D Texture;
            public IMaterial Material;
            public int ClipID;
            public bool IsOutline;
        }

        protected Rectangle[] Clips;
        protected SpriteItem[] Sprites;
        protected uint NextID;

        Color[] _singleColorList;
        int _curClipID;

        public SpriteBatcher(uint initialCapacity)
        {
            Sprites = new SpriteItem[initialCapacity];
            Clips = new Rectangle[256];
            _singleColorList = new Color[1];
        }

        protected ref SpriteItem GetItem()
        {
            if (NextID == Sprites.Length) // Increase length by 50%
                Array.Resize(ref Sprites, Sprites.Length + (Sprites.Length / 2));

            Sprites[NextID].ClipID = _curClipID;
            return ref Sprites[NextID++];
        }

        /// <summary>
        /// Pushes a new clipping <see cref="Rectangle"/> into the current <see cref="SpriteBatcher"/>.
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns>Returns true if the clip was valid. 
        /// False will be returned if <paramref name="bounds"/> is invalid, or if the clip is outside of a previously-pushed clip.</returns>
        public bool PushClip(Rectangle bounds)
        {
            if (_curClipID == Clips.Length)
                Array.Resize(ref Clips, Clips.Length * 2);

            // Cull the bounds to the current clip, if any.
            if (_curClipID > 0)
            {
                Rectangle cur = Clips[_curClipID];

                bounds.X = MathHelper.Clamp(bounds.X, cur.X, cur.Right);
                bounds.Y = MathHelper.Clamp(bounds.Y, cur.Y, cur.Bottom);
                bounds.Right = MathHelper.Clamp(bounds.Right, cur.X, cur.Right);
                bounds.Bottom = MathHelper.Clamp(bounds.Bottom, cur.Y, cur.Bottom);
            }

            if (bounds.Width > 0 && bounds.Height > 0)
            {
                Clips[++_curClipID] = bounds;
                return true;
            }

            return false;
        }

        public void PopClip()
        {
            if (_curClipID == 0)
                throw new Exception("There are no clips available to pop");

            _curClipID--;
        }

        /// <summary>Draws a string of text sprites by using a <see cref="SpriteFont"/> to source the needed data.</summary>
        /// <param name="font">The spritefont from which to retrieve font data.</param>
        /// <param name="text">The text to draw.</param>
        /// <param name="position">The position of the text.</param>
        /// <param name="color">The color of the text.</param>
        /// <param name="material">The material to use when rendering the string of text.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawString(TextFont font, string text, Vector2F position, Color color, IMaterial material = null)
        {
            DrawString(font, text, position, color, Vector2F.One, material);
        }

        /// <summary>Draws a string of text sprites by using a <see cref="SpriteFont"/> to source the needed data..</summary>
        /// <param name="font">The spritefont from which to retrieve font data.</param>
        /// <param name="text">The text to draw.</param>
        /// <param name="position">The position of the text.</param>
        /// <param name="color">The color of the text.</param>
        /// <param name="scale">The text scale. 1.0f is equivilent to the default size. 0.5f will half the size. 2.0f will double the size.</param>
        /// <param name="material">The material to use when rendering the string of text.</param>
        public void DrawString(TextFont font, string text, Vector2F position, Color color, Vector2F scale, IMaterial material = null)
        {
            if (string.IsNullOrEmpty(text))
                return;

            // Cycle through all characters in the string and process them
            Vector2F charPos = position;
            for (int i = 0; i < text.Length; i++)
            {
                TextFontSource.CachedGlyph cache = font.Source.GetCharGlyph(text[i]);

                ref SpriteItem item = ref GetItem();
                item.Texture = font.Source.UnderlyingTexture;
                item.Material = material;
                item.Format = SpriteFormat.MSDF;

                item.Vertex.Position = new Vector2F(charPos.X, charPos.Y + ((cache.YOffset * font.Scale) * scale.Y));
                item.Vertex.Rotation = 0; // TODO 2D text rotation.
                item.Vertex.ArraySlice = 0; // TODO SpriteFont array slice support.
                item.Vertex.Size = (new Vector2F(cache.Location.Width, cache.Location.Height) * font.Scale) * scale;
                item.Vertex.UV = new Vector4F(cache.Location.Left, cache.Location.Top, cache.Location.Right, cache.Location.Bottom);
                item.Vertex.Color = color;
                item.Vertex.Origin = Vector2F.Zero;

                // Increase pos by size of char (along X)
                charPos.X += (cache.AdvanceWidth * font.Scale) * scale.X;
            }
        }

        /// <summary>Draws a triangle using 3 provided points.</summary>
        /// <param name="p1">The first point.</param>
        /// <param name="p2">The second point.</param>
        /// <param name="p3">The third point.</param>
        /// <param name="color">The color of the triangle.</param>
        public void DrawTriangle(Vector2F p1, Vector2F p2, Vector2F p3, Color color)
        {
            ref SpriteItem item = ref GetItem();
            item.Texture = null;
            item.Material = null;
            item.Format = SpriteFormat.Triangle;

            item.Vertex.Position = p1;
            item.Vertex.Rotation = 0; // TODO triangle rotation.
            item.Vertex.ArraySlice = 0;
            item.Vertex.Size = p2;
            item.Vertex.UV = Vector4F.Zero; // Unused
            item.Vertex.Color = color;
            item.Vertex.Origin = p3;
        }

        /// <summary>
        /// Draws a polygon from a list of points. The point list is expected to be in triangle-list format. 
        /// This means every 3 points should form a triangle. The polygon should be made up of several triangles.
        /// </summary>
        /// <param name="points">A list of points that form the polygon. A minimum of 3 points is expected.</param>
        /// <param name="triColors">A list of colors. One color per triangle. A minimum of 1 color is expected. 
        /// Insufficient colors for the provided triangles will cause the colors to be repeated.</param>
        public void DrawTriangleList(IList<Vector2F> points, IList<Color> triColors)
        {
            if (points.Count % 3 > 0)
                throw new SpriteBatcherException(this, "Incorrect number of points for triangle list. There should be 3 points per triangle");

            if (triColors.Count == 0)
                throw new SpriteBatcherException(this, "There must be at least one color available in the triColors list.");

            for (int i = 0; i < points.Count; i += 3)
            {
                int colID = i / 3;

                ref SpriteItem item = ref GetItem();
                item.Texture = null;
                item.Material = null;
                item.Format = SpriteFormat.Triangle;

                item.Vertex.Position = points[i];
                item.Vertex.Rotation = 0;
                item.Vertex.ArraySlice = 0;
                item.Vertex.Size = points[i + 1];
                item.Vertex.UV = Vector4F.Zero; // Unused
                item.Vertex.Color = triColors[colID % triColors.Count];
                item.Vertex.Origin = points[i + 2];
            }
        }

        /// <summary>
        /// Draws a polygon from a list of points. The point list is expected to be in triangle-list format. 
        /// This means every 3 points should form a triangle. The polygon should be made up of several triangles.
        /// </summary>
        /// <param name="points">A list of points that form the polygon.</param>
        /// <param name="color">The color of the polygon.</param>
        public void DrawTriangleList(IList<Vector2F> points, Color color)
        {
            _singleColorList[0] = color;
            DrawTriangleList(points, _singleColorList);
        }

        /// <summary>Draws connecting lines between each of the provided points.</summary>
        /// <param name="points">The points between which to draw lines.</param>
        /// <param name="color">The color of the lines</param>
        /// <param name="thickness">The thickness of the line in pixels.</param>
        public void DrawLinePath(IList<Vector2F> points, Color color, float thickness)
        {
            _singleColorList[0] = color;
            DrawLinePath(points, 0, points.Count, _singleColorList, thickness);
        }

        /// <summary>Draws connecting lines between each of the provided points.</summary>
        /// <param name="points">The points between which to draw lines.</param>
        /// <param name="pointColors">A list of colors (one per point) that lines should transition to/from at each point.</param>
        /// <param name="thickness">The thickness of the line in pixels.</param>
        public void DrawLinePath(IList<Vector2F> points, IList<Color> pointColors, float thickness)
        {
            DrawLinePath(points, 0, points.Count, pointColors, thickness);
        }

        /// <summary>Draws connecting lines between each of the provided points.</summary>
        /// <param name="points">The points between which to draw lines.</param>
        /// <param name="Color">The color of all the lines in the path.</param>
        /// <param name="thickness">The thickness of the line in pixels.</param>
        /// <param name="startIndex">The start index within the points list from which to start drawing.</param>
        /// <param name="count">The number of points from the point list to draw.</param>
        public void DrawLinePath(IList<Vector2F> points, int startIndex, int count, Color color, float thickness)
        {
            _singleColorList[0] = color;
            DrawLinePath(points, startIndex, count, _singleColorList, thickness);
        }

        /// <summary>Draws connecting lines between each of the provided points.</summary>
        /// <param name="points">The points between which to draw lines.</param>
        /// <param name="pointColors">A list of colors (one per point) that lines should transition to/from at each point.</param>
        /// <param name="thickness">The thickness of the line in pixels.</param>
        /// <param name="startIndex">The start index within the points list from which to start drawing.</param>
        /// <param name="count">The number of points from the point list to draw.</param>
        public void DrawLinePath(IList<Vector2F> points, int startIndex, int count, IList<Color> pointColors, float thickness)
        {
            /*if (pointColors.Count == 0)
                throw new SpriteBatcherException(this, "There must be at least one color available in the pointColors list.");

            if (startIndex + count > points.Count)
                throw new SpriteBatcherException(this, "The sum of the start index and the count must be less than the point count.");

            if (count < 2)
                throw new SpriteBatcherException(this, "There must be at least 2 points in the point list.");

            if (count == 2)
            {
                int secondCol = pointColors.Count > 1 ? 1 : 0;
                DrawLine(points[0], points[1], pointColors[0], pointColors[secondCol], thickness);
            }
            else
            {
                int lineCount = points.Count - 1;

                Vector2F p1, p2;
                int last = startIndex + count - 1;
                int prev = 0;
                int next = 1;
                Color lastCol = pointColors[pointColors.Count - 1];
                Color4 lastCol4 = lastCol.ToColor4();
                uint spriteID = NextID;

                for (int i = startIndex; i < last; i++)
                {
                    p1 = points[i];
                    p2 = points[next];

                    ref SpriteItem item = ref GetItem();
                    item.Texture = null;
                    item.Material = null;
                    item.Format = SpriteFormat.Line;

                    item.Vertex.Position = p1;
                    item.Vertex.Rotation = thickness;
                    item.Vertex.ArraySlice = 0;
                    item.Vertex.Size = p2;
                    item.Vertex.UV = pointColors[next % pointColors.Count].ToVector4();
                    item.Vertex.Color = pointColors[i % pointColors.Count];

                    // Provide the previous line with the direction of the current line.
                    if (prev < i)
                        Sprites[spriteID - 1].Vertex.Origin = p2 - p1;

                    if (next + 1 == last) // If there is no line after the current, use the current line's direction to fill the tangent calculation.
                        item.Vertex.Origin = p2 - p1;

                    spriteID++;
                    next++;
                    prev = i;
                }
            }*/
        }

        /// <summary>
        /// Draws a line between two points.
        /// </summary>
        /// <param name="p1">The first point.</param>
        /// <param name="p2">The second point.</param>
        /// <param name="color">The color of the line.</param>
        /// <param name="thickness">The thickness of the line in pixels.</param>
        public void DrawLine(Vector2F p1, Vector2F p2, Color color, float thickness)
        {
            DrawLine(p1, p2, color, color, thickness);
        }

        /// <summary>
        /// Draws a line between two points with a color gradient produced with the two provided colors.
        /// </summary>
        /// <param name="p1">The first point.</param>
        /// <param name="p2">The second point.</param>
        /// <param name="color1">The color for pos1.</param>
        /// <param name="color2">The color for pos2.</param>
        /// <param name="thickness">The thickness of the line in pixels.</param>
        public void DrawLine(Vector2F p1, Vector2F p2, Color color1, Color color2, float thickness)
        {
            const float PADDING = 2f; // Extra quad thickness to allow for smooth/faded line edges.

            ref SpriteItem item = ref GetItem();
            item.Texture = null;
            item.Material = null;
            item.Format = SpriteFormat.Line;

            float dist = Vector2F.Distance(ref p1, ref p2);
            Vector2F dir = Vector2F.Normalize(p2 - p1);

            Vector2F size = new Vector2F(dist, thickness + PADDING);
            Vector2F pos = (p2 + p1) / 2; // The center of the line will be the mean position of both points.

            item.Vertex.Position = pos;
            item.Vertex.Rotation = (float)Math.Atan2(dir.Y, dir.X);
            item.Vertex.ArraySlice = 0;
            item.Vertex.Size = size;
            item.Vertex.UV = Vector4F.Zero;
            item.Vertex.Color2 = color2;
            item.Vertex.Color = color1;
            item.Vertex.Data.BorderThickness = new Vector2F(thickness) / size; // Convert to UV coordinate system (0 - 1) range
            item.Vertex.Origin = DEFAULT_ORIGIN_CENTER;
        }

        /// <summary>Adds a sprite to the batch.</summary>
        /// <param name="texture"></param>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        /// <param name="color"></param>
        /// <param name="material"></param>
        /// <param name="arraySlice"></param>
        public void Draw(RectangleF source, RectangleF destination, ref SpriteStyle style, ITexture2D texture = null, IMaterial material = null, uint arraySlice = 0)
        {
            DrawInternal(texture,
                source,
                destination.TopLeft,
                destination.Size,
                ref style,
                0,
                Vector2F.Zero,
                material,
                SpriteFormat.Sprite,
                arraySlice, 
                false);
        }

        /// <summary>Adds a sprite to the batch.</summary>
        /// <param name="texture"></param>
        /// <param name="destination"></param>
        /// <param name="color"></param>
        /// <param name="material"></param>
        public void Draw(RectangleF destination, ref SpriteStyle style, ITexture2D texture = null, IMaterial material = null, uint arraySlice = 0)
        {
            RectangleF src = texture != null ? new RectangleF(0, 0, texture.Width, texture.Height) : RectangleF.Empty;
            DrawInternal(texture, src, destination.TopLeft, destination.Size, ref style, 0, Vector2F.Zero, material, SpriteFormat.Sprite, arraySlice, false);
        }

        /// <summary>Adds a sprite to the batch.</summary>
        /// <param name="texture"></param>
        /// <param name="destination"></param>
        /// <param name="color"></param>
        /// <param name="rotation"></param>
        /// <param name="origin"></param>
        public void Draw(RectangleF destination, ref SpriteStyle style, float rotation, Vector2F origin, ITexture2D texture = null, IMaterial material = null, uint arraySlice = 0)
        {
            RectangleF src = texture != null ? new RectangleF(0, 0, texture.Width, texture.Height) : RectangleF.Empty;
            DrawInternal(texture, src, destination.TopLeft, destination.Size, ref style, rotation, origin, material, SpriteFormat.Sprite, arraySlice, false);
        }

        /// <summary>Adds a sprite to the batch.</summary>
        /// <param name="texture"></param>
        /// <param name="position"></param>
        /// <param name="color"></param>
        public void Draw(Vector2F position, ref SpriteStyle style, ITexture2D texture = null, IMaterial material = null, uint arraySlice = 0)
        {
            RectangleF src = texture != null ? new RectangleF(0, 0, texture.Width, texture.Height) : RectangleF.Empty;
            DrawInternal(texture, src, position, new Vector2F(src.Width, src.Height), ref style, 0, Vector2F.Zero, material, SpriteFormat.Sprite, arraySlice, false);
        }

        public void Draw(Sprite sprite)
        {
            DrawInternal(sprite.Data.Texture,
                sprite.Data.Source,
                sprite.Position,
                sprite.Data.Source.Size * sprite.Scale,
                ref sprite.Style,
                sprite.Rotation,
                sprite.Origin,
                sprite.Material,
                SpriteFormat.Sprite,
                sprite.Data.ArraySlice,
                false);
        }

        /// <summary>Adds a sprite to the batch.</summary>
        /// <param name="texture"></param>
        /// <param name="position"></param>
        /// <param name="style"></param>
        /// <param name="rotation">Rotation in radians.</param>
        /// <param name="origin">The origin, as a unit value. 1.0f will set the origin to the bottom-right corner of the sprite.
        /// 0.0f will set the origin to the top-left. The origin acts as the center of the sprite.</param>
        /// <param name="material">The material to use when rendering the sprite.</param>
        /// <param name="arraySlice">The texture array slice containing the source texture.</param>
        public void Draw(Vector2F position, ref SpriteStyle style, float rotation, Vector2F origin, ITexture2D texture, IMaterial material = null, float arraySlice = 0)
        {
            RectangleF src = texture != null ? new RectangleF(0, 0, texture.Width, texture.Height) : RectangleF.Empty;
            DrawInternal(texture, src, position, new Vector2F(src.Width, src.Height), ref style, rotation, origin, material, SpriteFormat.Sprite, arraySlice, false);
        }

        /// <summary>
        /// Adds a sprite to the batch using 2D coordinates.
        /// </summary>>
        /// <param name="texture"></param>
        /// <param name="source"></param>
        /// <param name="position"></param>
        /// <param name="size">The width and height of the sprite..</param>
        /// <param name="color"></param>
        /// <param name="rotation">Rotation in radians.</param>
        /// <param name="origin">The origin, as a unit value. 1.0f will set the origin to the bottom-right corner of the sprite.
        /// 0.0f will set the origin to the top-left. The origin acts as the center of the sprite.</param>
        /// <param name="material">The material to use when rendering the sprite.</param>
        /// <param name="arraySlice">The texture array slice containing the source texture.</param>
        public void Draw(ITexture2D texture,
            RectangleF source,
            Vector2F position,
            Vector2F size,
            ref SpriteStyle style,
            float rotation,
            Vector2F origin,
            IMaterial material,
            float arraySlice)
        {
            DrawInternal(texture, source, position, size, ref style, rotation, origin, material, SpriteFormat.Sprite, arraySlice, false);
        }

        /// <summary>
        /// Adds a sprite to the batch using 2D coordinates.
        /// </summary>>
        /// <param name="texture"></param>
        /// <param name="source"></param>
        /// <param name="position"></param>
        /// <param name="size">The width and height of the sprite..</param>
        /// <param name="color"></param>
        /// <param name="rotation">Rotation in radians.</param>
        /// <param name="origin">The origin, as a unit value. 1.0f will set the origin to the bottom-right corner of the sprite.
        /// 0.0f will set the origin to the top-left. The origin acts as the center of the sprite.</param>
        /// <param name="material">The material to use when rendering the sprite.</param>
        /// <param name="arraySlice">The texture array slice containing the source texture.</param>
        protected unsafe ref SpriteItem DrawInternal(ITexture2D texture,
            RectangleF source,
            Vector2F position,
            Vector2F size,
            ref SpriteStyle style,
            float rotation,
            Vector2F origin,
            IMaterial material,
            SpriteFormat format,
            float arraySlice,
            bool outline)
        {
            ref SpriteItem item = ref GetItem();
            item.Texture = texture;
            item.Material = material;
            item.Format = format;
            item.IsOutline = outline;

            item.Vertex.Position = position;
            item.Vertex.Rotation = rotation;
            item.Vertex.ArraySlice = arraySlice;
            item.Vertex.Size = size;
            item.Vertex.Color = style.Color;
            item.Vertex.Color2 = style.Color2;
            item.Vertex.Origin = origin;
            item.Vertex.UV = *(Vector4F*)&source; // Source rectangle values are stored in the same layout as we need for UV: left, top, right, bottom.
            item.Vertex.Data.BorderThickness = new Vector2F(style.Thickness) / size; // Convert to UV coordinate system (0 - 1) range

            return ref item;
        }

        public abstract void Dispose();
    }
}
