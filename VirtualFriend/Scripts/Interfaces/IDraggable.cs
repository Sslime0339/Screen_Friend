using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Xna.Framework;
using Genbox.VelcroPhysics.Dynamics;


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
        void OnDrag(Vector2 targetWorldPos, float responsiveness, float scrollDelta, float dt);

        // Метод броска объекта (отпускание мыши)
        void OnDragEnd(Vector2 releaseVelocity);
    }
}
