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
        private readonly List<WindowZoneObject> _activeWindowObjects = new List<WindowZoneObject>();
        private readonly World _physicsWorld;
        private readonly float _pixelsPerMeter;

        // --- СОБЫТИЯ ДЛЯ ВЫТАЛКИВАНИЯ И ЗАСАСЫВАНИЯ ОБЪЕКТОВ ---
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
            public bool IsMaximized; // <-- Добавили флаг максимизации
        }

        private List<WindowData> _pendingWindows = new List<WindowData>();
        private readonly object _lock = new object();
        private bool _isRunning = true;

        public WindowManager(World physicsWorld, float pixelsPerMeter)
        {
            _physicsWorld = physicsWorld;
            _pixelsPerMeter = pixelsPerMeter;
        }

        public bool ResizeAndMoveWindow(IntPtr hWnd, int x, int y, int width, int height)
        {
            const uint SWP_NOZORDER = 0x0004;
            const uint SWP_NOACTIVATE = 0x0010;
            return SetWindowPos(hWnd, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
        }

        public void Update(float ppm, float dt)
        {
            List<WindowData> snapshot = GetWindowsSnapshot();

            for (int i = _activeWindowObjects.Count - 1; i >= 0; i--)
            {
                var activeWin = _activeWindowObjects[i];
                if (!snapshot.Exists(w => w.Hwnd == activeWin.Hwnd))
                {
                    // Выталкиваем объекты перед полным уничтожением окна из системы
                    OnWindowMinimized?.Invoke(activeWin);
                    activeWin.Destroy(_physicsWorld);
                    _activeWindowObjects.RemoveAt(i);
                }
            }

            foreach (var data in snapshot)
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
                            // --- ТРИГГЕР: Окно свернули ---
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

                        if (data.IsMaximized)
                        {
                            // --- ТРИГГЕР: Окно развернуто на весь экран ---
                            OnWindowMaximized?.Invoke(existingWindow);
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

                while (_isRunning)
                {
                    int zCounter = 0;
                    List<WindowData> currentWindows = new List<WindowData>();

                    Win32Native.EnumWindows((hWnd, lParam) =>
                    {
                        if (hWnd == ReylibHWnd) return true;

                        bool isMinimized = Win32Native.IsIconic(hWnd);
                        // ИСПОЛЬЗУЕМ СУЩЕСТВУЮЩИЙ МЕТОД ИЗ КЛАССА Win32Native
                        bool isMaximized = Win32Native.IsZoomed(hWnd);

                        if (!Win32Native.IsWindowVisible(hWnd) && !isMinimized) return true;
                        if (hWnd == shellWindow) return true;

                        StringBuilder sb = new StringBuilder(256);
                        Win32Native.GetWindowText(hWnd, sb, sb.Capacity);
                        if (string.IsNullOrWhiteSpace(sb.ToString())) return true;

                        Win32Native.DwmGetWindowAttribute(hWnd, Win32Native.DWMWA_CLOAKED, out bool isCloaked, sizeof(bool));
                        if (isCloaked) return true;

                        if (!Win32Native.GetExactWindowRect(hWnd, out Win32Native.RECT rect)) return true;

                        int width = rect.Right - rect.Left;
                        int height = rect.Bottom - rect.Top;

                        if (!isMinimized)
                        {
                            if (width <= 16 || height <= 16 || rect.Left < -10000 || rect.Top < -10000) return true;
                        }

                        int style = Win32Native.GetWindowLong(hWnd, Win32Native.GWL_STYLE);
                        if ((style & Win32Native.WS_DISABLED) != 0) return true;

                        Win32Native.WINDOWPLACEMENT placement = new Win32Native.WINDOWPLACEMENT();
                        placement.length = Marshal.SizeOf(placement);
                        if (Win32Native.GetWindowPlacement(hWnd, ref placement))
                        {
                            if (placement.showCmd == 0) return true;
                        }

                        StringBuilder sbClass = new StringBuilder(256);
                        Win32Native.GetClassName(hWnd, sbClass, sbClass.Capacity);
                        string className = sbClass.ToString();

                        if (className == "Windows.UI.Core.CoreWindow" ||
                            className == "Progman" ||
                            className == "WorkerW") return true;

                        int exStyle = Win32Native.GetWindowLong(hWnd, Win32Native.GWL_EXSTYLE);
                        if ((exStyle & Win32Native.WS_EX_TOOLWINDOW) != 0) return true;

                        if (className == "ApplicationFrameWindow")
                        {
                            Win32Native.DwmGetWindowAttribute(hWnd, Win32Native.DWMWA_CLOAKED, out int cloakedVal, sizeof(int));
                            if (cloakedVal != 0) return true;
                        }

                        zCounter++;

                        currentWindows.Add(new WindowData
                        {
                            Hwnd = hWnd,
                            Width = width / _pixelsPerMeter,
                            Height = height / _pixelsPerMeter,
                            CenterX = (rect.Left + width / 2f) / _pixelsPerMeter,
                            CenterY = (rect.Top + height / 2f) / _pixelsPerMeter,
                            ZIndex = zCounter,
                            IsMinimized = isMinimized,
                            IsMaximized = isMaximized
                        });

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

        public List<WindowZoneObject> GetActiveWindowObjects()
        {
            return _activeWindowObjects;
        }

        public void Stop() => _isRunning = false;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    }
}
