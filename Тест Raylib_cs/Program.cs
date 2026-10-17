
using Raylib_cs;
using Point = System.Drawing.Point;

using System.Numerics;
using System.Runtime.InteropServices;


namespace Тест_Raylib_cs
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //[DllImport("user32.dll")]
            //static extern bool GetCursorPos(ref Point lpPoint);

            // 1. Устанавливаем флаг поддержки прозрачности окна ДО инициализации
            Raylib.SetConfigFlags(
                ConfigFlags.MousePassthroughWindow |
                ConfigFlags.UndecoratedWindow |
                ConfigFlags.TransparentWindow |
                ConfigFlags.TopmostWindow |
                ConfigFlags.FullscreenMode //|
                //ConfigFlags.UnfocusedWindow
                );



            // Инициализация окна: ширина, высота, заголовок
            Raylib.InitWindow(800, 480, "Привет Raylib-cs!");
            Raylib.SetTargetFPS(60);





            // 2. Генерация массива кодовых точек (Codepoints)
            // Нам нужны базовые символы (ASCII) и диапазон кириллицы
            int[] codepoints = new int[512];

            // Базовый ASCII (пробелы, цифры, английские буквы, знаки препинания)
            for (int i = 0; i < 95; i++) codepoints[i] = 32 + i;

            // Кириллица (основной блок Unicode: от 0x0400 до 0x04FF)
            for (int i = 0; i < 256; i++) codepoints[95 + i] = 0x0400 + i;

            // 3. Загрузка внешнего шрифта с поддержкой русского языка
            // Убедитесь, что файл шрифта находится в папке с проектом или укажите правильный путь
            Font myFont = Raylib.LoadFontEx("Arial.ttf", 32, codepoints, codepoints.Length);





            // Основной игровой цикл
            while (!Raylib.WindowShouldClose())
            {
                // 1. Обновление состояния игры (Логика)
                // TODO: Добавьте перемещение объектов, проверку коллизий и т.д.

                // 2. Отрисовка
                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.Blank);
                //Raylib.ClearBackground(Color.RayWhite);

                Raylib.DrawTextEx(
                    myFont, 
                    "Поздравляем! Ваше первое окно на Raylib-cs!",
                    new Vector2(50, 200),
                    24, // Размер шрифта
                    2,  // Расстояние между буквами (spacing)
                    Color.Black);
                Raylib.DrawCircle(400, 300, 50, Color.Maroon);


                Point p = new Point();
                //GetCursorPos(ref p);

                Raylib.DrawTextEx(
                    myFont,
                    $"Позиция курсора {p}, дельта ",
                    new Vector2(50, 100),
                    24, // Размер шрифта
                    2,  // Расстояние между буквами (spacing)
                    Color.Black);


                if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    Console.WriteLine("Нажата левая кнопка");
                }


                Raylib.DrawTextEx(
                    myFont,
                    "Поздравляем! Ваше первое окно на Raylib-cs!",
                    new Vector2(50, 125),
                    24, // Размер шрифта
                    2,  // Расстояние между буквами (spacing)
                    Color.Black);




                Raylib.EndDrawing();


                // Console.WriteLine("Тест");
            }



            // 5. Выгрузка шрифта из памяти при выходе
            Raylib.UnloadFont(myFont);
            // Закрытие окна и освобождение ресурсов
            Raylib.CloseWindow();
        }
    }
}
