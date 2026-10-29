using Genbox.VelcroPhysics.Dynamics;
using SysVector = System.Numerics.Vector2;
using XnaVector = Microsoft.Xna.Framework.Vector2;

namespace VirtualFriend.Scripts.Interfaces
{
    internal interface ITeleportable
    {
        // Метод для пересоздания физического тела объекта в новом мире
        void TeleportToWorld(World targetWorld, SysVector position, XnaVector velocity);
    }
}
