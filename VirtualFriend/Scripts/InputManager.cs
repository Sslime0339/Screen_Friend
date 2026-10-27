using Raylib_cs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using VirtualFriend.Scripts.GameObjects;
using SysVector = System.Numerics.Vector2;





namespace VirtualFriend.Scripts
{
    internal class InputManager
    {
        private readonly RenderEngine _renderEngine;

        // Коды клавиш WinAPI
        private const int VK_ESCAPE = 0x1B;
        private const int VK_C = 0x43;
        private const int VK_X = 0x58;
        private const int VK_LBUTTON = 0x01;

        // Состояния кнопок для эмуляции одиночного нажатия (Pressed)
        private bool _wasCPressed = false;
        private bool _wasXPressed = false;
        private bool _wasLButtonPressed = false;

        // Данные мыши
        private int _mouseX;
        private int _mouseY;
        private int _lastMouseX;
        private int _lastMouseY;
        private long _lastMoveTimestamp;

        // Свойства для получения текущего состояния мыши
        public int MouseX => _mouseX;
        public int MouseY => _mouseY;
        public int LastMouseX => _lastMouseX;
        public int LastMouseY => _lastMouseY;
        public long LastMoveTimestamp => _lastMoveTimestamp;

        public InputManager(RenderEngine renderEngine)
        {
            _renderEngine = renderEngine;
            ResetMousePosition();
        }

        /// <summary>
        /// Обновляет состояния всех клавиш и мыши. Должен вызываться КАЖДЫЙ кадр.
        /// </summary>
        public void Update(List<GameObject> gameObjects)
        {
            // 1. Обновление координат мыши
            if (Win32Native.GetCursorPos(out Win32Native.Point point))
            {
                _lastMouseX = _mouseX;
                _lastMouseY = _mouseY;
                _mouseX = point.X;
                _mouseY = point.Y;

                if (_mouseX != _lastMouseX || _mouseY != _lastMouseY)
                {
                    _lastMoveTimestamp = Stopwatch.GetTimestamp();
                }

            }

            // 2. Логика динамического клика насквозь (из старого проекта)
            bool isHoveringBox = false;
            SysVector mouseWorldPos = GetMouseWorldPosition();

            foreach (var obj in gameObjects)
            {
                // Проверяем только физические тела, у которых есть PhysicsBody
                if (obj.PhysicsBody != null && !obj.IsDestroyed)
                {
                    // Здесь мы используем фиксированный размер ящика (например, 1.0f или 1.2f в метрах)
                    // Если у вас размер ящика хранится внутри BoxObject, можно сделать свойство AABB или Size.
                    float halfSize = 1.0f / 2f;
                    var pos = obj.PhysicsBody.Position;

                    if (mouseWorldPos.X >= pos.X - halfSize && mouseWorldPos.X <= pos.X + halfSize &&
                        mouseWorldPos.Y >= pos.Y - halfSize && mouseWorldPos.Y <= pos.Y + halfSize)
                    {
                        isHoveringBox = true;
                        break;
                    }
                }
            }


            // Если мышь над коробкой — окно улавливает клики. Иначе — пропускает сквозь себя в ОС.
            if (isHoveringBox)
            {
                Raylib.ClearWindowState(ConfigFlags.MousePassthroughWindow);
            }
            else
            {
                // Если кнопка уже удерживается, не включаем passthrough, чтобы не потерять фокус во время перетаскивания
                if (!IsLButtonDown())
                {
                    Raylib.SetWindowState(ConfigFlags.MousePassthroughWindow);
                }
            }
        }

        // --- МЕТОДЫ-СОБЫТИЯ ДЛЯ КЛАВИШ ---

        public bool IsExitPressed()
        {
            return (Win32Native.GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0;
        }

        /// <summary>
        /// Возвращает true только в первый кадр нажатия клавиши 'C' (Аналог IsKeyPressed)
        /// </summary>
        public bool IsSpawnPressed()
        {
            bool isDown = (Win32Native.GetAsyncKeyState(VK_C) & 0x8000) != 0;
            bool pressed = isDown && !_wasCPressed;
            _wasCPressed = isDown; // Запоминаем состояние
            return pressed;
        }

        /// <summary>
        /// Возвращает true только в первый кадр нажатия клавиши 'X'
        /// </summary>
        public bool IsClearPressed()
        {
            bool isDown = (Win32Native.GetAsyncKeyState(VK_X) & 0x8000) != 0;
            bool pressed = isDown && !_wasXPressed;
            _wasXPressed = isDown;
            return pressed;
        }

        // --- МЕТОДЫ ДЛЯ ЛЕВОЙ КНОПКИ МЫШИ (ДЛЯ КОНТРОЛЛЕРА ЗАХВАТА) ---

        public bool IsLButtonDown()
        {
            return (Win32Native.GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
        }

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

        // --- ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ---

        /// <summary>
        /// Возвращает позицию мыши, автоматически переведенную в метры физического мира
        /// </summary>
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
                _lastMouseX = point.X;
                _lastMouseY = point.Y;
            }
        }
    }
}
