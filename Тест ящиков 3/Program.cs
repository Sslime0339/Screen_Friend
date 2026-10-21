using Genbox.VelcroPhysics.Dynamics;
using Genbox.VelcroPhysics.Factories;
using Raylib_cs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

// Псевдонимы типов для исключения конфликтов векторов
using SysVector = System.Numerics.Vector2;
using XnaVector = Microsoft.Xna.Framework.Vector2;

// Точка входа и общие поля/объявления WinAPI.
// Остальная реализация класса Program находится в других partial-файлах.
internal static partial class Program
{
    // --- WinAPI для работы с окнами ---
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

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out bool pvAttribute, int cbAttribute);

    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point lpPoint);

    private struct Point { public int X; public int Y; }

    // WinAPI для глобального отслеживания клавиш и мыши без хуков
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);






    // Подключаем функцию из системной библиотеки Windows (User32.dll)
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);




    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(int uiAction, int uiParam, ref RECT pvParam, int fWinIni);



    // --- Дополнительное WinAPI для разворачивания ---
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9; // Константа для восстановления окна из свернутого состояния
    private const int SW_SHOWNOACTIVATE = 4;

    /*
    // для установки флага чтобы программа не сворачивалась
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    */








    private const int VK_ESCAPE = 0x1B;
    private const int VK_C = 0x43;
    private const int VK_X = 0x58; // Код клавиши X для очистки
    private const int VK_LBUTTON = 0x01; // Левая кнопка мыши

    // --- Настройки физики и экрана ---
    const float PixelsPerMeter = 50f; // 50 пикселей = 1 метр
    private const float BoxSizeMeters = 1.2f; // Размер ящика в метрах

    private static volatile bool _isRunning = true;
    private static World _world;

    // Списки физических тел
    private static readonly List<Body> _boxes = new List<Body>();
    private static readonly List<Body> _windowColliders = new List<Body>(); // Физические тела для окон Windows
    private static readonly object _lock = new object();

    // Состояние мыши и захвата
    private static long _lastMoveTimestamp = 0;
    private static int _mouseX = 0;
    private static int _mouseY = 0;
    private static int _lastMouseX = 0;
    private static int _lastMouseY = 0;
    private static Body? _draggedBox = null;

    // Состояния для ручной обработки нажатий (эмуляция событий Pressed/Released)
    private static bool _wasCPressed = false;
    private static bool _wasXPressed = false; // Состояние для клавиши X
    private static bool _wasLButtonPressed = false;

    // Камера для конвертации координат
    private static Camera2D _camera;
}
