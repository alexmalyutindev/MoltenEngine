﻿using Silk.NET.Vulkan;

namespace Molten.Graphics.Vulkan
{
    internal class DepthSurfaceVK : Texture2DVK, IDepthStencilSurface
    {
        internal DepthSurfaceVK(DeviceVK device, uint width, uint height, uint mipCount, uint arraySize,
            AntiAliasLevel aaLevel, 
            MSAAQuality sampleQuality, 
            DepthFormat format, 
            GraphicsResourceFlags flags, 
            string name) : 
            base(device, GraphicsTextureType.Surface2D, width, height, mipCount, arraySize, aaLevel, sampleQuality, format.ToGraphicsFormat(), flags, name)
        {
            DepthFormat = format;
            Viewport = new ViewportF(0, 0, Width, Height);
        }

        public void Clear(GraphicsPriority priority, DepthClearFlags flags, float depthValue = 1.0f, byte stencilValue = 0)
        {
            Device.Renderer.PushTask(priority, this, new DepthClearTaskVK()
            {
                DepthValue = depthValue,
                StencilValue = stencilValue,
            });
        }

        public DepthFormat DepthFormat { get; }

        public ViewportF Viewport { get; }

        /// <summary>
        /// Gets surface clear color, if any.
        /// </summary>
        internal ClearDepthStencilValue? ClearValue { get; set; }
    }
}
