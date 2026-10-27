using System;
using System.Collections.Generic;
using System.Text;


using Raylib_cs;
using System.Collections.Generic;





namespace VirtualFriend.Scripts
{


    // Описывает конкретный кадр на большой текстуре (атласе)
    internal struct SpriteFrame
    {
        public Texture2D Texture; // Ссылка на саму текстуру-атлас
        public Rectangle SourceRec; // Координаты X, Y, Width, Height кадра внутри атласа
    }


    internal class SpriteSheet
    {

        public Texture2D AtlasTexture { get; private set; }
        private readonly List<SpriteFrame> _frames = new List<SpriteFrame>();

        public int FramesCount => _frames.Count;

        public SpriteSheet(Texture2D atlasTexture)
        {
            AtlasTexture = atlasTexture;
        }

        // Автоматическая нарезка регулярной сетки (например, анимация персонажа)
        public void SliceGrid(int frameWidth, int frameHeight)
        {
            int cols = AtlasTexture.Width / frameWidth;
            int rows = AtlasTexture.Height / frameHeight;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    _frames.Add(new SpriteFrame
                    {
                        Texture = AtlasTexture,
                        SourceRec = new Rectangle(x * frameWidth, y * frameHeight, frameWidth, frameHeight)
                    });
                }
            }
        }

        // Ручное добавление кадра произвольного размера (для UI или разных объектов на одном листе)
        public void AddFrame(Rectangle region)
        {
            _frames.Add(new SpriteFrame { Texture = AtlasTexture, SourceRec = region });
        }

        public SpriteFrame GetFrame(int index)
        {
            if (index < 0 || index >= _frames.Count) return default;
            return _frames[index];
        }
    }
}
