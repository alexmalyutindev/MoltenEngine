﻿using Molten.Audio.OpenAL;
using Molten.Graphics;
using Molten.Graphics.Vulkan;
using Molten.Input;

namespace Molten.Examples;

internal class Program
{
    static ExampleBrowser<RendererVK, MacOSInputService, AudioServiceAL> _browser;
    
    static void Main(string[] args)
    {
        EngineSettings settings = new EngineSettings();
        settings.Graphics.EnableDebugLayer.Value = true;
        settings.Graphics.VSync.Value = true;
        settings.Graphics.FrameBufferMode.Value = FrameBufferMode.Double;

        _browser = new ExampleBrowser<RendererVK, MacOSInputService, AudioServiceAL>("Example Browser");
        _browser.Start(settings, true);
    }
}