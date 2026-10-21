using Raylib_cs;
using System;
using System.Threading;
using System.Threading.Tasks;
using SysVector = System.Numerics.Vector2;

internal static class Program
{
    private const float PixelsPerMeter = 50f;
    private static volatile bool _isRunning = true;

    private static async Task Main(string[] args)
    {
        var windows = new Win32WindowProvider();
        var workArea = Win32WindowProvider.GetWorkArea();

        int workWidth = workArea.Right - workArea.Left;
        int workHeight = workArea.Bottom - workArea.Top;

        Raylib.SetConfigFlags(
            ConfigFlags.MousePassthroughWindow |
            ConfigFlags.UndecoratedWindow |
            ConfigFlags.TransparentWindow |
            ConfigFlags.TopmostWindow |
            ConfigFlags.AlwaysRunWindow);

        Raylib.InitWindow(workWidth, workHeight, "Raylib + Velcro Physics overlay");
        Raylib.SetTargetFPS(60);

        var physics = new PhysicsWorldService(workWidth, workHeight, PixelsPerMeter);
        var renderer = new RaylibRenderer();
        var input = new InputController(physics, PixelsPerMeter);

        using var cancellation = new CancellationTokenSource();
        Task windowScanTask = windows.RunScannerAsync(cancellation.Token);

        try
        {
            while (_isRunning && !Raylib.WindowShouldClose())
            {
                float dt = Raylib.GetFrameTime();

                input.Update(ref _isRunning);
                physics.SetWindowObstacles(windows.GetLatestSnapshot());
                physics.Step(Math.Min(dt, 1f / 60f));

                renderer.DrawFrame(physics, input, dt);
            }
        }
        finally
        {
            _isRunning = false;
            cancellation.Cancel();

            try { await windowScanTask; }
            catch (OperationCanceledException) { }

            renderer.Dispose();
            physics.Dispose();
            Raylib.CloseWindow();
        }
    }
}
