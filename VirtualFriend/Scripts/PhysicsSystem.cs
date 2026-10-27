using System;
using System.Collections.Generic;
using System.Text;


using Genbox.VelcroPhysics.Dynamics;
using Genbox.VelcroPhysics.Factories;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;



namespace VirtualFriend.Scripts
{
    internal class PhysicsSystem
    {

        public World World { get; private set; }
        private readonly List<Body> _windowColliders = new List<Body>();

        public PhysicsSystem(Vector2 gravity)
        {
            World = new World(gravity);
        }

        public void UpdateWindowsColliders(List<WindowManager.WindowData> windows)
        {
            // Очищаем старые физические границы окон ОС
            foreach (var oldCollider in _windowColliders)
            {
                World.DestroyBody(oldCollider);
            }
            _windowColliders.Clear();

            // Создаем новые
            foreach (var win in windows)
            {
                Body windowBody = BodyFactory.CreateRectangle(World, win.Width, win.Height, 1f, new Vector2(win.CenterX, win.CenterY));
                windowBody.BodyType = BodyType.Static;
                windowBody.Friction = 0.4f;
                windowBody.Restitution = 0.2f;
                _windowColliders.Add(windowBody);
            }
        }

        public void Step(float dt)
        {
            World.Step(Math.Min(dt, 1f / 60f));
        }
    }
}
