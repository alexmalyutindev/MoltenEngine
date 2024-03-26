using Molten.Graphics;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace Molten.Input;

public class MacOSInputService : InputService
{
    public override IClipboard Clipboard { get; }
    public override IInputNavigation Navigation { get; }

    private INativeSurface _surface;
    private IntPtr _windowHandle;
    private IInputContext _inputContext;

    protected override void OnInitialize(EngineSettings settings)
    {
        base.OnInitialize(settings);
        // TODO: 
        // _inputContext = Window.GetView().CreateInput();
    }

    protected override void OnStart(EngineSettings settings)
    {
        base.OnStart(settings);
    }

    protected override void OnStop(EngineSettings settings)
    {
        base.OnStop(settings);
    }

    protected override void OnBindSurface(INativeSurface surface)
    {
        if (_surface != surface)
        {
            if (_surface != null)
            {
                _surface.OnHandleChanged -= SurfaceHandleChanged;
                _surface.OnParentChanged -= SurfaceHandleChanged;
                // Win32.HookToWindow(IntPtr.Zero);
            }
            
            _surface = surface;
            if(_surface != null)
            {
                SurfaceHandleChanged(surface);
                _surface.OnHandleChanged += SurfaceHandleChanged;
                _surface.OnParentChanged += SurfaceHandleChanged;
                // Win32.HookToWindow(_windowHandle);
            }
        }
    }

    private void SurfaceHandleChanged(INativeSurface surface)
    {
        if (surface.WindowHandle != null)
        {
            _windowHandle = surface.WindowHandle.Value;
            // Win32.HookToWindow(_windowHandle);
        }
    }

    protected override void OnClearState()
    {
    }

    public override MouseDevice GetMouse()
    {
        // TODO:
        return null;
    }

    public override KeyboardDevice GetKeyboard()
    {
        return GetCustomDevice<MacOSKeyboardDevice>();
    }

    protected override GamepadDevice OnGetGamepad(int index, GamepadSubType subtype)
    {
        return null;
    }

    public override TouchDevice GetTouch()
    {
        return null;
    }

}