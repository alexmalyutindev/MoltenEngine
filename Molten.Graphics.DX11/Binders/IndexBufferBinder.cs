﻿using Silk.NET.DXGI;

namespace Molten.Graphics
{
    internal unsafe class IndexBufferBinder : GraphicsSlotBinder<IIndexBuffer>
    {
        public override void Bind(GraphicsSlot<IIndexBuffer> slot, IIndexBuffer value)
        {
            IndexBufferDX11 buffer = value as IndexBufferDX11;
            uint byteOffset = 0; // value.ByteOffset - May need again later for multi-part meshes.
            (slot.Cmd as CommandQueueDX11).Native->IASetIndexBuffer(buffer, buffer.D3DFormat, byteOffset);
        }

        public override void Unbind(GraphicsSlot<IIndexBuffer> slot, IIndexBuffer value)
        {
            (slot.Cmd as CommandQueueDX11).Native->IASetIndexBuffer(null, Format.FormatUnknown, 0);
        }
    }
}
