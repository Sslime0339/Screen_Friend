using Genbox.VelcroPhysics.Dynamics;
using Genbox.VelcroPhysics.Factories;
using SFML.Graphics;
using SFML.System;
using SFML.Window;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;


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

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(int uiAction, int uiParam, ref RECT pvParam, int fWinIni);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    // Стили окон Windows для прозрачности и кликов насквозь
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020; // Клик насквозь
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const uint LWA_COLORKEY = 0x00000001;

    private const int VK_ESCAPE = 0x1B;
    private const int VK_C = 0x43;
    private const int VK_X = 0x58;
    private const int VK_LBUTTON = 0x01;

    // --- Настройки физики и экрана ---
    const float PixelsPerMeter = 50f;
    private const float BoxSizeMeters = 1.2f;

    private static volatile bool _isRunning = true;
    private static World _world;

    private static readonly List<Body> _boxes = new List<Body>();
    private static readonly List<Body> _windowColliders = new List<Body>();
    private static readonly object _lock = new object();

    private static long _lastMoveTimestamp = 0;
    private static int _mouseX = 0;
    private static int _mouseY = 0;
    private static int _lastMouseX = 0;
    private static int _lastMouseY = 0;
    private static Body? _draggedBox = null;

    private static bool _wasCPressed = false;
    private static bool _wasXPressed = false;
    private static bool _wasLButtonPressed = false;

    private static RenderWindow _window;
    private static View _physicsView;

    static async Task Main(string[] args)
    {
        const int SPI_GETWORKAREA = 48;
        RECT workArea = new RECT();
        SystemParametersInfo(SPI_GETWORKAREA, 0, ref workArea, 0);

        int workAreaHeight = workArea.Bottom - workArea.Top;
        int workAreaWidth = workArea.Right - workArea.Left;

        const int SM_CXSCREEN = 0;
        const int SM_CYSCREEN = 1;
        int screenWidth = GetSystemMetrics(SM_CXSCREEN);
        int screenHeight = GetSystemMetrics(SM_CYSCREEN);

        // Инициализируем окно SFML без рамки (Popup стиль)
        VideoMode mode = new VideoMode((uint)workAreaWidth, (uint)workAreaHeight);
        _window = new RenderWindow(mode, "SFML Velcro Physics Overlay", Styles.None);
        _window.SetFramerateLimit(60);

        // Настройка WinAPI прозрачности для окна SFML
        IntPtr windowHandle = _window.SystemHandle;
        int initialExStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);

        // Делаем окно Layered, Topmost и скрываем из Alt-Tab (ToolWindow)
        int targetStyle = initialExStyle | WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW;
        SetWindowLong(windowHandle, GWL_EXSTYLE, targetStyle);

        // Задаем черный цвет (Color.Black / 0x00000000) как полностью прозрачный ключ
        SetLayeredWindowAttributes(windowHandle, 0, 0, LWA_COLORKEY);

        // Физический мир
        _world = new World(new XnaVector(0f, 19.6f));

        float viewWidthMeters = workAreaWidth / PixelsPerMeter;
        float viewHeightMeters = workAreaHeight / PixelsPerMeter;
        float wallThickness = 5.0f;

        float groundHeightMeters = 5f;
        float groundYMeters = workAreaHeight / PixelsPerMeter + (groundHeightMeters / 2f);
        float groundWidthMeters = workAreaWidth / PixelsPerMeter;

        // Пол
        Body ground = BodyFactory.CreateRectangle(_world, groundWidthMeters * 1.2f, groundHeightMeters, 1f, new XnaVector(groundWidthMeters / 2f, groundYMeters));
        ground.BodyType = BodyType.Static;
        ground.Friction = 0.6f;

        // Левая стена
        Body leftWall = BodyFactory.CreateRectangle(_world, wallThickness, viewHeightMeters * 8, 1f, new XnaVector(-(wallThickness / 2f), viewHeightMeters / 2f));
        leftWall.BodyType = BodyType.Static;
        leftWall.Friction = 0.4f;
        leftWall.Restitution = 0.3f;

        // Правая стена
        Body rightWall = BodyFactory.CreateRectangle(_world, wallThickness, viewHeightMeters * 8, 1f, new XnaVector(viewWidthMeters + (wallThickness / 2f), viewHeightMeters / 2f));
        rightWall.BodyType = BodyType.Static;
        rightWall.Friction = 0.4f;
        rightWall.Restitution = 0.3f;

        // Камера (View) для физического мира (в метрах)
        _physicsView = new View(new FloatRect(0, 0, viewWidthMeters, viewHeightMeters));

        // Шрифты
        Font myFont = new Font("Arial.ttf");

        // Загрузка текстур (Вместо GIF используем массив кадров)
        // Замените на реальные файлы или используйте один и тот же спрайт
        int framesBox = 1;
        Texture[] texBoxAnim = new Texture[framesBox];
        for (int i = 0; i < framesBox; i++)
        {
            // texBoxAnim[i] = new Texture($"resources/box_{i}.png");
            // Для теста создадим пустую текстуру, если файлов нет:
            Image img = new Image(60, 60, Color.Cyan);
            texBoxAnim[i] = new Texture(img);
        }

        int currentAnimFrame = 0;
        Clock animClock = new Clock();
        float frameDuration = 0.02f;
        float timeCount = 0.0f;

        InitializeMousePosition();

        List<WindowData> pendingWindows = new List<WindowData>();

        // Поток сканирования окон
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
                lock (_lock)
                {
                    pendingWindows = currentWindows;
                }
                await Task.Delay(100);
            }
        });
        Clock deltaClock = new Clock();
        // Главный цикл SFML
        while (_isRunning && _window.IsOpen)
        {
            _window.DispatchEvents();
            if (IsIconic(windowHandle))
            {
                ShowWindow(windowHandle, SW_RESTORE);
            }
            float dt = deltaClock.Restart().AsSeconds();
            // Обработка ввода
            UpdateInput(windowHandle);
            // Физика окон Windows
            lock (_lock)
            {
                foreach (var oldCollider in _windowColliders)
                {
                    _world.DestroyBody(oldCollider);
                }
                _windowColliders.Clear();
                foreach (var win in pendingWindows)
                {
                    Body windowBody = BodyFactory.CreateRectangle(_world, win.Width, win.Height, 1f, new XnaVector(win.CenterX, win.Y: win.CenterY));
                    windowBody.BodyType = BodyType.Static;
                    windowBody.Friction = 0.4f;
                    windowBody.Restitution = 0.2f;
                    _windowColliders.Add(windowBody);
                }
                _world.Step(Math.Min(dt, 1f / 60f));
            }
            // Анимация ящиков
            timeCount += dt;
            while (timeCount >= frameDuration)
            {
                currentAnimFrame = (currentAnimFrame + 1) % framesBox;
                timeCount -= frameDuration;
            }
            // --- ОТРИСОВКА ---
            // Очищаем в черный цвет, так как он задан прозрачным в WinAPI слое
            _window.Clear(Color.Black);
            // Рисуем физические объекты (переключаем View на метры)
            _window.SetView(_physicsView);
            lock (_lock)
            {
#if DEBUG
                // В режиме отладки рисуем коллайдеры окон Windows
                foreach (var win in pendingWindows)
                {
                    RectangleShape rectShape = new RectangleShape(new Vector2f(win.Width, win.Height));
                    rectShape.Origin = new Vector2f(win.Width / 2f, win.Height / 2f);
                    rectShape.Position = new Vector2f(win.CenterX, win.CenterY);
                    rectShape.FillColor = new Color(10, 20, 255, 60);
                    rectShape.OutlineColor = new Color(10, 20, 255, 150);
                    rectShape.OutlineThickness = 0.05f;
                    _window.Draw(rectShape);
                }
#endif
                // Рисуем динамические коробки
                Sprite boxSprite = new Sprite(texBoxAnim[currentAnimFrame]);
                // Центрируем Origin спрайта относительно текстуры
                boxSprite.Origin = new Vector2f(texBoxAnim[currentAnimFrame].Size.X / 2f, texBoxAnim[currentAnimFrame].Size.Y / 2f);
                // Масштабируем пиксельный спрайт под размер в метрах
                float scaleX = BoxSizeMeters * PixelsPerMeter / texBoxAnim[currentAnimFrame].Size.X;
                float scaleY = BoxSizeMeters * PixelsPerMeter / texBoxAnim[currentAnimFrame].Size.Y;
                boxSprite.Scale = new Vector2f(scaleX / PixelsPerMeter, scaleY / PixelsPerMeter);
                foreach (var box in _boxes)
                {
                    boxSprite.Position = new Vector2f(box.Position.X, box.Position.Y);
                    boxSprite.Rotation = box.Rotation * (180f / (float)Math.PI);
                    _window.Draw(boxSprite);
                }
            }
            // Рисуем UI (возвращаем стандартную камеру в пикселях)
            _window.SetView(_window.DefaultView);
            Color textShadowColor = new Color(20, 20, 20, 255);
            DrawTextWithShadow(myFont, "[C] - Создать коробку, [X] - Очистить всё, [ESC] - Закрыть программу", new Vector2f(10, 830), 24, Color.Magenta, textShadowColor, 2);
            DrawTextWithShadow(myFont, $"Окон-препятствий: {_windowColliders.Count} | Коробок: {_boxes.Count}", new Vector2f(10, 800), 24, Color.Cyan, textShadowColor, 2);
            // FPS Счетчик
            string fpsText = $"FPS: {(int)(1f / dt)}";
            DrawTextWithShadow(myFont, fpsText, new Vector2f(10, 770), 24, Color.Green, textShadowColor, 2);
            _window.Display();
        }
        // Очистка ресурсов
        for (int i = 0; i < framesBox; i++) texBoxAnim[i].Dispose();
        myFont.Dispose();
        _window.Dispose();
        _isRunning = false;
    }
    public static void DrawTextWithShadow(Font font, string textStr, Vector2f position, uint fontSize, Color textColor, Color shadowColor, float offset)
    {
        Text text = new Text(textStr, font, fontSize);
        // Рисуем контурную тень по 8 направлениям
        text.Color = shadowColor;
        Vector2f[] offsets = {
new Vector2f(-offset, 0), new Vector2f(offset, 0),
new Vector2f(0, -offset), new Vector2f(0, offset),
new Vector2f(-offset, -offset), new Vector2f(offset, -offset),
new Vector2f(-offset, offset), new Vector2f(offset, offset)
};
        foreach (var o in offsets)
        {
            text.Position = position + o;
            _window.Draw(text);
        }
        // Рисуем основной текст
        text.Color = textColor;
        text.Position = position;
        _window.Draw(text);
    }
    private static void UpdateInput(IntPtr windowHandle)
    {
        if ((GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0)
        {
            _isRunning = false;
            _window.Close();
            return;
        }
        if (GetCursorPos(out Point point))
        {
            _lastMouseX = _mouseX;
            _lastMouseY = _mouseY;
            _mouseX = point.X;
            _mouseY = point.Y;
            bool isHoveringBox = false;
            lock (_lock)
            {
                // Трансформируем экранные координаты мыши в координаты физического мира SFML
                Vector2f mouseWorldSFML = _window.MapPixelToCoords(new Vector2i(_mouseX, _mouseY), _physicsView);
                XnaVector mouseWorldPos = new XnaVector(mouseWorldSFML.X, mouseWorldSFML.Y);
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
            // Динамическое изменение прозрачности для клика:
            // Если мышка над коробкой, окно улавливает клики. Иначе — клик идет сквозь окно.
            int currentExStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);
            if (isHoveringBox || _draggedBox != null)
            {
                // Убираем флаг WS_EX_TRANSPARENT, чтобы клик регистрировался
                SetWindowLong(windowHandle, GWL_EXSTYLE, currentExStyle & ~WS_EX_TRANSPARENT);
            }
            else
            {
                // Добавляем флаг WS_EX_TRANSPARENT для сквозного клика
                SetWindowLong(windowHandle, GWL_EXSTYLE, currentExStyle | WS_EX_TRANSPARENT);
            }
            if (_mouseX != _lastMouseX || _mouseY != _lastMouseY)
            {
                _lastMoveTimestamp = Stopwatch.GetTimestamp();
                if (_draggedBox != null)
                {
                    lock (_lock)
                    {
                        Vector2f mouseWorldSFML = _window.MapPixelToCoords(new Vector2i(_mouseX, _mouseY), _physicsView);
                        _draggedBox.Position = new XnaVector(mouseWorldSFML.X, mouseWorldSFML.Y);
                    }
                }
            }
        }
        // Клавиша C (Создание)
        bool isCPressedNow = (GetAsyncKeyState(VK_C) & 0x8000) != 0;
        if (isCPressedNow && !_wasCPressed)
        {
            lock (_lock)
            {
                Vector2f mouseWorldSFML = _window.MapPixelToCoords(new Vector2i(_mouseX, _mouseY), _physicsView);
                XnaVector xnaSpawnPos = new XnaVector(mouseWorldSFML.X, mouseWorldSFML.Y);
                Body box = BodyFactory.CreateRectangle(_world, BoxSizeMeters, BoxSizeMeters, 2f, xnaSpawnPos);
                box.BodyType = BodyType.Dynamic;
                box.Restitution = 0.2f;
                box.Friction = 0.6f;
                _boxes.Add(box);
            }
        }
        _wasCPressed = isCPressedNow;
        // Клавиша X (Очистка)
        bool isXPressedNow = (GetAsyncKeyState(VK_X) & 0x8000) != 0;
        if (isXPressedNow && !_wasXPressed)
        {
            lock (_lock)
            {
                foreach (var box in _boxes)
                {
                    _world.DestroyBody(box);
                }
                _boxes.Clear();
                _draggedBox = null;
            }
        }
        _wasXPressed = isXPressedNow;
        // Левая кнопка мыши (Захват и бросок)
        bool isLButtonDown = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
        if (isLButtonDown && !_wasLButtonPressed)
        {
            lock (_lock)
            {
                Vector2f mouseWorldSFML = _window.MapPixelToCoords(new Vector2i(_mouseX, _mouseY), _physicsView);
                XnaVector mouseWorldPos = new XnaVector(mouseWorldSFML.X, mouseWorldSFML.Y);
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
        else if (!isLButtonDown && _wasLButtonPressed && _draggedBox != null)
        {
            lock (_lock)
            {
                if (_boxes.Contains(_draggedBox))
                {
                    float throwForceX = 0f;
                    float throwForceY = 0f;
                    long currentTimestamp = Stopwatch.GetTimestamp();
                    long elapsedTicks = currentTimestamp - _lastMoveTimestamp;
                    double elapsedMilliseconds = (double)elapsedTicks / Stopwatch.Frequency * 1000;
                    if (elapsedMilliseconds < 50)
                    {
                        Vector2f lastMouseWorld = _window.MapPixelToCoords(new Vector2i(_lastMouseX, _lastMouseY), _physicsView);
                        Vector2f currentMouseWorld = _window.MapPixelToCoords(new Vector2i(_mouseX, _mouseY), _physicsView);
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