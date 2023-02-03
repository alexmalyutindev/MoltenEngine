﻿namespace Molten.Graphics
{
    internal class FinalizeStep : RenderStep
    {
        RenderCamera _orthoCamera;
        ObjectRenderData _dummyData;

        internal override void Initialize(RenderService renderer)
        {
            _dummyData = new ObjectRenderData();
            _orthoCamera = new RenderCamera(RenderCameraMode.Orthographic);
        }

        public override void Dispose()
        {

        }

        int _frameCounter = 0;

        internal override void Render(RenderService renderer, RenderCamera camera, RenderChainContext context, Timing time)
        {
            _orthoCamera.Surface = camera.Surface;

            RectangleF bounds = new RectangleF(0, 0, camera.Surface.Width, camera.Surface.Height);
            GraphicsCommandQueue cmd = renderer.Device.Cmd;
            IRenderSurface2D finalSurface = camera.Surface;

            if (!camera.HasFlags(RenderCameraFlags.DoNotClear))
                renderer.ClearIfFirstUse(finalSurface, context.Scene.BackgroundColor);

            cmd.SetRenderSurfaces(finalSurface);
            cmd.DepthSurface.Value = null;
            cmd.DepthWriteOverride = GraphicsDepthWritePermission.Disabled;
            cmd.SetViewports(camera.Surface.Viewport);
            cmd.SetScissorRectangle((Rectangle)camera.Surface.Viewport.Bounds);

            // We only need scissor testing here
            ITexture2D sourceSurface = context.HasComposed ? context.PreviousComposition : renderer.Surfaces[MainSurfaceType.Scene];
            RectStyle style = RectStyle.Default;

            cmd.BeginDraw(StateConditions.ScissorTest);
            renderer.SpriteBatch.Draw(sourceSurface, bounds, Vector2F.Zero, camera.Surface.Viewport.Bounds.Size, 0, Vector2F.Zero, ref style, null, 0, 0);
            renderer.SpriteBatch.Draw(new RectangleF(300, 300, 512, 512), Color.White, Engine.Current.Fonts.UnderlyingTexture);

            if (camera.HasFlags(RenderCameraFlags.ShowOverlay))
                renderer.Overlay.Render(time, renderer.SpriteBatch, renderer.Profiler, context.Scene.Profiler, camera);

            renderer.SpriteBatch.Flush(cmd, _orthoCamera, _dummyData);
            cmd.EndDraw();

            if (_frameCounter >= 300)
            {
                _frameCounter -= 300;
                cmd.LogState();
            }

            _frameCounter++;
        }
    }
}
