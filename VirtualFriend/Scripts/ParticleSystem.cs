using Raylib_cs;
using System;
using VirtualFriend.Scripts.GameObjects;
using SysVector = System.Numerics.Vector2;

namespace VirtualFriend.Scripts
{
    internal struct Particle
    {
        public bool IsActive;
        public SysVector Position;
        public SysVector Velocity;
        public float Gravity;
        public float Drag; // Сопротивление воздуха (0..1)
        public float Rotation;
        public float AngularVelocity;
        public float StartSize;
        public float EndSize;
        public Color StartColor;
        public Color EndColor;
        public float LifeTime;
        public float MaxLifeTime;
        public WindowZoneObject OwnerWindow;
    }

    internal class ParticleSystem
    {
        private static ParticleSystem? _instance;
        public static ParticleSystem Instance => _instance ??= new ParticleSystem(1500);

        private readonly Particle[] _particles;
        private int _cursor = 0;

        private ParticleSystem(int maxParticles)
        {
            _particles = new Particle[maxParticles];
        }

        public void Update(float dt)
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                if (!_particles[i].IsActive) continue;

                ref var p = ref _particles[i];
                p.LifeTime -= dt;

                if (p.LifeTime <= 0f)
                {
                    p.IsActive = false;
                    continue;
                }

                // Физика движения
                p.Velocity.Y += p.Gravity * dt;
                p.Velocity *= MathF.Pow(1f - p.Drag, dt * 60f);
                p.Position += p.Velocity * dt;
                p.Rotation += p.AngularVelocity * dt;
            }
        }

        public void DrawForWindow(RenderEngine renderer, WindowZoneObject? targetWindow)
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                ref readonly var p = ref _particles[i];
                if (!p.IsActive || p.OwnerWindow != targetWindow) continue;

                float t = 1f - (p.LifeTime / p.MaxLifeTime); // Прогресс жизни от 0.0 до 1.0
                float currentSize = p.StartSize + (p.EndSize - p.StartSize) * t;
                Color currentColor = LerpColor(p.StartColor, p.EndColor, t);

                SysVector origin = new SysVector(currentSize / 2f, currentSize / 2f);
                renderer.DrawBox(p.Position, currentSize, currentSize, origin, p.Rotation, currentColor);
            }
        }

        private void Spawn(Particle particle)
        {
            particle.IsActive = true;
            _particles[_cursor] = particle;
            _cursor = (_cursor + 1) % _particles.Length;
        }

        // --- ГОТОВЫЕ ПРЕСЕТЫ ЭФФЕКТОВ ---

        /// <summary>Пыль при ударе о поверхности</summary>
        public void EmitImpactDust(SysVector position, SysVector normal, float intensity, WindowZoneObject? window)
        {
            int count = Math.Clamp((int)(intensity * 1.5f), 3, 12);

            for (int i = 0; i < count; i++)
            {
                float spread = (Random.Shared.NextSingle() - 0.5f) * 3.0f;
                // Разлет вдоль поверхности удара и немного от нее
                SysVector tangent = new SysVector(-normal.Y, normal.X);
                SysVector vel = (normal * (Random.Shared.NextSingle() * 1.5f + 0.5f)) + (tangent * spread * intensity * 0.4f);

                float life = 0.25f + Random.Shared.NextSingle() * 0.2f;
                float size = 0.08f + Random.Shared.NextSingle() * 0.08f;

                Spawn(new Particle
                {
                    Position = position + normal * 0.05f,
                    Velocity = vel,
                    Gravity = -1.5f, // Пыль слегка поднимается вверх
                    Drag = 0.08f,
                    Rotation = Random.Shared.NextSingle() * 360f,
                    AngularVelocity = (Random.Shared.NextSingle() - 0.5f) * 180f,
                    StartSize = size,
                    EndSize = size * 1.8f,
                    StartColor = new Color(220, 220, 220, 180),
                    EndColor = new Color(220, 220, 220, 0),
                    LifeTime = life,
                    MaxLifeTime = life,
                    OwnerWindow = window
                });
            }
        }

        /// <summary>Щепки при удалении/разрушении коробки</summary>
        public void EmitBoxShatter(SysVector position, float boxSize, WindowZoneObject? window)
        {
            int count = 16;
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Shared.NextSingle() * MathF.PI * 2f;
                float speed = 2f + Random.Shared.NextSingle() * 5f;
                SysVector vel = new SysVector(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed - 2f);

                float life = 0.4f + Random.Shared.NextSingle() * 0.3f;
                float size = (0.06f + Random.Shared.NextSingle() * 0.08f) * boxSize;

                // Оттенки картона/дерева
                byte r = (byte)(180 + Random.Shared.Next(40));
                byte g = (byte)(130 + Random.Shared.Next(30));
                byte b = (byte)(80 + Random.Shared.Next(20));

                Spawn(new Particle
                {
                    Position = position + new SysVector((Random.Shared.NextSingle() - 0.5f) * boxSize * 0.5f, (Random.Shared.NextSingle() - 0.5f) * boxSize * 0.5f),
                    Velocity = vel,
                    Gravity = 19.6f, // Падают с той же гравитацией, что и ящики
                    Drag = 0.02f,
                    Rotation = Random.Shared.NextSingle() * 360f,
                    AngularVelocity = (Random.Shared.NextSingle() - 0.5f) * 720f,
                    StartSize = size,
                    EndSize = size * 0.3f,
                    StartColor = new Color(r, g, b, (byte)255),
                    EndColor = new Color(r, g, b, (byte)0),
                    LifeTime = life,
                    MaxLifeTime = life,
                    OwnerWindow = window
                });
            }
        }

        private static Color LerpColor(Color a, Color b, float t)
        {
            return new Color(
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t),
                (byte)(a.A + (b.A - a.A) * t)
            );
        }

        public void EmitImpactDust(SysVector pointA, SysVector pointB, SysVector normal, float intensity, WindowZoneObject? window)
        {
            int count = Math.Clamp((int)(intensity * 1.5f), 3, 12);

            for (int i = 0; i < count; i++)
            {
                // Случайная позиция вдоль всего ребра контакта (от 0.0 до 1.0)
                float lerpFactor = Random.Shared.NextSingle();
                SysVector spawnPos = SysVector.Lerp(pointA, pointB, lerpFactor);

                float spread = (Random.Shared.NextSingle() - 0.5f) * 3.0f;
                SysVector tangent = new SysVector(-normal.Y, normal.X);
                SysVector vel = (normal * (Random.Shared.NextSingle() * 1.5f + 0.5f)) + (tangent * spread * intensity * 0.4f);

                float life = 0.25f + Random.Shared.NextSingle() * 0.2f;
                float size = 0.08f + Random.Shared.NextSingle() * 0.08f;

                Spawn(new Particle
                {
                    Position = spawnPos + normal * 0.05f, // Используем spawnPos вместо одиночной position
                    Velocity = vel,
                    Gravity = -1.5f,
                    Drag = 0.08f,
                    Rotation = Random.Shared.NextSingle() * 360f,
                    AngularVelocity = (Random.Shared.NextSingle() - 0.5f) * 180f,
                    StartSize = size,
                    EndSize = size * 1.8f,
                    StartColor = new Color(220, 220, 220, 180),
                    EndColor = new Color(220, 220, 220, 0),
                    LifeTime = life,
                    MaxLifeTime = life,
                    OwnerWindow = window
                });
            }
        }
    }
}
