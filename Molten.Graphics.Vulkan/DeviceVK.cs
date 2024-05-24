﻿using Molten.Collections;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.GLFW;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Queue = Silk.NET.Vulkan.Queue;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Molten.Graphics.Vulkan;

public unsafe class DeviceVK : GpuDevice
{
    public event DisplayOutputChanged OnOutputActivated;
    public event DisplayOutputChanged OnOutputDeactivated;

    DisplayManagerVK _manager;
    List<DisplayOutputVK> _outputs;
    List<DisplayOutputVK> _activeOutputs;
    ResourceManagerVK _resources;

    PhysicalDeviceMemoryProperties2 _memProperties;

    Instance* _vkInstance;
    RendererVK _renderer;
    List<CommandQueueVK> _queues;
    CommandQueueVK _mainQueue;
    DeviceLoaderVK _loader;
    MemoryManagerVK _memory;
    Device* _handle;

    int _swapChainCount;
    SwapchainKHR* _pSwapChains;
    uint* _pPresentIndices; // Back-buffer index for each presented swap-chain.
    Result* _pPresentResults;
    Fence* _pPresentFences;

    Stack<FenceVK> _freeFences;
    List<FenceVK> _fences;

    List<SwapChainSurfaceVK> _presentSurfaces;
    List<RenderPassVK> _renderPasses;
    List<FrameBufferVK> _frameBuffers;

    KhrSwapchain _extSwapChain;
    /// <summary>
    /// 
    /// </summary>
    /// <param name="renderer"></param>
    /// <param name="adapter"></param>
    /// <param name="instance"></param>
    internal DeviceVK(RendererVK renderer, DisplayManagerVK manager, PhysicalDevice pDevice, Instance* instance) :
        base(renderer, manager)
    {
        _freeFences = new Stack<FenceVK>();
        _fences = new List<FenceVK>();
        _presentSurfaces = new List<SwapChainSurfaceVK>();
        _renderPasses = new List<RenderPassVK>();

        _renderer = renderer;
        _vkInstance = instance;
        _manager = manager;
        Adapter = pDevice;
        _memory = new MemoryManagerVK(this);

        PhysicalDeviceProperties p;

        if (renderer.ApiVersion < new VersionVK(1, 1))
        {
            p = _manager.Renderer.VK.GetPhysicalDeviceProperties(Adapter);
        }
        else
        {
            PhysicalDeviceProperties2 p2 = new PhysicalDeviceProperties2(StructureType.PhysicalDeviceProperties2);
            _manager.Renderer.VK.GetPhysicalDeviceProperties2(Adapter, &p2);
            p = p2.Properties;

#if DEBUG
            _manager.CapBuilder.LogAdditionalProperties(_manager.Renderer.Log, &p2);
#endif
        }

        Name = SilkMarshal.PtrToString((nint)p.DeviceName, NativeStringEncoding.UTF8);
        ID = ParseDeviceID(p.DeviceID);
        Vendor = ParseVendorID(p.VendorID);
        Type = (GpuDeviceType)p.DeviceType;

        Capabilities = _manager.CapBuilder.Build(this, _manager.Renderer, ref p);

        _outputs = new List<DisplayOutputVK>();
        _activeOutputs = new List<DisplayOutputVK>();
        Outputs = _outputs.AsReadOnly();
        ActiveOutputs = _activeOutputs.AsReadOnly();
    }

    public override GpuCommandList GetCommandList(GpuCommandListFlags flags = GpuCommandListFlags.None)
    {
        return _mainQueue.Allocate(flags);
    }

    public override GpuFormatSupportFlags GetFormatSupport(GpuResourceFormat format)
    {
        throw new NotImplementedException();
    }

    internal RenderPassVK GetRenderPass(IRenderSurfaceVK[] surfaces, DepthSurfaceVK depthSurface)
    {
        foreach(RenderPassVK pass in _renderPasses)
        {
            if (pass.DoSurfacesMatch(this, surfaces, depthSurface))
                return pass;
        }

        RenderPassVK newPass = new RenderPassVK(this, surfaces, depthSurface);
        _renderPasses.Add(newPass);
        return newPass;
    }

    internal FrameBufferVK GetFrameBuffer(RenderPassVK renderPass, IRenderSurfaceVK[] surfaces, DepthSurfaceVK depthSurface)
    {
        foreach (FrameBufferVK fb in _frameBuffers)
        {
            if (fb.DoSurfacesMatch(this, renderPass, surfaces, depthSurface))
                return fb;
        }

        FrameBufferVK newFrameBuffer = new FrameBufferVK(this, renderPass, surfaces, depthSurface);
        _frameBuffers.Add(newFrameBuffer);
        return newFrameBuffer;
    }

    internal bool HasExtension(string extName)
    {
        return _loader.HasExtension(extName);
    }

    internal void PreInitialize(CommandSetCapabilityFlags requiredCmdFlags)
    {
        _handle = EngineUtil.Alloc<Device>();
        _queues = new List<CommandQueueVK>();
        _loader = new DeviceLoaderVK(_renderer, this, requiredCmdFlags);
    }

    private DeviceVendor ParseVendorID(uint vendorID)
    {
        // From docs: If the vendor has a PCI vendor ID, the low 16 bits of vendorID must contain that PCI vendor ID, and the remaining bits must be set to zero. 
        if ((vendorID & 0xFFFF0000) == 0)
            return EngineUtil.VendorFromPCI(vendorID & 0x0000FFFF); // PCI vendor ID
        else
            return (DeviceVendor)(vendorID & 0xFFFF0000); // Vulkan Vendor ID
    }

    private DeviceID ParseDeviceID(uint deviceID)
    {
        // Docs: The vendor is also responsible for the value returned in deviceID. If the implementation is driven primarily by a PCI device with a PCI device ID,
        //      the low 16 bits of deviceID must contain that PCI device ID, and the remaining bits must be set to zero. 
        if ((deviceID & 0xFFFF0000) == 0)
            return new DeviceID((deviceID & 0x0000FFFF)); // PCI device ID.
        else
            return new DeviceID(deviceID); // OS/Platform-based device ID.
    }

    internal bool AssociateOutput(DisplayOutputVK output)
    {
        if (output.AssociatedDevice == this)
            return true;

        output.AssociatedDevice?.UnassociateOutput(output);

        _outputs.Add(output);
        output.AssociatedDevice = this;

        return false;
    }

    internal void UnassociateOutput(DisplayOutputVK output)
    {
        if (output.AssociatedDevice != this)
            return;

        int index = _activeOutputs.IndexOf(output);
        if (index > -1)
            _activeOutputs.RemoveAt(index);

        _outputs.Remove(output);
        output.AssociatedDevice = null;
    }

    /// <inheritdoc/>
    public override void AddActiveOutput(IDisplayOutput output)
    {
        if (output is DisplayOutputVK vkOutput)
        {
            if (vkOutput.AssociatedDevice != this)
                return;

            if (!_activeOutputs.Contains(vkOutput))
                _activeOutputs.Add(vkOutput);
        }
    }

    /// <inheritdoc/>
    public override void RemoveActiveOutput(IDisplayOutput output)
    {
        if (output is DisplayOutputVK vkOutput)
        {
            if (vkOutput.AssociatedDevice != this)
                return;

            _activeOutputs.Remove(vkOutput);
        }
    }

    /// <inheritdoc/>
    public override void RemoveAllActiveOutputs()
    {
        throw new NotImplementedException();
    }

    internal void AddExtension<E>(Action<E> loadCallback = null, Action<E> destroyCallback = null)
        where E : NativeExtension<Vk>
    {
        _loader.AddExtension(loadCallback, destroyCallback);
    }

    internal E GetExtension<E>()
        where E : NativeExtension<Vk>
    {
        return _loader.GetExtension<E>();
    }

    protected override bool OnInitialize()
    {
        if (_loader.Build(_renderer.ApiVersion, Ptr))
        {
            for (int i = 0; i < _loader.QueueCount; i++)
            {
                ref DeviceQueueCreateInfo qi = ref _loader.QueueInfo[i];

                for (uint index = 0; index < qi.QueueCount; index++)
                {
                    Queue q = new Queue();
                    _renderer.VK.GetDeviceQueue(*Ptr, qi.QueueFamilyIndex, index, &q);
                    SupportedCommandSet set = Capabilities.CommandSets[(int)qi.QueueFamilyIndex];
                    CommandQueueVK queue = new CommandQueueVK(_renderer, this, qi.QueueFamilyIndex, q, index, set);
                    _queues.Add(queue);

                    // TODO maybe find the best queue, rather than first match?
                    if (_mainQueue == null && queue.HasFlags(CommandSetCapabilityFlags.Graphics))
                        _mainQueue = queue;

                    _renderer.Log.WriteLine($"Instantiated command queue -- Family: {qi.QueueFamilyIndex} -- Index: {index} -- Flags: {set.CapabilityFlags}");
                }
            }

            _extSwapChain = GetExtension<KhrSwapchain>();

            _resources = new ResourceManagerVK(this);

            return true;
        }

        return false;
    }

    /// <summary>
    /// Finds a <see cref="CommandQueueVK"/> that can present the provided <see cref="WindowSurfaceVK"/>.
    /// </summary>
    /// <param name="surface"></param>
    /// <returns></returns>
    internal CommandQueueVK FindPresentQueue(SwapChainSurfaceVK surface)
    {
        KhrSurface extSurface = _renderer.GetInstanceExtension<KhrSurface>();
        Bool32 presentSupported = false;

        foreach (CommandQueueVK queue in _queues)
        {
            Result r = extSurface.GetPhysicalDeviceSurfaceSupport(Adapter, queue.FamilyIndex, surface.SurfaceHandle, &presentSupported);
            if (r.Check(_renderer) && presentSupported)
                return queue;
        }

        return null;
    }

    /// <summary>
    /// Retrieves the <see cref="SharingMode"/> for a resource, based on <see cref="CommandQueueVK"/> queues that may potentionally access it.
    /// </summary>
    /// <param name="expectedQueues">The <see cref="CommandQueueVK"/> queues that are expected to share.</param>
    /// <returns></returns>
    internal (SharingMode, CommandQueueVK[]) GetSharingMode(params CommandQueueVK[] expectedQueues)
    {
        HashSet<CommandQueueVK> set = new HashSet<CommandQueueVK>();
        for (int i = 0; i < expectedQueues.Length; i++)
            set.Add(expectedQueues[i]);

        if (set.Count <= 1)
            return (SharingMode.Exclusive, set.ToArray());
        else
            return (SharingMode.Concurrent, set.ToArray());
    }

    protected override void OnDispose(bool immediate)
    {
        // Dispose of fences
        for (int i = 0; i < _fences.Count; i++)
            _fences[i].Dispose();

        _fences.Clear();
        while(_freeFences.Count > 0)
            _freeFences.Pop().Dispose();   

        _loader?.Dispose();
        if(Ptr != null)
            _renderer.VK.DestroyDevice(*Ptr, null);

        EngineUtil.Free(ref _handle);

        base.OnDispose(immediate);
    }

    internal FenceVK GetFence()
    {
        FenceVK fence = null;

        if (_freeFences.Count > 0)
        {
            fence = _freeFences.Pop();
            fence.Reset();
        }
        else
        {
            fence = new FenceVK(this, FenceCreateFlags.None);
            _fences.Add(fence);
        }

        return fence;
    }

    internal void FreeFence(FenceVK fence)
    {
        fence.Reset();
        _freeFences.Push(fence);
    }

    public override void Reset(GpuCommandList list)
    {
        throw new NotImplementedException();
    }

    public override bool Wait(GpuFence fence, ulong nsTimeout = ulong.MaxValue)
    {
        FenceVK fenceVK = fence as FenceVK;
        return fenceVK.Wait(nsTimeout);
    }

    public override void Execute(GpuCommandList cmd)
    {
        _mainQueue.Execute(cmd);
    }

    protected override void OnBeginFrame(GpuCommandList cmd, IReadOnlyThreadedList<ISwapChainSurface> surfaces)
    {
        // Collect all enabled swapchain surfaces.
        _presentSurfaces.Clear();

        CommandListVK vkCmd = cmd as CommandListVK;
        surfaces.For(0, (index, surface) =>
        {
            if (!surface.IsEnabled)
                return;

            SwapChainSurfaceVK vkSurface = surface as SwapChainSurfaceVK;
            if (vkSurface.SurfaceHandle.Handle == 0)
                vkSurface.Prepare(vkCmd, 0);

            _presentSurfaces.Add(vkSurface);
        });

        if (_presentSurfaces.Count == 0) // TODO Remove this - What if we're rendering directly to a texture that may be saved to file or streamed?
            return;

        // Check if our unsafe arrays need resizing.
        if (_swapChainCount != _presentSurfaces.Count)
        {
            EngineUtil.Free(ref _pSwapChains);
            EngineUtil.Free(ref _pPresentIndices);
            EngineUtil.Free(ref _pPresentFences);
            EngineUtil.Free(ref _pPresentResults);

            _swapChainCount = _presentSurfaces.Count;
            _pSwapChains = EngineUtil.AllocArray<SwapchainKHR>((nuint)_swapChainCount);
            _pPresentIndices = EngineUtil.AllocArray<uint>((nuint)_swapChainCount);
            _pPresentFences = EngineUtil.AllocArray<Fence>((nuint)_swapChainCount);
            _pPresentResults = EngineUtil.AllocArray<Result>((nuint)_swapChainCount);
        }

        Semaphore dummySemaphore = new Semaphore();
        Result r = Result.Success;

        for (int i = 0; i < _swapChainCount; i++)
        {
            SwapChainSurfaceVK vkSurface = _presentSurfaces[i];

            r = _extSwapChain.AcquireNextImage(this, vkSurface.SwapchainHandle, ulong.MaxValue, dummySemaphore, vkSurface.FrameFence, &_pPresentIndices[i]);
            if (!r.Check(this, () => "Failed to acquire next swapchain image"))
                return;

            _pPresentFences[i] = vkSurface.FrameFence;
        }

        // Wait for all swapchains to become available before proceeding to present again.
        r = FenceVK.WaitAll(this, _pPresentFences, _swapChainCount, ulong.MaxValue);
        if (!r.Check(this, () => "Failed to wait for all swapchains to become available."))
            return;

        r = FenceVK.ResetAll(this, _pPresentFences, _swapChainCount);
        if (!r.Check(this, () => "Failed to reset all swapchains."))
            return;
    }

    protected override void OnEndFrame(GpuCommandList cmd, IReadOnlyThreadedList<ISwapChainSurface> surfaces)
    {
        // Get the last semaphore of the last command list to be executed.
        CommandListVK vkCmd = cmd as CommandListVK;
        Semaphore* semaphores = stackalloc Semaphore[] {vkCmd.Semaphore.Handle};

        // Prepare presentable surfaces
        for (int i = 0; i < _swapChainCount; i++)
        {
            SwapChainSurfaceVK vkSurface = _presentSurfaces[i];
            vkSurface.Prepare(vkCmd, _pPresentIndices[i]);
            _pSwapChains[i] = vkSurface.SwapchainHandle;
        }

        // Presentation should wait on the last command list to be executed.
        PresentInfoKHR presentInfoKHR = new PresentInfoKHR()
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = semaphores,
            SwapchainCount = (uint)_swapChainCount,
            PSwapchains = _pSwapChains,
            PImageIndices = _pPresentIndices,
            PResults = _pPresentResults,
        };

        Queue cmdQueue = _mainQueue.Handle;
        Result r = _extSwapChain.QueuePresent(cmdQueue, &presentInfoKHR);
        if (!r.Check(this, () => $"Failed to present {_swapChainCount} swapchains:"))
        {
            for (int i = 0; i < _swapChainCount; i++)
                _pPresentResults[i].Check(this, () => $"   Swapchain {i} - {surfaces[i].Name}");
        }
    }

    protected override void OnPresent(IReadOnlyThreadedList<ISwapChainSurface> surfaces)
    {
        throw new NotImplementedException();
    }

    public static implicit operator PhysicalDevice(DeviceVK device)
    {
        return device.Adapter;
    }

    public static implicit operator Device(DeviceVK device)
    {
        return *device._handle;
    }

    /// <inheritdoc/>
    public override DeviceID ID { get; }

    /// <inheritdoc/>
    public override DeviceVendor Vendor { get; }

    /// <inheritdoc/>
    public override GpuDeviceType Type { get; }

    /// <inheritdoc/>
    public override IReadOnlyList<IDisplayOutput> Outputs { get; }

    /// <inheritdoc/>
    public override IReadOnlyList<IDisplayOutput> ActiveOutputs { get; }

    internal PhysicalDevice Adapter { get; private set; }

    internal ref PhysicalDeviceMemoryProperties2 MemoryProperties => ref _memProperties;

    /// <summary>
    /// Gets the <see cref="Instance"/> that the current <see cref="DeviceVK"/> is bound to.
    /// </summary>
    internal Instance* Instance => _vkInstance;

    /// <summary>
    /// The underlying, native device pointer.
    /// </summary>
    internal Device* Ptr => _handle;

    /// <summary>
    /// Gets a protected reference to the underlying device pointer.
    /// </summary>
    protected ref Device* PtrRef => ref _handle;

    internal Vk VK => _renderer.VK;

    internal Glfw GLFW => _renderer.GLFW;

    internal MemoryManagerVK Memory => _memory;

    public override ShaderLayoutCache LayoutCache => throw new NotImplementedException();

    public override ResourceManagerVK Resources => _resources;

    internal CommandQueueVK MainQueue => _mainQueue;
}
