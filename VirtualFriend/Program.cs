


// имя человечка - РИХЭЫШЪЕ
// да я сгенерировал случайные символы



using Raylib_cs;
using VirtualFriend.Scripts;

using SysVector = System.Numerics.Vector2;




// подключаем другие прстранства имён
using VirtualFriend.Scripts.GameObjects;
using static VirtualFriend.Scripts.Win32Native;






namespace VirtualFriend
{
    internal class Program
    {
        private static WindowManager _windowManager;
        private static PhysicsSystem _physicsSystem;
        private static RenderEngine _renderEngine;



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




            while (_isRunning)
            {

                float dt = Raylib.GetFrameTime();


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
                    obj.Draw(_renderEngine);
                }
                _renderEngine.EndWorldSpace();

                // Отрисовка UI текста
                _renderEngine.DrawTextWithShadow($"Коробок: {_gameObjects.Count}", new SysVector(10, 800), 24, Color.Pink);

                _renderEngine.End();


                if (Raylib.WindowShouldClose() || InputManager.IsExitPressed()) _isRunning = false;
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


    }
}
