using Raylib_cs;
using System;
using System.Collections.Generic;
using VirtualFriend.Scripts.GameObjects;
using VirtualFriend.Scripts.Interfaces;
using SysVector = System.Numerics.Vector2;

namespace VirtualFriend.Scripts
{
    internal class InputManager
    {
        private readonly RenderEngine _renderEngine;



        // Состояния всей клавиатуры (256 виртуальных клавиш Windows)
        private readonly bool[] _currentKeys = new bool[256];
        private readonly bool[] _previousKeys = new bool[256];

        // Буфер для секретных комбинаций (хранит последние N нажатых кодов клавиш)
        private readonly List<int> _keyHistory = new List<int>();
        private const int MaxHistoryLength = 20;
        private float _comboResetTimer = 0f;
        private const float ComboTimeout = 2.0f; // Сброс комбинации, если игрок не нажимает клавиши 2 секунды

        private int _mouseX;
        private int _mouseY;
        private SysVector _mouseWorldVelocity = SysVector.Zero;

        public int MouseX => _mouseX;
        public int MouseY => _mouseY;
        public SysVector MouseWorldVelocity => _mouseWorldVelocity;

        public InputManager(RenderEngine renderEngine)
        {
            _renderEngine = renderEngine;
            ResetMousePosition();
        }

        public void Update(List<GameObject> gameObjects, float dt, WindowZoneObject WinUnderMouse)
        {
            // 1. Обновляем состояние всех клавиш клавиатуры и кнопок мыши
            UpdateKeyboardState(dt);

            // 2. Обновление координат и скорости мыши
            SysVector previousMouseWorld = GetMouseWorldPosition();
            if (Win32Native.GetCursorPos(out Win32Native.Point point))
            {
                _mouseX = point.X;
                _mouseY = point.Y;
            }
            SysVector currentMouseWorld = GetMouseWorldPosition();

            _mouseWorldVelocity = dt > 0 ? (currentMouseWorld - previousMouseWorld) / dt : SysVector.Zero;

            // 3. Логика динамического клика насквозь
            bool isHoveringObject = false;
            foreach (var obj in gameObjects)
            {
                if (obj is IClickable clickable && clickable.IsUnderMouse(currentMouseWorld, WinUnderMouse))
                {
                    isHoveringObject = true;
                    break;
                }
            }

            if (isHoveringObject)
            {
                Raylib.ClearWindowState(ConfigFlags.MousePassthroughWindow);
            }
            else if (!IsLButtonDown() && !IsRButtonDown() && !IsMButtonDown())
            {
                Raylib.SetWindowState(ConfigFlags.MousePassthroughWindow);
            }
        }

        private void UpdateKeyboardState(float dt)
        {
            // Копируем состояние прошлого кадра
            Array.Copy(_currentKeys, _previousKeys, 256);

            // Таймер сброса комбо-последовательности
            if (_keyHistory.Count > 0)
            {
                _comboResetTimer += dt;
                if (_comboResetTimer >= ComboTimeout)
                {
                    _keyHistory.Clear();
                    _comboResetTimer = 0f;
                }
            }

            // Опрашиваем все клавиши (коды 1..254)
            for (int i = 1; i < 255; i++)
            {
                bool isDown = (Win32Native.GetAsyncKeyState(i) & 0x8000) != 0;
                _currentKeys[i] = isDown;

                // Если клавиша была нажата только что (и это не кнопка мыши, коды 1..6)
                if (i > 6 && isDown && !_previousKeys[i])
                {
                    RecordKeyPress(i);
                }
            }
        }

        private void RecordKeyPress(int vkCode)
        {
            _comboResetTimer = 0f;
            _keyHistory.Add(vkCode);
            if (_keyHistory.Count > MaxHistoryLength)
            {
                _keyHistory.RemoveAt(0);
            }
        }


        // --- Универсальные методы через KeyCode ---

        /// <summary>Клавиша удерживается в данный момент</summary>
        public bool IsKeyDown(KeyCode key) => _currentKeys[(int)key];

        /// <summary>Клавиша нажата именно в этом кадре</summary>
        public bool IsKeyPressed(KeyCode key) => _currentKeys[(int)key] && !_previousKeys[(int)key];

        /// <summary>Клавиша отпущена именно в этом кадре</summary>
        public bool IsKeyReleased(KeyCode key) => !_currentKeys[(int)key] && _previousKeys[(int)key];



        /// <summary>Проверка секретной комбинации из перечисления KeyCode</summary>
        public bool CheckSequence(params KeyCode[] sequence)
        {
            if (_keyHistory.Count < sequence.Length) return false;

            int offset = _keyHistory.Count - sequence.Length;
            for (int i = 0; i < sequence.Length; i++)
            {
                if (_keyHistory[offset + i] != (int)sequence[i])
                    return false;
            }

            _keyHistory.Clear();
            return true;
        }



        // --- Обертки для существующего кода ---
        public bool IsExitPressed() => IsKeyPressed(KeyCode.Escape);
        public bool IsClearPressed() => IsKeyPressed(KeyCode.K);
        public bool IsMenuPressed() => IsKeyPressed(KeyCode.I);

        public bool IsLButtonDown() => IsKeyDown(KeyCode.LeftMouse);
        public bool IsLButtonPressed() => IsKeyPressed(KeyCode.LeftMouse);
        public bool IsLButtonReleased() => IsKeyReleased(KeyCode.LeftMouse);

        public bool IsRButtonDown() => IsKeyDown(KeyCode.RightMouse);
        public bool IsRButtonPressed() => IsKeyPressed(KeyCode.RightMouse);
        public bool IsRButtonReleased() => IsKeyReleased(KeyCode.RightMouse);

        public bool IsMButtonDown() => IsKeyDown(KeyCode.MiddleMouse);
        public bool IsMButtonPressed() => IsKeyPressed(KeyCode.MiddleMouse);
        public bool IsMButtonReleased() => IsKeyReleased(KeyCode.MiddleMouse);

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
