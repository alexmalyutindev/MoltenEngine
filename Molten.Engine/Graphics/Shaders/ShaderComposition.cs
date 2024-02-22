﻿namespace Molten.Graphics;

public unsafe class ShaderComposition : GraphicsObject
{
    /// <summary>A list of const buffers the shader stage requires to be bound.</summary>
    public ShaderBindPoint<IConstantBuffer>[] ConstantBuffers = [];

    /// <summary>A list of resources that must be bound to the shader stage.</summary>
    public ShaderBindPoint<ShaderResourceVariable>[] Resources = [];

    /// <summary> A list of unordered-access resources that must be bound to the shader stage.</summary>
    public ShaderBindPoint<RWVariable>[] UavResources = [];

    /// <summary>A of samplers that must be bound to the shader stage.</summary>
    public ShaderBindPoint<ShaderSampler>[] Samplers = [];

    public ShaderIOLayout InputLayout;

    public ShaderIOLayout OutputLayout;

    public string EntryPoint { get; internal set; }

    public ShaderType Type { get; internal set; }

    void* _ptrShader;

    internal ShaderComposition(ShaderPass parentPass, ShaderType type) : 
        base(parentPass.Device)
    {
        Pass = parentPass;
        Type = type;
    }

    internal void AddBinding(ShaderSampler binding, uint bindPoint)
    {
        int index = Samplers.Length;
        EngineUtil.ArrayResize(ref Samplers, index + 1);
        Samplers[index] = new ShaderBindPoint<ShaderSampler>(bindPoint, 0, binding);
    }

    internal void AddBinding(ShaderResourceVariable binding, uint bindPoint)
    {
        int index = Resources.Length;
        EngineUtil.ArrayResize(ref Resources, index + 1);
        Resources[index] = new ShaderBindPoint<ShaderResourceVariable>(bindPoint, 0, binding);
    }

    internal void AddBinding(RWVariable binding, uint bindPoint)
    {
        int index = UavResources.Length;
        EngineUtil.ArrayResize(ref UavResources, index + 1);
        UavResources[index] = new ShaderBindPoint<RWVariable>(bindPoint, 0, binding);
    }

    internal void AddBinding(IConstantBuffer binding, uint bindPoint)
    {
        int index = ConstantBuffers.Length;
        EngineUtil.ArrayResize(ref ConstantBuffers, index + 1);
        ConstantBuffers[index] = new ShaderBindPoint<IConstantBuffer>(bindPoint, 0, binding);
    }

    protected override void OnGraphicsRelease() { }

    /// <summary>
    /// Gets a pointer to the native shader object or bytecode.
    /// </summary>
    public void* PtrShader
    {
        get => _ptrShader;
        internal set => _ptrShader = value;
    }

    /// <summary>
    /// Gets the parent <see cref="ShaderPass"/> that the current <see cref="ShaderComposition"/> belongs to.
    /// </summary>
    public ShaderPass Pass { get; }
}
