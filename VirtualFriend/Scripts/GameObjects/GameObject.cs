using Genbox.VelcroPhysics.Dynamics;
using Raylib_cs;



namespace VirtualFriend.Scripts.GameObjects
{
    internal abstract class GameObject
    {

        // Ссылка на физическое тело (может быть null, если объект нефизичный, например UI)
        public Body PhysicsBody { get; protected set; }

        // Флаг для удаления объекта из игры
        public bool IsDestroyed { get; protected set; }

        public abstract void Update(float dt);
        public abstract void Draw(RenderEngine renderer, ResourceManager resourceManager);

        public virtual void Destroy(World world)
        {
            IsDestroyed = true;
            if (PhysicsBody != null)
            {
                world.DestroyBody(PhysicsBody);
                PhysicsBody = null;
            }
        }
    }
}
