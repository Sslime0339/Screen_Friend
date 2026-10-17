using Raylib_cs;
using SharpHook;
using SharpHook.Data;
//using SharpHook.Native;
using System;
using System.Collections.Generic;

using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using MouseButton = SharpHook.Data.MouseButton;
using Point = System.Drawing.Point;


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





    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out bool pvAttribute, int cbAttribute);

    /*
    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    */




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
            ConfigFlags.TopmostWindow //|
            //ConfigFlags.FullscreenMode
            );

        

        // Инициализация окна: ширина, высота, заголовок
        Raylib.InitWindow(1600, 860, "Привет Raylib-cs!");
        Raylib.SetTargetFPS(2);

        unsafe
        {
            IntPtr RaylibWindowPtr = (IntPtr)Raylib.GetWindowHandle();
        }




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





        // 1. Загружаем ВСЕ кадры GIF в одну структуру Image (кадры идут друг за другом по вертикали)
        Image imBoxAnim = Raylib.LoadImageAnim("resources/box.gif", out int framesBox);

        int frameWidth = imBoxAnim.Width;
        int frameHeight = imBoxAnim.Height;

        // 2. Создаем текстуру на основе первого кадра
        Texture2D[] texBoxAnim = new Texture2D[framesBox];

        // Вычисляем адрес памяти, где начинается следующий кадр в imGifAnim.Data
        // Формат GIF в raylib всегда загружается как RGBA (4 байта на пиксель)
        int frameSize = imBoxAnim.Width * imBoxAnim.Height * 4;

        for (int i = 0; i < framesBox; i++)
        {
            unsafe
            {
                byte* basePtr = (byte*)imBoxAnim.Data;

                // Вырезаем каждый кадр отдельно
                Image singleFrameImg = new Image
                {
                    Data = basePtr + (i * frameSize),
                    Width = frameWidth,
                    Height = frameHeight,
                    Mipmaps = 1,
                    Format = PixelFormat.UncompressedR8G8B8A8
                };

                // Загружаем кадр прямиком в VRAM видеокарты
                texBoxAnim[i] = Raylib.LoadTextureFromImage(singleFrameImg);
            }
        }

        // 1. Исходный размер кадра (внутри самого GIF / текстуры)
        Rectangle sourceRec = new Rectangle(0.0f, 0.0f, texBoxAnim[0].Width, texBoxAnim[0].Height);
        

        // --- НАСТРОЙКИ ВРЕМЕНИ АНИМАЦИИ ---
        int currentAnimFrame = 0;
        float timeCount = 0.0f;           // Переменная для накопления прошедшего времени
        float frameDuration = 0.02f;       // Длительность ОДНОГО кадра GIF в секундах (0.1с = 100мс)





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
        //float dt = 0.016f; // Шаг времени (16 мс)
        while (_isRunning)
        {
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
            Raylib.DrawCircle(1600, 900, 50, Color.Maroon);


            Raylib.DrawCircle(200, 860, 50, Color.Maroon);








            /*

            List<string> windowList = new List<string>();

            EnumWindows((hWnd, lParam) =>
            {
                // Проверка на свёрнуто ли окно
                bool isNotMinimized = !IsIconic(hWnd);

                //int GWL_STYLE = -16;
                //uint WS_MAXIMIZE = 0x01000000;
                
                
                //int style = GetWindowLong(hWnd, GWL_STYLE);
                //bool IsWindowMaximized = (style & WS_MAXIMIZE) == WS_MAXIMIZE;

                bool IsWindowMaximized = IsZoomed(hWnd);

                int width = 0;
                int height = 0;

                int x = 0;
                int y = 0;

                // Использование:
                if (GetExactWindowRect(hWnd, out RECT rect) && IsWindowVisible(hWnd) && isNotMinimized)
                {
                    width = rect.Right - rect.Left;
                    height = rect.Bottom - rect.Top;
                    x = rect.Left;
                    y = rect.Top;
                    //int x = rect.Left
                    //int y = 
                    //Console.WriteLine($"Размеры окна: {width}x{height}");
                    StringBuilder sb = new StringBuilder(256);
                    int length = GetWindowText(hWnd, sb, sb.Capacity);
                    string title = sb.ToString();

                    if (!((width == 1600 && height == 900) ||
                    (width == 1600 && height == 860)))
                    {

                        if (!IsWindowMaximized)
                            Raylib.DrawRectangle(x, y, width, height, new Color(10, 20, 255, 20));
                        //else
                        //Raylib.DrawRectangle(x, y, width, height, new Color(255, 20, 255, 200));

                        Console.WriteLine($"[{isNotMinimized}] [Zoom:{IsZoomed(hWnd)}] [{x}:{y}] [{width};{height}] {hWnd}: {title}");
                    }
                }



                if (IsWindowVisible(hWnd))
                {
                    //StringBuilder sb = new StringBuilder(256);
                    //int length = GetWindowText(hWnd, sb, sb.Capacity);
                    //string title = sb.ToString();

                    //if (!string.IsNullOrWhiteSpace(title))
                    //{
                        //windowList.Add($"[{isNotMinimized}] [Zoom:{IsZoomed(hWnd)}] [{x}:{y}] [{width};{height}] {hWnd}: {title}");

                    //}




                }
                return true;
            }, IntPtr.Zero);
            */
            /*
            foreach (var win in windowList)
            {
                Console.WriteLine(win);
            }
            */

            // Константа для проверки скрытых окон (DWMWA_CLOAKED)
            const int DWMWA_CLOAKED = 14;

            EnumWindows((hWnd, lParam) => {
                // 1. Базовая проверка видимости (WinAPI)
                if (!IsWindowVisible(hWnd)) return true;

                // 2. Проверка заголовка (убирает 80% системного мусора)
                StringBuilder sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString();
                if (string.IsNullOrWhiteSpace(title)) return true;

                // 3. Проверка на "Скрытые" (Cloaked) окна Windows 10/11 
                // (Убирает окна с других виртуальных столов и фоновые UWP-апплеты)
                DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out bool isCloaked, sizeof(bool));
                if (isCloaked) return true;

                // 4. Проверка геометрии окна
                if (!GetExactWindowRect(hWnd, out RECT rect)) return true;

                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;
                int x = rect.Left;
                int y = rect.Top;

                // Исключаем окна с нулевым или слишком маленьким размером
                if (width <= 16 || height <= 16) return true;

                // Исключаем окна, улетевшие за пределы экрана (координаты -32000)
                if (x < -10000 || y < -10000) return true;

                // 5. Ваша кастомная фильтрация размеров (из прошлого шага)
                bool isTargetSize1 = (width == 1600 && height == 900);
                bool isTargetSize2 = (width == 1600 && height == 860);

                if (!isTargetSize1 && !isTargetSize2)
                {
                    bool isNotMinimized = !IsIconic(hWnd);
                    bool IsWindowMaximized = IsZoomed(hWnd);

                    if (!IsWindowMaximized && isNotMinimized)
                    {
                        Raylib.DrawRectangle(x, y, width, height, new Color(10, 20, 255, 50));
                    }

                    Console.WriteLine($"[{isNotMinimized}] [Zoom:{IsWindowMaximized}] [{x}:{y}] [{width};{height}] {hWnd}: {title}");
                }

                return true;
            }, IntPtr.Zero);













            // 1. Получаем Delta Time и накапливаем время
            float deltaTime = Raylib.GetFrameTime();
            timeCount += deltaTime;

            UpdatePhysics(deltaTime);


            // 2. Используем WHILE вместо IF. 
            // Если игра лагала и накопилось много времени, цикл выполнится несколько раз за один кадр игры
            while (timeCount >= frameDuration)
            {
                currentAnimFrame++;
                if (currentAnimFrame >= framesBox) currentAnimFrame = 0;

                // Вычитаем длительность кадра и проверяем, нужно ли шагнуть еще дальше
                timeCount -= frameDuration;

            }


            // Рисуем обновленную текстуру по центру экрана
            //int posX = Raylib.GetScreenWidth() / 2 - texBoxAnim.Width / 2;
            //int posY = Raylib.GetScreenHeight() / 2 - texBoxAnim.Height / 2;


            //Rectangle destRec = new Rectangle(posX, posY, 60, 60);

            // 3. Точка вращения/масштабирования (Vector2.Zero означает левый верхний угол destRec)
            Vector2 origin = new Vector2(0.0f, 0.0f);


            // Рисуем с измененным размером и углом поворота (0.0f)
            //Raylib.DrawTexturePro(texBoxAnim, sourceRec, destRec, origin, 0.0f, Color.White);

            Raylib.DrawText($"Кадр: {currentAnimFrame + 1}/{framesBox}", 10, 10, 20, Color.DarkGray);





            lock (_lock)
            {
                foreach (var box in _boxes)
                {
                    //Raylib.DrawRectangle((int)box.X - BoxSize / 2, (int)box.Y - BoxSize / 2, BoxSize, BoxSize, Color.Brown);

                    int posX = (int)box.X - BoxSize / 2;
                    int posY = (int)box.Y - BoxSize / 2;

                    Rectangle destRec = new Rectangle(posX, posY, BoxSize, BoxSize);
                    Raylib.DrawTexturePro(texBoxAnim[currentAnimFrame], sourceRec, destRec, origin, 0.0f, Color.White);
                    //Console.WriteLine(box.Y);dwasda
                }
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

            Raylib.DrawFPS(10, 10);


            Raylib.EndDrawing();

            //await Task.Delay(16);
        }


        // Очистка ресурсов из оперативной и видеопамяти
        //Raylib.UnloadTexture(texBoxAnim);
        for (int i = 0; i < framesBox; i++)
        {
            Raylib.UnloadTexture(texBoxAnim[i]);
        }
        Raylib.UnloadImage(imBoxAnim);

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
        dt = dt * 0.8f;
        const float Gravity = 980;     // Сила гравитации (пиксели/сек^2)
        const float Friction = 1.00f;    // Сопротивление воздуха (затухание)
        const int GroundY = 860;        // Условный пол (высота экрана)

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
                if (box.Y + BoxSize / 2 >= GroundY)
                {
                    box.Y = GroundY - BoxSize / 2;
                    box.Vy = -box.Vy * 1.0f; // Отскок от пола с потерей энергии (0.5)
                    //box.Y += (int)(box.Vy * dt);
                    //box.Vy -= Gravity * dt;

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
