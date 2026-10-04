using Raylib_cs;
using SharpHook;
using SharpHook.Data;
//using SharpHook.Native;
using System;
using System.Collections.Generic;

using System.Numerics;
using System.Threading.Tasks;
using MouseButton = SharpHook.Data.MouseButton;
using Point = System.Drawing.Point;


class Program
{
    private static volatile bool _isRunning = true;

    // Список всех созданных ящиков
    private static readonly List<Box> _boxes = new List<Box>();
    private static readonly object _lock = new object();

    // Состояние мыши и захвата
    private static int _mouseX = 0;
    private static int _mouseY = 0;
    private static int _lastMouseX = 0;
    private static int _lastMouseY = 0;
    private static Box? _draggedBox = null;

    // Размеры ящика по умолчанию для проверки клика (хитбокс)
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









        Console.WriteLine("=== ФИЗИКА ЯЩИКОВ В КОНСОЛИ ===");
        Console.WriteLine(" Нажмите [C] — Создать ящик под курсором");
        Console.WriteLine(" Зажмите [ЛКМ] на ящике — Схватить и тащить");
        Console.WriteLine(" Резко отпустите [ЛКМ] — Кинуть ящик");
        Console.WriteLine(" Нажмите [Escape] — Выход");
        Console.WriteLine("--------------------------------------------------");

        using var hook = new SimpleGlobalHook();

        hook.KeyPressed += OnKeyPressed;
        hook.MouseMoved += OnMouseMoved;
        hook.MouseDragged += OnMouseMoved; // Важно: обрабатывать движение с зажатой кнопкой
        hook.MousePressed += OnMousePressed;
        hook.MouseReleased += OnMouseReleased;

        var hookTask = hook.RunAsync();

        // ЦЕНТРАЛЬНЫЙ ФИЗИЧЕСКИЙ ЦИКЛ (Обновление со скоростью ~60 FPS)
        float dt = 0.016f; // Шаг времени (16 мс)
        while (_isRunning)
        {
            UpdatePhysics(dt);
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


        Console.WriteLine("\n[СИСТЕМА] Завершение работы...");
        hook.Dispose();
        await hookTask;
    }

    // 1. УПРАВЛЕНИЕ КЛАВИАТУРОЙ
    private static void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (e.Data.KeyCode == KeyCode.VcEscape)
        {
            _isRunning = false;
            return;
        }

        // Создание ящика по нажатию клавиши C
        if (e.Data.KeyCode == KeyCode.VcC)
        {
            lock (_lock)
            {
                _boxes.Add(new Box(_mouseX, _mouseY));
                Console.WriteLine($"[ИГРА] Создан ящик #{_boxes.Count} в координатах ({_mouseX}, {_mouseY})");
            }
        }
    }
    // 2. НАЖАТИЕ МЫШИ (ХВАТАЕМ ЯЩИК + БЛОКИРУЕМ ДЛЯ ОС)
    private static void OnMousePressed(object? sender, MouseHookEventArgs e)
    {
        if (e.Data.Button == MouseButton.Button1) // Левая кнопка мыши
        {
            lock (_lock)
            {
                // Ищем, попал ли клик в какой-нибудь ящик
                for (int i = _boxes.Count - 1; i >= 0; i--)
                {
                    var box = _boxes[i];
                    if (e.Data.X >= box.X - BoxSize / 2 && e.Data.X <= box.X + BoxSize / 2 &&
                        e.Data.Y >= box.Y - BoxSize / 2 && e.Data.Y <= box.Y + BoxSize / 2)
                    {
                        _draggedBox = box;
                        _draggedBox.IsDragged = true;
                        _draggedBox.Vx = 0;
                        _draggedBox.Vy = 0;

                        // ВАЖНО: Блокируем клик, чтобы ОС и другие окна его "не услышали"
                        e.SuppressEvent = true;

                        Console.WriteLine($"[ХВАТ] Ящик захвачен! Клик заблокирован для ОС в ({box.X}, {box.Y})");
                        break;
                    }
                }
            }
        }
    }

    // 3. ОТЖАТИЕ МЫШИ (КИДАЕМ ЯЩИК + СНИМАЕМ БЛОКИРОВКУ)
    private static void OnMouseReleased(object? sender, MouseHookEventArgs e)
    {
        if (e.Data.Button == MouseButton.Button1 && _draggedBox != null)
        {
            lock (_lock)
            {
                // Рассчитываем вектор броска
                float throwForceX = (_mouseX - _lastMouseX) * 50;
                float throwForceY = (_mouseY - _lastMouseY) * 50;

                _draggedBox.Vx = throwForceX;
                _draggedBox.Vy = throwForceY;
                _draggedBox.IsDragged = false;

                // ВАЖНО: Блокируем отпускание кнопки, так как само нажатие было скрыто от ОС
                e.SuppressEvent = true;

                Console.WriteLine($"[БРОСОК] Ящик отпущен. Блокировка мыши снята. Скорость: Vx={_draggedBox.Vx:F0}, Vy={_draggedBox.Vy:F0}");
                _draggedBox = null;
            }
        }
    }

    // 4. ДВИЖЕНИЕ МЫШИ (БЛОКИРУЕМ ПЕРЕМЕЩЕНИЕ ДЛЯ ОС ПРИ ПЕРЕТАСКИВАНИИ)
    private static void OnMouseMoved(object? sender, MouseHookEventArgs e)
    {
        // Если в данный момент мы тащим ящик, блокируем перемещение курсора для остальной системы
        /*
        if (_draggedBox != null)
        {
            e.SuppressEvent = true;
        }
        */

        // Сохраняем координаты для расчета импульса
        _lastMouseX = _mouseX;
        _lastMouseY = _mouseY;

        _mouseX = e.Data.X;
        _mouseY = e.Data.Y;

        if (_draggedBox != null)
        {
            _draggedBox.X = _mouseX;
            _draggedBox.Y = _mouseY;
        }

        Console.Title = $"Мышь: X={_mouseX}, Y={_mouseY} | Ящиков: {_boxes.Count} | Заблокировано: {_draggedBox != null}";
    }


    // 5. ДВИЖОК ФИЗИКИ (РАБОТАЕТ В ЦЕНТРАЛЬНОМ ЦИКЛЕ)
    private static void UpdatePhysics(float dt)
    {
        const float Gravity = 980;     // Сила гравитации (пиксели/сек^2)
        const float Friction = 1.00f;    // Сопротивление воздуха (затухание)
        const int GroundY = 500;        // Условный пол (высота экрана)

        lock (_lock)
        {
            foreach (var box in _boxes)
            {
                if (box.IsDragged) continue; // Удержание мыши отключает физику

                // Применяем гравитацию и сопротивление воздуха
                box.Vy += Gravity * dt;
                box.Vx *= Friction;
                box.Vy *= Friction;

                // Двигаем объект
                box.X += (int)(box.Vx * dt);
                box.Y += (int)(box.Vy * dt);

                // Столкновение с полом
                if (box.Y >= GroundY)
                {
                    box.Y = GroundY;
                    box.Vy = -box.Vy * 1.0f; // Отскок от пола с потерей энергии (0.5)
                    box.Vy -= Gravity * dt;

                    // Трение о пол
                    box.Vx *= 0.8f;
                }
            }
        }
    }

    // Класс описания ящика
    class Box
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Vx { get; set; } // Скорость по X
        public float Vy { get; set; } // Скорость по Y
        public bool IsDragged { get; set; } // Удерживается ли мышью в данный момент

        public Box(float x, float y)
        {
            X = x;
            Y = y;
            Vx = 0;
            Vy = 0;
            IsDragged = false;
        }
    }
}
