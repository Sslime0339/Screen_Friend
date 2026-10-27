using Raylib_cs;
using Genbox.VelcroPhysics.Dynamics;
using Genbox.VelcroPhysics.Factories;
using XnaVector = Microsoft.Xna.Framework.Vector2;
using SysVector = System.Numerics.Vector2;

namespace VirtualFriend.Scripts.GameObjects
{
    internal class BoxObject : GameObject
    {

        private readonly float _size;
        private int _animFrame = 0;

        private int _currentFrame = 0;
        private float _timeCount = 0f;
        private float _frameDuration = 0.05f; // Скорость анимации
        private float _sizeMeters = 1.2f;

        public BoxObject(World world, SysVector spawnPos, float size)
        {
            _size = size;
            PhysicsBody = BodyFactory.CreateRectangle(world, size, size, 2f, new XnaVector(spawnPos.X, spawnPos.Y));
            PhysicsBody.BodyType = BodyType.Dynamic;
            PhysicsBody.Restitution = 0.2f;
            PhysicsBody.Friction = 0.6f;
        }

        public override void Update(float dt)
        {
            // Здесь можно обновлять кадры анимации конкретно этой коробки
        }

        public override void Draw(RenderEngine renderer)
        {
            if (IsDestroyed) return;




            float rotationInDegrees = PhysicsBody.Rotation * (180f / (float)Math.PI);

            // Задаем размеры вывода на экране
            Rectangle destRec = new Rectangle(PhysicsBody.Position.X, PhysicsBody.Position.Y, _size, _size);
            SysVector origin = new SysVector(_size / 2f, _size / 2f);

            SpriteFrame currentFrame = ResourceManager.GetSpriteFrame(Assets.Sheets.Box, (int)rotationInDegrees);


            // Отрисовка через ваш RenderEngine (или напрямую через Raylib)
            // Используем SourceRec нашего кадра вместо всей текстуры!
            renderer.DrawTexturePro(
                currentFrame.Texture,
                currentFrame.SourceRec,
                destRec,
                origin,
                rotationInDegrees,
                Color.White
            );
        }
    }
}
