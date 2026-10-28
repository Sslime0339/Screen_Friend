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

        
        public RenderEngine(float pixelsPerMeter, ResourceManager resourceManager)
        {
            Camera = new Camera2D { Zoom = pixelsPerMeter };
            _myFont = resourceManager.GetFont(Assets.Fonts.MainFont);
        }

        /*
        public void LoadResources()
        {
            // Здесь грузим шрифт Arial и GIF-анимацию ящика, как в вашем Main
        }

        public void UnloadResources()
        {
            // Выгрузка ресурсов в конце игры
        }*/

        public void Begin(Color clearColor)
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(clearColor);
        }

        public void BeginWorldSpace() => Raylib.BeginMode2D(Camera);
        public void EndWorldSpace() => Raylib.EndMode2D();
        public void End() => Raylib.EndDrawing();

        public void DrawBox(SysVector position, float width, float height, SysVector origin, float rotation, Color color)
        {
            //SysVector origin = new SysVector(0, 0);
            
            // Отрисовка вращающегося квадрата
            Raylib.DrawRectanglePro(
                new Rectangle(position.X, position.Y, width, height),
                origin,
                rotation,
                Color.Red
            );

            /*
            float rotationInDegrees = box.Rotation * (180f / (float)Math.PI);
            Rectangle destRec = new Rectangle(box.Position.X, box.Position.Y, BoxSizeMeters, BoxSizeMeters);
            SysVector origin = new SysVector(BoxSizeMeters / 2f, BoxSizeMeters / 2f);
            Raylib.DrawTexturePro(texBoxAnim[currentAnimFrame], sourceRec, destRec, origin, rotationInDegrees, Color.White);

            var rect = new Rectangle(posX, posY, width, height);

            Raylib.DrawRectangleRec(rect, new Color(10, 20, 255, 60));
            //Raylib.DrawRectangle(posX, posY, width, height, color);*/
        }
        public void DrawTexturePro(Texture2D texture, Rectangle rec, Rectangle destRec, SysVector origin, float rotation, Color color)
        {
            Raylib.DrawTexturePro(texture, rec, destRec, origin, rotation, color);
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
