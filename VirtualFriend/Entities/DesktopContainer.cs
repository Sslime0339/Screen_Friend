using nkast.Aether.Physics2D.Dynamics;
using System;
using System.Collections.Generic;
using System.Text;
using VirtualFriend.Interfaces;


using AetherVector = nkast.Aether.Physics2D.Common.Vector2;
using SysVector = System.Numerics.Vector2;



namespace VirtualFriend.Entities
{

    internal class DesktopContainer : IObjectContainer
    {
        private readonly ObjectStorage _storage = new();
        public World PhysicsWorld { get; }
        public IReadOnlyList<GameObject> Objects => _storage.Objects;

        public DesktopContainer(World world) => PhysicsWorld = world;


        public void Sort() => _storage.SortByDepth();

        public void AddObject(GameObject obj, SysVector pos, AetherVector vel)
        {
            if (obj is ITeleportable t) t.TeleportToWorld(PhysicsWorld, pos, vel);
            obj.CurrentContainer = this; // Вместо CurrentWindow типа WindowZoneObject
            _storage.Add(obj);
        }

        public void RemoveObject(GameObject obj)
        {
            _storage.Remove(obj);
            if (obj.CurrentContainer == this) obj.CurrentContainer = null;
        }
    }

}
