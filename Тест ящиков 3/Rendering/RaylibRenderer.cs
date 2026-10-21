using Raylib_cs;
using System;
using Genbox.VelcroPhysics.Dynamics;
using SysVector = System.Numerics.Vector2;

internal sealed class RaylibRenderer : IDisposable
{
    private readonly Font _font;
    private readonly Image _boxImage;
    private readonly Texture2D[] _boxFrames;
    private readonly Rectangle _sourceRect;
    private int _currentFrame;
    private float _frameTime;
    private const float FrameDuration = 0.02f;
    private const float PixelsPerMeter = 50f;
    private const int TextOffset = 2;
    private readonly Camera2D _camera;

    public RaylibRenderer()
    {
        int[] codepoints = new int[512];
        for (int i = 0; i < 95; i++) codepoints[i] = 32 + i;
        for (int i = 0; i < 256; i++) codepoints[95 + i] = 0x0400 + i;
        _font = Raylib.LoadFontEx("Arial.ttf", 32, codepoints, codepoints.Length);

        _boxImage = Raylib.LoadImageAnim("resources/box.gif", out int frameCount);
        _boxFrames = new Texture2D[frameCount];
        int frameSize = _boxImage.Width * _boxImage.Height * 4;

        for (int i = 0; i < frameCount; i++)
        {
            unsafe
            {
                byte* basePtr = (byte*)_boxImage.Data;
                Image frame = new()
                {
                    Data = basePtr + (i * frameSize),
                    Width = _boxImage.Width,
                    Height = _boxImage.Height,
                    Mipmaps = 1,
                    Format = PixelFormat.UncompressedR8G8B8A8
                };
                _boxFrames[i] = Raylib.LoadTextureFromImage(frame);
            }
        }

        _sourceRect = new Rectangle(0, 0, _boxFrames[0].Width, _boxFrames[0].Height);
        _camera = new Camera2D
        {
            Target = SysVector.Zero,
            Offset = SysVector.Zero,
            Rotation = 0f,
            Zoom = PixelsPerMeter
        };
    }

    public void DrawFrame(PhysicsWorldService physics, Scene scene, InputController input, float dt)
    {
        _frameTime += dt;
        while (_frameTime >= FrameDuration)
        {
            _currentFrame = (_currentFrame + 1) % _boxFrames.Length;
            _frameTime -= FrameDuration;
        }

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Blank);
        Raylib.BeginMode2D(_camera);

#if DEBUG
        foreach (var _ in physics.WindowColliders)
        {
            // Коллайдеры можно визуализировать отдельно при необходимости.
        }
#endif

        foreach (GameObject gameObject in scene.Objects)
        {
            Body box = gameObject.Body;
            float rotation = box.Rotation * (180f / (float)Math.PI);
            Rectangle destination = new(
                box.Position.X, box.Position.Y,
                gameObject.Width, gameObject.Height);
            SysVector origin = new(gameObject.Width / 2f, gameObject.Height / 2f);
            Raylib.DrawTexturePro(
                _boxFrames[_currentFrame], _sourceRect, destination,
                origin, rotation, Color.White);
        }

        Raylib.EndMode2D();

        var shadow = new Color(20, 20, 20, 255);
        DrawTextWithShadow(
            $"Окон-препятствий: {physics.WindowColliders.Count} | Объектов сцены: {scene.Objects.Count}",
            new SysVector(10, 800), 24, 2, Color.Pink, shadow, TextOffset);
        DrawTextWithShadow(
            "[C] - Создать, [Delete] - Удалить объект под курсором, [X] - Очистить, [ESC] - Выход",
            new SysVector(10, 830), 24, 2, Color.Purple, shadow, TextOffset);
        Raylib.DrawFPS(10, 770);
        Raylib.EndDrawing();
    }

    private void DrawTextWithShadow(
        string text, SysVector position, float fontSize, float spacing,
        Color textColor, Color shadowColor, int offset)
    {
        Raylib.DrawTextEx(_font, text, new SysVector(position.X + offset, position.Y), fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(_font, text, new SysVector(position.X - offset, position.Y), fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(_font, text, new SysVector(position.X, position.Y + offset), fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(_font, text, new SysVector(position.X, position.Y - offset), fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(_font, text, new SysVector(position.X + offset, position.Y + offset), fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(_font, text, new SysVector(position.X - offset, position.Y + offset), fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(_font, text, new SysVector(position.X + offset, position.Y - offset), fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(_font, text, new SysVector(position.X - offset, position.Y - offset), fontSize, spacing, shadowColor);
        Raylib.DrawTextEx(_font, text, position, fontSize, spacing, textColor);
    }

    public void Dispose()
    {
        foreach (var texture in _boxFrames)
            Raylib.UnloadTexture(texture);
        Raylib.UnloadImage(_boxImage);
        Raylib.UnloadFont(_font);
    }
}
