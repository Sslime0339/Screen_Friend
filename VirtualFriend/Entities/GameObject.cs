

using Raylib_cs;


using nkast.Aether.Physics2D.Dynamics;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Collision.Shapes;
using AetherVector = nkast.Aether.Physics2D.Common.Vector2;
using VirtualFriend.Graphics;
using VirtualFriend.Entities.Objects;

namespace VirtualFriend.Entities
{
    internal abstract class GameObject
    {
        public Body PhysicsBody { get; protected set; }
        public bool IsDestroyed { get; protected set; }

        public WindowZoneObject CurrentWindow { get; set; } = null;

        // Первое число: Глобальный слой
        public DrawLayer Layer { get; set; } = DrawLayer.DynamicObjects;

        // Второе число: Порядок внутри этого слоя (Z-Index)
        public int ZIndex { get; set; } = 0;

        public abstract void Update(float dt);
        public abstract void Draw(RenderEngine renderer, ResourceManager resourceManager);

        public virtual void Destroy()
        {
            IsDestroyed = true;
            if (PhysicsBody != null)
            {
                PhysicsBody.World?.Remove(PhysicsBody);
                PhysicsBody = null;
            }
        }

    }
}
