


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
using VirtualFriend.Views;
using static VirtualFriend.Scripts.Win32Native;
using SysVector = System.Numerics.Vector2;
using XnaVector = Microsoft.Xna.Framework.Vector2;



// TODO: Сделать перемещение окон через скорость а не телепортацию по позиции, и вынести эту функцию


// TODO: вынести сортировку и отрисовку в отдельную функцию
// TODO: Сделать чтобы если ящик перекрывает окно то за ящик нельзя схватиться


// TODO: Сделать обрезку объектов которые находятся на рабочем столе


// TODO: Сделать норм миры хз
// TODO: хрень полная



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



        private static readonly List<GameObject> _allGameObjects = new List<GameObject>();

        private static readonly List<GameObject> _desktopGameObjects = new List<GameObject>();
        private static bool _isRunning = true;
        private static bool _isMenuWindowOpen = false; 
        private static System.Windows.Application? _wpfApp = null;

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

            IntPtr ReylibHWnd;
            
            unsafe
            {
                ReylibHWnd = (IntPtr)Raylib.GetWindowHandle();
            }



            const float ppm = 50f; // Pixels Per Meter


            _resourceManager = new ResourceManager();
            _resourceManager.LoadSpriteSheet(Assets.Sheets.Box, "Assets/Textures/box_sheet.png", 128, 128);
            _resourceManager.LoadFont(Assets.Fonts.MainFont, "Assets/Fonts/arial.ttf");


            _physicsSystem = new PhysicsSystem(new Microsoft.Xna.Framework.Vector2(0f, 19.6f)); // Гравитация
            _renderEngine = new RenderEngine(ppm, _resourceManager);
            InitWalls(ppm);


            GameObjectFactory.Initialize(_allGameObjects);


            _windowManager = new WindowManager(_physicsSystem.World, ppm);

            _windowManager.OnWindowMinimized += Global_OnWindowMinimized;
            _windowManager.OnWindowMaximized += Global_OnWindowMaximized;

            _windowManager.StartScanning(workAreaWidth, workAreaHeight, screenWidth, screenHeight, ReylibHWnd);


            _inputManager = new InputManager(_renderEngine);




            GameObject newObj = GameObjectFactory.Instance.CreateBox(_physicsSystem.World, new SysVector(30, 5), 5);
            _desktopGameObjects.Add(newObj);




            RenderTexture2D _canvas = Raylib.LoadRenderTexture(screenWidth, screenHeight);



            OpenMainMenu();




            while (_isRunning)
            {


                float dt = Raylib.GetFrameTime();


                WindowZoneObject WinUnderMouse = GetTopWindowUnderMouse(_inputManager.GetMouseWorldPosition());
                // ОБЯЗАТЕЛЬНО: Обновляем состояние ввода каждый кадр!
                _inputManager.Update(_allGameObjects, dt, WinUnderMouse);

                // --- Логика управления ящиками и объектами ---
                HandleInput(dt);

                // Обновление логики объектов
                for (int i = _allGameObjects.Count - 1; i >= 0; i--)
                {
                    _allGameObjects[i].Update(dt);
                    if (_allGameObjects[i].IsDestroyed) _allGameObjects.RemoveAt(i);
                }

                _windowManager.Update(ppm, dt);

                // Шаг физического мира
                _physicsSystem.Step(dt);


                // --- ДВУХУРОВНЕВАЯ СОРТИРОВКА ОТРИСОВКИ ---
                _desktopGameObjects.Sort((a, b) =>
                {
                    // 1. Сначала сравниваем по слою (Layer)
                    int layerCompare = a.Layer.CompareTo(b.Layer);
                    if (layerCompare != 0) return layerCompare;

                    // 2. Если слои равны, сортируем по внутреннему ZIndex
                    return a.ZIndex.CompareTo(b.ZIndex);
                });






                // ШАГ 1: Рисуем внутренние объекты на текстуру в режиме WorldSpace
                Raylib.BeginTextureMode(_canvas);
                Raylib.ClearBackground(Color.Blank);


                _renderEngine.BeginWorldSpace();
                foreach (var obj in _desktopGameObjects)
                {
                    if (!(obj is WindowZoneObject))
                    {
                        obj.Draw(_renderEngine, _resourceManager);
                    }
                }
                _renderEngine.EndWorldSpace();



                // ШАГ 2: Вырезаем области окон, которые находятся ВЫШЕ текущего окна (ZIndex меньше)
                Raylib.BeginBlendMode(BlendMode.Custom); // Включаем стирание контента

                foreach (var win in _windowManager.GetActiveWindowObjects())
                {
                    if (!win.IsShow) continue;

                    // Переводим метры обратно в пиксели экрана для корректного вырезания
                    float screenW = win.Width * ppm;
                    float screenH = win.Height * ppm;
                    float screenX = (win.Center.X * ppm) - (screenW / 2f);
                    float screenY = (win.Center.Y * ppm) - (screenH / 2f);

                    // Вырезаем «дыру» на холсте
                    Raylib.DrawRectangleRec(new Rectangle(screenX, screenY, screenW, screenH), Color.Blank);
                    
                }

                Raylib.EndBlendMode();
                Raylib.EndTextureMode();


                Raylib.ClearBackground(Color.Blank);

                // ШАГ 3: Выводим готовую замаскированную текстуру на экран
                // Текстуры RenderTexture в Raylib инвертированы по вертикали, переворачиваем по Y (-canvas.Texture.Height)
                Raylib.DrawTextureRec(
                    _canvas.Texture,
                    new Rectangle(0, 0, _canvas.Texture.Width, -_canvas.Texture.Height),
                    SysVector.Zero,
                    Color.White
                );








                /*
                // Отрисовка сцены
                _renderEngine.Begin(Color.Blank);

                _renderEngine.BeginWorldSpace();
                foreach (var obj in _desktopGameObjects)
                {
                    if (!(obj is WindowZoneObject))
                    {
                        obj.Draw(_renderEngine, _resourceManager);
                    }
                }
                _renderEngine.EndWorldSpace();*/

                var activeWindows = _windowManager.GetActiveWindowObjects();
                foreach (var obj in _windowManager.GetActiveWindowObjects())
                {
                    // Вызываем кастомный метод отрисовки, передавая ему список всех окон для анализа
                    obj.DrawWithMasking(_renderEngine, _resourceManager, activeWindows);
                }

                // Отрисовка UI текста (пока рисуется жестко поверх всего кода)
                _renderEngine.DrawTextWithShadow($"Коробок: {_allGameObjects.Count}", new SysVector(10, 800), 24, Color.Pink);

                _renderEngine.End();


                if (Raylib.WindowShouldClose() || _inputManager.IsExitPressed()) _isRunning = false;
            }

            _windowManager.Stop();
            _resourceManager.UnloadAll();
            Raylib.CloseWindow();


            System.Environment.Exit(0);

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

        private static void HandleInput(float dt)
        {
            // --- 1. Создание ящика (Клавиша C) ---
            if (_inputManager.IsSpawnPressed())
            {
                SysVector mouseWorldPos = _inputManager.GetMouseWorldPosition();
                float boxSize = 1.0f;
                BoxObject newBox = GameObjectFactory.Instance.CreateBox(_physicsSystem.World, mouseWorldPos, boxSize);
                _desktopGameObjects.Add(newBox);
                HandleObjectTeleportation(newBox, mouseWorldPos, new XnaVector(0,0));
            }

            // --- 2. Очистка сцены (Клавиша X) ---
            if (_inputManager.IsClearPressed())
            {
                foreach (var obj in _allGameObjects)
                {
                    if (obj is BoxObject)
                    {
                        obj.Destroy(_physicsSystem.World);
                    }
                }
                _draggedObject = null; // Сбрасываем захват при очистке
            }


            // --- Открывание главного меню ---
            if (_inputManager.IsMenuPressed())
            {
                OpenMainMenu();
            }

            // --- 3. ХВАТАНИЕ И ПЕРЕТАСКИВАНИЕ (Через интерфейсы) ---
            SysVector currentMouseWorld = _inputManager.GetMouseWorldPosition();
            XnaVector currentMouseWorldXna = new XnaVector(currentMouseWorld.X, currentMouseWorld.Y);

            // 1. СОБЫТИЕ: Клик только что произошел (Нажатие кнопки)
            if (_inputManager.IsLButtonPressed() && _draggedObject == null)
            {
                WindowZoneObject WinUnderMouse = GetTopWindowUnderMouse(currentMouseWorld);
                foreach (var obj in _allGameObjects)
                {
                    // Проверяем, поддерживает ли объект перетаскивание
                    if (obj is IDraggable draggable)
                    {
                        // Проверяем попадание мыши методами интерфейса IClickable
                        if (draggable.IsUnderMouse(currentMouseWorld, WinUnderMouse))
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

                if (_draggedObject is GameObject boxObj) HandleObjectTeleportation(boxObj, currentMouseWorld, releaseVelocity);

                _draggedObject.OnDragEnd(releaseVelocity);
                _draggedObject = null;
            }

            // 3. СОБЫТИЕ: Удержание кнопки мыши (Перетаскивание)
            else if (_inputManager.IsLButtonDown() && _draggedObject != null)
            {
                float mouseWorldVelocityLength = _inputManager.MouseWorldVelocity.Length();
                float responsiveness = 1f * (mouseWorldVelocityLength * mouseWorldVelocityLength * 0.01f + 10);

                // Считываем вертикальное движение колесика мыши (возвращает -1, 0 или 1)
                float scrollDelta = Raylib.GetMouseWheelMove();

                // Передаем объекту задачу следовать за курсором и крутиться
                _draggedObject.OnDrag(currentMouseWorldXna, responsiveness, scrollDelta, dt);
            }

            // Страховка от потери фокуса Windows
            if (!_inputManager.IsLButtonDown() && _draggedObject != null)
            {
                _draggedObject = null;
            }
        }
        private static void HandleObjectTeleportation(GameObject obj, SysVector currentMouseWorld, XnaVector releaseVelocity)
        {
            if (obj is ITeleportable teleportableObj)
            {
                // Получаем верхнее окно под мышкой
                WindowZoneObject topWindow = GetTopWindowUnderMouse(currentMouseWorld);

                if (topWindow != null)
                {
                    // Если окно под мышкой отличается от текущего окна объекта
                    if (obj.CurrentWindow != topWindow)
                    {
                        // Если объект был внутри какого-то окна, извлекаем его оттуда
                        if (obj.CurrentWindow != null)
                        {
                            obj.CurrentWindow.TeleportOut(obj);
                        }
                        // Если объект был на рабочем столе (внутри списка GameObject), удаляем его оттуда
                        else if (teleportableObj is GameObject gameObj)
                        {
                            _desktopGameObjects.Remove(gameObj);
                        }

                        // Переносим объект в новое окно
                        topWindow.TeleportIn(obj, currentMouseWorld, releaseVelocity);
                        //Console.WriteLine("Объект телепортирован в окно");
                    }
                }
                // Если окон под мышкой нет, но объект сейчас находится внутри окна — выносим его на рабочий стол
                else if (obj.CurrentWindow != null)
                {
                    obj.CurrentWindow.TeleportOut(obj);
                    teleportableObj.TeleportToWorld(_physicsSystem.World, currentMouseWorld, releaseVelocity);

                    // Если это игровой объект, возвращаем его в список отрисовки рабочего стола
                    if (teleportableObj is GameObject gameObj)
                    {
                        _desktopGameObjects.Add(gameObj);
                    }

                    //Console.WriteLine("Объект телепортирован на рабочий стол");
                }
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

        private static WindowZoneObject GetTopWindowUnderMouse(SysVector mouseWorldPos)
        {
            List<WindowZoneObject> windowsUnderMouse = new List<WindowZoneObject>();

            // Собираем все окна, в границы которых попадает курсор мыши
            foreach (var win in _windowManager.GetActiveWindowObjects())
            {
                if (!win.IsShow) continue;

                float halfW = win.Width / 2f;
                float halfH = win.Height / 2f;

                if (mouseWorldPos.X >= win.Center.X - halfW && mouseWorldPos.X <= win.Center.X + halfW &&
                    mouseWorldPos.Y >= win.Center.Y - halfH && mouseWorldPos.Y <= win.Center.Y + halfH)
                {
                    windowsUnderMouse.Add(win);
                }
            }

            // Если нашли окна под мышкой, сортируем по ZIndex и возвращаем самое верхнее (индекс 0)
            if (windowsUnderMouse.Count > 0)
            {
                windowsUnderMouse.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));
                return windowsUnderMouse[0];
            }

            // Окон под курсором нет
            return null;
        }

        private static void Global_OnWindowMinimized(WindowZoneObject minimizedWindow)
        {
            Console.WriteLine($"Окно {minimizedWindow.Hwnd} свернуто. Выталкиваем объекты наружу...");

            // Создаем копию списка, так как HandleObjectTeleportation будет модифицировать оригинальный коллекцию внутри окна
            var objectsToMove = new List<GameObject>(minimizedWindow.MicroObjects);

            foreach (var obj in objectsToMove)
            {
                if (obj.PhysicsBody == null) continue;

                // Получаем текущие параметры объекта в метрах физического мира
                SysVector currentPos = new SysVector(obj.PhysicsBody.Position.X, obj.PhysicsBody.Position.Y);
                XnaVector currentVelocity = obj.PhysicsBody.LinearVelocity;

                // Временно отвязываем от текущего свернутого окна, чтобы HandleObjectTeleportation посчитал его свободным
                //minimizedWindow.TeleportOut(obj);

                // Функция сама определит: отправить объект в окно уровнем ниже (если оно под ним) или на рабочий стол
                HandleObjectTeleportation(obj, currentPos, currentVelocity);
            }
        }

        private static void Global_OnWindowMaximized(WindowZoneObject maximizedWindow)
        {
            Console.WriteLine($"Окно {maximizedWindow.Hwnd} развернуто на весь экран! Собираем объекты рабочего стола...");

            // Создаем копию списка объектов рабочего стола, так как будем удалять элементы из него во время итерации
            var desktopObjectsCopy = new List<GameObject>(_desktopGameObjects);

            foreach (var obj in desktopObjectsCopy)
            {
                // WindowZoneObject тоже лежит в общем списке, его не трогаем, переносим только ящики/персонажей
                if (obj is WindowZoneObject || obj.PhysicsBody == null) continue;

                SysVector currentPos = new SysVector(obj.PhysicsBody.Position.X, obj.PhysicsBody.Position.Y);
                XnaVector currentVelocity = obj.PhysicsBody.LinearVelocity;

                // Удаляем с рабочего стола
                _desktopGameObjects.Remove(obj);

                // Телепортируем внутрь распахнутого окна
                maximizedWindow.TeleportIn(obj, currentPos, currentVelocity);
            }
        }


        private static void OpenMainMenu()
        {
            if (_isMenuWindowOpen) return;

            _isMenuWindowOpen = true;

            // Создаем и запускаем поток WPF в режиме STA
            Thread wpfThread = new Thread(() =>
            {
                try
                {
                    // Инициализируем глобальное WPF приложение строго ОДИН РАЗ за всю жизнь процесса
                    if (_wpfApp == null)
                    {
                        _wpfApp = new System.Windows.Application();

                        // ВАЖНО: Запрещаем WPF автоматически завершать работу приложения при закрытии окон.
                        // Это сохранит контекст живым для последующих открытий меню!
                        _wpfApp.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
                    }

                    // Создаем экземпляр главного меню
                    MainWindow mainMenu = new MainWindow();

                    // Когда окно закрывается пользователем, просто сбрасываем флаг контроля
                    mainMenu.Closed += (s, e) =>
                    {
                        _isMenuWindowOpen = false;
                    };

                    // Открываем окно
                    mainMenu.ShowDialog();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при открытии меню: {ex.Message}");
                    _isMenuWindowOpen = false;
                }
            });

            wpfThread.SetApartmentState(ApartmentState.STA); // Обязательно для WPF
            wpfThread.Start();
        }

    }
}
