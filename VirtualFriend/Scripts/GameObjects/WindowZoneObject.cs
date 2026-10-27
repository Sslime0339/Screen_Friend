using System;
using System.Collections.Generic;
using Raylib_cs;
using Genbox.VelcroPhysics.Dynamics;
using Genbox.VelcroPhysics.Factories;
using XnaVector = Microsoft.Xna.Framework.Vector2;
using SysVector = System.Numerics.Vector2;

namespace VirtualFriend.Scripts.GameObjects
{
    internal class WindowZoneObject : GameObject
    {
        public IntPtr Hwnd { get; private set; } // Хэндл реального окна для связи
        public float Width { get; private set; }
        public float Height { get; private set; }
        public SysVector Center { get; private set; }

        private readonly List<GameObject> _microObjects = new List<GameObject>();
        private readonly List<Body> _innerWalls = new List<Body>();

        private readonly World _localWorld;

        public WindowZoneObject(World world, WindowManager.WindowData data)
        {
            Hwnd = data.Hwnd;
            Width = data.Width;
            Height = data.Height;
            Center = new SysVector(data.CenterX, data.CenterY);
            _localWorld = new World(new Microsoft.Xna.Framework.Vector2(0f, 19.6f));

            // 1. Создаем внешнюю оболочку (сенсор)
            PhysicsBody = BodyFactory.CreateRectangle(world, Width, Height, 1f, new XnaVector(Center.X, Center.Y));
            PhysicsBody.BodyType = BodyType.Static;
            // PhysicsBody.IsSensor = true;

            // 2. Строим внутренние «стены» окна
            BuildInnerBoundaries(_localWorld);

            // 3. Инициализируем микромир
            SpawnMicroWorldContents(_localWorld);
        }

        // Метод динамического обновления положения физических границ
        public void UpdatePositionAndSize(WindowManager.WindowData data)
        {
            // Если окно не двигалось и не меняло размер — ничего не делаем
            if (Width == data.Width && Height == data.Height && Center == new SysVector(data.CenterX, data.CenterY))
                return;

            Width = data.Width;
            Height = data.Height;
            Center = new SysVector(data.CenterX, data.CenterY);

            // Перемещаем основное тело-сенсор
            if (PhysicsBody != null)
            {
                PhysicsBody.Position = new XnaVector(Center.X, Center.Y);
                // Если изменился размер, лучше пересоздать fixture, но для простоты двигаем саму ноду
            }

            // Пересоздаем внутренние стены под новые размеры окна
            ClearInnerBoundaries(_localWorld);
            BuildInnerBoundaries(_localWorld);
        }

        private void BuildInnerBoundaries(World world)
        {
            float thickness = 5f;
            // Нижняя граница (пол окна)
            var floor = BodyFactory.CreateRectangle(world, Width * 2, thickness, 1f, new XnaVector(Center.X, Center.Y + Height / 2f + thickness / 2));
            floor.BodyType = BodyType.Static;
            _innerWalls.Add(floor);

            // верхняя граница
            var ceiling = BodyFactory.CreateRectangle(world, Width * 2, thickness, 1f, new XnaVector(Center.X, Center.Y - Height / 2f - thickness / 2));
            ceiling.BodyType = BodyType.Static;
            _innerWalls.Add(ceiling);

            // Левая стена
            var leftWall = BodyFactory.CreateRectangle(world, thickness, Height * 2, 1f, new XnaVector(Center.X - Width / 2f - thickness / 2, Center.Y));
            leftWall.BodyType = BodyType.Static;
            _innerWalls.Add(leftWall);

            // Правая стена
            var rightWall = BodyFactory.CreateRectangle(world, thickness, Height * 2, 1f, new XnaVector(Center.X + Width / 2f + thickness / 2, Center.Y));
            rightWall.BodyType = BodyType.Static;
            _innerWalls.Add(rightWall);


        }

        private void ClearInnerBoundaries(World world)
        {
            foreach (var wall in _innerWalls)
            {
                world.DestroyBody(wall);
            }
            _innerWalls.Clear();
        }

        private void SpawnMicroWorldContents(World world)
        {
            for (int i = 0; i < 5; i++)
            {
                SysVector spawnPos = new SysVector(Center.X + (i * 0.2f) - 0.4f, Center.Y - 0.2f);
                _microObjects.Add(GameObjectFactory.Instance.CreateBox(world, spawnPos, 0.5f));
            }
        }

        public override void Update(float dt)
        {
            _localWorld.Step(dt);

            foreach (var obj in _microObjects)
            {
                obj.Update(dt);
            }
        }

        public override void Draw(RenderEngine renderer, ResourceManager resourceManager)
        {
#if DEBUG
            var rect = new Rectangle(Center.X - Width / 2f, Center.Y - Height / 2f, Width, Height);
            Raylib.DrawRectangleLinesEx(rect, 0.05f, Color.Blue);
#endif

            foreach (var obj in _microObjects)
            {
                obj.Draw(renderer, resourceManager);
            }
        }

        public override void Destroy(World world)
        {
            foreach (var obj in _microObjects) obj.Destroy(world);
            _microObjects.Clear();

            ClearInnerBoundaries(world);

            _localWorld.Clear();

            base.Destroy(world);
        }
    }
}
