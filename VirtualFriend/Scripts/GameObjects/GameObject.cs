using Genbox.VelcroPhysics.Dynamics;
using Raylib_cs;



namespace VirtualFriend.Scripts.GameObjects
{
    internal abstract class GameObject
    {
        public Body PhysicsBody { get; protected set; }
        public bool IsDestroyed { get; protected set; }

        // Добавляем слой отрисовки. По умолчанию пусть будет средний слой мира
        public DrawLayer Layer { get; set; } = DrawLayer.DynamicObjects;

        public abstract void Update(float dt);
        public abstract void Draw(RenderEngine renderer, ResourceManager resourceManager);

        public virtual void Destroy(World world) // TODO: убрать ссылку на world
        {
            IsDestroyed = true;
            if (PhysicsBody != null)
            {
                PhysicsBody.RemoveFromWorld();
                PhysicsBody = null;
            }
        }
    }
}
