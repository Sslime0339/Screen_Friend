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

        public override void Draw(RenderEngine renderer)
        {
            if (PhysicsBody == null) return;

            SysVector pos = new SysVector(PhysicsBody.Position.X, PhysicsBody.Position.Y);
            float rotationDeg = PhysicsBody.Rotation * (180f / (float)Math.PI);

            renderer.DrawBox(pos, _size, rotationDeg, _animFrame);
        }
    }
}
