using Genbox.VelcroPhysics.Dynamics;
using Genbox.VelcroPhysics.Factories;
using Raylib_cs;
using SharpHook;
using SharpHook.Data;
//using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
//using static System.Net.Mime.MediaTypeNames;
using MouseButton = SharpHook.Data.MouseButton;
// Псевдонимы типов для исключения конфликтов векторов
using SysVector = System.Numerics.Vector2;
using XnaVector = Microsoft.Xna.Framework.Vector2;

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







    // --- Настройки физики и экрана ---
    const float PixelsPerMeter = 50f; // 50 пикселей = 1 метр
    private const float BoxSizeMeters = 1.2f; // Размер ящика в метрах (1.2м * 50пкс = 60 пикселей)

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

        const int screenWidth = 1600;
        const int screenHeight = 860;
        Raylib.InitWindow(screenWidth, screenHeight, "Raylib-cs + Velcro Physics overlay");
        Raylib.SetTargetFPS(60);

        // 2. Инициализация физического мира Velcro (Гравитация вниз)
        _world = new World(new XnaVector(0f, 19.6f));




        // --- СОЗДАНИЕ ГРАНИЦ ЭКРАНА (СТЕНЫ И ПОЛ) ---
        float viewWidthMeters = screenWidth / PixelsPerMeter;
        float viewHeightMeters = screenHeight / PixelsPerMeter * 8;
        float wallThickness = 5.0f; // Толщина стен (в метрах), чтобы коробки не пролетали сквозь них на огромной скорости

        float groundHeightMeters = 5f;
        float groundYMeters = screenHeight / PixelsPerMeter + (groundHeightMeters / 2f);
        float groundWidthMeters = screenWidth / PixelsPerMeter;



        // Нижний пол (чуть ниже видимой границы)
        Body ground = BodyFactory.CreateRectangle(
            _world,
            groundWidthMeters * 1.2f, groundHeightMeters,
            1f,
            new XnaVector(groundWidthMeters / 2f, groundYMeters)
        );
        ground.BodyType = BodyType.Static;
        ground.Friction = 0.6f;

        // Левая стена (чуть левее X = 0)
        Body leftWall = BodyFactory.CreateRectangle(
            _world,
            wallThickness, viewHeightMeters,
            1f,
            new XnaVector(-(wallThickness / 2f), viewHeightMeters / 2f)
        );
        leftWall.BodyType = BodyType.Static;
        leftWall.Friction = 0.4f;
        leftWall.Restitution = 0.3f;

        // Правая стена (чуть правее максимального X)
        Body rightWall = BodyFactory.CreateRectangle(
            _world,
            wallThickness, viewHeightMeters,
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


        int textOffset = 2; // Размер смещения тени в пикселях




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

        // 6. Запуск SharpHook глобального перехвата
        using var hook = new SimpleGlobalHook();
        hook.KeyPressed += OnKeyPressed;
        hook.MouseMoved += OnMouseMoved;
        hook.MouseDragged += OnMouseMoved;
        hook.MousePressed += OnMousePressed;
        hook.MouseReleased += OnMouseReleased;

        var hookTask = hook.RunAsync();

        // Основной игровой цикл
        while (_isRunning && !Raylib.WindowShouldClose())
        {
            float dt = Raylib.GetFrameTime();

            // --- ОБНОВЛЕНИЕ ФИЗИЧЕСКИХ ОКОН WINDOWS ---
            lock (_lock)
            {
                // 1. Удаляем старые коллайдеры окон из физического мира
                foreach (var oldCollider in _windowColliders)
                {
                    _world.DestroyBody(oldCollider);
                }
                _windowColliders.Clear();

                // 2. Ищем актуальные окна ОС и строим для них новые твердые тела
                const int DWMWA_CLOAKED = 14;
                EnumWindows((hWnd, lParam) => {
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

                    // Отсекаем само окно Raylib, чтобы оно не создавало физический барьер само себе
                    if (!((width == 1600 && height == 900) || (width == 1600 && height == 860)))
                    {
                        if (!IsZoomed(hWnd) && !IsIconic(hWnd))
                        {
                            // Переводим размеры и координаты из пикселей в метры Velcro Physics
                            float wMeters = width / PixelsPerMeter;
                            float hMeters = height / PixelsPerMeter;

                            // Вычисляем центр прямоугольника окна (так как Velcro ставит позицию в центр геометрии)
                            float centerXMeters = (x + width / 2f) / PixelsPerMeter;
                            float centerYMeters = (y + height / 2f) / PixelsPerMeter;

                            // Создаем статическое тело. Статическое тело идеально подходит, 
                            // так как оно неподвижно в рамках одного физического шага, но его можно переносить
                            Body windowBody = BodyFactory.CreateRectangle(
                                _world,
                                wMeters, hMeters,
                                1f,
                                new XnaVector(centerXMeters, centerYMeters)
                            );
                            windowBody.BodyType = BodyType.Static;
                            windowBody.Friction = 0.4f;      // Трение для стенок окон
                            windowBody.Restitution = 0.2f;   // Небольшой отскок при ударе об окно

                            // Сохраняем ссылку на созданное тело, чтобы нарисовать его и удалить на следующем кадре
                            _windowColliders.Add(windowBody);
                        }
                    }
                    return true;
                }, IntPtr.Zero);

                // Шаг симуляции физики Velcro
                _world.Step(Math.Min(dt, 1f / 60f));
                //_world.Step(dt);
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
            Raylib.ClearBackground(Color.Blank); // Прозрачный фон
                                                 // Включаем 2D режим камеры (все координаты далее в метрах)
            Raylib.BeginMode2D(_camera);
            lock (_lock)
            {
                // 1. Визуализируем синие полупрозрачные окна прямо через их физические тела
                foreach (var winBody in _windowColliders)
                {
                    // Получаем размеры и координаты обратно из физических свойств
                    // Для отрисовки Rectangle нам нужен левый верхний угол, а не центр
                    var fixture = winBody.FixtureList[0];
                    var shape = (Genbox.VelcroPhysics.Collision.Shapes.PolygonShape)fixture.Shape;
                    // Вычисляем ширину и высоту на основе локальных вершин формы
                    float wMeters = (shape.Vertices[1].X - shape.Vertices[0].X);
                    float hMeters = (shape.Vertices[2].Y - shape.Vertices[1].Y);
                    float leftMeters = winBody.Position.X - (wMeters / 2f);
                    float topMeters = winBody.Position.Y - (hMeters / 2f);
                    // Рисуем синее окно
                    Raylib.DrawRectangleRec(
                    new Rectangle(leftMeters, topMeters, wMeters, hMeters),
                    new Color(10, 20, 255, 60) // Полупрозрачный синий
                    );
                    // Рисуем рамку окна для четкости
                    Raylib.DrawRectangleLinesEx(
                    new Rectangle(leftMeters, topMeters, wMeters, hMeters),
                    0.05f, // Толщина линии в метрах
                    new Color(10, 20, 255, 150)
                    );
                }
                // 2. Рисуем все динамические ящики (коробки)
                foreach (var box in _boxes)
                {
                    float rotationInDegrees = box.Rotation * (180f / (float)Math.PI);
                    Rectangle destRec = new Rectangle(box.Position.X, box.Position.Y, BoxSizeMeters, BoxSizeMeters);
                    SysVector origin = new SysVector(BoxSizeMeters / 2f, BoxSizeMeters / 2f);
                    Raylib.DrawTexturePro(texBoxAnim[currentAnimFrame], sourceRec, destRec, origin, rotationInDegrees, Color.White);
                }
            }
            Raylib.EndMode2D();

            // Отрисовка UI текста поверх в стандартных пикселях экрана

            Raylib.DrawTextEx(myFont, "Коробки теперь сталкиваются с синими окнами Windows!", new SysVector(50 + textOffset, 100 + textOffset), 24, 2, Color.DarkGray);
            Raylib.DrawTextEx(myFont, "Коробки теперь сталкиваются с синими окнами Windows!", new SysVector(50, 100), 24, 2, Color.Purple);

            Color textColor = new Color(20, 20, 20, 255);
            Raylib.DrawTextEx(myFont, $"Окон-препятствий: {_windowColliders.Count} | Коробок: {_boxes.Count}", new SysVector(10 + textOffset, 10), 24, 2, textColor);
            Raylib.DrawTextEx(myFont, $"Окон-препятствий: {_windowColliders.Count} | Коробок: {_boxes.Count}", new SysVector(10 - textOffset, 10), 24, 2, textColor);
            Raylib.DrawTextEx(myFont, $"Окон-препятствий: {_windowColliders.Count} | Коробок: {_boxes.Count}", new SysVector(10, 10 + textOffset), 24, 2, textColor);
            Raylib.DrawTextEx(myFont, $"Окон-препятствий: {_windowColliders.Count} | Коробок: {_boxes.Count}", new SysVector(10, 10 - textOffset), 24, 2, textColor);
            Raylib.DrawTextEx(myFont, $"Окон-препятствий: {_windowColliders.Count} | Коробок: {_boxes.Count}", new SysVector(10 + textOffset, 10 + textOffset), 24, 2, textColor);
            Raylib.DrawTextEx(myFont, $"Окон-препятствий: {_windowColliders.Count} | Коробок: {_boxes.Count}", new SysVector(10 - textOffset, 10 + textOffset), 24, 2, textColor);
            Raylib.DrawTextEx(myFont, $"Окон-препятствий: {_windowColliders.Count} | Коробок: {_boxes.Count}", new SysVector(10 + textOffset, 10 - textOffset), 24, 2, textColor);
            Raylib.DrawTextEx(myFont, $"Окон-препятствий: {_windowColliders.Count} | Коробок: {_boxes.Count}", new SysVector(10 - textOffset, 10 - textOffset), 24, 2, textColor);
            Raylib.DrawTextEx(myFont, $"Окон-препятствий: {_windowColliders.Count} | Коробок: {_boxes.Count}", new SysVector(10, 10), 24, 2, Color.Pink);


            Raylib.DrawFPS(10, 35);
            Raylib.EndDrawing();
        }
        // Очистка ресурсов
        for (int i = 0; i < framesBox; i++) Raylib.UnloadTexture(texBoxAnim[i]);
        Raylib.UnloadImage(imBoxAnim);
        Raylib.UnloadFont(myFont);
        Raylib.CloseWindow();
        _isRunning = false;
        hook.Dispose();
        await hookTask;
    }
    // --- ОБРАБОТЧИКИ СОБЫТИЙ SHARPHOOK ---
    private static void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (e.Data.KeyCode == KeyCode.VcEscape)
        {
            _isRunning = false;
            return;
        }
        if (e.Data.KeyCode == KeyCode.VcC)
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
    }
    private static void OnMousePressed(object? sender, MouseHookEventArgs e)
    {
        if (e.Data.Button == MouseButton.Button1)
        {
            lock (_lock)
            {
                SysVector mouseWorldPos = Raylib.GetScreenToWorld2D(new SysVector(e.Data.X, e.Data.Y), _camera);
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
                        e.SuppressEvent = true;
                        break;
                    }
                }
            }
        }
    }
    private static void OnMouseReleased(object? sender, MouseHookEventArgs e)
    {
        if (e.Data.Button == MouseButton.Button1 && _draggedBox != null)
        {
            lock (_lock)
            {
                float throwForceX = 0f;
                float throwForceY = 0f;

                // Проверяем, как давно мышь двигалась в последний раз
                long currentTimestamp = Stopwatch.GetTimestamp();
                long elapsedTicks = currentTimestamp - _lastMoveTimestamp;
                double elapsedMilliseconds = (double)elapsedTicks / Stopwatch.Frequency * 1000;

                // Если отпустили в течение 50 мс после движения — считаем бросок.
                // Если мышь стояла дольше 50 мс — коробка просто упадет вниз.
                if (elapsedMilliseconds < 50)
                {
                    SysVector lastMouseWorld = Raylib.GetScreenToWorld2D(new SysVector(_lastMouseX, _lastMouseY), _camera);
                    SysVector currentMouseWorld = Raylib.GetScreenToWorld2D(new SysVector(_mouseX, _mouseY), _camera);

                    throwForceX = (currentMouseWorld.X - lastMouseWorld.X) * 35f;
                    throwForceY = (currentMouseWorld.Y - lastMouseWorld.Y) * 35f;
                }

                _draggedBox.BodyType = BodyType.Dynamic;
                _draggedBox.LinearVelocity = new XnaVector(throwForceX, throwForceY);

                e.SuppressEvent = true;
                _draggedBox = null;
            }
        }
    }

    private static void OnMouseMoved(object? sender, MouseHookEventArgs e)
    {
        _lastMouseX = _mouseX;
        _lastMouseY = _mouseY;
        _mouseX = e.Data.X;
        _mouseY = e.Data.Y;

        // Запоминаем время последнего движения (в миллисекундах)
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