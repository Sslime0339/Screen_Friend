
namespace VirtualFriend.Scripts
{
    internal enum KeyCode : int
    {
        // Кнопки мыши
        LeftMouse = 0x01,
        RightMouse = 0x02,
        MiddleMouse = 0x04,

        // Управляющие клавиши
        Backspace = 0x08,
        Tab = 0x09,
        Enter = 0x0D,
        Shift = 0x10,
        Control = 0x11,
        Alt = 0x12,
        Pause = 0x13,
        CapsLock = 0x14,
        Escape = 0x1B,
        Space = 0x20,

        // Навигация и стрелки
        PageUp = 0x21,
        PageDown = 0x22,
        End = 0x23,
        Home = 0x24,
        Left = 0x25,
        Up = 0x26,
        Right = 0x27,
        Down = 0x28,
        Insert = 0x2D,
        Delete = 0x2E,

        // Верхний ряд цифр
        D0 = 0x30,
        D1 = 0x31,
        D2 = 0x32,
        D3 = 0x33,
        D4 = 0x34,
        D5 = 0x35,
        D6 = 0x36,
        D7 = 0x37,
        D8 = 0x38,
        D9 = 0x39,

        // Буквы (совпадают с ASCII-кодами заглавных латинских букв)
        A = 0x41,
        B = 0x42,
        C = 0x43,
        D = 0x44,
        E = 0x45,
        F = 0x46,
        G = 0x47,
        H = 0x48,
        I = 0x49,
        J = 0x4A,
        K = 0x4B,
        L = 0x4C,
        M = 0x4D,
        N = 0x4E,
        O = 0x4F,
        P = 0x50,
        Q = 0x51,
        R = 0x52,
        S = 0x53,
        T = 0x54,
        U = 0x55,
        V = 0x56,
        W = 0x57,
        X = 0x58,
        Y = 0x59,
        Z = 0x5A,

        // Numpad
        NumPad0 = 0x60,
        NumPad1 = 0x61,
        NumPad2 = 0x62,
        NumPad3 = 0x63,
        NumPad4 = 0x64,
        NumPad5 = 0x65,
        NumPad6 = 0x66,
        NumPad7 = 0x67,
        NumPad8 = 0x68,
        NumPad9 = 0x69,

        // Функциональные клавиши
        F1 = 0x70,
        F2 = 0x71,
        F3 = 0x72,
        F4 = 0x73,
        F5 = 0x74,
        F6 = 0x75,
        F7 = 0x76,
        F8 = 0x77,
        F9 = 0x78,
        F10 = 0x79,
        F11 = 0x7A,
        F12 = 0x7B,

        // Левые и правые модификаторы
        LeftShift = 0xA0,
        RightShift = 0xA1,
        LeftControl = 0xA2,
        RightControl = 0xA3,
        LeftAlt = 0xA4,
        RightAlt = 0xA5,



        // Символы пунктуации (стандартная раскладка US QWERTY)
        Semicolon = 0xBA,    // ; : (Ж в русской раскладке)
        Plus = 0xBB,         // = +
        Comma = 0xBC,        // , < (Б в русской раскладке)
        Minus = 0xBD,        // - _
        Period = 0xBE,       // . > (Ю в русской раскладке)
        Slash = 0xBF,        // / ? (. , в русской раскладке)
        Tilde = 0xC0,        // ` ~ (Ё в русской раскладке)
        OpenBracket = 0xDB,  // [ { (Х в русской раскладке)
        Backslash = 0xDC,    // \ |
        CloseBracket = 0xDD, // ] } (Ъ в русской раскладке)
        Quote = 0xDE         // ' " (Э в русской раскладке)

    }
}
