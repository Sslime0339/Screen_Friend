using SharpHook;



namespace Тест_SharpHook
{
    internal class Program
    {

        // Флаг, управляющий блокировкой кликов (условие)
        private static bool _shouldBlockClicks = false;

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Консольное приложение запущено ===");
            Console.WriteLine("Нажмите 'B' для переключения блокировки кликов мыши.");
            Console.WriteLine("Нажмите 'Q' для выхода.");

            // 1. Используем SimpleGlobalHook для поддержки блокировки (SuppressEvent)
            using var hook = new SimpleGlobalHook();

            // 2. Подписываемся на события мыши и клавиатуры
            hook.MouseMoved += OnMouseMoved;
            hook.MousePressed += OnMousePressed; // Перехватываем НАЖАТИЕ, а не клик
            hook.KeyPressed += OnKeyPressed;

            // 3. Запускаем хук в фоновом потоке, чтобы он не блокировал наш Main
            var hookTask = hook.RunAsync();

            // 4. НАШ ЦЕНТРАЛЬНЫЙ ЦИКЛ приложения
            bool isRunning = true;
            while (isRunning)
            {
                // Здесь выполняется логика вашего центрального цикла...

                // Для примера проверяем нажатия клавиш внутри консоли (не глобальные)
                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey(intercept: true);
                    if (keyInfo.Key == ConsoleKey.Q)
                    {
                        isRunning = false;
                    }
                    else if (keyInfo.Key == ConsoleKey.B)
                    {
                        // Меняем условие блокировки
                        _shouldBlockClicks = !_shouldBlockClicks;
                        Console.ForegroundColor = _shouldBlockClicks ? ConsoleColor.Red : ConsoleColor.Green;
                        Console.WriteLine($"\n[ЦИКЛ] Статус блокировки кликов изменен: {_shouldBlockClicks}");
                        Console.ResetColor();
                    }
                }

                // Имитация полезной работы цикла и предотвращение 100% загрузки процессора
                await Task.Delay(10);
            }

            // 5. Корректное завершение работы
            Console.WriteLine("Остановка хука...");
            hook.Dispose();
            await hookTask;
            Console.WriteLine("Приложение успешно завершено.");
        }

        // Обработчик движения мыши
        private static void OnMouseMoved(object? sender, MouseHookEventArgs e)
        {
            // Выводим координаты (e.Data.X, e.Data.Y)
            Console.Title = $"Мышь: X={e.Data.X}, Y={e.Data.Y} | Блокировка: {_shouldBlockClicks}";
        }

        // Обработчик кликов мыши
        private static void OnMousePressed(object? sender, MouseHookEventArgs e)
        {
            Console.WriteLine($"[ХУК] Нажата кнопка мыши: {e.Data.Button} в точке ({e.Data.X}, {e.Data.Y})");

            // Проверяем наше условие
            if (_shouldBlockClicks)
            {
                // Подавляем распространение события в операционную систему
                e.SuppressEvent = true;
                Console.WriteLine("[ХУК] Клик был ЗАБЛОКИРОВАН!");
            }
        }

        // Обработчик клавиатуры (глобальный)
        private static void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
        {
            // e.Data.KeyCode содержит код клавиши (например, KeyCode.VcQ)
            Console.WriteLine($"[ХУК] Нажата клавиша: {e.Data.KeyCode}");
        }
    }
}
