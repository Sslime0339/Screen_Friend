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



struct WindowData
{
    public float Width;
    public float Height;
    public float CenterX;
    public float CenterY;
}





class Program
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

    static async Task Main(string[] args)
    {
        // 1. Настройка прозрачности и стиля окна Raylib
        Raylib.SetConfigFlags(
            ConfigFlags.MousePassthroughWindow |
            ConfigFlags.UndecoratedWindow |
            ConfigFlags.TransparentWindow |
            ConfigFlags.TopmostWindow
        );

        //int screenWidth = Raylib.GetMonitorWidth(Raylib.GetCurrentMonitor());
        //int screenHeight = Raylib.GetMonitorHeight(Raylib.GetCurrentMonitor()) - 40;


        const int SPI_GETWORKAREA = 48; // Код для получения рабочей области

        RECT workArea = new RECT();
        SystemParametersInfo(SPI_GETWORKAREA, 0, ref workArea, 0);

        // Вычисляем чистую высоту (Нижняя граница минус Верхняя граница)
        int workAreaHeight = workArea.Bottom - workArea.Top;
        int workAreaWidth = workArea.Right - workArea.Left;

        Console.WriteLine(workAreaHeight);
        Console.WriteLine(workAreaWidth);


        // Константы для ширины и высоты главного экрана
        const int SM_CXSCREEN = 0;
        const int SM_CYSCREEN = 1;
        // 1. Получаем размеры экрана ДО ИНИЦИАЛИЗАЦИИ Raylib
        int screenWidth = GetSystemMetrics(SM_CXSCREEN);
        int screenHeight = GetSystemMetrics(SM_CYSCREEN);


        //Console.WriteLine(Raylib.GetCurrentMonitor());

        //Console.WriteLine(screenWidth);
        //Console.WriteLine(screenHeight);

        Raylib.InitWindow(workAreaWidth, workAreaHeight, "Raylib-cs + Velcro Physics overlay");
        Raylib.SetTargetFPS(60);

        // 2. Инициализация физического мира Velcro (Гравитация вниз)
        _world = new World(new XnaVector(0f, 19.6f));

        // --- СОЗДАНИЕ ГРАНИЦ ЭКРАНА (СТЕНЫ И ПОЛ) ---
        float viewWidthMeters = workAreaWidth / PixelsPerMeter;
        float viewHeightMeters = workAreaHeight / PixelsPerMeter;
        float wallThickness = 5.0f;

        float groundHeightMeters = 5f;
        float groundYMeters = workAreaHeight / PixelsPerMeter + (groundHeightMeters / 2f);
        float groundWidthMeters = workAreaWidth / PixelsPerMeter;

        // Нижний пол
        Body ground = BodyFactory.CreateRectangle(
            _world,
            groundWidthMeters * 1.2f, groundHeightMeters,
            1f,
            new XnaVector(groundWidthMeters / 2f, groundYMeters)
        );
        ground.BodyType = BodyType.Static;
        ground.Friction = 0.6f;

        // Левая стена
        Body leftWall = BodyFactory.CreateRectangle(
            _world,
            wallThickness, viewHeightMeters * 8,
            1f,
            new XnaVector(-(wallThickness / 2f), viewHeightMeters / 2f)
        );
        leftWall.BodyType = BodyType.Static;
        leftWall.Friction = 0.4f;
        leftWall.Restitution = 0.3f;

        // Правая стена
        Body rightWall = BodyFactory.CreateRectangle(
            _world,
            wallThickness, viewHeightMeters * 8,
            1f,
            new XnaVector(viewWidthMeters + (wallThickness / 2f), viewHeightMeters / 2f)
        );
        rightWall.BodyType = BodyType.Static;
        rightWall.Friction = 0.4f;
        rightWall.Restitution = 0.3f;

        // 3. Настройка камеры Raylib
        _camera = new Camera2D
        {
            Target = SysVector.Zero,
            Offset = SysVector.Zero,
            Rotation = 0.0f,
            Zoom = PixelsPerMeter
        };

        // 4. Генерация кодовых точек для шрифта (Кириллица)
        int[] codepoints = new int[512];
        for (int i = 0; i < 95; i++) codepoints[i] = 32 + i;
        for (int i = 0; i < 256; i++) codepoints[95 + i] = 0x0400 + i;
        Font myFont = Raylib.LoadFontEx("Arial.ttf", 32, codepoints, codepoints.Length);

        int textOffset = 2;

        // 5. Загрузка GIF анимации
        Image imBoxAnim = Raylib.LoadImageAnim("resources/box.gif", out int framesBox);
        int frameWidth = imBoxAnim.Width;
        int frameHeight = imBoxAnim.Height;
        Texture2D[] texBoxAnim = new Texture2D[framesBox];
        int frameSize = imBoxAnim.Width * imBoxAnim.Height * 4;

        for (int i = 0; i < framesBox; i++)
        {
            unsafe
            {
                byte* basePtr = (byte*)imBoxAnim.Data;
                Image singleFrameImg = new Image
                {
                    Data = basePtr + (i * frameSize),
                    Width = frameWidth,
                    Height = frameHeight,
                    Mipmaps = 1,
                    Format = PixelFormat.UncompressedR8G8B8A8
                };
                texBoxAnim[i] = Raylib.LoadTextureFromImage(singleFrameImg);
            }
        }

        Rectangle sourceRec = new Rectangle(0.0f, 0.0f, texBoxAnim[0].Width, texBoxAnim[0].Height);
        int currentAnimFrame = 0;
        float timeCount = 0.0f;
        float frameDuration = 0.02f;

        InitializeMousePosition();




        // Список для обмена данными между потоками
        List<WindowData> pendingWindows = new List<WindowData>();

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
                    pendingWindows = currentWindows;
                }

                // Пауза 100 мс (хватит для плавной физики окон, разгрузит процессор на 99%)
                await Task.Delay(100);
            }
        });


        IntPtr windowHandle;
        // Получаем системный Handle нашего окна Raylib
        unsafe
        {
            windowHandle = (IntPtr)Raylib.GetWindowHandle();
        }


        // Фоновая задача на чистом WinAPI, работающая в обход замороженного потока Raylib
        _ = Task.Run(async () =>
        {
            while (_isRunning)
            {
                // Проверяем, свернуто ли наше окно оверлея
                if (IsIconic(windowHandle)) // Функция IsIconic уже есть в вашем коде!
                {
                    // Принудительно восстанавливаем окно силами Windows
                    ShowWindow(windowHandle, SW_RESTORE);
                }

                await Task.Delay(50); // Проверка каждые 50мс
            }
        });






        // Основной игровой цикл
        while (_isRunning && !Raylib.WindowShouldClose())
        {




            float dt = Raylib.GetFrameTime();


            // --- ОБРАБОТКА ГЛОБАЛЬНОГО ВВОДА ---
            UpdateInput();

            // --- ОБНОВЛЕНИЕ ФИЗИЧЕСКИХ ОКОН WINDOWS ---
            lock (_lock)
            {
                // 1. Удаляем старые коллайдеры
                foreach (var oldCollider in _windowColliders)
                {
                    _world.DestroyBody(oldCollider);
                }
                _windowColliders.Clear();

                // 2. Строим новые коллайдеры на основе данных из фонового потока
                foreach (var win in pendingWindows)
                {
                    Body windowBody = BodyFactory.CreateRectangle(
                        _world,
                        win.Width, win.Height,
                        1f,
                        new XnaVector(win.CenterX, win.CenterY)
                    );
                    windowBody.BodyType = BodyType.Static;
                    windowBody.Friction = 0.4f;
                    windowBody.Restitution = 0.2f;
                    _windowColliders.Add(windowBody);
                }

                // Шаг симуляции физики Velcro
                _world.Step(Math.Min(dt, 1f / 60f));
            }
            // Обновление кадров GIF-анимации
            timeCount += dt;
            while (timeCount >= frameDuration)
            {
                currentAnimFrame = (currentAnimFrame + 1) % framesBox;
                timeCount -= frameDuration;
            }
            // --- ОТРИСОВКА ---
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Blank);
            Raylib.BeginMode2D(_camera);
            lock (_lock)
            {

#if DEBUG
                // 2. Отрисовка в основном/графическом потоке
                foreach (var win in pendingWindows)
                {
                    // Вычисляем верхний левый угол на основе центра и размеров
                    float leftMeters = win.CenterX - (win.Width / 2f);
                    float topMeters = win.CenterY - (win.Height / 2f);

                    var rect = new Rectangle(leftMeters, topMeters, win.Width, win.Height);

                    // Рисуем полупрозрачное окно
                    Raylib.DrawRectangleRec(rect, new Color(10, 20, 255, 60));

                    // Рисуем контур окна
                    Raylib.DrawRectangleLinesEx(rect, 0.05f, new Color(10, 20, 255, 150));
                }

#endif

                // 2. Рисуем все динамические ящики
                foreach (var box in _boxes)
                {
                    float rotationInDegrees = box.Rotation * (180f / (float)Math.PI);
                    Rectangle destRec = new Rectangle(box.Position.X, box.Position.Y, BoxSizeMeters, BoxSizeMeters);
                    SysVector origin = new SysVector(BoxSizeMeters / 2f, BoxSizeMeters / 2f);
                    Raylib.DrawTexturePro(texBoxAnim[currentAnimFrame], sourceRec, destRec, origin, rotationInDegrees, Color.White);
                }
            }
            Raylib.EndMode2D();
            // Отрисовка UI текста
            Color textShadowColor = new Color(20, 20, 20, 255);
            //Raylib.DrawTextEx(myFont, "[C] - Создать коропку, [X] - Очистить всё, [ESC] - Закрыть программу", new SysVector(500 + textOffset, 10 + textOffset), 24, 2, Color.DarkGray);
            //Raylib.DrawTextEx(myFont, "[C] - Создать коропку, [X] - Очистить всё, [ESC] - Закрыть программу", new SysVector(500, 10), 24, 2, Color.Purple);
            DrawTextExWithShadow(myFont, "[C] - Создать коропку, [X] - Очистить всё, [ESC] - Закрыть программу", new SysVector(10, 830), 24, 2, Color.Purple, textShadowColor, textOffset);
            /*Raylib.DrawTextEx(myFont, $"Окон-препятствий: {_windowColliders.Count} | Коробок: {_boxes.Count}", new SysVector(10 + textOffset, 10), 24, 2, textColor);
            Raylib.DrawTextEx(myFont, $"Окон-препятствий: {_windowColliders.Count} | Коробок: {_boxes.Count}", new SysVector(10 - textOffset, 10), 24, 2, textColor);
            Raylib.DrawTextEx(myFont, $"Окон-препятствий: {_windowColliders.Count} | Коробок: {_boxes.Count}", new SysVector(10, 10 + textOffset), 24, 2, textColor);
            Raylib.DrawTextEx(myFont, $"Окон-препятствий: {_windowColliders.Count} | Коробок: {_boxes.Count}", new SysVector(10, 10 - textOffset), 24, 2, textColor);
            Raylib.DrawTextEx(myFont, $"Окон-препятствий: {_windowColliders.Count} | Коробок: {_boxes.Count}", new SysVector(10 + textOffset, 10 + textOffset), 24, 2, textColor);
            Raylib.DrawTextEx(myFont, $"Окон-препятствий: {_windowColliders.Count} | Коробок: {_boxes.Count}", new SysVector(10 - textOffset, 10 + textOffset), 24, 2, textColor);
            Raylib.DrawTextEx(myFont, $"Окон-препятствий: {_windowColliders.Count} | Коробок: {_boxes.Count}", new SysVector(10 + textOffset, 10 - textOffset), 24, 2, textColor);
            Raylib.DrawTextEx(myFont, $"Окон-препятствий: {_windowColliders.Count} | Коробок: {_boxes.Count}", new SysVector(10 - textOffset, 10 - textOffset), 24, 2, textColor);
            */
            DrawTextExWithShadow(myFont, $"Окон-препятствий: {_windowColliders.Count} | Коробок: {_boxes.Count}", new SysVector(10, 800), 24, 2, Color.Pink, textShadowColor, textOffset);
            Raylib.DrawFPS(10, 770);


            Raylib.EndDrawing();
        }
        // Очистка ресурсов
        for (int i = 0; i < framesBox; i++) Raylib.UnloadTexture(texBoxAnim[i]);
        Raylib.UnloadImage(imBoxAnim);
        Raylib.UnloadFont(myFont);
        Raylib.CloseWindow();
        _isRunning = false;
    }


    // отрисовка текста с тенью

    public static void DrawTextExWithShadow(
        Font font,
        string text,
        SysVector position,
        float fontSize,
        float spacing,
        Color textColor,
        Color shadowColor,
        int offset)
    {
        // 1. Рисуем тень
        //Raylib.DrawTextEx(font, text, position + offset, fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(font, text, new SysVector(position.X + offset, position.Y), fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(font, text, new SysVector(position.X - offset, position.Y), fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(font, text, new SysVector(position.X, position.Y + offset), fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(font, text, new SysVector(position.X, position.Y - offset), fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(font, text, new SysVector(position.X + offset, position.Y + offset), fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(font, text, new SysVector(position.X - offset, position.Y + offset), fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(font, text, new SysVector(position.X + offset, position.Y - offset), fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(font, text, new SysVector(position.X - offset, position.Y - offset), fontSize, spacing, shadowColor);

        // 2. Рисуем основной текст
        Raylib.DrawTextEx(font, text, position, fontSize, spacing, textColor);
    }




    // --- ОБНОВЛЕНИЕ ПОЛОЖЕНИЯ МЫШИ И НАЖАТИЙ КЛАВИШ ЧЕРЕЗ WINAPI ---
    private static void UpdateInput()
    {
        // 1. Проверяем нажатие Escape для выхода
        if ((GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0)
        {
            _isRunning = false;
            return;
        }
        // 2. Получаем координаты курсора глобально на всем экране
        if (GetCursorPos(out Point point))
        {
            _lastMouseX = _mouseX;
            _lastMouseY = _mouseY;
            _mouseX = point.X;
            _mouseY = point.Y;
            // --- НОВАЯ ЛОГИКА ДИНАМИЧЕСКОГО КЛИКА НА СКВОЗЬ ---
            bool isHoveringBox = false;
            lock (_lock)
            {
                SysVector mouseWorldPos = Raylib.GetScreenToWorld2D(new SysVector(_mouseX, _mouseY), _camera);
                for (int i = _boxes.Count - 1; i >= 0; i--)
                {
                    var box = _boxes[i];
                    float halfSize = BoxSizeMeters / 2f;
                    if (mouseWorldPos.X >= box.Position.X - halfSize && mouseWorldPos.X <= box.Position.X + halfSize &&
                    mouseWorldPos.Y >= box.Position.Y - halfSize && mouseWorldPos.Y <= box.Position.Y + halfSize)
                    {
                        isHoveringBox = true;
                        break;
                    }
                }
            }
            // Если мы навели на коробку или уже тащим её — окно должно принимать клики.
            // Если курсор на пустом месте — включаем флаг Passthrough, чтобы клик пролетал дальше в Windows.
            if (isHoveringBox || _draggedBox != null)
            {
                Raylib.ClearWindowState(ConfigFlags.MousePassthroughWindow);
            }
            else
            {
                Raylib.SetWindowState(ConfigFlags.MousePassthroughWindow);
            }
            // --------------------------------------------------
            if (_mouseX != _lastMouseX || _mouseY != _lastMouseY)
            {
                _lastMoveTimestamp = Stopwatch.GetTimestamp();
                if (_draggedBox != null)
                {
                    lock (_lock)
                    {
                        SysVector mouseWorldPos = Raylib.GetScreenToWorld2D(new SysVector(_mouseX, _mouseY), _camera);
                        _draggedBox.Position = new XnaVector(mouseWorldPos.X, mouseWorldPos.Y);
                    }
                }
            }
        }
        // 3. Обработка клавиши "C" (Создание коробки)
        bool isCPressedNow = (GetAsyncKeyState(VK_C) & 0x8000) != 0;
        if (isCPressedNow && !_wasCPressed)
        {
            lock (_lock)
            {
                SysVector mouseWorldPos = Raylib.GetScreenToWorld2D(new SysVector(_mouseX, _mouseY), _camera);
                XnaVector xnaSpawnPos = new XnaVector(mouseWorldPos.X, mouseWorldPos.Y);
                Body box = BodyFactory.CreateRectangle(_world, BoxSizeMeters, BoxSizeMeters, 2f, xnaSpawnPos);
                box.BodyType = BodyType.Dynamic;
                box.Restitution = 0.2f;
                box.Friction = 0.6f;
                _boxes.Add(box);
            }
        }
        _wasCPressed = isCPressedNow;
        // 3.5 Обработка клавиши "X" (Очистить все коробки)
        bool isXPressedNow = (GetAsyncKeyState(VK_X) & 0x8000) != 0;
        if (isXPressedNow && !_wasXPressed)
        {
            lock (_lock)
            {
                // Удаляем все коробки из физического мира Velcro
                foreach (var box in _boxes)
                {
                    _world.DestroyBody(box);
                }
                _boxes.Clear();
                // Если в этот момент коробка удерживалась мышью, сбрасываем захват
                _draggedBox = null;
            }
        }
        _wasXPressed = isXPressedNow;
        // 4. Обработка Левой Кнопки Мыши (Захват и Бросок)
        bool isLButtonDown = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
        // Событие: Мышь только что нажата (Pressed)
        if (isLButtonDown && !_wasLButtonPressed)
        {
            lock (_lock)
            {
                SysVector mouseWorldPos = Raylib.GetScreenToWorld2D(new SysVector(_mouseX, _mouseY), _camera);
                for (int i = _boxes.Count - 1; i >= 0; i--)
                {
                    var box = _boxes[i];
                    float halfSize = BoxSizeMeters / 2f;
                    if (mouseWorldPos.X >= box.Position.X - halfSize && mouseWorldPos.X <= box.Position.X + halfSize &&
                    mouseWorldPos.Y >= box.Position.Y - halfSize && mouseWorldPos.Y <= box.Position.Y + halfSize)
                    {
                        _draggedBox = box;
                        _draggedBox.BodyType = BodyType.Kinematic;
                        _draggedBox.LinearVelocity = XnaVector.Zero;
                        _draggedBox.AngularVelocity = 0f;
                        break;
                    }
                }
            }
        }
        // Событие: Мышь только что отпущена (Released)
        else if (!isLButtonDown && _wasLButtonPressed && _draggedBox != null)
        {
            lock (_lock)
            {
                // Проверяем существование объекта, так как его могли удалить клавишей X во время удержания
                if (_boxes.Contains(_draggedBox))
                {
                    float throwForceX = 0f;
                    float throwForceY = 0f;
                    long currentTimestamp = Stopwatch.GetTimestamp();
                    long elapsedTicks = currentTimestamp - _lastMoveTimestamp;
                    double elapsedMilliseconds = (double)elapsedTicks / Stopwatch.Frequency * 1000;
                    if (elapsedMilliseconds < 50)
                    {
                        SysVector lastMouseWorld = Raylib.GetScreenToWorld2D(new SysVector(_lastMouseX, _lastMouseY), _camera);
                        SysVector currentMouseWorld = Raylib.GetScreenToWorld2D(new SysVector(_mouseX, _mouseY), _camera);
                        throwForceX = (currentMouseWorld.X - lastMouseWorld.X) * 35f;
                        throwForceY = (currentMouseWorld.Y - lastMouseWorld.Y) * 35f;
                    }
                    _draggedBox.BodyType = BodyType.Dynamic;
                    _draggedBox.LinearVelocity = new XnaVector(throwForceX, throwForceY);
                }
                _draggedBox = null;
            }
        }
        _wasLButtonPressed = isLButtonDown;
    }
    private static void InitializeMousePosition()
    {
        if (GetCursorPos(out Point point))
        {
            _mouseX = point.X;
            _mouseY = point.Y;
            _lastMouseX = point.X;
            _lastMouseY = point.Y;
        }
    }
}