using Genbox.VelcroPhysics.Dynamics;
using Genbox.VelcroPhysics.Factories;
using Raylib_cs;
using System;
using System.Collections.Generic;
using VirtualFriend.Scripts.Interfaces;
using SysVector = System.Numerics.Vector2;
using XnaVector = Microsoft.Xna.Framework.Vector2;

namespace VirtualFriend.Scripts.GameObjects
{
    internal class WindowZoneObject : GameObject
    {
        public IntPtr Hwnd { get; private set; }
        public float Width { get; private set; }
        public float Height { get; private set; }
        public SysVector Center { get; private set; }
        public bool IsShow { get; private set; }

        private readonly List<GameObject> _microObjects = new List<GameObject>();
        private readonly List<Body> _innerWalls = new List<Body>();
        private readonly World _localWorld;
        private readonly World _bodyWorld;

        // Холст для рисования микромира текущего окна
        private RenderTexture2D _canvas;
        private bool _isCanvasInitialized = false;
        private readonly float _ppm;



        private const float WallThickness = 50;

        public WindowZoneObject(World world, WindowManager.WindowData data, float ppm)
        {
            Hwnd = data.Hwnd;
            Width = data.Width;
            Height = data.Height;
            Center = new SysVector(data.CenterX, data.CenterY);
            ZIndex = data.ZIndex; // Сохраняем ZIndex
            _ppm = ppm;
            _bodyWorld = world;

            _localWorld = new World(new Microsoft.Xna.Framework.Vector2(0f, 19.6f));

            CreateOuterBody(world);
            BuildInnerBoundaries(_localWorld);
            SpawnMicroWorldContents(_localWorld);

            this.Layer = DrawLayer.Background;

            // Инициализируем текстуру под размер экрана оверлея
            InitCanvas();
        }

        private void CreateOuterBody(World world)
        {
            if (PhysicsBody != null)
            {
                Console.WriteLine("Тело уже есть");
                return;
            }
            PhysicsBody = BodyFactory.CreateRectangle(world, Width, Height, 1f, new XnaVector(Center.X, Center.Y));
            PhysicsBody.BodyType = BodyType.Kinematic;
        }

        private void DeleteOuterBody()
        {
            if (PhysicsBody == null)
            {
                Console.WriteLine("Тело нет");
                return;
            }
            PhysicsBody.RemoveFromWorld();
            PhysicsBody = null;
        }


        private void InitCanvas()
        {
            int screenWidth = Raylib.GetScreenWidth();
            int screenHeight = Raylib.GetScreenHeight();
            _canvas = Raylib.LoadRenderTexture(screenWidth, screenHeight);
            _isCanvasInitialized = true;
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
                PhysicsSystem.MoveKinematicBody(PhysicsBody, new XnaVector(Center.X, Center.Y), dt, force);
                //cPhysicsBody.Position = new XnaVector(Center.X, Center.Y);
            }

            // Вместо удаления (ClearInnerBoundaries) и пересоздания стен, 
            // мы вычисляем их новые целевые позиции и двигаем через физику!
            if (_innerWalls.Count == 4)
            {
                XnaVector targetFloor = new XnaVector(Center.X, Center.Y + Height / 2f + WallThickness / 2);
                XnaVector targetCeiling = new XnaVector(Center.X, Center.Y - Height / 2f - WallThickness / 2);
                XnaVector targetLeft = new XnaVector(Center.X - Width / 2f - WallThickness / 2, Center.Y);
                XnaVector targetRight = new XnaVector(Center.X + Width / 2f + WallThickness / 2, Center.Y);

                PhysicsSystem.MoveKinematicBody(_innerWalls[0], targetFloor, dt, force);
                PhysicsSystem.MoveKinematicBody(_innerWalls[1], targetCeiling, dt, force);
                PhysicsSystem.MoveKinematicBody(_innerWalls[2], targetLeft, dt, force);
                PhysicsSystem.MoveKinematicBody(_innerWalls[3], targetRight, dt, force);
            }

        }

        private void BuildInnerBoundaries(World world)
        {

            // Каждая из стен теперь создается изначально, но мы настраиваем тип на Kinematic
            var floor = BodyFactory.CreateRectangle(world, Width * 2, WallThickness, 1f, new XnaVector(Center.X, Center.Y + Height / 2f + WallThickness / 2));
            floor.BodyType = BodyType.Kinematic;
            _innerWalls.Add(floor);

            var ceiling = BodyFactory.CreateRectangle(world, Width * 2, WallThickness, 1f, new XnaVector(Center.X, Center.Y - Height / 2f - WallThickness / 2));
            ceiling.BodyType = BodyType.Kinematic;
            _innerWalls.Add(ceiling);

            var leftWall = BodyFactory.CreateRectangle(world, WallThickness, Height * 2, 1f, new XnaVector(Center.X - Width / 2f - WallThickness / 2, Center.Y));
            leftWall.BodyType = BodyType.Kinematic;
            _innerWalls.Add(leftWall);

            var rightWall = BodyFactory.CreateRectangle(world, WallThickness, Height * 2, 1f, new XnaVector(Center.X + Width / 2f + WallThickness / 2, Center.Y));
            rightWall.BodyType = BodyType.Kinematic;
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
                BoxObject newObject = GameObjectFactory.Instance.CreateBox(world, spawnPos, 0.5f);
                newObject.CurrentWindow = this;
                _microObjects.Add(newObject);
            }
        }


        public void TeleportIn(GameObject obj, SysVector worldPos, XnaVector velocity)
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

            foreach (var obj in _microObjects)
            {
                obj.Update(dt);
            }
        }

                // В метод Draw передаем список всех активных окон для вычисления перекрытия
        public void DrawWithMasking(RenderEngine renderer, ResourceManager resourceManager, List<WindowZoneObject> allWindows)
        {
            if (!_isCanvasInitialized || IsDestroyed || !IsShow) return;

            // ШАГ 1: Рисуем внутренние объекты на текстуру в режиме WorldSpace
            Raylib.BeginTextureMode(_canvas);
            Raylib.ClearBackground(Color.Blank);

            // --- ОБНОВЛЕНО: ОБРЕЗКА ПО РЕАЛЬНЫМ КООРДИНАТАМ СТЕН ---
            float thisScreenX, thisScreenY, thisScreenW, thisScreenH;

            if (_innerWalls.Count == 4)
            {
                // Берем координаты из физических тел (учитывая толщину стен thickness = 50f)
                // В BuildInnerBoundaries: 
                // левая стена центрирована по X = Center.X - Width/2 - thickness/2
                // верхняя стена центрирована по Y = Center.Y - Height/2 - thickness/2
                float thickness = 50f;

                float physicsLeftEdge = _innerWalls[2].Position.X + (thickness / 2f);
                float physicsTopEdge = _innerWalls[1].Position.Y + (thickness / 2f);
                float physicsRightEdge = _innerWalls[3].Position.X - (thickness / 2f);
                float physicsBottomEdge = _innerWalls[0].Position.Y - (thickness / 2f);

                // Переводим в экранные пиксели
                thisScreenX = physicsLeftEdge * _ppm;
                thisScreenY = physicsTopEdge * _ppm;
                thisScreenW = (physicsRightEdge - physicsLeftEdge) * _ppm;
                thisScreenH = (physicsBottomEdge - physicsTopEdge) * _ppm;
            }
            else
            {
                // Запасной вариант, если стены еще не создались
                thisScreenW = Width * _ppm;
                thisScreenH = Height * _ppm;
                thisScreenX = (Center.X * _ppm) - (thisScreenW / 2f);
                thisScreenY = (Center.Y * _ppm) - (thisScreenH / 2f);
            }

            // Активируем обрезку по реальным физическим границам
            Raylib.BeginScissorMode((int)thisScreenX, (int)thisScreenY, (int)thisScreenW, (int)thisScreenH);


            // --- ДВУХУРОВНЕВАЯ СОРТИРОВКА ОТРИСОВКИ ---
            _microObjects.Sort((a, b) =>
            {
                // 1. Сначала сравниваем по слою (Layer)
                int layerCompare = a.Layer.CompareTo(b.Layer);
                if (layerCompare != 0) return layerCompare;

                // 2. Если слои равны, сортируем по внутреннему ZIndex
                return a.ZIndex.CompareTo(b.ZIndex);
            });

            renderer.BeginWorldSpace(); // Включаем матрицу камеры
            foreach (var obj in _microObjects)
            {
                obj.Draw(renderer, resourceManager);
            }
            renderer.EndWorldSpace();

            // Выключаем обрезку по границам текущего окна
            Raylib.EndScissorMode();


            // ШАГ 2: Вырезаем области окон, которые находятся ВЫШЕ текущего окна (ZIndex меньше)
            Raylib.BeginBlendMode(BlendMode.Custom); // Включаем стирание контента

            foreach (var win in allWindows)
            {
                if (!win.IsShow) continue;

                // Если чужое окно выше нас по иерархии окон Windows (1 — топ)
                if (win.Hwnd != this.Hwnd && win.ZIndex < this.ZIndex)
                {
                    // Переводим метры обратно в пиксели экрана для корректного вырезания
                    float screenW = win.Width * _ppm;
                    float screenH = win.Height * _ppm;
                    float screenX = (win.Center.X * _ppm) - (screenW / 2f);
                    float screenY = (win.Center.Y * _ppm) - (screenH / 2f);

                    // Вырезаем «дыру» на холсте
                    Raylib.DrawRectangleRec(new Rectangle(screenX, screenY, screenW, screenH), Color.Blank);
                }
            }

            Raylib.EndBlendMode();
            Raylib.EndTextureMode();

            // ШАГ 3: Выводим готовую замаскированную текстуру на экран
            // Текстуры RenderTexture в Raylib инвертированы по вертикали, переворачиваем по Y (-canvas.Texture.Height)
            Raylib.DrawTextureRec(
                _canvas.Texture,
                new Rectangle(0, 0, _canvas.Texture.Width, -_canvas.Texture.Height),
                SysVector.Zero,
                Color.White
            );

#if DEBUG
            // Отрисовка отладочной рамки самого окна (уже на экране)
            var rect = new Rectangle((Center.X - Width / 2f) * _ppm, (Center.Y - Height / 2f) * _ppm, Width * _ppm, Height * _ppm);
            Raylib.DrawRectangleLinesEx(rect, 1f, Color.Blue);

            renderer.DrawTextWithShadow($"{this.Hwnd}", new SysVector((Center.X - Width / 2f) * _ppm, (Center.Y - Height / 2f) * _ppm), 24, Color.White);

            DrawWalls();
#endif
        }


#if DEBUG

        public void DrawWalls()
        {
            foreach (var wall in _innerWalls)
            {
                if (wall.FixtureList == null || wall.FixtureList.Count == 0)
                    continue;

                // В VelcroPhysics прямоугольники, созданные через BodyFactory.CreateRectangle, являются PolygonShape
                if (wall.FixtureList[0].Shape is Genbox.VelcroPhysics.Collision.Shapes.PolygonShape polygonShape)
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

        public override void Destroy(World world)
        {
            foreach (var obj in _microObjects) obj.Destroy(_localWorld);
            _microObjects.Clear();
            ClearInnerBoundaries(_localWorld);
            _localWorld.Clear();

            if (_isCanvasInitialized)
            {
                Raylib.UnloadRenderTexture(_canvas);
                _isCanvasInitialized = false;
            }

            base.Destroy(world);
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
