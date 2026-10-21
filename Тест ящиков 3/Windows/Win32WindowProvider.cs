using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

internal sealed class Win32WindowProvider
{
    private readonly HashSet<nint> _trackedHandles = new();
    private readonly Dictionary<nint, WindowData> _latest = new();
    private long _lastDiscoveryTick;
    private const int DiscoveryIntervalMs = 1000;
    private const float PixelsPerMeter = 50f;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hWnd, int attribute, out bool value, int valueSize);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hWnd, int attribute, out RECT rect, int valueSize);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(int action, int param, ref RECT rect, int flags);
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const int DWMWA_CLOAKED = 14;
    private const int SPI_GETWORKAREA = 48;
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    public static (int Left, int Top, int Right, int Bottom) GetWorkArea()
    {
        RECT rect = new();
        if (!SystemParametersInfo(SPI_GETWORKAREA, 0, ref rect, 0))
            return (0, 0, GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));

        return (rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    /// <summary>
    /// Вызывается из основного цикла. Проверяет уже известные HWND каждый кадр,
    /// а полный поиск новых окон выполняет раз в секунду.
    /// </summary>
    public IReadOnlyList<WindowData> Update()
    {
        long now = Environment.TickCount64;
        if (_lastDiscoveryTick == 0 || now - _lastDiscoveryTick >= DiscoveryIntervalMs)
        {
            DiscoverWindows();
            _lastDiscoveryTick = now;
        }

        _latest.Clear();
        var closed = new List<nint>();

        foreach (nint handle in _trackedHandles)
        {
            if (!IsWindow(handle))
            {
                closed.Add(handle);
                continue;
            }

            // Свернутые, скрытые и развёрнутые на весь экран окна остаются
            // отслеживаемыми, но не участвуют в физике.
            if (!IsWindowVisible(handle) || IsIconic(handle) || IsZoomed(handle))
                continue;

            if (TryReadWindow(handle, out WindowData data))
                _latest[handle] = data;
        }

        foreach (nint handle in closed)
        {
            _trackedHandles.Remove(handle);
            _latest.Remove(handle);
        }

        return new List<WindowData>(_latest.Values);
    }

    private void DiscoverWindows()
    {
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            var title = new StringBuilder(256);
            GetWindowText(hWnd, title, title.Capacity);
            if (string.IsNullOrWhiteSpace(title.ToString()))
                return true;

            if (DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out bool cloaked, sizeof(bool)) == 0 && cloaked)
                return true;

            _trackedHandles.Add(hWnd);
            return true;
        }, IntPtr.Zero);
    }

    private static bool TryReadWindow(nint handle, out WindowData data)
    {
        data = default;

        if (DwmGetWindowAttribute(handle, DWMWA_CLOAKED, out bool cloaked, sizeof(bool)) == 0 && cloaked)
            return false;

        if (DwmGetWindowAttribute(handle, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT rect, Marshal.SizeOf<RECT>()) != 0)
            return false;

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width <= 16 || height <= 16 || rect.Left < -10000 || rect.Top < -10000)
            return false;

        var workArea = GetWorkArea();
        int workWidth = workArea.Right - workArea.Left;
        int workHeight = workArea.Bottom - workArea.Top;
        int screenWidth = GetSystemMetrics(SM_CXSCREEN);
        int screenHeight = GetSystemMetrics(SM_CYSCREEN);

        bool isOverlaySize =
            (width == workWidth && height == workHeight) ||
            (width == screenWidth && height == screenHeight);
        if (isOverlaySize)
            return false;

        data = new WindowData(
            handle,
            width / PixelsPerMeter,
            height / PixelsPerMeter,
            (rect.Left + width / 2f) / PixelsPerMeter,
            (rect.Top + height / 2f) / PixelsPerMeter);
        return true;
    }
}
