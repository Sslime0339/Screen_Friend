using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;


using nkast.Aether.Physics2D.Dynamics;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Collision.Shapes;
using AetherVector = nkast.Aether.Physics2D.Common.Vector2;
using VirtualFriend.Core;
using VirtualFriend.Entities;
using VirtualFriend.Entities.Objects;

namespace VirtualFriend.Systems
{
    internal class WindowManager
    {
        private readonly List<WindowZoneObject> _activeWindowObjects = new List<WindowZoneObject>();
        private readonly World _physicsWorld;
        private readonly float _pixelsPerMeter;

        public event Action<WindowZoneObject> OnWindowMinimized;
        public event Action<WindowZoneObject> OnWindowMaximized;

        public struct WindowData
        {
            public IntPtr Hwnd;
            public float Width;
            public float Height;
            public float CenterX;
            public float CenterY;
            public int ZIndex;
            public bool IsMinimized;
            public bool IsMaximized;
        }

        // Двойная буферизация списков: избегаем аллокаций каждые 100 мс
        private List<WindowData> _frontBuffer = new List<WindowData>(32);
        private List<WindowData> _backBuffer = new List<WindowData>(32);

        // Переиспользуемые объекты для Win32 API (0 аллокаций в цикле)
        private readonly StringBuilder _sharedStringBuilder = new StringBuilder(256);
        private Win32Native.WINDOWPLACEMENT _sharedPlacement = new Win32Native.WINDOWPLACEMENT
        {
            length = Marshal.SizeOf(typeof(Win32Native.WINDOWPLACEMENT))
        };

        private readonly object _lock = new object();
        private bool _isRunning = true;

        public WindowManager(World physicsWorld, float pixelsPerMeter)
        {
            _physicsWorld = physicsWorld;
            _pixelsPerMeter = pixelsPerMeter;
        }

        public void Update(float ppm, float dt)
        {
            // Блокируем поток только на время чтения актуального списка
            lock (_lock)
            {
                for (int i = _activeWindowObjects.Count - 1; i >= 0; i--)
                {
                    var activeWin = _activeWindowObjects[i];
                    bool exists = false;
                    for (int j = 0; j < _frontBuffer.Count; j++)
                    {
                        if (_frontBuffer[j].Hwnd == activeWin.Hwnd)
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                    {
                        OnWindowMinimized?.Invoke(activeWin);
                        activeWin.Destroy();
                        _activeWindowObjects.RemoveAt(i);
                    }
                }

                foreach (var data in _frontBuffer)
                {
                    var existingWindow = _activeWindowObjects.Find(w => w.Hwnd == data.Hwnd);
                    if (existingWindow == null)
                    {
                        if (!data.IsMinimized)
                        {
                            var newWindowObj = GameObjectFactory.Instance.CreateWindowZone(_physicsWorld, data, ppm);
                            _activeWindowObjects.Add(newWindowObj);

                            if (data.IsMaximized)
                            {
                                newWindowObj.WasMaximized = true;
                                OnWindowMaximized?.Invoke(newWindowObj);
                            }
                        }
                    }
                    else
                    {
                        if (data.IsMinimized)
                        {
                            if (existingWindow.IsShow)
                            {
                                existingWindow.Hide();
                                existingWindow.WasMaximized = false;
                                OnWindowMinimized?.Invoke(existingWindow);
                            }
                        }
                        else
                        {
                            if (!existingWindow.IsShow)
                            {
                                existingWindow.Show();
                            }

                            existingWindow.UpdatePositionAndSize(data, dt);

                            if (data.IsMaximized && !existingWindow.WasMaximized)
                            {
                                existingWindow.WasMaximized = true;
                                OnWindowMaximized?.Invoke(existingWindow);
                            }
                            else if (!data.IsMaximized)
                            {
                                existingWindow.WasMaximized = false;
                            }
                        }
                    }
                }
            }
        }

        public void StartScanning(int workAreaWidth, int workAreaHeight, int screenWidth, int screenHeight, IntPtr ReylibHWnd)
        {
            _ = Task.Run(async () =>
            {
                IntPtr shellWindow = Win32Native.GetShellWindow();
                float invPpm = 1f / _pixelsPerMeter; // Умножение быстрее деления в цикле

                while (_isRunning)
                {
                    int zCounter = 0;
                    _backBuffer.Clear(); // Очищаем буфер без освобождения памяти

                    Win32Native.EnumWindows((hWnd, lParam) =>
                    {
                        // 1. САМЫЕ БЫСТРЫЕ ПРОВЕРКИ (мгновенный отсев в ядре ОС)
                        if (hWnd == ReylibHWnd || hWnd == shellWindow) return true;

                        bool isMinimized = Win32Native.IsIconic(hWnd);
                        if (!isMinimized && !Win32Native.IsWindowVisible(hWnd)) return true;

                        int style = Win32Native.GetWindowLong(hWnd, Win32Native.GWL_STYLE);
                        if ((style & Win32Native.WS_DISABLED) != 0) return true;

                        int exStyle = Win32Native.GetWindowLong(hWnd, Win32Native.GWL_EXSTYLE);
                        if ((exStyle & Win32Native.WS_EX_TOOLWINDOW) != 0) return true;

                        // 2. ПРОВЕРКА ДЛИНЫ ЗАГОЛОВКА (без выделения памяти под строку)
                        if (Win32Native.GetWindowTextLength(hWnd) == 0) return true;

                        // 3. ПРОВЕРКА КООРДИНАТ И РАЗМЕРОВ
                        if (!Win32Native.GetExactWindowRect(hWnd, out Win32Native.RECT rect)) return true;

                        int width = rect.Right - rect.Left;
                        int height = rect.Bottom - rect.Top;

                        if (!isMinimized && (width <= 16 || height <= 16 || rect.Left < -10000 || rect.Top < -10000))
                            return true;

                        // 4. БОЛЕЕ ДОРОГИЕ DWM-ПРОВЕРКИ (только для оставшихся реальных окон)
                        Win32Native.DwmGetWindowAttribute(hWnd, Win32Native.DWMWA_CLOAKED, out int cloakedVal, sizeof(int));
                        if (cloakedVal != 0) return true;

                        if (Win32Native.GetWindowPlacement(hWnd, ref _sharedPlacement) && _sharedPlacement.showCmd == 0)
                            return true;

                        // 5. ПРОВЕРКА ИМЕНИ КЛАССА (переиспользуем StringBuilder)
                        _sharedStringBuilder.Clear();
                        Win32Native.GetClassName(hWnd, _sharedStringBuilder, _sharedStringBuilder.Capacity);

                        // Сравниваем без создания лишних string, где возможно
                        if (_sharedStringBuilder.Equals("Progman") ||
                            _sharedStringBuilder.Equals("WorkerW") ||
                            _sharedStringBuilder.Equals("Windows.UI.Core.CoreWindow"))
                        {
                            return true;
                        }

                        zCounter++;

                        _backBuffer.Add(new WindowData
                        {
                            Hwnd = hWnd,
                            Width = width * invPpm,
                            Height = height * invPpm,
                            CenterX = (rect.Left + width * 0.5f) * invPpm,
                            CenterY = (rect.Top + height * 0.5f) * invPpm,
                            ZIndex = zCounter,
                            IsMinimized = isMinimized,
                            IsMaximized = Win32Native.IsZoomed(hWnd)
                        });

                        return true;
                    }, IntPtr.Zero);

                    // Меняем буферы местами за O(1) — без копирования элементов
                    lock (_lock)
                    {
                        var temp = _frontBuffer;
                        _frontBuffer = _backBuffer;
                        _backBuffer = temp;
                    }

                    await Task.Delay(100);
                }
            });
        }

        public List<WindowZoneObject> GetActiveWindowObjects()
        {
            return _activeWindowObjects;
        }

        public void Stop() => _isRunning = false;
    }

}
