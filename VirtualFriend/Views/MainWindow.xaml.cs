using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace VirtualFriend.Views
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Храним ссылки на открытые окна, чтобы не плодить дубликаты
        private SettingsWindow? _activeSettingsWindow = null;
        private InfoWindow? _activeInfoWindow = null;


        public MainWindow()
        {
            InitializeComponent();
        }

        // Кнопка "Инструкция"
        private void InfoButton_Click(object sender, RoutedEventArgs e)
        {
            // Если окно уже создано и отображается
            if (_activeInfoWindow != null && _activeInfoWindow.IsLoaded)
            {
                _activeInfoWindow.Activate(); // Выводим на передний план
                return;
            }

            _activeInfoWindow = new InfoWindow();
            _activeInfoWindow.Owner = this;

            // Очищаем ссылку, когда пользователь закроет окно крестиком
            _activeInfoWindow.Closed += (s, args) => { _activeInfoWindow = null; };

            _activeInfoWindow.Show();
        }

        // Кнопка "Настройки"
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Если окно настроек уже создано и отображается
            if (_activeSettingsWindow != null && _activeSettingsWindow.IsLoaded)
            {
                _activeSettingsWindow.Activate(); // Выводим на передний план
                return;
            }

            _activeSettingsWindow = new SettingsWindow();
            _activeSettingsWindow.Owner = this;

            // Очищаем ссылку, когда пользователь закроет окно крестиком
            _activeSettingsWindow.Closed += (s, args) => { _activeSettingsWindow = null; };

            _activeSettingsWindow.Show();
        }


        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
            System.Environment.Exit(0);
        }
    }
}
