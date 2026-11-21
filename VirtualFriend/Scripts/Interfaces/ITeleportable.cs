

using SysVector = System.Numerics.Vector2;



using nkast.Aether.Physics2D.Dynamics;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Collision.Shapes;
using AetherVector = nkast.Aether.Physics2D.Common.Vector2;

namespace VirtualFriend.Scripts.Interfaces
{
    internal interface ITeleportable
    {
        // Метод для пересоздания физического тела объекта в новом мире
        void TeleportToWorld(World targetWorld, SysVector position, AetherVector velocity);
    }
}
