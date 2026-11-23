using nkast.Aether.Physics2D.Dynamics;
using System;
using System.Collections.Generic;
using System.Text;
using VirtualFriend.Entities;


using AetherVector = nkast.Aether.Physics2D.Common.Vector2;
using SysVector = System.Numerics.Vector2;


namespace VirtualFriend.Interfaces
{
    internal interface IObjectContainer
    {
        World PhysicsWorld { get; }
        IReadOnlyList<GameObject> Objects { get; }

        void AddObject(GameObject obj, SysVector position, AetherVector velocity);
        void RemoveObject(GameObject obj);
    }
}
