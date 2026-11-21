using System;
using System.Collections.Generic;
using System.Text;




using nkast.Aether.Physics2D.Dynamics;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Collision.Shapes;
using AetherVector = nkast.Aether.Physics2D.Common.Vector2;



namespace VirtualFriend.Scripts.Interfaces
{
    // Наследуемся от IClickable, так как перетаскивание всегда начинается с клика
    internal interface IDraggable : IClickable
    {
        // Ссылка на физическое тело для манипуляций со скоростью
        Body PhysicsBody { get; }

        // Метод начала перетаскивания (например, сбросить старую скорость)
        void OnDragStart();

        // Метод самого процесса удержания мыши
        void OnDrag(AetherVector targetWorldPos, float responsiveness, float scrollDelta, float dt);

        // Метод броска объекта (отпускание мыши)
        void OnDragEnd(AetherVector releaseVelocity);
    }
}
