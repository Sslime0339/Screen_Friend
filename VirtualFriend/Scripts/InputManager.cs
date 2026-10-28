using System;
using System.Collections.Generic;
using System.Diagnostics;
using Raylib_cs;
using VirtualFriend.Scripts.GameObjects;
using SysVector = System.Numerics.Vector2;

namespace VirtualFriend.Scripts
{
    internal class InputManager
    {
        private readonly RenderEngine _renderEngine;

        private const int VK_ESCAPE = 0x1B;
        private const int VK_C = 0x43;
        private const int VK_X = 0x58;
        private const int VK_LBUTTON = 0x01;

        private bool _wasCPressed = false;
        private bool _wasXPressed = false;
        private bool _wasLButtonPressed = false;

        private int _mouseX;
        private int _mouseY;

        // Вектор скорости мыши в мировых координатах (метров в секунду)
        private SysVector _mouseWorldVelocity = SysVector.Zero;

        public int MouseX => _mouseX;
        public int MouseY => _mouseY;

        // Свойство для получения актуальной скорости броска
        public SysVector MouseWorldVelocity => _mouseWorldVelocity;

        public InputManager(RenderEngine renderEngine)
        {
            _renderEngine = renderEngine;
            ResetMousePosition();
        }

        public void Update(List<GameObjects.GameObject> gameObjects, float dt)
        {
            // Сохраняем предыдущую позицию в метрах перед обновлением
            SysVector previousMouseWorld = GetMouseWorldPosition();

            // 1. Обновление координат мыши
            if (Win32Native.GetCursorPos(out Win32Native.Point point))
            {
                _mouseX = point.X;
                _mouseY = point.Y;
            }

            SysVector currentMouseWorld = GetMouseWorldPosition();

            // 2. Считаем мгновенную скорость мыши (изменение позиции / время кадра)
            if (dt > 0)
            {
                _mouseWorldVelocity = (currentMouseWorld - previousMouseWorld) / dt;
            }
            else
            {
                _mouseWorldVelocity = SysVector.Zero;
            }

            // 3. Логика динамического клика насквозь
            bool isHoveringBox = false;
            foreach (var obj in gameObjects)
            {
                if (obj.PhysicsBody != null && !obj.IsDestroyed && obj is BoxObject box)
                {
                    float halfSize = box.Size / 2f;
                    var pos = obj.PhysicsBody.Position;

                    if (currentMouseWorld.X >= pos.X - halfSize && currentMouseWorld.X <= pos.X + halfSize &&
                        currentMouseWorld.Y >= pos.Y - halfSize && currentMouseWorld.Y <= pos.Y + halfSize)
                    {
                        isHoveringBox = true;
                        break;
                    }
                }
            }

            if (isHoveringBox)
            {
                Raylib.ClearWindowState(ConfigFlags.MousePassthroughWindow);
            }
            else
            {
                if (!IsLButtonDown())
                {
                    Raylib.SetWindowState(ConfigFlags.MousePassthroughWindow);
                }
            }
        }

        public bool IsExitPressed() => (Win32Native.GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0;

        public bool IsSpawnPressed()
        {
            bool isDown = (Win32Native.GetAsyncKeyState(VK_C) & 0x8000) != 0;
            bool pressed = isDown && !_wasCPressed;
            _wasCPressed = isDown;
            return pressed;
        }

        public bool IsClearPressed()
        {
            bool isDown = (Win32Native.GetAsyncKeyState(VK_X) & 0x8000) != 0;
            bool pressed = isDown && !_wasXPressed;
            _wasXPressed = isDown;
            return pressed;
        }

        public bool IsLButtonDown() => (Win32Native.GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;

        public bool IsLButtonPressed()
        {
            bool isDown = IsLButtonDown();
            bool pressed = isDown && !_wasLButtonPressed;
            _wasLButtonPressed = isDown;
            return pressed;
        }

        public bool IsLButtonReleased()
        {
            bool isDown = IsLButtonDown();
            bool released = !isDown && _wasLButtonPressed;
            _wasLButtonPressed = isDown;
            return released;
        }

        public SysVector GetMouseWorldPosition()
        {
            return Raylib.GetScreenToWorld2D(new SysVector(_mouseX, _mouseY), _renderEngine.Camera);
        }

        public void ResetMousePosition()
        {
            if (Win32Native.GetCursorPos(out Win32Native.Point point))
            {
                _mouseX = point.X;
                _mouseY = point.Y;
            }
        }
    }
}
