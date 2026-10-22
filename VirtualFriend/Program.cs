


// имя человечка - РИХЭЫШЪЕ
// да я сгенерировал случайные символы



using Raylib_cs;

namespace VirtualFriend
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // 1. Настройка прозрачности и стиля окна Raylib
            Raylib.SetConfigFlags(
                ConfigFlags.MousePassthroughWindow |
                ConfigFlags.UndecoratedWindow |
                ConfigFlags.TransparentWindow |
                ConfigFlags.TopmostWindow |
                ConfigFlags.AlwaysRunWindow
            );


            int workAreaWidth = 1600;
            int workAreaHeight = 860;

            Raylib.InitWindow(workAreaWidth, workAreaHeight, "Raylib-cs + Velcro Physics overlay");
            Raylib.SetTargetFPS(60);


        }
    }
}
