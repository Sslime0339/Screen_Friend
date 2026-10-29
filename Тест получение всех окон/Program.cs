using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

struct WindowData
{
    public string Title;
    public IntPtr Handle;
    public int ZIndex;
    public bool IsMaximized;
    public float Width;
    public float Height;
    public float CenterX;
    public float CenterY;
}

class Program
{
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    // Новые импорты для фильтрации системных окон
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const int DWMWA_CLOAKED = 14;

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out bool pvAttribute, int cbAttribute);

    public static bool GetExactWindowRect(IntPtr hWnd, out RECT rect)
    {
        int result = DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf(typeof(RECT)));
        return result == 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private static bool _isRunning = true;
    private static object _lock = new object();
    private static float PixelsPerMeter = 1000f;

    static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        List<WindowData> windowList = new List<WindowData>();
        IntPtr shellWindow = GetShellWindow(); // Получаем дескриптор рабочего стола для фильтрации

        _ = Task.Run(async () =>
        {
            while (_isRunning)
            {
                List<WindowData> currentWindows = new List<WindowData>();
                int zCounter = 0;

                EnumWindows((hWnd, lParam) =>
                {
                    // 1. Базовая проверка видимости и свернутости
                    if (!IsWindowVisible(hWnd) || IsIconic(hWnd)) return true;

                    // 2. Исключаем Shell-окно (тот самый Program Manager)
                    if (hWnd == shellWindow) return true;

                    // 3. Получаем заголовок окна
                    StringBuilder sbTitle = new StringBuilder(256);
                    GetWindowText(hWnd, sbTitle, sbTitle.Capacity);
                    string title = sbTitle.ToString();
                    if (string.IsNullOrWhiteSpace(title)) return true;

                    // 4. Проверка на "скрытые" (Cloaked) окна (UWP-приложения в фоне)
                    DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out bool isCloaked, sizeof(bool));
                    if (isCloaked) return true;

                    // 5. Фильтрация по имени системного класса Windows
                    StringBuilder sbClass = new StringBuilder(256);
                    GetClassName(hWnd, sbClass, sbClass.Capacity);
                    string className = sbClass.ToString();

                    // Отсекаем служебные классы Windows, которые не должны быть в оверлее
                    if (className == "ApplicationFrameWindow" && title == "Настройки")
                    {
                        // Некоторые фоновые окна настроек UWP имеют видимый размер, но скрыты
                        // Дополнительная проверка на фантомность окна UWP
                    }
                    if (className == "Windows.UI.Core.CoreWindow" ||
                        className == "Progman" ||
                        className == "WorkerW") return true;

                    // 6. Исключаем Tool-окна (панели инструментов, скрытые фоновые утилиты)
                    int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
                    if ((exStyle & WS_EX_TOOLWINDOW) != 0) return true;

                    // 7. Получаем точные координаты и размеры окна
                    if (!GetExactWindowRect(hWnd, out RECT rect)) return true;

                    int width = rect.Right - rect.Left;
                    int height = rect.Bottom - rect.Top;
                    int x = rect.Left;
                    int y = rect.Top;

                    // Фильтрация невидимых окон нулевого размера или вынесенных далеко за экран
                    if (width <= 16 || height <= 16) return true;
                    if (x < -10000 || y < -10000) return true;

                    // Если все проверки пройдены — окно считается валидным и пользовательским
                    zCounter++;
                    bool isMaximized = IsZoomed(hWnd);

                    currentWindows.Add(new WindowData
                    {
                        Title = title,
                        Handle = hWnd,
                        ZIndex = zCounter,
                        IsMaximized = isMaximized,
                        Width = width / PixelsPerMeter,
                        Height = height / PixelsPerMeter,
                        CenterX = (x + width / 2f) / PixelsPerMeter,
                        CenterY = (y + height / 2f) / PixelsPerMeter
                    });

                    return true;
                }, IntPtr.Zero);

                lock (_lock)
                {
                    windowList = currentWindows;
                }

                await Task.Delay(100);
            }
        });

        // Поток вывода в консоль
        while (_isRunning)
        {
            Console.Clear();
            Console.WriteLine($"=== Список активных окон (Всего: {windowList.Count}) ===");
            Console.WriteLine($"{"Z-Index",-9} | {"На весь экран?",-15} | {"Размеры (М)",-15} | {"Заголовок"}");
            Console.WriteLine(new string('-', 80));

            List<WindowData> localList;
            lock (_lock)
            {
                localList = new List<WindowData>(windowList);
            }

            foreach (var win in localList)
            {
                string maximizedStatus = win.IsMaximized ? "ДА" : "Нет";
                string sizeStr = $"{win.Width:F2}x{win.Height:F2}";

                Console.WriteLine($"{win.ZIndex,-9} | {maximizedStatus,-15} | {sizeStr,-15} | {win.Title}");
            }

            await Task.Delay(1000);
        }
    }
}
