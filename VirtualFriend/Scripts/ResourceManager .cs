using System;
using System.Collections.Generic;
using System.Text;

using Raylib_cs;




namespace VirtualFriend.Scripts
{
    internal class ResourceManager
    {


        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, SpriteSheet> _spriteSheets = new Dictionary<string, SpriteSheet>();
        private readonly Dictionary<string, Font> _fonts = new Dictionary<string, Font>();

        // Загрузка одиночной текстуры
        public void LoadTexture(string id, string filePath)
        {
            if (_textures.ContainsKey(id)) return;
            _textures[id] = Raylib.LoadTexture(filePath);
        }

        // Загрузка спрайт-листа с автоматической нарезкой на кадры
        public void LoadSpriteSheet(string id, string filePath, int frameWidth, int frameHeight)
        {
            if (_spriteSheets.ContainsKey(id)) return;

            Texture2D atlas = Raylib.LoadTexture(filePath);
            SpriteSheet sheet = new SpriteSheet(atlas);
            sheet.SliceGrid(frameWidth, frameHeight);

            _spriteSheets[id] = sheet;
        }

        public void LoadFont(string id, string filePath)
        {

            // 4. Генерация кодовых точек для шрифта (Кириллица)
            int[] codepoints = new int[512];
            for (int i = 0; i < 95; i++) codepoints[i] = 32 + i;
            for (int i = 0; i < 256; i++) codepoints[95 + i] = 0x0400 + i;
            Font myFont = Raylib.LoadFontEx(filePath, 32, codepoints, codepoints.Length);

            _fonts[id] = myFont;
        }

        // Получение кадра из спрайт-листа
        public SpriteFrame GetSpriteFrame(string sheetId, int frameIndex)
        {
            if (_spriteSheets.TryGetValue(sheetId, out var sheet))
            {
                return sheet.GetFrame(frameIndex);
            }
            return default; // Возвратит пустой кадр, если не найдено
        }

        public Texture2D GetTexture(string id) => _textures.GetValueOrDefault(id);

        public void UnloadAll()
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
