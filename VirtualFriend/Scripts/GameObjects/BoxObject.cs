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

        public override void Draw(RenderEngine renderer, ResourceManager resourceManager)
        {
            if (IsDestroyed) return;



            float rotationInDegrees = PhysicsBody.Rotation * (180f / (float)Math.PI);

            // Задаем размеры вывода на экране
            Rectangle destRec = new Rectangle(PhysicsBody.Position.X, PhysicsBody.Position.Y, _size, _size);
            SysVector origin = new SysVector(_size / 2f, _size / 2f);
            
            int textureId = (int)((rotationInDegrees % 360 + 360) % 360);
            SpriteFrame currentFrame = resourceManager.GetSpriteFrame(Assets.Sheets.Box, textureId);

            //Console.WriteLine((int)rotationInDegrees);
            //Console.WriteLine(currentFrame.Texture.Height);


            renderer.DrawBox(destRec.Position, (int)_size, (int)_size, origin, rotationInDegrees, Color.Lime);

            // Отрисовка через ваш RenderEngine (или напрямую через Raylib)
            // Используем SourceRec нашего кадра вместо всей текстуры!
            renderer.DrawTexturePro(
                currentFrame.Texture,
                currentFrame.SourceRec,
                destRec,
                origin,
                0,
                //rotationInDegrees,
                Color.White
            );
        }
    }
}
