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
    /// Логика взаимодействия для SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }


        // ОБНОВЛЕНО: Открывает подсказку как отдельное независимое окно
        private void OpenInfo_Click(object sender, RoutedEventArgs e)
        {
            InfoWindow infoWindow = new InfoWindow();
            infoWindow.Owner = this; // Привязываем к окну настроек
            infoWindow.ShowDialog(); // Открываем поверх модально
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Здесь будет логика сохранения чекбоксов (например через глобальные конфиги)
            MessageBox.Show("Изменения успешно применены!", "Настройки", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }
    }
}
