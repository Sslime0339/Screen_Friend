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

// Инициализация Raylib, игровой цикл и завершение приложения.
internal static partial class Program
{
    static async Task Main(string[] args)
    {
        // 1. Настройка прозрачности и стиля окна Raylib
        Raylib.SetConfigFlags(
            ConfigFlags.MousePassthroughWindow |
            ConfigFlags.UndecoratedWindow |
            ConfigFlags.TransparentWindow |
            ConfigFlags.TopmostWindow |
            ConfigFlags.AlwaysRunWindow
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
        /*

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
                    ShowWindow(windowHandle, SW_SHOWNOACTIVATE);
                }

                await Task.Delay(50); // Проверка каждые 50мс
            }
        });

        */



        // Задаем стиль ToolWindow. Это защитит оверлей от Aero Shake, 
        // а также скроет его из меню Alt+Tab (что полезно для оверлеев)
        //int exStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);
        //SetWindowLong(windowHandle, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);



        // Основной игровой цикл
        while (_isRunning && !Raylib.WindowShouldClose())
        {


            // --- ЛОГИКА ВОССТАНОВЛЕНИЯ ПОСЛЕ СВОРУЧИВАНИЯ ---
            // Если Raylib обнаружила, что окно свернули:
            if (Raylib.IsWindowMinimized())
            {
                // 1. Принудительно разворачиваем его обратно средствами Raylib
                //Raylib.RestoreWindow();

                ShowWindow(windowHandle, SW_RESTORE);


                Console.WriteLine("окно свёрнуто");

                // 2. Сбрасываем флаги окна. Принудительно выключаем и включаем прозрачность и безрамочность.
                // Это заставит GLFW и Windows полностью перерисовать контекст с нуля и уберет "зависание"
                
                //Raylib.ClearWindowState(ConfigFlags.TransparentWindow | ConfigFlags.UndecoratedWindow);

                // Даем Windows 10-20 миллисекунд прийти в себя
                
                //System.Threading.Thread.Sleep(16);

                //Raylib.SetWindowState(ConfigFlags.TransparentWindow | ConfigFlags.UndecoratedWindow | ConfigFlags.TopmostWindow);

                // 3. Пропускаем текущий поврежденный кадр рендеринга, переходя к следующему чистому
                
                //continue;
            }


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
}
