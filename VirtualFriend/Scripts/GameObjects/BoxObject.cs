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

            int textureId = (int)((Math.Round(rotationInDegrees) % 360 + 360) % 360);
            SpriteFrame currentFrame = resourceManager.GetSpriteFrame(Assets.Sheets.Box, textureId);

            // --- так как текстура в спрайт листе расположена спецефически, из-за краёв, её нужно правильно расположить
            // потом этот функционал можно переместить куда нибуть

            // короче магические числа для того чтобы текстура встала ровно в хит бокс

            float dWidth = 0.64f * _size;
            float dHeight = 0.64f * _size;

            float dX =  + 0.045f * _size - dWidth / 2;
            float dY = - 0.055f * _size - dHeight / 2;

            Rectangle destRec = new Rectangle(PhysicsBody.Position.X + dX, PhysicsBody.Position.Y + dY, _size + dWidth, _size + dHeight);
            SysVector origin = new SysVector(_size / 2f, _size / 2f);



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


            // для отслеживания хитбокса
            //destRec = new Rectangle(PhysicsBody.Position.X, PhysicsBody.Position.Y, _size, _size);
            //origin = new SysVector(_size / 2f, _size / 2f);


            //renderer.DrawBox(destRec.Position, _size, _size, origin, rotationInDegrees, new Color(0, 255, 0, 20));
        }
    }
}
