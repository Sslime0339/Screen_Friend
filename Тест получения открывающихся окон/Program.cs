using System;
using System.Runtime.InteropServices;
using System.Text;

class Program
{
    // МЕНЯЕМ НА EVENT_OBJECT_SHOW (0x8002) - окно уже показано и готово передать свой заголовок
    private const uint EVENT_OBJECT_SHOW = 0x8002;
    private const uint WINEVENT_OUTOFCONTEXT = 0;
    private const int OBJID_WINDOW = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public System.Drawing.Point pt;
    }

    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    private static WinEventDelegate _procDelegate = new WinEventDelegate(WinEventProc);
    private static IntPtr _hookHandle;

    static void Main(string[] args)
    {
        // Регистрируем хук на SHOW вместо CREATE
        _hookHandle = SetWinEventHook(EVENT_OBJECT_SHOW, EVENT_OBJECT_SHOW, IntPtr.Zero, _procDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);

        if (_hookHandle == IntPtr.Zero)
        {
            Console.WriteLine("Ошибка: не удалось установить хук!");
            return;
        }

        Console.WriteLine("Хук успешно установлен. Откройте Блокнот или браузер для проверки...");

        MSG msg;
        while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        if (_hookHandle != IntPtr.Zero)
            UnhookWinEvent(_hookHandle);
    }

    private static void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // Проверяем, что это окно
        if (idObject == OBJID_WINDOW)
        {
            StringBuilder title = new StringBuilder(256);
            GetWindowText(hwnd, title, 256);

            // Выводим вообще все окна, у которых есть заголовок, чтобы проверить работу хука
            if (title.Length > 0)
            {
                Console.WriteLine($"[Окно обнаружено] HWND = {hwnd.ToInt64():X}, Заголовок = \"{title}\"");
            }
        }
    }
}
