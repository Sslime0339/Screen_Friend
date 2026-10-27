


// имя человечка - РИХЭЫШЪЕ
// да я сгенерировал случайные символы



using Genbox.VelcroPhysics.Dynamics;
using Genbox.VelcroPhysics.Factories;
using Raylib_cs;
using VirtualFriend.Scripts;
// подключаем другие прстранства имён
using VirtualFriend.Scripts.GameObjects;
using static VirtualFriend.Scripts.Win32Native;
using SysVector = System.Numerics.Vector2;
using XnaVector = Microsoft.Xna.Framework.Vector2;






namespace VirtualFriend
{
    internal class Program
    {
        private static WindowManager _windowManager;
        private static PhysicsSystem _physicsSystem;
        private static RenderEngine _renderEngine;
        private static InputManager _inputManager;
        private static ResourceManager _resourceManager;



        private static readonly List<GameObject> _gameObjects = new List<GameObject>();
        private static bool _isRunning = true;


        static void Main(string[] args)
        {
            // 1. Настройка прозрачности и стиля окна Raylib
            Raylib.SetConfigFlags(
                ConfigFlags.MousePassthroughWindow |
                ConfigFlags.UndecoratedWindow |
                ConfigFlags.TransparentWindow |
                ConfigFlags.TopmostWindow |
                ConfigFlags.AlwaysRunWindow
            );



            GetWorkAreaSize(out int workAreaWidth, out int workAreaHeight);
            GetScreenSize(out int screenWidth, out int screenHeight);



            Raylib.InitWindow(workAreaWidth, workAreaHeight, "Raylib-cs + Velcro Physics overlay");
            Raylib.SetTargetFPS(60);



            const float ppm = 50f; // Pixels Per Meter

            _windowManager = new WindowManager(ppm);
            _windowManager.StartScanning(workAreaWidth, workAreaHeight, screenWidth, screenHeight);

            _physicsSystem = new PhysicsSystem(new Microsoft.Xna.Framework.Vector2(0f, 19.6f)); // Гравитация
            _renderEngine = new RenderEngine(ppm);
            InitWalls(ppm);

            _inputManager = new InputManager(_renderEngine);

            _resourceManager = new ResourceManager();
            _resourceManager.LoadSpriteSheet(Assets.Sheets.Box, "Assets/Textures/box_sheet.png", 128, 128);
            _resourceManager.LoadFont(Assets.Fonts.MainFont, "Assets/Fonts/arial.ttf");


            while (_isRunning)
            {

                float dt = Raylib.GetFrameTime();

                // ОБЯЗАТЕЛЬНО: Обновляем состояние ввода каждый кадр!
                _inputManager.Update();

                // --- Логика управления ящиками ---
                HandleInput();

                // Обновление логики объектов
                for (int i = _gameObjects.Count - 1; i >= 0; i--)
                {
                    _gameObjects[i].Update(dt);
                    if (_gameObjects[i].IsDestroyed) _gameObjects.RemoveAt(i);
                }


                // Шаг физического мира
                _physicsSystem.Step(dt);


                // Отрисовка сцены
                _renderEngine.Begin(Color.Blank);

                _renderEngine.BeginWorldSpace();
                foreach (var obj in _gameObjects)
                {
                    obj.Draw(_renderEngine, _resourceManager);
                }
                _renderEngine.EndWorldSpace();

                // Отрисовка UI текста
                _renderEngine.DrawTextWithShadow($"Коробок: {_gameObjects.Count}", new SysVector(10, 800), 24, Color.Pink);

                _renderEngine.End();


                if (Raylib.WindowShouldClose() || _inputManager.IsExitPressed()) _isRunning = false;
            }


            // После завершения цикла while (_isRunning)
            _renderEngine.UnloadResources(); // Выгружаем текстуры и шрифты
            Raylib.CloseWindow();

        }


        private static void GetWorkAreaSize(out int workAreaWidth, out int workAreaHeight)
        {

            const int SPI_GETWORKAREA = 48; // Код для получения рабочей области

            RECT workArea = new RECT();
            SystemParametersInfo(SPI_GETWORKAREA, 0, ref workArea, 0);

            // Вычисляем чистую высоту (Нижняя граница минус Верхняя граница)
            workAreaHeight = workArea.Bottom - workArea.Top;
            workAreaWidth = workArea.Right - workArea.Left;

        }


        private static void GetScreenSize(out int screenWidth, out int screenHeight)
        {

            // Константы для ширины и высоты главного экрана
            const int SM_CXSCREEN = 0;
            const int SM_CYSCREEN = 1;
            // 1. Получаем размеры экрана ДО ИНИЦИАЛИЗАЦИИ Raylib
            screenWidth = GetSystemMetrics(SM_CXSCREEN);
            screenHeight = GetSystemMetrics(SM_CYSCREEN);

        }


        private static void HandleInput()
        {
            // 1. Создание ящика по нажатию клавиши 'C'
            if (_inputManager.IsSpawnPressed())
            {
                // Получаем позицию мыши, переведенную в метры физического мира
                System.Numerics.Vector2 mouseWorldPos = _inputManager.GetMouseWorldPosition();

                // Задаем размер коробки (например, 1.0 метра в физическом мире)
                float boxSize = 1.0f;

                // Создаем коробку в физическом мире Velcro
                BoxObject newBox = new BoxObject(_physicsSystem.World, mouseWorldPos, boxSize);

                // Добавляем в общий список для обновлений и отрисовки
                _gameObjects.Add(newBox);
            }

            // 2. Очистка всех ящиков по нажатию клавиши 'X'
            if (_inputManager.IsClearPressed())
            {
                foreach (var obj in _gameObjects)
                {
                    // Метод Destroy удалит физическое тело из _physicsSystem.World
                    obj.Destroy(_physicsSystem.World);
                }

                // Список очистится сам на следующей итерации цикла благодаря проверке IsDestroyed,
                // либо можно принудительно очистить его здесь:
                _gameObjects.Clear();
            }
        }


        private static void InitWalls(float PixelsPerMeter)
        {
            World _world = _physicsSystem.World;

            GetWorkAreaSize(out int workAreaWidth, out int workAreaHeight);
            GetScreenSize(out int screenWidth, out int screenHeight);

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
        }



    }
}
