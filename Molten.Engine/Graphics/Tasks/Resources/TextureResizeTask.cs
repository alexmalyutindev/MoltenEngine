﻿namespace Molten.Graphics;

public class TextureResizeTask : GpuResourceTask<GpuTexture>
{
    public TextureDimensions NewDimensions;

    public GpuResourceFormat NewFormat;

    public override void ClearForPool()
    {
        NewDimensions = new TextureDimensions();
        NewFormat = GpuResourceFormat.Unknown;
    }

    public override bool Validate()
    {
        return true;
    }

    protected override bool OnProcess(RenderService renderer, GpuCommandList cmd)
    {
        Resource.ResizeTextureImmediate(cmd, NewDimensions, NewFormat);
        return true;
    }
}
