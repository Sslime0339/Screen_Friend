using System;
using System.Collections.Generic;
using System.Text;

namespace VirtualFriend.Scripts
{
    internal class WindowManager
    {
        private List<WindowData> _pendingWindows = new List<WindowData>();
        private readonly object _lock = new object();
        private bool _isRunning = true;
        private readonly float _pixelsPerMeter;

        public WindowManager(float pixelsPerMeter)
        {
            _pixelsPerMeter = pixelsPerMeter;
        }

        public void StartScanning(int workAreaWidth, int workAreaHeight, int screenWidth, int screenHeight)
        {
            _ = Task.Run(async () =>
            {
                const int DWMWA_CLOAKED = 14;
                while (_isRunning)
                {
                    List<WindowData> currentWindows = new List<WindowData>();
                    EnumWindows((hWnd, lParam) =>
                    {
                        if (!IsWindowVisible(hWnd)) return true;
                        StringBuilder sb = new StringBuilder(256);
                        GetWindowText(hWnd, sb, sb.Capacity);
                        if (string.IsNullOrWhiteSpace(sb.ToString())) return true;

                        DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out bool isCloaked, sizeof(bool));
                        if (isCloaked) return true;

                        if (!GetExactWindowRect(hWnd, out RECT rect)) return true;

                        int width = rect.Right - rect.Left;
                        int height = rect.Bottom - rect.Top;
                        if (width <= 16 || height <= 16 || rect.Left < -10000) return true;

                        if (!((width == workAreaWidth && height == workAreaHeight) || (width == screenWidth && height == screenHeight)))
                        {
                            if (!IsZoomed(hWnd) && !IsIconic(hWnd))
                            {
                                currentWindows.Add(new WindowData
                                {
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

        // Сюда переносятся все DllImport (EnumWindows, GetWindowText, DwmGetWindowAttribute и т.д.) из вашего старого кода
    }
}
