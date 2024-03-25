using Molten.Graphics;

namespace Molten.Input;

public class MacOSInputService : InputService
{
    public override IClipboard Clipboard { get; }
    public override IInputNavigation Navigation { get; }

    private INativeSurface _surface;
    private IntPtr _windowHandle;

    protected override void OnClearState()
    {
        throw new NotImplementedException();
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

    protected override GamepadDevice OnGetGamepad(int index, GamepadSubType subtype)
    {
        throw new NotImplementedException();
    }

    public override MouseDevice GetMouse()
    {
        throw new NotImplementedException();
    }

    public override KeyboardDevice GetKeyboard()
    {
        throw new NotImplementedException();
    }


    public override TouchDevice GetTouch()
    {
        throw new NotImplementedException();
    }

    private void SurfaceHandleChanged(INativeSurface surface)
    {
        if (surface.WindowHandle != null)
        {
            _windowHandle = surface.WindowHandle.Value;
            // Win32.HookToWindow(_windowHandle);
        }
    }
}