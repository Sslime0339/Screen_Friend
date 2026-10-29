using Genbox.VelcroPhysics.Dynamics;
using Genbox.VelcroPhysics.Factories;
using Raylib_cs;
using VirtualFriend.Scripts.Interfaces;
using SysVector = System.Numerics.Vector2;
using XnaVector = Microsoft.Xna.Framework.Vector2;

namespace VirtualFriend.Scripts.GameObjects
{
    // Добавили наследование интерфейса IDraggable
    internal class BoxObject : GameObject, IDraggable
    {
        private readonly float _size;
        private int _animFrame = 0;

        public float Size => _size;

        public BoxObject(World world, SysVector spawnPos, float size)
        {
            _size = size;
            PhysicsBody = BodyFactory.CreateRectangle(world, size, size, 2f, new XnaVector(spawnPos.X, spawnPos.Y));
            PhysicsBody.BodyType = BodyType.Dynamic;
            PhysicsBody.Restitution = 0.2f;
            PhysicsBody.Friction = 0.6f;
        }

        // --- Реализация интерфейса IClickable ---

        public bool IsUnderMouse(SysVector mouseWorldPos)
        {
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
        }

        // --- Стандартные методы GameObject ---

        public override void Update(float dt)
        {
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
