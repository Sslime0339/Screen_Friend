using System;
using System.Collections.Generic;
using System.Text;
using VirtualFriend.Entities.Objects;

namespace VirtualFriend.Interfaces
{
    internal interface IClickable
    {
        // Проверяет, попадает ли координата мыши (в метрах) по объекту
        bool IsUnderMouse(System.Numerics.Vector2 mouseWorldPos, WindowZoneObject WinUnderMouse);

        // Срабатывает при клике
        void OnClick(); // Левая кнопка мыши
        void OnRightClick() { } // Правая кнопка мыши
        void OnMiddleClick() { } // Средняя кнопка мыши
    }
}
