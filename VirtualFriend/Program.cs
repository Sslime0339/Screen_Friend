


// имя человечка - РИХЭЫШЪЕ
// да я сгенерировал случайные символы



using Genbox.VelcroPhysics.Dynamics;
using Genbox.VelcroPhysics.Factories;
using Raylib_cs;
using System.Diagnostics;
using VirtualFriend.Scripts;
// подключаем другие прстранства имён
using VirtualFriend.Scripts.GameObjects;
using VirtualFriend.Scripts.Interfaces;
using static VirtualFriend.Scripts.Win32Native;
using SysVector = System.Numerics.Vector2;
using XnaVector = Microsoft.Xna.Framework.Vector2;





// TODO: сделать интерфейс чтобы объект был кликабельным/таскаемым (возможно два разных интерфейса)
// TODO: сделать сортировку отрисовки
// TODO: сделать окно (виндовс формс или WPF) для создания объектов настроек и т.д.
// TODO: сделать класс для UI (на Raylib) там текст, кнопочки, к нопкам можно применить интерфейс кликабельность
// TODO: добавить класс для персонажей
// TODO: добавить РИХЭЫШЪЕ





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

        // ИСПОЛЬЗУЕМ ИНТЕРФЕЙС: Храним ссылку на любой таскаемый объект
        private static IDraggable _draggedObject = null;

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




            GameObjectFactory.Instance.CreateBox(_physicsSystem.World, new SysVector(30, 5), 5);



            while (_isRunning)
            {


                float dt = Raylib.GetFrameTime();

                // ОБЯЗАТЕЛЬНО: Обновляем состояние ввода каждый кадр!
                _inputManager.Update(_gameObjects, dt);

                // --- Логика управления ящиками и объектами ---
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


                // --- ДВУХУРОВНЕВАЯ СОРТИРОВКА ОТРИСОВКИ ---
                _gameObjects.Sort((a, b) =>
                {
                    // 1. Сначала сравниваем по слою (Layer)
                    int layerCompare = a.Layer.CompareTo(b.Layer);
                    if (layerCompare != 0) return layerCompare;

                    // 2. Если слои равны, сортируем по внутреннему ZIndex
                    return a.ZIndex.CompareTo(b.ZIndex);
                });


                // Отрисовка сцены
                _renderEngine.Begin(Color.Blank);

                _renderEngine.BeginWorldSpace();
                foreach (var obj in _gameObjects)
                {
                    obj.Draw(_renderEngine, _resourceManager);
                }
                _renderEngine.EndWorldSpace();

                // Отрисовка UI текста (пока рисуется жестко поверх всего кода)
                _renderEngine.DrawTextWithShadow($"Коробок: {_gameObjects.Count}", new SysVector(10, 800), 24, Color.Pink);

                _renderEngine.End();


                if (Raylib.WindowShouldClose() || _inputManager.IsExitPressed()) _isRunning = false;
            }

            _resourceManager.UnloadAll();
            Raylib.CloseWindow();

        }


        private static void GetWorkAreaSize(out int workAreaWidth, out int workAreaHeight)
        {
            const int SPI_GETWORKAREA = 48;
            RECT workArea = new RECT();
            SystemParametersInfo(SPI_GETWORKAREA, 0, ref workArea, 0);
            workAreaHeight = workArea.Bottom - workArea.Top;
            workAreaWidth = workArea.Right - workArea.Left;
        }

        private static void GetScreenSize(out int screenWidth, out int screenHeight)
        {
            const int SM_CXSCREEN = 0;
            const int SM_CYSCREEN = 1;
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
                    if (obj is BoxObject)
                    {
                        obj.Destroy(_physicsSystem.World);
                    }
                }
                _draggedObject = null; // Сбрасываем захват при очистке
            }

            // --- 3. ХВАТАНИЕ И ПЕРЕТАСКИВАНИЕ (Через интерфейсы) ---
            SysVector currentMouseWorld = _inputManager.GetMouseWorldPosition();
            XnaVector currentMouseWorldXna = new XnaVector(currentMouseWorld.X, currentMouseWorld.Y);

            // 1. СОБЫТИЕ: Клик только что произошел (Нажатие кнопки)
            if (_inputManager.IsLButtonPressed() && _draggedObject == null)
            {
                foreach (var obj in _gameObjects)
                {
                    // Проверяем, поддерживает ли объект перетаскивание
                    if (obj is IDraggable draggable)
                    {
                        // Проверяем попадание мыши методами интерфейса IClickable
                        if (draggable.IsUnderMouse(currentMouseWorld))
                        {
                            _draggedObject = draggable;

                            // Вызываем события интерфейсов
                            _draggedObject.OnClick();
                            _draggedObject.OnDragStart();
                            break;
                        }
                    }
                }
            }
            // 2. СОБЫТИЕ: Кнопка мыши отпущена (Бросок)
            else if (_inputManager.IsLButtonReleased() && _draggedObject != null)
            {
                float launchModifier = 1.0f;
                XnaVector releaseVelocity = new XnaVector(
                    _inputManager.MouseWorldVelocity.X * launchModifier,
                    _inputManager.MouseWorldVelocity.Y * launchModifier
                );

                // Оповещаем объект об окончании перетаскивания и передаем скорость броска
                _draggedObject.OnDragEnd(releaseVelocity);
                _draggedObject = null;
            }
            // 3. СОБЫТИЕ: Удержание кнопки мыши (Перетаскивание)
            else if (_inputManager.IsLButtonDown() && _draggedObject != null)
            {
                float responsiveness = 12f;

                // Передаем объекту задачу следовать за курсором
                _draggedObject.OnDrag(currentMouseWorldXna, responsiveness);
            }

            // Страховка от потери фокуса Windows
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
