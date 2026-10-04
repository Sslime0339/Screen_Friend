
using Raylib_cs;
using SharpHook.Data;
//using SharpHook.Native;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Threading.Tasks;
using Color = Raylib_cs.Color;
using MouseButton = SharpHook.Data.MouseButton;





namespace Тест_с_ящиками_2
{
    class Program
    {










        private static bool _isRunning = true;
        private static readonly List<Box> _boxes = new List<Box>();
        private static Box? _draggedBox = null;
        private const int BoxSize = 60;

        static async Task Main(string[] args)
        {






            // 1. Устанавливаем флаг поддержки прозрачности окна ДО инициализации
            Raylib.SetConfigFlags(
                ConfigFlags.MousePassthroughWindow |
                ConfigFlags.UndecoratedWindow |
                ConfigFlags.TransparentWindow |
                ConfigFlags.TopmostWindow |
                ConfigFlags.MaximizedWindow
                );



            // Инициализация окна: ширина, высота, заголовок
            Raylib.InitWindow(1600, 850, "Привет Raylib-cs!");
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
















            Console.WriteLine("=== АРХИТЕКТУРА С ЦЕНТРАЛЬНЫМ ЦИКЛОМ ВВОДА ===");
            Console.WriteLine(" Нажмите [C] — Создать ящик под курсором");
            Console.WriteLine(" Зажмите [ЛКМ] на ящике — Схватить и тащить");
            Console.WriteLine(" Резко отпустите [ЛКМ] — Кинуть ящик");
            Console.WriteLine(" Нажмите [Escape] — Выход");
            Console.WriteLine("--------------------------------------------------");

            // Инициализируем наш менеджер ввода
            using var input = new InputManager();

            double dt = 0.016; // Шаг времени (16 мс = ~60 FPS)

            // ГЛАВНЫЙ ЦИКЛ ПРИЛОЖЕНИЯ
            while (_isRunning)
            {
                input.UpdateMouseHistory();

                // Если ящик НЕ удерживается, менеджер ввода готов перехватить следующий клик
                if (_draggedBox == null)
                {
                    input.SuppressMouseClicks = true;
                }

                while (input.PollEvent(out InputEvent ev))
                {
                    ProcessInputEvent(ev, input);
                }

                if (_draggedBox != null)
                {
                    _draggedBox.X = input.MouseX;
                    _draggedBox.Y = input.MouseY;
                }

                UpdatePhysics(dt);

                Console.Title = $"Курсор: X={input.MouseX}, Y={input.MouseY} | Ящиков: {_boxes.Count} | Перетаскивание: {_draggedBox != null}";







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


                Raylib.DrawCircle(0, 0, 50, Color.Maroon);
                Raylib.DrawCircle(1600, 0, 50, Color.Maroon);
                Raylib.DrawCircle(0, 900, 50, Color.Maroon);
                Raylib.DrawCircle(400, 300, 50, Color.Maroon);




                foreach (var box in _boxes)
                {
                    Raylib.DrawRectangle((int)box.X - BoxSize / 2, (int)box.Y - BoxSize / 2, BoxSize, BoxSize, Color.Brown);

                    //Console.WriteLine(box.Y);dwasda
                }












                Point p = new Point();
                //GetCursorPos(ref p);

                Raylib.DrawTextEx(
                    myFont,
                    $"Позиция курсора {p}, дельта ",
                    new Vector2(50, 100),
                    24, // Размер шрифта
                    2,  // Расстояние между буквами (spacing)
                    Color.Black);




                Raylib.DrawTextEx(
                    myFont,
                    "Поздравляем! Ваше первое окно на Raylib-cs!",
                    new Vector2(50, 125),
                    24, // Размер шрифта
                    2,  // Расстояние между буквами (spacing)
                    Color.Black);




                Raylib.EndDrawing();

                //await Task.Delay(16);
            }




            // 5. Выгрузка шрифта из памяти при выходе
            Raylib.UnloadFont(myFont);
            // Закрытие окна и освобождение ресурсов
            Raylib.CloseWindow();

            Console.WriteLine("\n[СИСТЕМА] Приложение успешно закрыто.");




        }

        private static void ProcessInputEvent(InputEvent ev, InputManager input)
        {
            switch (ev.Type)
            {
                case InputEventType.KeyPressed:
                    if (ev.KeyCode == KeyCode.VcEscape) _isRunning = false;
                    else if (ev.KeyCode == KeyCode.VcC)
                    {
                        _boxes.Add(new Box(input.MouseX, input.MouseY));
                        Console.WriteLine($"[ИГРА] Создан ящик #{_boxes.Count} в ({input.MouseX}, {input.MouseY})");
                    }
                    break;

                case InputEventType.MousePressed:
                    if (ev.Button == MouseButton.Button1)
                    {
                        bool hitBox = false;
                        for (int i = _boxes.Count - 1; i >= 0; i--)
                        {
                            var box = _boxes[i];
                            if (ev.X >= box.X - BoxSize / 2 && ev.X <= box.X + BoxSize / 2 &&
                                ev.Y >= box.Y - BoxSize / 2 && ev.Y <= box.Y + BoxSize / 2)
                            {
                                _draggedBox = box;
                                _draggedBox.IsDragged = true;
                                _draggedBox.Vx = 0;
                                _draggedBox.Vy = 0;
                                hitBox = true;

                                // Оставляем блокировку включенной — мы тащим ящик
                                input.SuppressMouseClicks = true;
                                Console.WriteLine($"[ХВАТ] Клик заблокирован. Тащим ящик...");
                                break;
                            }
                        }

                        // Если промахнулись по всем ящикам, снимаем блокировку с менеджера ввода
                        if (!hitBox)
                        {
                            input.SuppressMouseClicks = false;
                        }
                    }
                    break;

                case InputEventType.MouseReleased:
                    if (ev.Button == MouseButton.Button1 && _draggedBox != null)
                    {
                        double throwForceX = (input.MouseX - input.LastMouseX) * 50;
                        double throwForceY = (input.MouseY - input.LastMouseY) * 50;

                        _draggedBox.Vx = throwForceX;
                        _draggedBox.Vy = throwForceY;
                        _draggedBox.IsDragged = false;

                        // Ящик отпущен, выключаем блокировку кликов
                        input.SuppressMouseClicks = false;

                        Console.WriteLine($"[БРОСОК] Ящик запущен. Клики разблокированы.");
                        _draggedBox = null;
                    }
                    break;
            }
        }

        private static void UpdatePhysics(double dt)
        {
            const double Gravity = 980;
            const double Friction = 0.98;
            const int GroundY = 800;

            foreach (var box in _boxes)
            {
                if (box.IsDragged) continue;

                box.Vy += Gravity * dt;
                box.Vx *= Friction;
                box.Vy *= Friction;

                box.X += (int)(box.Vx * dt);
                box.Y += (int)(box.Vy * dt);

                if (box.Y >= GroundY)
                {
                    box.Y = GroundY;
                    box.Vy = -box.Vy * 0.5;
                    box.Vx *= 0.8;
                }
            }
        }
    }

    // Класс данных ящика
    class Box
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Vx { get; set; }
        public double Vy { get; set; }
        public bool IsDragged { get; set; }

        public Box(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

}
