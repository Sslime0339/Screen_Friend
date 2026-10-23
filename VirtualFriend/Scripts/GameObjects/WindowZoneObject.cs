using System;
using System.Collections.Generic;
using System.Text;

using System.Collections.Generic;
using Raylib_cs;
using Genbox.VelcroPhysics.Dynamics;
using Genbox.VelcroPhysics.Factories;
using XnaVector = Microsoft.Xna.Framework.Vector2;
using SysVector = System.Numerics.Vector2;



namespace VirtualFriend.Scripts.GameObjects
{
    internal class WindowZoneObject
    {
        public float Width { get; private set; }
        public float Height { get; private set; }
        public SysVector Center { get; private set; }

        // Список объектов, принадлежащих конкретно этому микромиру
        private readonly List<GameObject> _microObjects = new List<GameObject>();

        // Коллайдеры внутренних границ (чтобы объекты не вылетали из окна)
        private readonly List<Body> _innerWalls = new List<Body>();

        public WindowZoneObject(World world, WindowData data)
        {
            Width = data.Width;
            Height = data.Height;
            Center = new SysVector(data.CenterX, data.CenterY);

            // 1. Создаем внешнюю оболочку (для коллизий с глобальными коробками, если нужно)
            PhysicsBody = BodyFactory.CreateRectangle(world, Width, Height, 1f, new XnaVector(Center.X, Center.Y));
            PhysicsBody.BodyType = BodyType.Static;
            PhysicsBody.IsSensor = true; // Делаем сенсором, если хотим, чтобы внешние объекты могли залетать внутрь, или твердым, если окна заперты

            // 2. Строим внутренние «стены» этого окна, чтобы микро-сущности не падали вниз экрана
            BuildInnerBoundaries(world);

            // 3. Инициализируем микромир (создаем локальные коробки/эффекты)
            SpawnMicroWorldContents(world);
        }

        private void BuildInnerBoundaries(World world)
        {
            float thickness = 0.1f;
            // Нижняя граница (пол окна)
            var floor = BodyFactory.CreateRectangle(world, Width, thickness, 1f, new XnaVector(Center.X, Center.Y + Height / 2f));
            floor.BodyType = BodyType.Static;
            _innerWalls.Add(floor);

            // Левая и правая стены окна... (по аналогии)
        }

        private void SpawnMicroWorldContents(World world)
        {
            // Например, спавним 5 маленьких коробочек внутри этого конкретного окна
            for (int i = 0; i < 5; i++)
            {
                // Координаты относительно центра окна
                SysVector spawnPos = new SysVector(Center.X + (i * 0.2f) - 0.4f, Center.Y - 0.2f);
                _microObjects.Add(new BoxObject(world, spawnPos, 0.3f)); // Маленькие коробки по 0.3 метра
            }
        }

        public override void Update(float dt)
        {
            // Обновляем все микро-объекты, живущие в этом окне
            foreach (var obj in _microObjects)
            {
                obj.Update(dt);
            }
        }

        public override void Draw(RenderEngine renderer)
        {
#if DEBUG
            // Рисуем границы самого окна ОС
            var rect = new Rectangle(Center.X - Width / 2f, Center.Y - Height / 2f, Width, Height);
            Raylib.DrawRectangleLinesEx(rect, 0.05f, Color.Blue);
#endif

            // Отрисовываем уникальное содержимое этого окна
            foreach (var obj in _microObjects)
            {
                obj.Draw(renderer);
            }
        }

        public override void Destroy(World world)
        {
            // При закрытии окна Windows — уничтожаем весь его микромир
            foreach (var obj in _microObjects) obj.Destroy(world);
            _microObjects.Clear();

            foreach (var wall in _innerWalls) world.DestroyBody(wall);
            _innerWalls.Clear();

            base.Destroy(world);
        }
    }
}
