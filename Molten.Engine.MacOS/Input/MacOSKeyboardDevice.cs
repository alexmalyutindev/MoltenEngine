using Molten.Graphics;
using Silk.NET.GLFW;
using Silk.NET.Input;
using Silk.NET.Input.Glfw;
using Silk.NET.Windowing;

namespace Molten.Input;

public class MacOSKeyboardDevice : KeyboardDevice
{
    private IInputContext _inputContext;
    private unsafe WindowHandle* _window;
    public override string DeviceName => "MacOS Keyboard";

    protected override List<InputDeviceFeature> OnInitialize(InputService service)
    {
        List<InputDeviceFeature> baseFeatures = base.OnInitialize(service);
        return baseFeatures;
    }

    protected override unsafe bool ProcessState(ref KeyboardKeyState newState, ref KeyboardKeyState prevState)
    {
        return base.ProcessState(ref newState, ref prevState);
    }

    protected override void OnClearState() { }

    public override void OpenControlPanel() { }

    protected override unsafe void OnBind(INativeSurface surface)
    {
        // _inputContext = Window.GetView().CreateInput();
        _window = (WindowHandle*)surface.WindowHandle;
        var callback = Glfw.GetApi().SetCharCallback(_window, CharCallback);
    }

    private static unsafe void CharCallback(WindowHandle* window, uint codepoint)
    {
        Console.WriteLine(codepoint);
    }

    protected override void OnUnbind(INativeSurface surface)
    {
    }

    protected override void OnUpdate(Timing time) { }
}