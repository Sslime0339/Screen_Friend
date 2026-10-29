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
            public IntPtr Hwnd;
            public float Width;
            public float Height;
            public float CenterX;
            public float CenterY;
            public int ZIndex;
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
        public void Update(float ppm)
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
                    var newWindowObj = GameObjectFactory.Instance.CreateWindowZone(_physicsWorld, data, ppm);
                    _activeWindowObjects.Add(newWindowObj);
                }
                else
                {
                    // Если окно уже есть — обновляем его параметры в игровом движке
                    existingWindow.UpdatePositionAndSize(data);
                }
            }
        }

        public void StartScanning(int workAreaWidth, int workAreaHeight, int screenWidth, int screenHeight, IntPtr ReylibHWnd)
        {
            _ = Task.Run(async () =>
            {
                // Получаем дескриптор рабочего стола для фильтрации
                IntPtr shellWindow = Win32Native.GetShellWindow();

                while (_isRunning)
                {
                    int zCounter = 0;
                    List<WindowData> currentWindows = new List<WindowData>();

                    Win32Native.EnumWindows((hWnd, lParam) =>
                    {

                        // Проверка что окно не является самим окном Raylib
                        if (hWnd == ReylibHWnd) return true;


                        // 1. Базовая проверка видимости WinAPI и свернутости
                        if (!Win32Native.IsWindowVisible(hWnd) || Win32Native.IsIconic(hWnd)) return true;

                        // 2. Исключаем Shell-окно (рабочий стол)
                        if (hWnd == shellWindow) return true;

                        // 3. Получаем заголовок окна
                        StringBuilder sb = new StringBuilder(256);
                        Win32Native.GetWindowText(hWnd, sb, sb.Capacity);
                        if (string.IsNullOrWhiteSpace(sb.ToString())) return true;

                        // 4. Тройная DWM-проверка (Проверка на Cloaked - замороженные/скрытые приложения Store)
                        Win32Native.DwmGetWindowAttribute(hWnd, Win32Native.DWMWA_CLOAKED, out bool isCloaked, sizeof(bool));
                        if (isCloaked) return true;

                        // Фильтр: Отрисовывает ли DWM физическую рамку оверлея приложения.
                        if (!Win32Native.GetExactWindowRect(hWnd, out Win32Native.RECT rect)) return true;

                        int width = rect.Right - rect.Left;
                        int height = rect.Bottom - rect.Top;

                        // Окна нулевого/минимального размера или сброшенные системой в виртуальные координаты
                        if (width <= 16 || height <= 16 || rect.Left < -10000 || rect.Top < -10000) return true;

                        // 5. Фильтрация по стилям (отсекаем отключенные системой окна)
                        int style = Win32Native.GetWindowLong(hWnd, Win32Native.GWL_STYLE);
                        if ((style & Win32Native.WS_DISABLED) != 0) return true;

                        // 6. Проверка WindowPlacement (showCmd == 0 означает SW_HIDE)
                        Win32Native.WINDOWPLACEMENT placement = new Win32Native.WINDOWPLACEMENT();
                        placement.length = Marshal.SizeOf(placement);
                        if (Win32Native.GetWindowPlacement(hWnd, ref placement))
                        {
                            if (placement.showCmd == 0) return true;
                        }

                        // 7. Фильтрация по имени системного класса Windows
                        StringBuilder sbClass = new StringBuilder(256);
                        Win32Native.GetClassName(hWnd, sbClass, sbClass.Capacity);
                        string className = sbClass.ToString();

                        if (className == "Windows.UI.Core.CoreWindow" ||
                            className == "Progman" ||
                            className == "WorkerW") return true;

                        // 8. Исключаем Tool-окна (панели инструментов)
                        int exStyle = Win32Native.GetWindowLong(hWnd, Win32Native.GWL_EXSTYLE);
                        if ((exStyle & Win32Native.WS_EX_TOOLWINDOW) != 0) return true;

                        // 9. Бронебойный фильтр для Realtek и ApplicationFrameWindow
                        if (className == "ApplicationFrameWindow")
                        {
                            Win32Native.DwmGetWindowAttribute(hWnd, Win32Native.DWMWA_CLOAKED, out int cloakedVal, sizeof(int));
                            if (cloakedVal != 0) return true;
                        }

                        // 10. Проверка на соответствие размерам рабочего стола или экрана
                        //if (!((width == workAreaWidth && height == workAreaHeight) || (width == screenWidth && height == screenHeight)))
                        //{
                            //if (!Win32Native.IsZoomed(hWnd))
                            //{




                        zCounter++;

                        currentWindows.Add(new WindowData
                        {
                            Hwnd = hWnd,
                            Width = width / _pixelsPerMeter,
                            Height = height / _pixelsPerMeter,
                            CenterX = (rect.Left + width / 2f) / _pixelsPerMeter,
                            CenterY = (rect.Top + height / 2f) / _pixelsPerMeter,
                            ZIndex = zCounter
                        });
                            //}
                        //}
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

        // Импорт функции Win32 API для изменения размеров/позиции
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    }
}
