using Genbox.VelcroPhysics.Dynamics;
using Genbox.VelcroPhysics.Factories;
using Raylib_cs;
using System;
//using VelcroPhysics.Dynamics;
//using VelcroPhysics.Factories;
using SysVector = System.Numerics.Vector2;
// Объявляем псевдонимы типов для исключения конфликтов
using XnaVector = Microsoft.Xna.Framework.Vector2;

class Program
{
    // Сколько пикселей содержится в одном метре
    const float PixelsPerMeter = 50f;

    static void Main()
    {
        const int screenWidth = 800;
        const int screenHeight = 600;
        Raylib.InitWindow(screenWidth, screenHeight, "Raylib-cs + Velcro Physics (XNA Aliases)");
        Raylib.SetTargetFPS(60);

        // 1. Создаем мир (Гравитация принимает XnaVector)
        World world = new World(new XnaVector(0f, 9.8f));

        // 2. Настраиваем камеру Raylib (Камера принимает SysVector)
        Camera2D camera = new Camera2D();
        camera.Target = SysVector.Zero;
        camera.Offset = SysVector.Zero;
        camera.Rotation = 0.0f;
        camera.Zoom = PixelsPerMeter; // 1 единица в Raylib = 1 метр в физике

        // 3. Создаем статическую землю (Координаты передаем в XnaVector)
        Body ground = BodyFactory.CreateRectangle(
            world,
            16f, 0.8f,
            1f,
            new XnaVector(8f, 11.6f)
        );
        ground.BodyType = BodyType.Static;
        ground.Friction = 0.6f;

        // 4. Создаем кинематический квадрат (платформу)
        Body kinematicSquare = BodyFactory.CreateRectangle(
            world,
            2f, 0.6f,
            1f,
            new XnaVector(8f, 8f)
        );
        kinematicSquare.BodyType = BodyType.Kinematic;
        kinematicSquare.LinearVelocity = new XnaVector(-2f, 0f); // Скорость влево

        // Список для хранения динамических ящиков
        var boxes = new System.Collections.Generic.List<Body>();

        while (!Raylib.WindowShouldClose())
        {
            float dt = Raylib.GetFrameTime();

            // Логика движения кинематической платформы
            if (kinematicSquare.Position.X < 2f && kinematicSquare.LinearVelocity.X < 0)
                kinematicSquare.LinearVelocity = new XnaVector(2f, 0f);
            else if (kinematicSquare.Position.X > 14f && kinematicSquare.LinearVelocity.X > 0)
                kinematicSquare.LinearVelocity = new XnaVector(-2f, 0f);

            // Спавн ящиков по клику мыши
            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                SysVector mouseScreenPos = Raylib.GetMousePosition();
                // Получаем координаты клика в метрах (возвращает SysVector)
                SysVector mouseWorldPos = Raylib.GetScreenToWorld2D(mouseScreenPos, camera);

                // Конвертируем системный вектор в XNA вектор для Velcro
                XnaVector xnaSpawnPos = new XnaVector(mouseWorldPos.X, mouseWorldPos.Y);

                // Создаем динамический ящик размером 0.8 x 0.8 метра
                Body box = BodyFactory.CreateRectangle(world, 0.8f, 0.8f, 1f, xnaSpawnPos);
                box.BodyType = BodyType.Dynamic;
                box.Restitution = 0.2f;
                box.Friction = 0.5f;

                boxes.Add(box);
            }

            // Шаг симуляции физики
            world.Step(Math.Min(dt, 1f / 30f));

            // --- ОТРИСОВКА ---
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.RayWhite);

            // Включаем режим камеры (работаем в метрах)
            Raylib.BeginMode2D(camera);

            // Рисуем землю
            DrawPhysicsBody(ground, 16f, 0.8f, Color.Green);

            // Рисуем кинематическую платформу
            DrawPhysicsBody(kinematicSquare, 2f, 0.6f, Color.Blue);

            // Рисуем все динамические ящики
            foreach (var box in boxes)
            {
                DrawPhysicsBody(box, 0.8f, 0.8f, Color.Red);
            }

            Raylib.EndMode2D();

            // Интерфейс в обычных пикселях
            Raylib.DrawText($"Ящиков на экране: {boxes.Count}", 10, 10, 20, Color.DarkGray);

            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }

    // Метод отрисовки, который безопасно конвертирует XnaVector из тела в SysVector для Raylib
    static void DrawPhysicsBody(Body body, float width, float height, Color color)
    {
        float rotationInDegrees = body.Rotation * (180f / (float)Math.PI);

        // Явно переводим XnaVector (body.Position) в SysVector для структуры Rectangle в Raylib
        SysVector sysPos = new SysVector(body.Position.X, body.Position.Y);

        Rectangle rec = new Rectangle(sysPos.X, sysPos.Y, width, height);
        SysVector origin = new SysVector(width / 2f, height / 2f);

        Raylib.DrawRectanglePro(rec, origin, rotationInDegrees, color);
    }
}
