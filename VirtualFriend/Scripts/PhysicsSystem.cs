using System;
using System.Collections.Generic;
using Genbox.VelcroPhysics.Dynamics;
using Genbox.VelcroPhysics.Factories;
using Microsoft.Xna.Framework;

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

        /*
        public void UpdateWindowsColliders(List<WindowManager.WindowData> windows)
        {
            // Очищаем старые физические границы окон ОС
            foreach (var oldCollider in _windowColliders)
            {
                if (oldCollider != null)
                {
                    World.DestroyBody(oldCollider);
                }
            }
            _windowColliders.Clear();

            // Если список пуст (например, при Win+D все скрылось), просто выходим
            if (windows == null || windows.Count == 0) return;

            // Создаем новые
            foreach (var win in windows)
            {

                // Защита 2: Velcro Physics не умеет создавать коллайдеры с нулевой или отрицательной геометрией.
                // При сворачивании окон Windows может отдавать некорректные или нулевые размеры.
                if (win.Width <= 0.001f || win.Height <= 0.001f) continue;

                // Защита 3: На всякий случай проверяем координаты на корректность (NaN или Infinity)
                if (float.IsNaN(win.CenterX) || float.IsNaN(win.CenterY)) continue;

                Body windowBody = BodyFactory.CreateRectangle(World, win.Width, win.Height, 1f, new Vector2(win.CenterX, win.CenterY));

                // Защита 4: Если движок всё же вернул null из-за внутренних ограничений, пропускаем шаг
                if (windowBody == null) continue;

                windowBody.BodyType = BodyType.Static;
                windowBody.Friction = 0.4f;
                windowBody.Restitution = 0.2f;
                _windowColliders.Add(windowBody);
            }
        }*/

        public void Step(float dt)
        {
            World.Step(Math.Min(dt, 1f / 60f));
        }



        public static void MoveKinematicBody(Body body, Microsoft.Xna.Framework.Vector2 targetPosition, float dt)
        {
            if (body == null || dt <= 0f) return;

            // Переводим тело в режим Kinematic, чтобы у него работала скорость и передача импульсов
            if (body.BodyType != BodyType.Kinematic)
            {
                body.BodyType = BodyType.Kinematic;
            }

            // Вычисляем вектор расстояния до цели
            Microsoft.Xna.Framework.Vector2 delta = targetPosition - body.Position;

            // Формула скорости: Скорость = Расстояние / Время
            // Это заставляет тело точно прийти в целевую точку к следующему шагу физики
            body.LinearVelocity = delta / dt;
        }
    }
}
