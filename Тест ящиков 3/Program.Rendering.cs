using Raylib_cs;
using System;

// Переиспользуемые методы отрисовки.
using SysVector = System.Numerics.Vector2;

internal static partial class Program
{
    // отрисовка текста с тенью

    public static void DrawTextExWithShadow(
        Font font,
        string text,
        SysVector position,
        float fontSize,
        float spacing,
        Color textColor,
        Color shadowColor,
        int offset)
    {
        // 1. Рисуем тень
        //Raylib.DrawTextEx(font, text, position + offset, fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(font, text, new SysVector(position.X + offset, position.Y), fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(font, text, new SysVector(position.X - offset, position.Y), fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(font, text, new SysVector(position.X, position.Y + offset), fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(font, text, new SysVector(position.X, position.Y - offset), fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(font, text, new SysVector(position.X + offset, position.Y + offset), fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(font, text, new SysVector(position.X - offset, position.Y + offset), fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(font, text, new SysVector(position.X + offset, position.Y - offset), fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(font, text, new SysVector(position.X - offset, position.Y - offset), fontSize, spacing, shadowColor);

        // 2. Рисуем основной текст
        Raylib.DrawTextEx(font, text, position, fontSize, spacing, textColor);
    }
}
