using Genbox.VelcroPhysics.Dynamics;
using Genbox.VelcroPhysics.Factories;
using Raylib_cs;
using VirtualFriend.Scripts.Interfaces;
using SysVector = System.Numerics.Vector2;
using XnaVector = Microsoft.Xna.Framework.Vector2;

namespace VirtualFriend.Scripts.GameObjects
{
    // Добавили наследование интерфейса IDraggable
    internal class BoxObject : GameObject, IDraggable, ITeleportable
    {
        private readonly float _size;
        private int _animFrame = 0;

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
            PhysicsBody = BodyFactory.CreateRectangle(world, _size, _size, 2f, new XnaVector(pos.X, pos.Y));
            PhysicsBody.BodyType = BodyType.Dynamic;
            PhysicsBody.Restitution = 0.2f;
            PhysicsBody.Friction = 0.6f;
            PhysicsBody.Rotation = rotation;
            PhysicsBody.AngularVelocity = angularVelocity;
        }

        // Реализация метода телепортации
        public void TeleportToWorld(World targetWorld, SysVector position, XnaVector velocity)
        {
            float rotation = PhysicsBody.Rotation;
            float angularVelocity = PhysicsBody.AngularVelocity;
            // 1. Если у объекта уже есть тело в каком-то мире — удаляем его
            if (PhysicsBody != null)
            {
                PhysicsBody.RemoveFromWorld();
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

        public bool IsUnderMouse(SysVector mouseWorldPos)
        {
            if (CurrentWindow != null)
            {
                if (!CurrentWindow.IsShow) return false;
            }

            if (PhysicsBody == null || IsDestroyed) return false;

            float halfSize = _size / 2f;
            var pos = PhysicsBody.Position;

            return mouseWorldPos.X >= pos.X - halfSize && mouseWorldPos.X <= pos.X + halfSize &&
                   mouseWorldPos.Y >= pos.Y - halfSize && mouseWorldPos.Y <= pos.Y + halfSize;
        }

        public void OnClick()
        {
            // Логика при простом клике (если нужна в будущем)
        }

        // --- Реализация интерфейса IDraggable ---

        public void OnDragStart()
        {
            if (PhysicsBody == null) return;
            // Обнуляем старые скорости, чтобы коробка мгновенно перешла под контроль
            PhysicsBody.LinearVelocity = XnaVector.Zero;
            PhysicsBody.AngularVelocity = 0f;

            // ЭФФЕКТ: Приподнимаем коробку над остальными во время перетаскивания
            // Layer = DrawLayer.Characters;
        }

        public void OnDrag(XnaVector targetWorldPos, float responsiveness)
        {
            if (PhysicsBody == null) return;

            // Вычисляем вектор расстояния до мыши
            float targetX = targetWorldPos.X - PhysicsBody.Position.X;
            float targetY = targetWorldPos.Y - PhysicsBody.Position.Y;

            // Притягиваем ящик к курсору
            PhysicsBody.LinearVelocity = new XnaVector(
                targetX * responsiveness,
                targetY * responsiveness
            );
        }

        public void OnDragEnd(XnaVector releaseVelocity)
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

            // Берем текущие координаты ящика из физического тела
            float x = PhysicsBody.Position.X;
            float y = PhysicsBody.Position.Y;

            //float Cos5 = (float)Math.Cos(5.0 * Math.PI / 180.0);
            //float Sin5 = (float)Math.Sin(5.0 * Math.PI / 180.0);

            // Считаем проекцию по вашей формуле
            //float rawSortKey = y * Cos5 - x * Sin5;
            //object.screenY + (object.screenX * 0.087);

            float halfSize = _size / 2f;

            float rawSortKey = (y) - (x);

            // Инвертируем, чтобы верхние объекты (меньший Y) имели больший ZIndex и рисовались позже (поверх)
            // Умножаем на 10000 для сохранения точности float при конвертации в int
            ZIndex = (int)(-rawSortKey * 10000f);
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
