using System;
using System.Collections.Generic;
using System.Text;

using Raylib_cs;




namespace VirtualFriend.Scripts
{
    internal static class ResourceManager
    {


        private static readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, SpriteSheet> _spriteSheets = new Dictionary<string, SpriteSheet>();
        private static readonly Dictionary<string, Font> _fonts = new Dictionary<string, Font>();

        // Загрузка одиночной текстуры
        public static void LoadTexture(string id, string filePath)
        {
            if (_textures.ContainsKey(id)) return;
            _textures[id] = Raylib.LoadTexture(filePath);
        }

        // Загрузка спрайт-листа с автоматической нарезкой на кадры
        public static void LoadSpriteSheet(string id, string filePath, int frameWidth, int frameHeight)
        {
            if (_spriteSheets.ContainsKey(id)) return;

            Texture2D atlas = Raylib.LoadTexture(filePath);
            SpriteSheet sheet = new SpriteSheet(atlas);
            sheet.SliceGrid(frameWidth, frameHeight);

            _spriteSheets[id] = sheet;
        }

        // Получение кадра из спрайт-листа
        public static SpriteFrame GetSpriteFrame(string sheetId, int frameIndex)
        {
            if (_spriteSheets.TryGetValue(sheetId, out var sheet))
            {
                return sheet.GetFrame(frameIndex);
            }
            return default; // Возвратит пустой кадр, если не найдено
        }

        public static Texture2D GetTexture(string id) => _textures.GetValueOrDefault(id);

        public static void UnloadAll()
        {
            foreach (var tex in _textures.Values) Raylib.UnloadTexture(tex);
            foreach (var sheet in _spriteSheets.Values) Raylib.UnloadTexture(sheet.AtlasTexture);
            foreach (var font in _fonts.Values) Raylib.UnloadFont(font);

            _textures.Clear();
            _spriteSheets.Clear();
            _fonts.Clear();
        }

    }
}
