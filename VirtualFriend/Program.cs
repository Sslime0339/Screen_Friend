


// имя человечка - РИХЭЫШЪЕ
// да я сгенерировал случайные символы



using Genbox.VelcroPhysics.Dynamics;
using Genbox.VelcroPhysics.Factories;
using Raylib_cs;
using System.Diagnostics;
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



        private static GameObject _draggedObject = null;


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


            _resourceManager = new ResourceManager();
            _resourceManager.LoadSpriteSheet(Assets.Sheets.Box, "Assets/Textures/box_sheet.png", 128, 128);
            _resourceManager.LoadFont(Assets.Fonts.MainFont, "Assets/Fonts/arial.ttf");


            _physicsSystem = new PhysicsSystem(new Microsoft.Xna.Framework.Vector2(0f, 19.6f)); // Гравитация
            _renderEngine = new RenderEngine(ppm, _resourceManager);
            InitWalls(ppm);


            GameObjectFactory.Initialize(_gameObjects);


            _windowManager = new WindowManager(_physicsSystem.World, ppm);
            _windowManager.StartScanning(workAreaWidth, workAreaHeight, screenWidth, screenHeight);


            _inputManager = new InputManager(_renderEngine);





            while (_isRunning)
            {

                float dt = Raylib.GetFrameTime();

                // ОБЯЗАТЕЛЬНО: Обновляем состояние ввода каждый кадр!
                _inputManager.Update(_gameObjects, dt);

                // --- Логика управления ящиками ---
                HandleInput();

                // Обновление логики объектов
                for (int i = _gameObjects.Count - 1; i >= 0; i--)
                {
                    _gameObjects[i].Update(dt);
                    if (_gameObjects[i].IsDestroyed) _gameObjects.RemoveAt(i);
                }

                _windowManager.Update();

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
            //_renderEngine.UnloadResources(); // Выгружаем текстуры и шрифты
            _resourceManager.UnloadAll();
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
            // --- 1. Создание ящика (Клавиша C) ---
            if (_inputManager.IsSpawnPressed())
            {
                SysVector mouseWorldPos = _inputManager.GetMouseWorldPosition();
                float boxSize = 1.0f;
                BoxObject newBox = GameObjectFactory.Instance.CreateBox(_physicsSystem.World, mouseWorldPos, boxSize);
                _gameObjects.Add(newBox);
            }

            // --- 2. Очистка сцены (Клавиша X) ---
            if (_inputManager.IsClearPressed())
            {
                foreach (var obj in _gameObjects)
                {
                    obj.Destroy(_physicsSystem.World);
                }
                _gameObjects.Clear();
                _draggedObject = null; // Сбрасываем захват при очистке
            }

            // --- 3. ХВАТАНИЕ И ПЕРЕТАСКИВАНИЕ (Левая кнопка мыши) ---
            SysVector currentMouseWorld = _inputManager.GetMouseWorldPosition();

            // 1. СОБЫТИЕ: Клик только что произошел (Нажатие кнопки)
            // Захватываем объект ТОЛЬКО если мы никого не держим в данный момент
            if (_inputManager.IsLButtonPressed() && _draggedObject == null)
            {
                foreach (var obj in _gameObjects)
                {
                    if (obj.PhysicsBody != null && !obj.IsDestroyed)
                    {
                        float halfSize = 1.0f / 2f; // Половина размера коробки в метрах
                        var pos = obj.PhysicsBody.Position;

                        // Проверяем, попал ли курсор в границы коробки
                        if (currentMouseWorld.X >= pos.X - halfSize && currentMouseWorld.X <= pos.X + halfSize &&
                            currentMouseWorld.Y >= pos.Y - halfSize && currentMouseWorld.Y <= pos.Y + halfSize)
                        {
                            _draggedObject = obj;

                            // Обнуляем старые скорости, чтобы коробка мгновенно перешла под контроль
                            _draggedObject.PhysicsBody.LinearVelocity = Microsoft.Xna.Framework.Vector2.Zero;
                            _draggedObject.PhysicsBody.AngularVelocity = 0f;
                            break;
                        }
                    }
                }
            }
            // 2. СОБЫТИЕ: Кнопка мыши отпущена (Бросок)
            // Используем else if, чтобы бросок обрабатывался отдельно от удержания
            else if (_inputManager.IsLButtonReleased() && _draggedObject != null)
            {
                float launchModifier = 1.0f;

                // Передаем финальное ускорение от броска руки
                _draggedObject.PhysicsBody.LinearVelocity = new Microsoft.Xna.Framework.Vector2(
                    _inputManager.MouseWorldVelocity.X * launchModifier,
                    _inputManager.MouseWorldVelocity.Y * launchModifier
                );

                // СТРОГО ОБЯЗАТЕЛЬНО: зануляем ссылку, чтобы программа забыла про этот ящик
                _draggedObject = null;
            }
            // 3. СОБЫТИЕ: Удержание кнопки мыши (Перетаскивание)
            // Выполняется ТОЛЬКО если кнопка зажата, объект выбран и мы его НЕ отпускаем в этом кадре
            else if (_inputManager.IsLButtonDown() && _draggedObject != null)
            {
                // Вычисляем вектор расстояния до мыши
                float targetX = currentMouseWorld.X - _draggedObject.PhysicsBody.Position.X;
                float targetY = currentMouseWorld.Y - _draggedObject.PhysicsBody.Position.Y;

                float responsiveness = 12f;

                // Притягиваем ящик к курсору
                _draggedObject.PhysicsBody.LinearVelocity = new Microsoft.Xna.Framework.Vector2(
                    targetX * responsiveness,
                    targetY * responsiveness
                );
            }

            // Дополнительная страховка: если физически кнопка мыши отпущена в Windows, 
            // но игра пропустила событие IsLButtonReleased из-за потери фокуса
            if (!_inputManager.IsLButtonDown() && _draggedObject != null)
            {
                _draggedObject = null;
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
