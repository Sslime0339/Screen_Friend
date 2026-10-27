using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Genbox.VelcroPhysics.Dynamics;
using VirtualFriend.Scripts.GameObjects;

namespace VirtualFriend.Scripts
{
    internal class WindowManager
    {
        // Хранилище активных игровых объектов окон
        private readonly List<WindowZoneObject> _activeWindowObjects = new List<WindowZoneObject>();
        private readonly World _physicsWorld;
        private readonly float _pixelsPerMeter;

        public struct WindowData
        {
            public IntPtr Hwnd; // Добавлено для связи с Win32 API
            public float Width;
            public float Height;
            public float CenterX;
            public float CenterY;
        }

        private List<WindowData> _pendingWindows = new List<WindowData>();
        private readonly object _lock = new object();
        private bool _isRunning = true;

        public WindowManager(World physicsWorld, float pixelsPerMeter)
        {
            _physicsWorld = physicsWorld;
            _pixelsPerMeter = pixelsPerMeter;
        }

        // --- Метод для управления положением окна из Win32 ---
        public bool ResizeAndMoveWindow(IntPtr hWnd, int x, int y, int width, int height)
        {
            const uint SWP_NOZORDER = 0x0004;
            const uint SWP_NOACTIVATE = 0x0010;

            // Вызов Win32 API функции SetWindowPos
            return SetWindowPos(hWnd, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
        }

        // Обновление состояния: создание, актуализация и удаление игровых объектов окон
        public void Update()
        {
            List<WindowData> snapshot = GetWindowsSnapshot();

            // 1. Удаляем закрытые окна
            for (int i = _activeWindowObjects.Count - 1; i >= 0; i--)
            {
                var activeWin = _activeWindowObjects[i];
                if (!snapshot.Exists(w => w.Hwnd == activeWin.Hwnd))
                {
                    activeWin.Destroy(_physicsWorld);
                    _activeWindowObjects.RemoveAt(i);
                }
            }

            // 2. Создаем новые или обновляем существующие
            foreach (var data in snapshot)
            {
                var existingWindow = _activeWindowObjects.Find(w => w.Hwnd == data.Hwnd);
                if (existingWindow == null)
                {
                    // Создаем новый объект окна и сохраняем его в список
                    var newWindowObj = GameObjectFactory.Instance.CreateWindowZone(_physicsWorld, data);
                    _activeWindowObjects.Add(newWindowObj);
                }
                else
                {
                    // Если окно уже есть — обновляем его параметры в игровом движке
                    existingWindow.UpdatePositionAndSize(data);
                }
            }
        }

        public void StartScanning(int workAreaWidth, int workAreaHeight, int screenWidth, int screenHeight)
        {
            _ = Task.Run(async () =>
            {
                const int DWMWA_CLOAKED = 14;
                while (_isRunning)
                {
                    List<WindowData> currentWindows = new List<WindowData>();
                    Win32Native.EnumWindows((hWnd, lParam) =>
                    {
                        if (!Win32Native.IsWindowVisible(hWnd)) return true;
                        StringBuilder sb = new StringBuilder(256);
                        Win32Native.GetWindowText(hWnd, sb, sb.Capacity);
                        if (string.IsNullOrWhiteSpace(sb.ToString())) return true;

                        Win32Native.DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out bool isCloaked, sizeof(bool));
                        if (isCloaked) return true;

                        if (!Win32Native.GetExactWindowRect(hWnd, out Win32Native.RECT rect)) return true;

                        int width = rect.Right - rect.Left;
                        int height = rect.Bottom - rect.Top;
                        if (width <= 16 || height <= 16 || rect.Left < -10000) return true;

                        if (!((width == workAreaWidth && height == workAreaHeight) || (width == screenWidth && height == screenHeight)))
                        {
                            if (!Win32Native.IsZoomed(hWnd) && !Win32Native.IsIconic(hWnd))
                            {
                                currentWindows.Add(new WindowData
                                {
                                    Hwnd = hWnd, // Сохраняем хэндл окна
                                    Width = width / _pixelsPerMeter,
                                    Height = height / _pixelsPerMeter,
                                    CenterX = (rect.Left + width / 2f) / _pixelsPerMeter,
                                    CenterY = (rect.Top + height / 2f) / _pixelsPerMeter
                                });
                            }
                        }
                        return true;
                    }, IntPtr.Zero);

                    lock (_lock) { _pendingWindows = currentWindows; }
                    await Task.Delay(100);
                }
            });
        }

        public List<WindowData> GetWindowsSnapshot()
        {
            lock (_lock) { return new List<WindowData>(_pendingWindows); }
        }

        public void Stop() => _isRunning = false;

        // Импорт функции Win32 API для изменения размеров/позиции
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    }
}
