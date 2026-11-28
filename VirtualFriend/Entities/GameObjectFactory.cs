using nkast.Aether.Physics2D.Collision.Shapes;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Dynamics;
using System;
using System.Collections.Generic;

using VirtualFriend.Entities.Objects;
using VirtualFriend.Interfaces;
using VirtualFriend.Systems;
using AetherVector = nkast.Aether.Physics2D.Common.Vector2;
using SysVector = System.Numerics.Vector2;




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

        private readonly ObjectManager _objectManager;


        private GameObjectFactory(ObjectManager objectManager)
        {
            _objectManager = objectManager;
        }

        public static void Initialize(ObjectManager objectManager)
        {
            if (_instance != null) throw new InvalidOperationException("Уже инициализирована.");
            _instance = new GameObjectFactory(objectManager);
        }

        // --- Методы создания объектов ---


        // Метод для создания коробок
        public BoxObject CreateBox(SysVector spawnPos, float size, IObjectContainer? container = null)
        {
            container ??= _objectManager.Desktop;
            var box = new BoxObject(container.PhysicsWorld, spawnPos, size);
            _objectManager.Register(box, container);
            return box;
        }

        // Метод для создания зон окон
        // TODO: как костыль чтобы не рисовать эти объекты когда свёрнуты, 
        public WindowZoneObject CreateWindowZone(World world, WindowManager.WindowData data, float ppm)
        {
            var zone = new WindowZoneObject(world, data, ppm);
            _objectManager.Register(zone); // Окна не кладем в Desktop, они сами контейнеры
            return zone;
        }


    }
}
