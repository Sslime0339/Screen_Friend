


// имя человечка - РИХЭЫШЪЕ
// да я сгенерировал случайные символы



using Raylib_cs;
using VirtualFriend.Scripts;





// подключаем другие прстранства имён
using VirtualFriend.Scripts.GameObjects;






namespace VirtualFriend
{
    internal class Program
    {
        private readonly WindowManager _windowManager;
        private readonly PhysicsSystem _physicsSystem;
        private readonly RenderEngine _renderEngine;



        private readonly List<GameObject> _gameObjects = new List<GameObject>();
        private bool _isRunning = true;


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


            int workAreaWidth = 1600;
            int workAreaHeight = 860;

            Raylib.InitWindow(workAreaWidth, workAreaHeight, "Raylib-cs + Velcro Physics overlay");
            Raylib.SetTargetFPS(60);



            const float ppm = 50f; // Pixels Per Meter
            _windowManager = new WindowManager(ppm);
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


                if (Raylib.WindowShouldClose()) _isRunning = false;
            }


            // После завершения цикла while (_isRunning)
            _renderEngine.UnloadResources(); // Выгружаем текстуры и шрифты
            Raylib.CloseWindow();

        }
    }
}
