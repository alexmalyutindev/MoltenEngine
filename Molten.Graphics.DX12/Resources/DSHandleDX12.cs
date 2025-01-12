﻿using Silk.NET.Direct3D12;

namespace Molten.Graphics.DX12;
internal class DSHandleDX12 : ResourceHandleDX12
{
    internal unsafe DSHandleDX12(DepthSurfaceDX12 depthSurface, params ID3D12Resource1*[] resources) : 
        base(depthSurface, resources)
    {
        DSV = new DSViewDX12(this);
        ReadOnlyDSV = new DSViewDX12(this);
    }

    internal unsafe DSHandleDX12(TextureDX12 texture, ID3D12Resource1** resources, uint numResources) : 
        base(texture, resources, numResources)
    {
        DSV = new DSViewDX12(this);
        ReadOnlyDSV = new DSViewDX12(this);
    }

    protected override void OnGpuRelease()
    {
        DSV.Dispose();
        ReadOnlyDSV.Dispose();
        base.OnGpuRelease();
    }

    internal DSViewDX12 DSV { get; }

    internal DSViewDX12 ReadOnlyDSV { get; }
}
