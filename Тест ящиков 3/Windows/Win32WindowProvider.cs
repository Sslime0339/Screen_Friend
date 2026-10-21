
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

internal sealed class Win32WindowProvider
{
    private readonly object _snapshotLock = new();
    private WindowData[] _latestSnapshot = Array.Empty<WindowData>();

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

    public IReadOnlyList<WindowData> GetLatestSnapshot()
    {
        lock (_snapshotLock)
            return (WindowData[])_latestSnapshot.Clone();
    }

    public async Task RunScannerAsync(CancellationToken cancellationToken)
    {
        const int scanIntervalMs = 250;

        while (!cancellationToken.IsCancellationRequested)
        {
            var workArea = GetWorkArea();
            int workWidth = workArea.Right - workArea.Left;
            int workHeight = workArea.Bottom - workArea.Top;
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);
            var found = new List<WindowData>();

            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd) || IsIconic(hWnd) || IsZoomed(hWnd))
                    return true;

                var title = new StringBuilder(256);
                GetWindowText(hWnd, title, title.Capacity);
                if (string.IsNullOrWhiteSpace(title.ToString()))
                    return true;

                if (DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out bool cloaked, sizeof(bool)) == 0 && cloaked)
                    return true;

                if (DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT rect, Marshal.SizeOf<RECT>()) != 0)
                    return true;

                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;
                if (width <= 16 || height <= 16 || rect.Left < -10000 || rect.Top < -10000)
                    return true;

                bool isOverlaySize =
                    (width == workWidth && height == workHeight) ||
                    (width == screenWidth && height == screenHeight);

                if (!isOverlaySize)
                {
                    const float pixelsPerMeter = 50f;
                    found.Add(new WindowData(
                        hWnd,
                        width / pixelsPerMeter,
                        height / pixelsPerMeter,
                        (rect.Left + width / 2f) / pixelsPerMeter,
                        (rect.Top + height / 2f) / pixelsPerMeter));
                }

                return true;
            }, IntPtr.Zero);

            lock (_snapshotLock)
                _latestSnapshot = found.ToArray();

            await Task.Delay(scanIntervalMs, cancellationToken);
        }
    }
}
