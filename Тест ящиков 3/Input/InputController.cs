using Raylib_cs;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Genbox.VelcroPhysics.Dynamics;
using Microsoft.Xna.Framework;
using SysVector = System.Numerics.Vector2;

internal sealed class InputController
{
    private const int VK_ESCAPE = 0x1B;
    private const int VK_C = 0x43;
    private const int VK_X = 0x58;
    private const int VK_LBUTTON = 0x01;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    private readonly PhysicsWorldService _physics;
    private readonly Scene _scene;
    private readonly float _pixelsPerMeter;
    private int _mouseX, _mouseY, _lastMouseX, _lastMouseY;
    private long _lastMoveTimestamp;
    private Body? _draggedBox;
    private bool _wasCPressed, _wasXPressed, _wasLButtonPressed;

    public InputController(PhysicsWorldService physics, Scene scene, float pixelsPerMeter)
    {
        _physics = physics;
        _scene = scene;
        _pixelsPerMeter = pixelsPerMeter;
        InitializeMousePosition();
    }

    public Body? DraggedBox => _draggedBox;

    public void Update(ref bool isRunning)
    {
        if ((GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0)
        {
            isRunning = false;
            return;
        }

        if (GetCursorPos(out NativePoint point))
        {
            _lastMouseX = _mouseX;
            _lastMouseY = _mouseY;
            _mouseX = point.X;
            _mouseY = point.Y;

            bool hovering = _physics.FindBoxAt(_mouseX / _pixelsPerMeter, _mouseY / _pixelsPerMeter) != null;
            if (hovering || _draggedBox != null)
                Raylib.ClearWindowState(ConfigFlags.MousePassthroughWindow);
            else
                Raylib.SetWindowState(ConfigFlags.MousePassthroughWindow);

            if (_mouseX != _lastMouseX || _mouseY != _lastMouseY)
            {
                _lastMoveTimestamp = Stopwatch.GetTimestamp();
                if (_draggedBox != null)
                    _draggedBox.Position = new Vector2(_mouseX / _pixelsPerMeter, _mouseY / _pixelsPerMeter);
            }
        }

        bool cPressed = (GetAsyncKeyState(VK_C) & 0x8000) != 0;
        if (cPressed && !_wasCPressed)
            _scene.CreateBox(_mouseX / _pixelsPerMeter, _mouseY / _pixelsPerMeter);
        _wasCPressed = cPressed;

        bool xPressed = (GetAsyncKeyState(VK_X) & 0x8000) != 0;
        if (xPressed && !_wasXPressed)
        {
            _scene.Clear();
            _draggedBox = null;
        }
        _wasXPressed = xPressed;

        bool leftDown = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
        if (leftDown && !_wasLButtonPressed)
        {
            _draggedBox = _physics.FindBoxAt(_mouseX / _pixelsPerMeter, _mouseY / _pixelsPerMeter);
            if (_draggedBox != null)
            {
                _draggedBox.BodyType = BodyType.Kinematic;
                _draggedBox.LinearVelocity = Vector2.Zero;
                _draggedBox.AngularVelocity = 0f;
            }
        }
        else if (!leftDown && _wasLButtonPressed && _draggedBox != null)
        {
            if (_physics.ContainsBox(_draggedBox))
            {
                float vx = 0f, vy = 0f;
                long elapsedTicks = Stopwatch.GetTimestamp() - _lastMoveTimestamp;
                double elapsedMs = (double)elapsedTicks / Stopwatch.Frequency * 1000;
                if (elapsedMs < 50)
                {
                    vx = ((_mouseX - _lastMouseX) / _pixelsPerMeter) * 35f;
                    vy = ((_mouseY - _lastMouseY) / _pixelsPerMeter) * 35f;
                }

                _draggedBox.BodyType = BodyType.Dynamic;
                _draggedBox.LinearVelocity = new Vector2(vx, vy);
            }

            _draggedBox = null;
        }

        _wasLButtonPressed = leftDown;
    }

    private void InitializeMousePosition()
    {
        if (GetCursorPos(out NativePoint point))
        {
            _mouseX = _lastMouseX = point.X;
            _mouseY = _lastMouseY = point.Y;
        }
    }
}
