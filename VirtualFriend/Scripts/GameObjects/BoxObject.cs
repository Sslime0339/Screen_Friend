

using Raylib_cs;
using VirtualFriend.Scripts.Interfaces;
using SysVector = System.Numerics.Vector2;



using nkast.Aether.Physics2D.Dynamics;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Collision.Shapes;
using AetherVector = nkast.Aether.Physics2D.Common.Vector2;




namespace VirtualFriend.Scripts.GameObjects
{
    // Добавили наследование интерфейса IDraggable
    internal class BoxObject : GameObject, IDraggable, ITeleportable
    {
        private readonly float _size;
        // private int _animFrame = 0;
        private const float CrushDotThreshold = -0.75f;     // Угол между нормалями > ~140 градусов (сжатие с двух сторон)
        private const float CrushPenetrationThreshold = 0.06f; // Глубина вдавливания в метрах (подбирается под жесткость стен)
        private float _accumulatedCrushImpulse = 0f;


        public float Size => _size;


        // Конструктор остается прежним...
        public BoxObject(World world, SysVector spawnPos, float size)
        {
            _size = size;
            CreatePhysics(world, spawnPos);
            Layer = DrawLayer.DynamicObjects;
        }

        // Выносим создание тела в отдельный приватный метод для удобства переиспользования
        private void CreatePhysics(World world, SysVector pos, float rotation = 0, float angularVelocity = 0)
        {

            PhysicsBody = world.CreateRectangle(_size, _size, 2f, new AetherVector(pos.X, pos.Y), rotation, BodyType.Dynamic);
            PhysicsBody.BodyType = BodyType.Dynamic; 
            PhysicsBody.FixtureList[0].Restitution = 0.2f;
            PhysicsBody.FixtureList[0].Friction = 0.6f;
            PhysicsBody.Rotation = rotation;
            PhysicsBody.AngularVelocity = angularVelocity;
            PhysicsBody.OnCollision += (sender, other, contact) =>
            {
                // Считаем относительную скорость удара
                var relVel = sender.Body.LinearVelocity - other.Body.LinearVelocity;
                float impactSpeed = relVel.Length();

                // Спавним пыль только если удар достаточно сильный (порог 3.5 м/с)
                if (impactSpeed > 3.5f && contact.Manifold.PointCount > 0)
                {
                    contact.GetWorldManifold(out AetherVector normal, out FixedArray2<AetherVector> points);

                    // В Aether.Physics2D нормаль всегда направлена от FixtureA к FixtureB.
                    // Нам нужно, чтобы вектор смотрел ОТ препятствия (other) К нашему ящику (sender).
                    bool isSenderB = contact.FixtureB.Body == sender.Body;
                    AetherVector outwardNormal = isSenderB ? normal : -normal;
                    SysVector hitNormal = new SysVector(outwardNormal.X, outwardNormal.Y);

                    SysVector pointA = new SysVector(points[0].X, points[0].Y);
                    SysVector pointB = contact.Manifold.PointCount > 1
                        ? new SysVector(points[1].X, points[1].Y)
                        : pointA;

                    ParticleSystem.Instance.EmitImpactDust(pointA, pointB, hitNormal, impactSpeed * 0.5f, CurrentWindow);
                }
                return true;
            };


        }

        // Реализация метода телепортации
        public void TeleportToWorld(World targetWorld, SysVector position, AetherVector velocity)
        {
            float rotation = PhysicsBody != null ? PhysicsBody.Rotation : 0f;
            float angularVelocity = PhysicsBody != null ? PhysicsBody.AngularVelocity : 0f;

            // 1. Если у объекта уже есть тело в каком-то мире — удаляем его
            if (PhysicsBody != null)
            {
                PhysicsBody.World?.Remove(PhysicsBody);
                PhysicsBody = null;
            }

            // 2. Создаем точно такое же тело, но уже в новом targetWorld
            CreatePhysics(targetWorld, position, rotation, angularVelocity);

            // 3. Возвращаем объекту скорость, которая у него была в момент переноса
            if (PhysicsBody != null)
            {
                PhysicsBody.LinearVelocity = velocity;
            }
        }


        // --- Реализация интерфейса IClickable ---

        public bool IsUnderMouse(SysVector mouseWorldPos, WindowZoneObject WinUnderMouse)
        {

            if (CurrentWindow != null)
            {
                if (!CurrentWindow.IsShow) return false;
            }

            if (PhysicsBody == null || IsDestroyed) return false;

            float halfSize = _size / 2f;
            var pos = PhysicsBody.Position;


            if (WinUnderMouse != null && CurrentWindow != null)
            {
                if (CurrentWindow.ZIndex > WinUnderMouse.ZIndex) return false;
            }
            else if (WinUnderMouse != null &&  CurrentWindow == null) return false;


            return mouseWorldPos.X >= pos.X - halfSize && mouseWorldPos.X <= pos.X + halfSize &&
                   mouseWorldPos.Y >= pos.Y - halfSize && mouseWorldPos.Y <= pos.Y + halfSize;
        }

        public void OnClick()
        {
            // Логика при простом клике (если нужна в будущем)
        }


        // Прикольная идея нейронки
        // TODO: когда добавлю меню взаимодействия реализовать тут в ящике
        // Добавьте в класс BoxObject рядом с OnClick():
        public void OnRightClick()
        {
            if (PhysicsBody == null) return;

            // Подбрасываем коробку вверх и слегка закручиваем
            PhysicsBody.LinearVelocity = new AetherVector(PhysicsBody.LinearVelocity.X, -8f);
            PhysicsBody.AngularVelocity = (Random.Shared.NextSingle() - 0.5f) * 10f;
        }

        public void OnMiddleClick()
        {
            Shatter();
        }





        // --- Реализация интерфейса IDraggable ---

        public void OnDragStart()
        {
            if (PhysicsBody == null) return;
            // Обнуляем старые скорости, чтобы коробка мгновенно перешла под контроль
            PhysicsBody.LinearVelocity = AetherVector.Zero;
            PhysicsBody.AngularVelocity = 0f;

            // ЭФФЕКТ: Приподнимаем коробку над остальными во время перетаскивания
            // Layer = DrawLayer.Characters;
        }

        public void OnDrag(AetherVector targetWorldPos, float responsiveness, float scrollDelta, float dt)
        {
            if (PhysicsBody == null) return;



            // Вычисляем вектор расстояния до мыши
            float targetX = targetWorldPos.X - PhysicsBody.Position.X;
            float targetY = targetWorldPos.Y - PhysicsBody.Position.Y;

            // Притягиваем ящик к курсору
            PhysicsBody.LinearVelocity = new AetherVector(
                targetX * responsiveness,
                targetY * responsiveness
            );

            float _targetRotation = PhysicsBody.Rotation;

            // --- 2. ВРАЩЕНИЕ (Через угловую скорость) ---
            // Изменяем целевой угол при прокрутке колесика (например, шаг в 15 градусов)
            if (Math.Abs(scrollDelta) > 0.01f)
            {
                float angleStep = 50f * ((float)Math.PI / 180f); // переводим в радианы
                _targetRotation += Math.Sign(scrollDelta) * angleStep;
            }

            // Вычисляем разницу между целевым и текущим углами
            float angleError = _targetRotation - PhysicsBody.Rotation;

            // Нормализуем разницу углов в диапазон от -PI до PI, чтобы объект крутился по кратчайшему пути
            angleError = (float)Math.Atan2(Math.Sin(angleError), Math.Cos(angleError));

            // Жесткость вращения (чем выше, тем быстрее объект принимает нужный угол)
            float angularResponsiveness = 20f;

            // Формула сглаживания для угловой скорости, аналогичная физике перемещения окон
            float factor = 1f - (float)Math.Exp(-angularResponsiveness * dt);
            PhysicsBody.AngularVelocity = (angleError * factor) / dt;
        }

        public void OnDragEnd(AetherVector releaseVelocity)
        {
            if (PhysicsBody == null) return;
            // Передаем финальное ускорение от броска руки
            PhysicsBody.LinearVelocity = releaseVelocity;

            // Возвращаем обычный слой, когда бросили
            // Layer = DrawLayer.DynamicObjects;
        }

        // --- Стандартные методы GameObject ---


        public override void Update(float dt)
        {
            if (PhysicsBody == null || IsDestroyed) return;

            // 1. Проверка на раздавливание (тиски)
            if (IsCrushed())
            {
                Shatter();
                return;
            }

            // 2. Существующая сортировка отрисовки
            float x = PhysicsBody.Position.X;
            float y = PhysicsBody.Position.Y;
            float rawSortKey = y - x;
            ZIndex = (int)(-rawSortKey * 10000f);
        }

        private bool IsCrushed()
        {
            // 1. Проверка, если окно сжали меньше габаритов ящика (оставляем как есть)
            if (CurrentWindow != null)
            {
                float cos = Math.Abs((float)Math.Cos(PhysicsBody.Rotation));
                float sin = Math.Abs((float)Math.Sin(PhysicsBody.Rotation));
                float boundingWidth = _size * (cos + sin);
                float boundingHeight = _size * (sin + cos);

                if (CurrentWindow.Width < boundingWidth * 0.85f || CurrentWindow.Height < boundingHeight * 0.85f)
                {
                    return true;
                }
            }

            // 2. Проверка тисков по силе давления (NormalImpulse)
            List<(AetherVector normal, float impulse)> contacts = new List<(AetherVector, float)>();

            var contactEdge = PhysicsBody.ContactList;
            while (contactEdge != null)
            {
                var contact = contactEdge.Contact;
                if (contact != null && contact.IsTouching && contact.Manifold.PointCount > 0)
                {
                    contact.GetWorldManifold(out AetherVector normal, out _);

                    bool isBodyB = contact.FixtureB.Body == PhysicsBody;
                    AetherVector inwardNormal = isBodyB ? -normal : normal;

                    // Суммируем нормальный импульс по всем точкам этого контакта (1 или 2 точки)
                    float totalNormalImpulse = 0f;
                    for (int i = 0; i < contact.Manifold.PointCount; i++)
                    {
                        totalNormalImpulse += contact.Manifold.Points[i].NormalImpulse;
                    }

                    contacts.Add((inwardNormal, totalNormalImpulse));
                }
                contactEdge = contactEdge.Next;
            }

            if (contacts.Count < 2) return false;

            // Порог импульса сжатия (зависит от массы ящика, для _size = 1.0 и плотности 2.0 вес ~ 1.5 - 2.0)
            float crushImpulseThreshold = 15f * (_size * _size);

            for (int i = 0; i < contacts.Count; i++)
            {
                for (int j = i + 1; j < contacts.Count; j++)
                {
                    var n1 = contacts[i].normal;
                    var n2 = contacts[j].normal;
                    float dot = n1.X * n2.X + n1.Y * n2.Y;

                    // Если силы направлены друг против друга
                    if (dot < CrushDotThreshold)
                    {
                        // Берем минимальный из двух встречных импульсов — это и есть сила сжатия в тисках
                        float squeezeForce = Math.Min(contacts[i].impulse, contacts[j].impulse);
                        if (squeezeForce > crushImpulseThreshold)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }






        public void Shatter()
        {
            if (IsDestroyed) return;

            if (PhysicsBody != null)
            {
                SysVector pos = new SysVector(PhysicsBody.Position.X, PhysicsBody.Position.Y);
                ParticleSystem.Instance.EmitBoxShatter(pos, _size, CurrentWindow);
            }

            if (CurrentWindow != null)
            {
                CurrentWindow.TeleportOut(this);
            }

            Destroy();
        }


        public override void Draw(RenderEngine renderer, ResourceManager resourceManager)
        {
            if (IsDestroyed) return;

            float rotationInDegrees = PhysicsBody.Rotation * (180f / (float)Math.PI);
            int textureId = (int)((Math.Round(rotationInDegrees) % 360 + 360) % 360);
            SpriteFrame currentFrame = resourceManager.GetSpriteFrame(Assets.Sheets.Box, textureId);

            float dWidth = 0.64f * _size;
            float dHeight = 0.64f * _size;
            float dX = 0.045f * _size - dWidth / 2;
            float dY = -0.055f * _size - dHeight / 2;

            Rectangle destRec = new Rectangle(PhysicsBody.Position.X + dX, PhysicsBody.Position.Y + dY, _size + dWidth, _size + dHeight);
            SysVector origin = new SysVector(_size / 2f, _size / 2f);

            renderer.DrawTexturePro(
                currentFrame.Texture,
                currentFrame.SourceRec,
                destRec,
                origin,
                0,
                Color.White
            );
        }
    }
}
