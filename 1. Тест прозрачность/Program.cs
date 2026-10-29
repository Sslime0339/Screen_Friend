using System;
using System.Runtime.InteropServices;
using System.Numerics;
using Raylib_cs;

class Program
{
    // Импортируем функции Windows для работы с окнами
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    // Структура для хранения координат окна Windows
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
        Raylib.SetConfigFlags(
            ConfigFlags.UndecoratedWindow |
            ConfigFlags.TransparentWindow |
            ConfigFlags.MousePassthroughWindow
        );

        int monitor = Raylib.GetCurrentMonitor();
        // По умолчанию рисовать можно везде (весь экран)
        int screenWidth = Raylib.GetMonitorWidth(monitor);
        int screenHeight = Raylib.GetMonitorHeight(monitor) - 60;


        Raylib.InitWindow(screenWidth, screenHeight, "Desktop Sprite");
        Raylib.SetTargetFPS(60);

        Vector2 position = new Vector2(300, 300);
        float speed = 200.0f;
        int radius = 30;

        while (!Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();
            if (Raylib.IsKeyDown(KeyboardKey.Right)) position.X += speed * deltaTime;
            if (Raylib.IsKeyDown(KeyboardKey.Left)) position.X -= speed * deltaTime;
            if (Raylib.IsKeyDown(KeyboardKey.Down)) position.Y += speed * deltaTime;
            if (Raylib.IsKeyDown(KeyboardKey.Up)) position.Y -= speed * deltaTime;

            Raylib.BeginDrawing();

            // ИСПРАВЛЕНО: Очищаем фон абсолютно прозрачным цветом вместо черного
            Raylib.ClearBackground(Color.Blank);


            IntPtr activeWindow = GetForegroundWindow();

            // ИСПРАВЛЕНО: Объявляем переменную rect заранее или прямо здесь, 
            // так как в условии выше (которое мы удалили) её больше нет.
            if (activeWindow != IntPtr.Zero && GetWindowRect(activeWindow, out RECT rect))
            {
                // Если персонаж подошел к левой границе окна справа
                if (position.X > rect.Right)
                {
                    // Разрешаем рисовать только СПРАВА от окна (от rect.Right до конца экрана)
                    Raylib.BeginScissorMode(rect.Right, 0, screenWidth - rect.Right, screenHeight);
                }
                // Если персонаж подошел к правой границе окна слева
                else if (position.X < rect.Left)
                {
                    // Разрешаем рисовать только СЛЕВА от окна (от 0 до rect.Left)
                    Raylib.BeginScissorMode(0, 0, rect.Left, screenHeight);
                }
                else
                {
                    // Если он полностью внутри координат окна — не рисуем его
                    Raylib.BeginScissorMode(0, 0, 0, 0);
                }
            }
            else
            {
                // Если окон нет, рисуем на весь экран
                Raylib.BeginScissorMode(0, 0, screenWidth, screenHeight);
            }

            // Отрисовка персонажа (она будет автоматически "обрезаться" по границам Scissor)
            Raylib.DrawCircleV(position, radius, Color.Red);

            Raylib.EndScissorMode(); // Обязательно закрываем режим обрезки
            Raylib.EndDrawing();
        }
        Raylib.CloseWindow();
    }
}
