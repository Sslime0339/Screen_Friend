using Raylib_cs;
using System;
using System.Numerics;

class Program
{
    struct Character
    {
        public Vector2 Position;
        public Vector2 Speed;
        public Color Color;
    }

    static void Main()
    {
        const int screenWidth = 800;
        const int screenHeight = 600;

        Raylib.InitWindow(screenWidth, screenHeight, "Raylib-cs - 100 Персонажей без Rlgl");
        Raylib.SetTargetFPS(60);

        // 1. Создаем холст для отрисовки персонажей во весь экран
        RenderTexture2D canvas = Raylib.LoadRenderTexture(screenWidth, screenHeight);

        // Создаем 100 персонажей
        Character[] characters = new Character[100];
        Random rand = new Random();
        for (int i = 0; i < 100; i++)
        {
            characters[i].Position = new Vector2(rand.Next(50, 750), rand.Next(50, 550));
            characters[i].Speed = new Vector2(rand.Next(-2, 3), rand.Next(-2, 3));
            if (characters[i].Speed.X == 0 && characters[i].Speed.Y == 0) characters[i].Speed.X = 1;
            characters[i].Color = new Color(rand.Next(100, 255), rand.Next(100, 255), rand.Next(100, 255), 255);
        }

        Rectangle windowRect = new Rectangle(300, 200, 200, 300); // Квадрат-ластик

        while (!Raylib.WindowShouldClose())
        {


            // Движение персонажей
            for (int i = 0; i < 100; i++)
            {
                characters[i].Position += characters[i].Speed;
                if (characters[i].Position.X < 0 || characters[i].Position.X > screenWidth) characters[i].Speed.X *= -1;
                if (characters[i].Position.Y < 0 || characters[i].Position.Y > screenHeight) characters[i].Speed.Y *= -1;
            }

            // ----------------------------------------------------
            // ШАГ 1: Рисуем персонажей и вырезаем квадрат на холсте
            // ----------------------------------------------------
            Raylib.BeginTextureMode(canvas);
            Raylib.ClearBackground(Color.Blank); // Полностью прозрачный фон

            // Сначала рисуем ВСЕХ 100 персонажей на прозрачный холст
            for (int i = 0; i < 100; i++)
            {
                Raylib.DrawCircleV(characters[i].Position, 15, characters[i].Color);
            }

            // Включаем специальный режим смешивания (Стирание)
            Raylib.BeginBlendMode(BlendMode.Custom);

            // Этот квадрат буквально "выжигает" дыру во всех нарисованных персонажах
            Raylib.DrawRectangleRec(windowRect, Color.Blank);

            Raylib.EndBlendMode();
            Raylib.EndTextureMode();

            // ----------------------------------------------------
            // ШАГ 2: Выводим результат на экран
            // ----------------------------------------------------
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.RayWhite); // Фон экрана

            Rectangle windowRect2 = new Rectangle(300 + 50, 150 + 50, 200, 300);
            Raylib.DrawRectangleRec(windowRect2, new Color(255, 150, 255, 80));

            // Рисуем текстуру холста (переворачиваем по Y, так как в Raylib текстуры рендеринга инвертированы)
            Raylib.DrawTextureRec(
                canvas.Texture,
                new Rectangle(0, 0, canvas.Texture.Width, -canvas.Texture.Height),
                Vector2.Zero,
                Color.White
            );




            // Рисуем визуальный полупрозрачный квадрат поверх "дыры"
            Raylib.DrawRectangleRec(windowRect, new Color(0, 150, 255, 80));

            Raylib.DrawFPS(10, 10);
            Raylib.EndDrawing();
        }

        // Чистим память
        Raylib.UnloadRenderTexture(canvas);
        Raylib.CloseWindow();
    }
}
