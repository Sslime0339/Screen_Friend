using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;





struct WindowData
{
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

    // Возвращает true, если окно развёрнуто на весь экран
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    //[DllImport("user32.dll")]
    //[return: MarshalAs(UnmanagedType.Bool)]
    //private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);



    // Константа, указывающая, что нам нужны точные видимые границы окна
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    // Импорт функции из dwmapi.dll
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        out RECT pvAttribute,
        int cbAttribute
    );

    // Удобный метод-обёртка
    public static bool GetExactWindowRect(IntPtr hWnd, out RECT rect)
    {
        // Передаем размер структуры в байтах (cbAttribute)
        int result = DwmGetWindowAttribute(
            hWnd,
            DWMWA_EXTENDED_FRAME_BOUNDS,
            out rect,
            Marshal.SizeOf(typeof(RECT))
        );

        // В WinAPI 0 (S_OK) означает успех
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




    static void Main()
    {

        List<string> windowList = new List<string>();


        // Запускаем фоновую задачу бесконечного сканирования окон ОС
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
                    string title = sb.ToString();
                    if (string.IsNullOrWhiteSpace(title)) return true;

                    DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out bool isCloaked, sizeof(bool));
                    if (isCloaked) return true;

                    if (!GetExactWindowRect(hWnd, out RECT rect)) return true;

                    int width = rect.Right - rect.Left;
                    int height = rect.Bottom - rect.Top;
                    int x = rect.Left;
                    int y = rect.Top;

                    if (width <= 16 || height <= 16) return true;
                    if (x < -10000 || y < -10000) return true;

                    // Исключаем само окно оверлея
                    if (!((width == workAreaWidth && height == workAreaHeight) || (width == screenWidth && height == screenHeight)))
                    {
                        if (!IsZoomed(hWnd) && !IsIconic(hWnd))
                        {
                            currentWindows.Add(new WindowData
                            {
                                Width = width / PixelsPerMeter,
                                Height = height / PixelsPerMeter,
                                CenterX = (x + width / 2f) / PixelsPerMeter,
                                CenterY = (y + height / 2f) / PixelsPerMeter
                            });
                        }
                    }
                    return true;
                }, IntPtr.Zero);

                // Быстро и безопасно переносим данные в основной поток
                lock (_lock)
                {
                    windowList = currentWindows;
                }

                // Пауза 100 мс (хватит для плавной физики окон, разгрузит процессор на 99%)
                await Task.Delay(100);
            }
        });



        /*
        EnumWindows((hWnd, lParam) =>
        {
            // Проверка на свёрнуто ли окно
            bool isNotMinimized = !IsIconic(hWnd);

            int width = 0;
            int height = 0;

            int x = 0;
            int y = 0;

            // Использование:
            if (GetExactWindowRect(hWnd, out RECT rect))
            {
                width = rect.Right - rect.Left;
                height = rect.Bottom - rect.Top;
                x = rect.Left;
                y = rect.Top;
                //int x = rect.Left
                //int y = 
                //Console.WriteLine($"Размеры окна: {width}x{height}");
            }



            if (IsWindowVisible(hWnd))
            {
                StringBuilder sb = new StringBuilder(256);
                int length = GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString();

                if (!string.IsNullOrWhiteSpace(title))
                {
                    windowList.Add($"[{isNotMinimized}] [Zoom:{IsZoomed(hWnd)}] [{x}:{y}] [{width};{height}] {hWnd}: {title}");

                }
            }
            return true;
        }, IntPtr.Zero);

        
        foreach (var win in windowList)
        {
            Console.WriteLine(win);
        }*/
        
    }


}
