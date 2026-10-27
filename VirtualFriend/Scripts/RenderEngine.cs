using Raylib_cs;
using System;
using System.Collections.Generic;
using System.Text;

using Font = Raylib_cs.Font;
using SysVector = System.Numerics.Vector2;





namespace VirtualFriend.Scripts
{
    internal class RenderEngine
    {
        public Camera2D Camera { get; private set; }
        private Font _myFont;
        private Texture2D[] _boxTextures;

        public RenderEngine(float pixelsPerMeter)
        {
            Camera = new Camera2D { Zoom = pixelsPerMeter };
        }

        public void LoadResources()
        {
            // Здесь грузим шрифт Arial и GIF-анимацию ящика, как в вашем Main
        }

        public void UnloadResources()
        {
            // Выгрузка ресурсов в конце игры
        }

        public void Begin(Color clearColor)
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(clearColor);
        }

        public void BeginWorldSpace() => Raylib.BeginMode2D(Camera);
        public void EndWorldSpace() => Raylib.EndMode2D();
        public void End() => Raylib.EndDrawing();

        public void DrawBox(SysVector position, float size, float rotation, int currentFrame)
        {
            // Логика отрисовки Raylib.DrawTexturePro с текстурой ящика
        }

        public void DrawTextWithShadow(string text, SysVector position, float fontSize, Color textColor)
        {
            int offset = 2;
            int spacing = 2;
            Color shadowColor = new Color(0, 0, 0, 0);
            // 1. Рисуем тень
            //Raylib.DrawTextEx(font, text, position + offset, fontSize, spacing, shadowColor);
            Raylib.DrawTextEx(_myFont, text, new SysVector(position.X + offset, position.Y), fontSize, spacing, shadowColor);
            Raylib.DrawTextEx(_myFont, text, new SysVector(position.X - offset, position.Y), fontSize, spacing, shadowColor);
            Raylib.DrawTextEx(_myFont, text, new SysVector(position.X, position.Y + offset), fontSize, spacing, shadowColor);
            Raylib.DrawTextEx(_myFont, text, new SysVector(position.X, position.Y - offset), fontSize, spacing, shadowColor);
            Raylib.DrawTextEx(_myFont, text, new SysVector(position.X + offset, position.Y + offset), fontSize, spacing, shadowColor);
            Raylib.DrawTextEx(_myFont, text, new SysVector(position.X - offset, position.Y + offset), fontSize, spacing, shadowColor);
            Raylib.DrawTextEx(_myFont, text, new SysVector(position.X + offset, position.Y - offset), fontSize, spacing, shadowColor);
            Raylib.DrawTextEx(_myFont, text, new SysVector(position.X - offset, position.Y - offset), fontSize, spacing, shadowColor);

            // 2. Рисуем основной текст
            Raylib.DrawTextEx(_myFont, text, position, fontSize, spacing, textColor);
        }
    }
}
