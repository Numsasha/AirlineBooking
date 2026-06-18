using BCrypt.Net;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WpfApp2
{
    /// <summary>
    /// Логика взаимодействия для LoginWindow.xaml
    /// </summary>
   // Статический класс для хранения сессии текущего пользователя
    public static class UserSession
    {
        public static int UserID { get; set; }
        public static string FullName { get; set; }
        public static string Role { get; set; }
    }

    public partial class LoginWindow : Window
    {
        private readonly string connectionString = @"data source=510ec13\sqlexpress;initial catalog=AirlineDB;integrated security=true;encrypt=true;trustservercertificate=true;application name=AirlineApp;";

        public LoginWindow()
        {
            InitializeComponent();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailTextBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Заполните все поля!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Запрос сразу проверяет и Email, и Пароль в открытом виде
            string query = "SELECT UserID, FullName, Role FROM Users WHERE Email = @Email AND PasswordHash = @Password";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.Add("@Email", SqlDbType.VarChar).Value = email;
                        cmd.Parameters.Add("@Password", SqlDbType.VarChar).Value = password; // Передаем чистый пароль
                        conn.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // Если строка нашлась — пароль верный
                                UserSession.UserID = Convert.ToInt32(reader["UserID"]);
                                UserSession.FullName = reader["FullName"].ToString();
                                UserSession.Role = reader["Role"].ToString();

                                // Разделение ролей
                                if (UserSession.Role == "Admin")
                                {
                                    AdminWindow adminWin = new AdminWindow();
                                    adminWin.Show();
                                }
                                else
                                {
                                    PassengerWindow passengerWin = new PassengerWindow();
                                    passengerWin.Show();
                                }
                                this.Close();
                            }
                            else
                            {
                                // Если запись не найдена, значит либо почта не существует, либо пароль не подошел
                                MessageBox.Show("Неверный Email или Пароль!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка авторизации: {ex.Message}");
            }
        }


        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailTextBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Заполните Email и Пароль!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!Regex.IsMatch(email, emailPattern))
            {
                MessageBox.Show("Некорректный формат email!", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Запрос отправляет чистый незащищенный пароль напрямую
            string insertQuery = "INSERT INTO Users (FullName, Email, PasswordHash, Role) VALUES (@Name, @Email, @Password, 'Passenger')";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.Add("@Name", SqlDbType.NVarChar).Value = "Новый Пассажир";
                        cmd.Parameters.Add("@Email", SqlDbType.VarChar).Value = email;
                        cmd.Parameters.Add("@Password", SqlDbType.VarChar).Value = password; // Запись без хэширования

                        conn.Open();
                        cmd.ExecuteNonQuery();
                        MessageBox.Show("Успешная регистрация!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Этот Email уже занят или ошибка БД: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
