using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

// Расширенная структура для хранения данных об окне
struct WindowData
{
    public string Title;
    public IntPtr Handle;
    public int ZIndex; // 1 — самое верхнее окно, 2 — под ним и так далее
    public bool IsMaximized;
    public float Width;
    public float Height;
    public float CenterX;
    public float CenterY;
}

class Program
{
    // Сигнатура делегата для перечисления окон
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsZoomed(IntPtr hWnd); // True, если окно развернуто на весь экран

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd); // True, если окно свернуто

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

    // Управляющие переменные (заглушки из вашего кода для работоспособности)
    private static bool _isRunning = true;
    private static object _lock = new object();
    private static float PixelsPerMeter = 1000f; // Коэффициент перевода пикселей в "метры"

    static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        List<WindowData> windowList = new List<WindowData>();

        // Запускаем фоновую задачу сканирования окон ОС
        _ = Task.Run(async () =>
        {
            while (_isRunning)
            {
                List<WindowData> currentWindows = new List<WindowData>();
                int zCounter = 0; // Счетчик для определения Z-порядка

                EnumWindows((hWnd, lParam) =>
                {
                    // 1. Базовые проверки на видимость
                    if (!IsWindowVisible(hWnd)) return true;
                    if (IsIconic(hWnd)) return true; // Пропускаем свернутые

                    // 2. Получаем заголовок окна
                    StringBuilder sb = new StringBuilder(256);
                    GetWindowText(hWnd, sb, sb.Capacity);
                    string title = sb.ToString();
                    if (string.IsNullOrWhiteSpace(title)) return true;

                    // 3. Проверка на "скрытые" (Cloaked) окна Windows (например, окна на других виртуальных рабочих столах)
                    DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out bool isCloaked, sizeof(bool));
                    if (isCloaked) return true;

                    // 4. Получаем точные координаты
                    if (!GetExactWindowRect(hWnd, out RECT rect)) return true;

                    int width = rect.Right - rect.Left;
                    int height = rect.Bottom - rect.Top;
                    int x = rect.Left;
                    int y = rect.Top;

                    // Фильтрация системного мусора и фантомных окон
                    if (width <= 16 || height <= 16) return true;
                    if (x < -10000 || y < -10000) return true;

                    // Увеличиваем Z-индекс. Так как EnumWindows идет сверху вниз,
                    // первое валидное окно получит ZIndex = 1 (самое верхнее).
                    zCounter++;

                    // Проверяем, развернуто ли окно на весь экран
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

                // Синхронизируем и обновляем основной список
                lock (_lock)
                {
                    windowList = currentWindows;
                }

                await Task.Delay(100);
            }
        });

        // Поток для вывода информации в консоль
        while (_isRunning)
        {
            Console.Clear();
            Console.WriteLine($"=== Список активных окон (Всего: {windowList.Count}) ===");
            Console.WriteLine($"{"Z-Index",-9} | {"На весь экран?",-15} | {"Размеры (М)",-15} | {"Заголовок"}");
            Console.WriteLine(new string('-', 80));

            List<WindowData> localList;
            lock (_lock)
            {
                // Делаем копию для безопасного вывода без блокировки на время отрисовки
                localList = new List<WindowData>(windowList);
            }

            foreach (var win in localList)
            {
                string maximizedStatus = win.IsMaximized ? "ДА" : "Нет";
                string sizeStr = $"{win.Width:F2}x{win.Height:F2}";

                Console.WriteLine($"{win.ZIndex,-9} | {maximizedStatus,-15} | {sizeStr,-15} | {win.Title}");
            }

            // Обновляем отображение в консоли раз в секунду, чтобы она не мерцала слишком часто
            await Task.Delay(1000);
        }
    }
}
