using System;
using System.Collections.Generic;
using System.Text;
using VirtualFriend.Scripts.GameObjects;

namespace VirtualFriend.Scripts.Interfaces
{
    internal interface IClickable
    {
        // Проверяет, попадает ли координата мыши (в метрах) по объекту
        bool IsUnderMouse(System.Numerics.Vector2 mouseWorldPos, WindowZoneObject WinUnderMouse);

        // Срабатывает при клике
        void OnClick();
    }
}
