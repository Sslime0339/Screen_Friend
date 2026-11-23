using nkast.Aether.Physics2D.Dynamics;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using VirtualFriend.Entities;
using VirtualFriend.Interfaces;



using AetherVector = nkast.Aether.Physics2D.Common.Vector2;
using SysVector = System.Numerics.Vector2;



namespace VirtualFriend.Systems
{
    internal class ObjectManager
    {
        private readonly List<GameObject> _allObjects = new();

        public IReadOnlyList<GameObject> AllObjects => _allObjects;

        //private readonly List<UIElement> _uiElements = new();
        public DesktopContainer Desktop { get; } // Контейнер по умолчанию

        public ObjectManager(World desktopWorld)
        {
            Desktop = new DesktopContainer(desktopWorld);
        }


        public void Register(GameObject obj, IObjectContainer? container = null)
        {
            if (!_allObjects.Contains(obj))
                _allObjects.Add(obj);

            if (container != null)
            {
                var pos = obj.PhysicsBody != null ? new SysVector(obj.PhysicsBody.Position.X, obj.PhysicsBody.Position.Y) : SysVector.Zero;
                var vel = obj.PhysicsBody != null ? obj.PhysicsBody.LinearVelocity : AetherVector.Zero;
                container.AddObject(obj, pos, vel);
            }
        }


        public void MoveObject(GameObject obj, IObjectContainer target, SysVector pos, AetherVector vel)
        {
            if (obj.CurrentContainer == target) return;

            obj.CurrentContainer?.RemoveObject(obj);
            target.AddObject(obj, pos, vel);
        }


        public void UpdateAll(float dt)
        {
            for (int i = _allObjects.Count - 1; i >= 0; i--)
            {
                var obj = _allObjects[i];
                obj.Update(dt);

                if (obj.IsDestroyed)
                {
                    obj.CurrentContainer?.RemoveObject(obj);
                    _allObjects.RemoveAt(i);
                }
            }
        }
    }

}
