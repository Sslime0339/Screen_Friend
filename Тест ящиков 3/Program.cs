using Genbox.VelcroPhysics.Dynamics;
using Genbox.VelcroPhysics.Factories;
using Raylib_cs;
using SharpHook;
using SharpHook.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using MouseButton = SharpHook.Data.MouseButton;
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
    private const int DWMWA_CLOAKED = 14;

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
    const float PixelsPerMeter = 50f;
    private const float BoxSizeMeters = 1.2f;

    private static volatile bool _isRunning = true;
    private static World _world;

    private static readonly List<Body> _boxes = new List<Body>();
    private static readonly List<Body> _windowColliders = new List<Body>();
    private static readonly object _lock = new object();

    // Переменные обмена координатами между потоками (мгновенная запись)
    private static volatile int _rawMouseX = 0;
    private static volatile int _rawMouseY = 0;
    private static int _mouseX = 0;
    private static int _mouseY = 0;
    private static int _lastMouseX = 0;
    private static int _lastMouseY = 0;
    private static long _rawLastMoveTimestamp = 0;
    private static long _lastMoveTimestamp = 0;

    // Атомарные флаги команд для исключения взаимных блокировок
    private static volatile bool _spawnRequested = false;
    private static volatile bool _dragRequested = false;
    private static volatile bool _releaseRequested = false;
    private static volatile bool _suppressCurrentEvent = false;

    private static Body? _draggedBox = null;
    private static Camera2D _camera;

    // Оптимизация WinAPI: обновление окон раз в 0.5 секунд
    private static float _windowUpdateTimer = 0f;
    private const float WindowUpdateInterval = 0.5f;

    static async Task Main(string[] args)
    {
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

        _world = new World(new XnaVector(0f, 19.6f));

        float viewWidthMeters = screenWidth / PixelsPerMeter;
        float viewHeightMeters = screenHeight / PixelsPerMeter;
        float wallThickness = 5.0f;

        float groundHeightMeters = 5f;
        float groundYMeters = screenHeight / PixelsPerMeter + (groundHeightMeters / 2f);
        float groundWidthMeters = screenWidth / PixelsPerMeter;

        Body ground = BodyFactory.CreateRectangle(_world, groundWidthMeters * 1.2f, groundHeightMeters, 1f, new XnaVector(groundWidthMeters / 2f, groundYMeters));
        ground.BodyType = BodyType.Static;
        ground.Friction = 0.6f;

        Body leftWall = BodyFactory.CreateRectangle(_world, wallThickness, viewHeightMeters * 8, 1f, new XnaVector(-(wallThickness / 2f), viewHeightMeters / 2f));
        leftWall.BodyType = BodyType.Static;
        leftWall.Friction = 0.4f;
        leftWall.Restitution = 0.3f;

        Body rightWall = BodyFactory.CreateRectangle(_world, wallThickness, viewHeightMeters * 8, 1f, new XnaVector(viewWidthMeters + (wallThickness / 2f), viewHeightMeters / 2f));
        rightWall.BodyType = BodyType.Static;
        rightWall.Friction = 0.4f;
        rightWall.Restitution = 0.3f;

        _camera = new Camera2D { Target = SysVector.Zero, Offset = SysVector.Zero, Rotation = 0.0f, Zoom = PixelsPerMeter };

        int[] codepoints = new int[512];
        for (int i = 0; i < 95; i++) codepoints[i] = 32 + i;
        for (int i = 0; i < 256; i++) codepoints[95 + i] = 0x0400 + i;
        Font myFont = Raylib.LoadFontEx("Arial.ttf", 32, codepoints, codepoints.Length);

        int textOffset = 2;

        Image imBoxAnim = Raylib.LoadImageAnim("resources/box.gif", out int framesBox);
        Texture2D[] texBoxAnim = new Texture2D[framesBox];
        int frameSize = imBoxAnim.Width * imBoxAnim.Height * 4;

        for (int i = 0; i < framesBox; i++)
        {
            unsafe
            {
                byte* basePtr = (byte*)imBoxAnim.Data;
                Image singleFrameImg = new Image { Data = basePtr + (i * frameSize), Width = imBoxAnim.Width, Height = imBoxAnim.Height, Mipmaps = 1, Format = PixelFormat.UncompressedR8G8B8A8 };
                texBoxAnim[i] = Raylib.LoadTextureFromImage(singleFrameImg);
            }
        }

        Rectangle sourceRec = new Rectangle(0.0f, 0.0f, texBoxAnim[0].Width, texBoxAnim[0].Height);
        int currentAnimFrame = 0;
        float timeCount = 0.0f;
        float frameDuration = 0.02f;

        InitializeMousePosition();

        using var hook = new SimpleGlobalHook();
        hook.KeyPressed += OnKeyPressed;
        hook.MouseMoved += OnMouseMoved;
        hook.MouseDragged += OnMouseMoved;
        hook.MousePressed += OnMousePressed;
        hook.MouseReleased += OnMouseReleased;

        var hookTask = hook.RunAsync();

        // Список хранения кэшированных данных окон для отрисовки
        List<(float x, float y, float w, float h)> activeWindowsRenderData = new List<(float, float, float, float)>();

        while (_isRunning && !Raylib.WindowShouldClose())
        {
            float dt = Raylib.GetFrameTime();

            // Читаем потокобезопасные сырые значения мыши в локальные переменные кадра
            _mouseX = _rawMouseX;
            _mouseY = _rawMouseY;
            _lastMoveTimestamp = _rawLastMoveTimestamp;

            lock (_lock)
            {
                // --- ОБРАБОТКА ЗАПРОСОВ ИЗ ХУКОВ (БЕЗОПАСНО В ОСНОВНОМ ПОТОКЕ) ---
                if (_spawnRequested)
                {
                    _spawnRequested = false;
                    SysVector mouseWorldPos = Raylib.GetScreenToWorld2D(new SysVector(_mouseX, _mouseY), _camera);
                    Body box = BodyFactory.CreateRectangle(_world, BoxSizeMeters, BoxSizeMeters, 2f, new XnaVector(mouseWorldPos.X, mouseWorldPos.Y));
                    box.BodyType = BodyType.Dynamic;
                    box.Restitution = 0.2f;
                    box.Friction = 0.6f;
                    _boxes.Add(box);
                }

                if (_dragRequested)
                {
                    _dragRequested = false;
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
                            _suppressCurrentEvent = true; // Разрешаем скрыть клик
                            break;
                        }
                    }
                }

                if (_releaseRequested)
                {
                    _releaseRequested = false;
                    if (_draggedBox != null)
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
                        _draggedBox = null;
                    }
                }
                if (_draggedBox != null)
                {
                    SysVector mouseWorldPos = Raylib.GetScreenToWorld2D(new SysVector(_mouseX, _mouseY), _camera);
                    _draggedBox.Position = new XnaVector(mouseWorldPos.X, mouseWorldPos.Y);
                }
                // ---ОПТИМИЗИРОВАННОЕ ОБНОВЛЕНИЕ ОКОН ПО ТАЙМЕРУ ---
                _windowUpdateTimer += dt;
                if (_windowUpdateTimer >= WindowUpdateInterval)
                {
                    _windowUpdateTimer = 0f;
                    foreach (var oldCollider in _windowColliders) _world.DestroyBody(oldCollider);
                    _windowColliders.Clear();
                    activeWindowsRenderData.Clear();
                    EnumWindows((hWnd, lParam) => {
                        if (!IsWindowVisible(hWnd)) return true;
                        StringBuilder sb = new StringBuilder(256);
                        GetWindowText(hWnd, sb, sb.Capacity);
                        if (string.IsNullOrWhiteSpace(sb.ToString())) return true;
                        DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out bool isCloaked, sizeof(bool));
                        if (isCloaked) return true;
                        if (!GetExactWindowRect(hWnd, out RECT rect)) return true;
                        int width = rect.Right - rect.Left;
                        int height = rect.Bottom - rect.Top;
                        if (width <= 16 || height <= 16 || rect.Left < -10000 || rect.Top < -10000) return true;
                        if (!((width == 1600 && height == 900) || (width == 1600 && height == 860)))
                        {
                            if (!IsZoomed(hWnd) && !IsIconic(hWnd))
                            {
                                float wMeters = width / PixelsPerMeter;
                                float hMeters = height / PixelsPerMeter;
                                float centerXMeters = (rect.Left + width / 2f) / PixelsPerMeter;
                                float centerYMeters = (rect.Top + height / 2f) / PixelsPerMeter;
                                Body windowBody = BodyFactory.CreateRectangle(_world, wMeters, hMeters, 1f, new XnaVector(centerXMeters, centerYMeters));
                                windowBody.BodyType = BodyType.Static;
                                windowBody.Friction = 0.4f;
                                windowBody.Restitution = 0.2f;
                                _windowColliders.Add(windowBody);
                                activeWindowsRenderData.Add((rect.Left / PixelsPerMeter, rect.Top / PixelsPerMeter, wMeters, hMeters));
                            }
                        }
                        return true;
                    }, IntPtr.Zero);
                }
                // Шаг симуляции физики Velcro
                _world.Step(Math.Min(dt, 1f / 60f));
            }
            _lastMouseX = _mouseX;
            _lastMouseY = _mouseY;
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
                // Отрисовка кэшированных окон Windows (без тяжелых вызовов WinAPI)
                foreach (var win in activeWindowsRenderData)
                {
                    Raylib.DrawRectangleRec(new Rectangle(win.x, win.y, win.w, win.h), new Color(10, 20, 255, 60));
                    Raylib.DrawRectangleLinesEx(new Rectangle(win.x, win.y, win.w, win.h), 0.05f, new Color(10, 20, 255, 150));
                }
                // Отрисовка динамических ящиков
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
        for (int i = 0; i < framesBox; i++) Raylib.UnloadTexture(texBoxAnim[i]);
        Raylib.UnloadImage(imBoxAnim);
        Raylib.UnloadFont(myFont);
        Raylib.CloseWindow();
        _isRunning = false;
        hook.Dispose();
        await hookTask;
    }
    // --- ОБРАБОТЧИКИ СОБЫТИЙ SHARPHOOK (ТЕПЕРЬ БЕЗ БЛОКИРОВОК И ЗАВИСАНИЙ) ---
    private static void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (e.Data.KeyCode == KeyCode.VcEscape) { _isRunning = false; return; }
        if (e.Data.KeyCode == KeyCode.VcC) _spawnRequested = true;
    }
    private static void OnMousePressed(object? sender, MouseHookEventArgs e)
    {
        if (e.Data.Button == MouseButton.Button1)
        {
            _dragRequested = true;
            // Короткое неблокирующее ожидание, чтобы проверить, схватил ли Raylib коробку
            int spin = 0;
            while (!_dragRequested && spin < 400) { spin++; }
            if (_suppressCurrentEvent)
            {
                _suppressCurrentEvent = false;
                e.SuppressEvent = true;
            }
        }
    }
    private static void OnMouseReleased(object? sender, MouseHookEventArgs e)
    {
        if (e.Data.Button == MouseButton.Button1)
        {
            _releaseRequested = true;
            e.SuppressEvent = true;
        }
    }
    private static void OnMouseMoved(object? sender, MouseHookEventArgs e)
    {
        _rawMouseX = e.Data.X;
        _rawMouseY = e.Data.Y;

        lock (_lock)
        {
            _rawLastMoveTimestamp = Stopwatch.GetTimestamp();
        }
    }
    private static void InitializeMousePosition()
    {
        if (GetCursorPos(out Point point))
        {
            _rawMouseX = point.X;
            _rawMouseY = point.Y;
            _lastMouseX = point.X;
            _lastMouseY = point.Y;
        }
    }
}