

using Raylib_cs;
using System;
using System.Collections.Generic;
using VirtualFriend.Scripts.Interfaces;
using SysVector = System.Numerics.Vector2;



using nkast.Aether.Physics2D.Dynamics;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Collision.Shapes;
using AetherVector = nkast.Aether.Physics2D.Common.Vector2;



namespace VirtualFriend.Scripts.GameObjects
{
    internal class WindowZoneObject : GameObject
    {
        public IntPtr Hwnd { get; private set; }
        public float Width { get; private set; }
        public float Height { get; private set; }
        public SysVector Center { get; private set; }
        public bool IsShow { get; private set; }
        public bool WasMaximized { get; set; } = false;



        public IReadOnlyList<GameObject> MicroObjects => _microObjects;




        private readonly List<GameObject> _microObjects = new List<GameObject>();
        private readonly List<Body> _innerWalls = new List<Body>();
        private readonly World _localWorld;
        private readonly World _bodyWorld;


        private readonly float _ppm;



        private const float WallThickness = 50;

        public WindowZoneObject(World world, WindowManager.WindowData data, float ppm)
        {
            IsShow = true;
            Hwnd = data.Hwnd;
            Width = data.Width;
            Height = data.Height;
            Center = new SysVector(data.CenterX, data.CenterY);
            ZIndex = data.ZIndex;
            _ppm = ppm;
            _bodyWorld = world;

            _localWorld = new World(new AetherVector(0f, 19.6f));

            CreateOuterBody(world);
            BuildInnerBoundaries(_localWorld);
            SpawnMicroWorldContents(_localWorld);

            this.Layer = DrawLayer.Background;


        }

        private void CreateOuterBody(World world)
        {
            if (PhysicsBody != null)
            {
                Console.WriteLine("Тело уже есть");
                return;
            }

            PhysicsBody = world.CreateRectangle(Width, Height, 1f, new AetherVector(Center.X, Center.Y), 0f, BodyType.Kinematic);
        }

        private void DeleteOuterBody()
        {
            if (PhysicsBody == null)
            {
                Console.WriteLine("Тело нет");
                return;
            }

            _bodyWorld.Remove(PhysicsBody);
            PhysicsBody = null;
        }




        public void UpdatePositionAndSize(WindowManager.WindowData data, float dt) // <-- ДОБАВЛЕН dt ПАРАМЕТР
        {
            ZIndex = data.ZIndex;

            // ВАЖНО: Больше не делаем return при совпадении координат, 
            // так как кинематические тела должны поддерживать скорость (или занулять её), пока окно статично.

            Width = data.Width;
            Height = data.Height;
            Center = new SysVector(data.CenterX, data.CenterY);

            // Сила стремления стен к целевой позиции окна (настройте под себя)
            // 20f — хорошая базовая жесткость, чтобы стены не отставали слишком сильно
            float force = 20f;


            if (PhysicsBody != null)
            {
                PhysicsSystem.MoveKinematicBody(PhysicsBody, new AetherVector(Center.X, Center.Y), dt, force);
                //cPhysicsBody.Position = new XnaVector(Center.X, Center.Y);
            }

            // Вместо удаления (ClearInnerBoundaries) и пересоздания стен, 
            // мы вычисляем их новые целевые позиции и двигаем через физику!
            if (_innerWalls.Count == 4)
            {
                AetherVector targetFloor = new AetherVector(Center.X, Center.Y + Height / 2f + WallThickness / 2);
                AetherVector targetCeiling = new AetherVector(Center.X, Center.Y - Height / 2f - WallThickness / 2);
                AetherVector targetLeft = new AetherVector(Center.X - Width / 2f - WallThickness / 2, Center.Y);
                AetherVector targetRight = new AetherVector(Center.X + Width / 2f + WallThickness / 2, Center.Y);

                PhysicsSystem.MoveKinematicBody(_innerWalls[0], targetFloor, dt, force);
                PhysicsSystem.MoveKinematicBody(_innerWalls[1], targetCeiling, dt, force);
                PhysicsSystem.MoveKinematicBody(_innerWalls[2], targetLeft, dt, force);
                PhysicsSystem.MoveKinematicBody(_innerWalls[3], targetRight, dt, force);
            }

        }

        private void BuildInnerBoundaries(World world)
        {
            var floor = world.CreateRectangle(Width * 2, WallThickness, 1f, new AetherVector(Center.X, Center.Y + Height / 2f + WallThickness / 2), 0f, BodyType.Kinematic);
            _innerWalls.Add(floor);

            var ceiling = world.CreateRectangle(Width * 2, WallThickness, 1f, new AetherVector(Center.X, Center.Y - Height / 2f - WallThickness / 2), 0f, BodyType.Kinematic);
            _innerWalls.Add(ceiling);

            var leftWall = world.CreateRectangle(WallThickness, Height * 2, 1f, new AetherVector(Center.X - Width / 2f - WallThickness / 2, Center.Y), 0f, BodyType.Kinematic);
            _innerWalls.Add(leftWall);

            var rightWall = world.CreateRectangle(WallThickness, Height * 2, 1f, new AetherVector(Center.X + Width / 2f + WallThickness / 2, Center.Y), 0f, BodyType.Kinematic);
            _innerWalls.Add(rightWall);
        }

        private void ClearInnerBoundaries(World world)
        {
            foreach (var wall in _innerWalls)
            {
                world.Remove(wall);
            }
            _innerWalls.Clear();
        }


        private void SpawnMicroWorldContents(World world)
        {
#if DEBUG
            for (int i = 0; i < 5; i++)
            {
                SysVector spawnPos = new SysVector(Center.X + (i * 0.2f) - 0.4f, Center.Y - 0.2f);
                BoxObject newObject = GameObjectFactory.Instance.CreateBox(world, spawnPos, 0.5f);
                newObject.CurrentWindow = this;
                _microObjects.Add(newObject);
            }
#endif
        }


        public void TeleportIn(GameObject obj, SysVector worldPos, AetherVector velocity)
        {
            if (obj is ITeleportable teleportableObj)
            {
                // Переносим физическое тело объекта в локальный мир этого окна
                teleportableObj.TeleportToWorld(_localWorld, worldPos, velocity);

                // Прописываем «дом»
                obj.CurrentWindow = this;

                // Перемещаем объект во внутренний список окна
                _microObjects.Add(obj);
            }
        }
        public void TeleportOut(GameObject obj)
        {
            _microObjects.Remove(obj);
            obj.CurrentWindow = null; // Объект больше не принадлежит этому окну
        }




        public override void Update(float dt)
        {
            if (IsDestroyed || _localWorld == null) return;

            _localWorld.Step(Math.Min(dt, 1f / 60f));

            for (int i = _microObjects.Count - 1; i >= 0; i--)
            {
                _microObjects[i].Update(dt);
            }
        }
        // Новый сверхбыстрый метод отрисовки с использованием буфера глубины
        public void DrawWithDepthMask(RenderEngine renderer, ResourceManager resourceManager, float depthZ)
        {

            // TODO: проверить код от нейронки для отрисовки в WindowZoneObject



            if (IsDestroyed || !IsShow) return;

            float thisScreenX, thisScreenY, thisScreenW, thisScreenH;

            if (_innerWalls.Count == 4)
            {
                float thickness = 50f; 
                float physicsLeftEdge = _innerWalls[2].Position.X + (thickness / 2f);
                float physicsTopEdge = _innerWalls[1].Position.Y + (thickness / 2f);
                float physicsRightEdge = _innerWalls[3].Position.X - (thickness / 2f);
                float physicsBottomEdge = _innerWalls[0].Position.Y - (thickness / 2f);


                thisScreenX = physicsLeftEdge * _ppm;
                thisScreenY = physicsTopEdge * _ppm;
                thisScreenW = (physicsRightEdge - physicsLeftEdge) * _ppm;
                thisScreenH = (physicsBottomEdge - physicsTopEdge) * _ppm;
            }
            else
            {
                thisScreenW = Width * _ppm;
                thisScreenH = Height * _ppm;
                thisScreenX = (Center.X * _ppm) - (thisScreenW / 2f);
                thisScreenY = (Center.Y * _ppm) - (thisScreenH / 2f);
            }
            // 1. РИСУЕМ ОБЪЕКТЫ ВНУТРИ ОКНА
            //if (_microObjects.Count > 0)
            {
                _microObjects.Sort((a, b) =>
                {
                    int layerCompare = a.Layer.CompareTo(b.Layer);
                    if (layerCompare != 0) return layerCompare;
                    return a.ZIndex.CompareTo(b.ZIndex);
                });

                Raylib.BeginScissorMode((int)thisScreenX, (int)thisScreenY, (int)thisScreenW, (int)thisScreenH);

                renderer.BeginWorldSpace(); // Сначала запускаем камеру!
                Rlgl.DrawRenderBatchActive();
                Rlgl.PushMatrix();
                Rlgl.Translatef(0f, 0f, depthZ); // Теперь сдвигаем по Z внутри мирового пространства

                foreach (var obj in _microObjects)
                {
                    obj.Draw(renderer, resourceManager);
                }


                ParticleSystem.Instance.DrawForWindow(renderer, this);


                Rlgl.DrawRenderBatchActive();
                Rlgl.PopMatrix();
                renderer.EndWorldSpace();

                Raylib.EndScissorMode();
            }

            // 2. ЗАПЕЧАТЫВАЕМ ОКНО В БУФЕР ГЛУБИНЫ
            Rlgl.DrawRenderBatchActive();
            Rlgl.ColorMask(false, false, false, false);

            Rlgl.PushMatrix();
            Rlgl.Translatef(0f, 0f, depthZ);

            float winScreenW = Width * _ppm;
            float winScreenH = Height * _ppm;
            float winScreenX = (Center.X * _ppm) - (winScreenW / 2f);
            float winScreenY = (Center.Y * _ppm) - (winScreenH / 2f);

            Raylib.DrawRectangle((int)winScreenX, (int)winScreenY, (int)winScreenW, (int)winScreenH, Color.White);

            Rlgl.DrawRenderBatchActive();
            Rlgl.PopMatrix();

            Rlgl.ColorMask(true, true, true, true);
        }


#if DEBUG

        public void DrawWalls()
        {
            foreach (var wall in _innerWalls)
            {
                if (wall.FixtureList == null || wall.FixtureList.Count == 0)
                    continue;


                if (wall.FixtureList[0].Shape is nkast.Aether.Physics2D.Collision.Shapes.PolygonShape polygonShape)
                {
                    // Получаем локальные вершины полигона
                    var vertices = polygonShape.Vertices;
                    if (vertices.Count < 4) continue;

                    // Находим минимальные и максимальные локальные координаты, чтобы вычислить размеры
                    float minX = float.MaxValue, maxX = float.MinValue;
                    float minY = float.MaxValue, maxY = float.MinValue;

                    foreach (var vertex in vertices)
                    {
                        if (vertex.X < minX) minX = vertex.X;
                        if (vertex.X > maxX) maxX = vertex.X;
                        if (vertex.Y < minY) minY = vertex.Y;
                        if (vertex.Y > maxY) maxY = vertex.Y;
                    }

                    float width = maxX - minX;
                    float height = maxY - minY;

                    // Переводим позицию тела из метров в пиксели экрана
                    float posX = wall.Position.X * _ppm;
                    float posY = wall.Position.Y * _ppm;

                    // Вычисляем размеры прямоугольника в пикселях
                    float screenW = width * _ppm;
                    float screenH = height * _ppm;

                    // Вычисляем верхний левый угол для отрисовки в Raylib (смещение от центра тела)
                    float screenX = posX - (width / 2f * _ppm);
                    float screenY = posY - (height / 2f * _ppm);

                    // Рисуем полупрозрачную рамку вокруг стены для отладки
                    var rect = new Rectangle(screenX, screenY, screenW, screenH);
                    Raylib.DrawRectangleLinesEx(rect, 2f, Color.Red);
                    Raylib.DrawRectangleRec(rect, new Color(255, 0, 0, 40)); // Легкая красная заливка
                }
            }
        }
        
#endif


        public override void Draw(RenderEngine renderer, ResourceManager resourceManager)
        {
            // Этот метод оставляем пустым или вызываем базовый, так как основная логика теперь в DrawWithMasking
        }

        public override void Destroy()
        {
            foreach (var obj in _microObjects) obj.Destroy();
            _microObjects.Clear();
            ClearInnerBoundaries(_localWorld);
            _localWorld.Clear();


            base.Destroy();
        }




        public void Hide()
        {
            if (!IsShow) throw new Exception("Окно уже скрыто");
            IsShow = false;
            DeleteOuterBody();
        }

        public void Show()
        {
            if (IsShow) throw new Exception("Окно уже показано");
            IsShow = true;
            CreateOuterBody(_bodyWorld);
        }


    }
}
