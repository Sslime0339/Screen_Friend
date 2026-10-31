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
        public MainWindow()
        {
            InitializeComponent();
        }

        // Кнопка "Вернуться к игре" просто закрывает главное меню
        private void ResumeButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Кнопка "Настройки" открывает окно настроек поверх меню
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            //SettingsWindow settings = new SettingsWindow();

            // Устанавливаем MainWindow в качестве владельца. 
            // Это нужно, чтобы настройки не ушли НАД или ПОД главное меню случайным образом
            //settings.Owner = this;

            // ShowDialog открывает окно как модальное (блокирует главное меню, пока настройки не закроют)
            //settings.ShowDialog();
        }

        // Кнопка "Выход" полностью закрывает приложение (вместе с Raylib)
        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
            System.Environment.Exit(0);
        }
    }
}
