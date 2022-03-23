﻿using Silk.NET.DXGI;

namespace Molten.Graphics
{
    internal class SurfaceTracker : IDisposable
    {
        SurfaceSizeMode _mode;
        Dictionary<AntiAliasLevel, RenderSurface2D> _surfaces;
        RendererDX11 _renderer;

        uint _width;
        uint _height;
        Format _dxgiFormat;
        string _name;

        internal SurfaceTracker(
            RendererDX11 renderer,
            AntiAliasLevel[] aaLevels,
            uint width,
            uint height,
            Format dxgiFormat,
            string name,
            SurfaceSizeMode mode = SurfaceSizeMode.Full)
        {
            _width = width;
            _height = height;
            _dxgiFormat = dxgiFormat;
            _name = name;
            _mode = mode;
            _renderer = renderer;
            _surfaces = new Dictionary<AntiAliasLevel, RenderSurface2D>();
        }

        internal void RefreshSize(uint minWidth, uint minHeight)
        {
            _width = minWidth;
            _height = minHeight;

            switch (_mode)
            {
                case SurfaceSizeMode.Full:
                    foreach(RenderSurface2D rs in _surfaces.Values)
                        rs?.Resize(minWidth, minHeight);
                    break;

                case SurfaceSizeMode.Half:
                    foreach (RenderSurface2D rs in _surfaces.Values)
                        rs?.Resize((minWidth / 2) + 1, (minHeight / 2) + 1);
                    break;
            }
        }

        internal RenderSurface2D Create(AntiAliasLevel aa)
        {
            RenderSurface2D rs = new RenderSurface2D(
                _renderer, 
                _width, 
                _height, 
                _dxgiFormat, 
                name: $"{_name}_{aa}aa", 
                aaLevel: aa
            );

            _surfaces[aa] = rs;
            return rs;
        }

        public void Dispose()
        {
            foreach(RenderSurface2D rs in _surfaces.Values)
                rs.Dispose();
        }

        internal RenderSurface2D this[AntiAliasLevel aaLevel]
        {
            get
            {
                if (!_surfaces.TryGetValue(aaLevel, out RenderSurface2D rs))
                {
                    rs = Create(aaLevel);
                    _surfaces[aaLevel] = rs;
                }
                else if (rs.Width != _width || rs.Height != _height)
                {
                    rs.Resize(_width, _height);
                }

                return rs;
            }
        }
    }
}
