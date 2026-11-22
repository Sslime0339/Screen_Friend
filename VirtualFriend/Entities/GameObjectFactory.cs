using System;
using System.Collections.Generic;
using SysVector = System.Numerics.Vector2;


using nkast.Aether.Physics2D.Dynamics;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Collision.Shapes;
using AetherVector = nkast.Aether.Physics2D.Common.Vector2;
using VirtualFriend.Systems;
using VirtualFriend.Entities.Objects;




namespace VirtualFriend.Entities
{

    internal class GameObjectFactory
    {


        // 1. Статический поток для хранения единственного экземпляра (Синглтон)
        private static GameObjectFactory? _instance;

        public static GameObjectFactory Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new InvalidOperationException("Фабрика GameObjectFactory еще не была инициализирована! Вызовите Initialize() на старте игры.");
                }
                return _instance;
            }
        }

        private readonly List<GameObject> _gameObjects;

        // Приватный или внутренний конструктор, чтобы предотвратить случайное создание через new вне контроля
        private GameObjectFactory(List<GameObject> gameObjects)
        {
            _gameObjects = gameObjects ?? throw new ArgumentNullException(nameof(gameObjects));
        }

        // 2. Метод для инициализации Синглтона на старте программы
        public static void Initialize(List<GameObject> gameObjects)
        {
            if (_instance != null)
            {
                throw new InvalidOperationException("Фабрика GameObjectFactory уже инициализирована.");
            }
            _instance = new GameObjectFactory(gameObjects);
        }

        // --- Методы создания объектов ---


        // Метод для создания коробок
        public BoxObject CreateBox(World world, SysVector spawnPos, float size)
        {
            var box = new BoxObject(world, spawnPos, size);
            _gameObjects.Add(box); // АВТОМАТИЧЕСКОЕ ДОБАВЛЕНИЕ В СПИСОК
            return box;
        }

        // Метод для создания зон окон
        // TODO: как костыль чтобы не рисовать эти объекты когда свёрнуты, 
        public WindowZoneObject CreateWindowZone(World world, WindowManager.WindowData data, float ppm)
        {
            // Передаем саму фабрику (this) внутрь зоны, 
            // чтобы зона могла плодить микро-коробки через фабрику!
            var zone = new WindowZoneObject(world, data, ppm);
            _gameObjects.Add(zone); // АВТОМАТИЧЕСКОЕ ДОБАВЛЕНИЕ В СПИСОК
            return zone;
        }


    }
}
