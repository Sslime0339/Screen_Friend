using Raylib_cs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using VirtualFriend.Scripts.GameObjects;
using VirtualFriend.Scripts.Interfaces;
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
        private const int VK_RBUTTON = 0x02;
        private const int VK_MBUTTON = 0x04;
        private const int VK_I = 0x49;


        private bool _isLButtonDown = false;
        private bool _isLButtonPressed = false;
        private bool _isLButtonReleased = false;
        private bool _wasLButtonDownLastFrame = false; // Для отслеживания изменений

        private bool _isRButtonDown = false;
        private bool _isRButtonPressed = false;
        private bool _isRButtonReleased = false;
        private bool _wasRButtonDownLastFrame = false;

        private bool _isMButtonDown = false;
        private bool _isMButtonPressed = false;
        private bool _isMButtonReleased = false;
        private bool _wasMButtonDownLastFrame = false;


        private bool _wasCPressed = false;
        private bool _wasXPressed = false;
        private bool _wasMenuPressed = false;


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

        public void Update(List<GameObjects.GameObject> gameObjects, float dt, WindowZoneObject WinUnderMouse)
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

            // 3. Логика динамического клика насквозь через интерфейс IClickable
            bool isHoveringObject = false;
            foreach (var obj in gameObjects)
            {
                // Проверяем, реализует ли объект интерфейс кликабельности
                if (obj is IClickable clickable)
                {
                    // Сам объект решает, попала ли в него мышь
                    if (clickable.IsUnderMouse(currentMouseWorld, WinUnderMouse))
                    {
                        isHoveringObject = true;
                        break;
                    }
                }
            }

            if (isHoveringObject)
            {
                Raylib.ClearWindowState(ConfigFlags.MousePassthroughWindow);
            }
            else
            {
                if (!IsLButtonDown() && !IsRButtonDown() && !IsMButtonDown())
                {
                    Raylib.SetWindowState(ConfigFlags.MousePassthroughWindow);
                }
            }



            // считываем состояние ЛКМ один раз за кадр:
            bool currentDown = (Win32Native.GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;

            // Кнопка нажата именно в этом кадре (сейчас зажата, а в прошлом кадре — нет)
            _isLButtonPressed = currentDown && !_wasLButtonDownLastFrame;

            // Кнопка отпущена именно в этом кадре (сейчас НЕ зажата, а в прошлом — была)
            _isLButtonReleased = !currentDown && _wasLButtonDownLastFrame;

            // Текущее состояние зажатия
            _isLButtonDown = currentDown;

            // Сохраняем состояние для следующего кадра
            _wasLButtonDownLastFrame = currentDown;


            bool currentRDown = (Win32Native.GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;
            _isRButtonPressed = currentRDown && !_wasRButtonDownLastFrame;
            _isRButtonReleased = !currentRDown && _wasRButtonDownLastFrame;
            _isRButtonDown = currentRDown;
            _wasRButtonDownLastFrame = currentRDown;

            bool currentMDown = (Win32Native.GetAsyncKeyState(VK_MBUTTON) & 0x8000) != 0;
            _isMButtonPressed = currentMDown && !_wasMButtonDownLastFrame;
            _isMButtonReleased = !currentMDown && _wasMButtonDownLastFrame;
            _isMButtonDown = currentMDown;
            _wasMButtonDownLastFrame = currentMDown;

        }

        public bool IsExitPressed() => (Win32Native.GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0;

        public bool IsSpawnPressed()
        {
            bool isDown = (Win32Native.GetAsyncKeyState(VK_C) & 0x8000) != 0;
            bool pressed = isDown && !_wasCPressed;
            _wasCPressed = isDown;
            return pressed;
        }

        public bool IsMenuPressed()
        {
            bool isDown = (Win32Native.GetAsyncKeyState(VK_I) & 0x8000) != 0;
            bool pressed = isDown && !_wasMenuPressed;
            _wasMenuPressed = isDown;
            return pressed;
        }

        public bool IsClearPressed()
        {
            bool isDown = (Win32Native.GetAsyncKeyState(VK_X) & 0x8000) != 0;
            bool pressed = isDown && !_wasXPressed;
            _wasXPressed = isDown;
            return pressed;
        }


        public bool IsLButtonDown() => _isLButtonDown;
        public bool IsLButtonPressed() => _isLButtonPressed;
        public bool IsLButtonReleased() => _isLButtonReleased;

        public bool IsRButtonDown() => _isRButtonDown;
        public bool IsRButtonPressed() => _isRButtonPressed;
        public bool IsRButtonReleased() => _isRButtonReleased;

        public bool IsMButtonDown() => _isMButtonDown;
        public bool IsMButtonPressed() => _isMButtonPressed;
        public bool IsMButtonReleased() => _isMButtonReleased;


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
