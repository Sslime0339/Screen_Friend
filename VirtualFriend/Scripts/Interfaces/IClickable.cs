using System;
using System.Collections.Generic;
using System.Text;

namespace VirtualFriend.Scripts.Interfaces
{
    internal interface IClickable
    {
        // Проверяет, попадает ли координата мыши (в метрах) по объекту
        bool IsUnderMouse(System.Numerics.Vector2 mouseWorldPos);

        // Срабатывает при клике
        void OnClick();
    }
}
