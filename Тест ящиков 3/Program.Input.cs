using Genbox.VelcroPhysics.Dynamics;
using Genbox.VelcroPhysics.Factories;
using Raylib_cs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

// Псевдонимы типов для исключения конфликтов векторов
using SysVector = System.Numerics.Vector2;
using XnaVector = Microsoft.Xna.Framework.Vector2;

// Глобальный ввод и взаимодействие с коробками.
internal static partial class Program
{
    // --- ОБНОВЛЕНИЕ ПОЛОЖЕНИЯ МЫШИ И НАЖАТИЙ КЛАВИШ ЧЕРЕЗ WINAPI ---
    private static void UpdateInput()
    {
        // 1. Проверяем нажатие Escape для выхода
        if ((GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0)
        {
            _isRunning = false;
            return;
        }
        // 2. Получаем координаты курсора глобально на всем экране
        if (GetCursorPos(out Point point))
        {
            _lastMouseX = _mouseX;
            _lastMouseY = _mouseY;
            _mouseX = point.X;
            _mouseY = point.Y;
            // --- НОВАЯ ЛОГИКА ДИНАМИЧЕСКОГО КЛИКА НА СКВОЗЬ ---
            bool isHoveringBox = false;
            lock (_lock)
            {
                SysVector mouseWorldPos = Raylib.GetScreenToWorld2D(new SysVector(_mouseX, _mouseY), _camera);
                for (int i = _boxes.Count - 1; i >= 0; i--)
                {
                    var box = _boxes[i];
                    float halfSize = BoxSizeMeters / 2f;
                    if (mouseWorldPos.X >= box.Position.X - halfSize && mouseWorldPos.X <= box.Position.X + halfSize &&
                    mouseWorldPos.Y >= box.Position.Y - halfSize && mouseWorldPos.Y <= box.Position.Y + halfSize)
                    {
                        isHoveringBox = true;
                        break;
                    }
                }
            }
            // Если мы навели на коробку или уже тащим её — окно должно принимать клики.
            // Если курсор на пустом месте — включаем флаг Passthrough, чтобы клик пролетал дальше в Windows.
            if (isHoveringBox || _draggedBox != null)
            {
                Raylib.ClearWindowState(ConfigFlags.MousePassthroughWindow);
            }
            else
            {
                Raylib.SetWindowState(ConfigFlags.MousePassthroughWindow);
            }
            // --------------------------------------------------
            if (_mouseX != _lastMouseX || _mouseY != _lastMouseY)
            {
                _lastMoveTimestamp = Stopwatch.GetTimestamp();
                if (_draggedBox != null)
                {
                    lock (_lock)
                    {
                        SysVector mouseWorldPos = Raylib.GetScreenToWorld2D(new SysVector(_mouseX, _mouseY), _camera);
                        _draggedBox.Position = new XnaVector(mouseWorldPos.X, mouseWorldPos.Y);
                    }
                }
            }
        }
        // 3. Обработка клавиши "C" (Создание коробки)
        bool isCPressedNow = (GetAsyncKeyState(VK_C) & 0x8000) != 0;
        if (isCPressedNow && !_wasCPressed)
        {
            lock (_lock)
            {
                SysVector mouseWorldPos = Raylib.GetScreenToWorld2D(new SysVector(_mouseX, _mouseY), _camera);
                XnaVector xnaSpawnPos = new XnaVector(mouseWorldPos.X, mouseWorldPos.Y);
                Body box = BodyFactory.CreateRectangle(_world, BoxSizeMeters, BoxSizeMeters, 2f, xnaSpawnPos);
                box.BodyType = BodyType.Dynamic;
                box.Restitution = 0.2f;
                box.Friction = 0.6f;
                _boxes.Add(box);
            }
        }
        _wasCPressed = isCPressedNow;
        // 3.5 Обработка клавиши "X" (Очистить все коробки)
        bool isXPressedNow = (GetAsyncKeyState(VK_X) & 0x8000) != 0;
        if (isXPressedNow && !_wasXPressed)
        {
            lock (_lock)
            {
                // Удаляем все коробки из физического мира Velcro
                foreach (var box in _boxes)
                {
                    _world.DestroyBody(box);
                }
                _boxes.Clear();
                // Если в этот момент коробка удерживалась мышью, сбрасываем захват
                _draggedBox = null;
            }
        }
        _wasXPressed = isXPressedNow;
        // 4. Обработка Левой Кнопки Мыши (Захват и Бросок)
        bool isLButtonDown = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
        // Событие: Мышь только что нажата (Pressed)
        if (isLButtonDown && !_wasLButtonPressed)
        {
            lock (_lock)
            {
                SysVector mouseWorldPos = Raylib.GetScreenToWorld2D(new SysVector(_mouseX, _mouseY), _camera);
                for (int i = _boxes.Count - 1; i >= 0; i--)
                {
                    var box = _boxes[i];
                    float halfSize = BoxSizeMeters / 2f;
                    if (mouseWorldPos.X >= box.Position.X - halfSize && mouseWorldPos.X <= box.Position.X + halfSize &&
                    mouseWorldPos.Y >= box.Position.Y - halfSize && mouseWorldPos.Y <= box.Position.Y + halfSize)
                    {
                        _draggedBox = box;
                        _draggedBox.BodyType = BodyType.Kinematic;
                        _draggedBox.LinearVelocity = XnaVector.Zero;
                        _draggedBox.AngularVelocity = 0f;
                        break;
                    }
                }
            }
        }
        // Событие: Мышь только что отпущена (Released)
        else if (!isLButtonDown && _wasLButtonPressed && _draggedBox != null)
        {
            lock (_lock)
            {
                // Проверяем существование объекта, так как его могли удалить клавишей X во время удержания
                if (_boxes.Contains(_draggedBox))
                {
                    float throwForceX = 0f;
                    float throwForceY = 0f;
                    long currentTimestamp = Stopwatch.GetTimestamp();
                    long elapsedTicks = currentTimestamp - _lastMoveTimestamp;
                    double elapsedMilliseconds = (double)elapsedTicks / Stopwatch.Frequency * 1000;
                    if (elapsedMilliseconds < 50)
                    {
                        SysVector lastMouseWorld = Raylib.GetScreenToWorld2D(new SysVector(_lastMouseX, _lastMouseY), _camera);
                        SysVector currentMouseWorld = Raylib.GetScreenToWorld2D(new SysVector(_mouseX, _mouseY), _camera);
                        throwForceX = (currentMouseWorld.X - lastMouseWorld.X) * 35f;
                        throwForceY = (currentMouseWorld.Y - lastMouseWorld.Y) * 35f;
                    }
                    _draggedBox.BodyType = BodyType.Dynamic;
                    _draggedBox.LinearVelocity = new XnaVector(throwForceX, throwForceY);
                }
                _draggedBox = null;
            }
        }
        _wasLButtonPressed = isLButtonDown;
    }
    private static void InitializeMousePosition()
    {
        if (GetCursorPos(out Point point))
        {
            _mouseX = point.X;
            _mouseY = point.Y;
            _lastMouseX = point.X;
            _lastMouseY = point.Y;
        }
    }
}
