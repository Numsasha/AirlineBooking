using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using WpfApp2;

namespace WpfApp2
{
    public partial class AdminWindow : Window
    {
        // Укажите вашу строку подключения к MS SQL Server
        private readonly string connectionString = @"data source=510ec13\sqlexpress;initial catalog=AirlineDB;integrated security=true;encrypt=true;trustservercertificate=true;application name=AirlineApp;";
        private int currentFlightId = -1;

        public AdminWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshGrid();
        }

        // Метод обновления таблицы рейсов
        private void RefreshGrid()
        {
            List<FlightRow> list = new List<FlightRow>();
            string query = "SELECT FlightID, FlightNumber, DepartureCity, ArrivalCity, DepartureTime, ArrivalTime, Price FROM Flights";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    SqlCommand cmd = new SqlCommand(query, conn);
                    conn.Open();
                    SqlDataReader r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        list.Add(new FlightRow
                        {
                            FlightID = (int)r["FlightID"],
                            FlightNumber = r["FlightNumber"].ToString(),
                            DepartureCity = r["DepartureCity"].ToString(),
                            ArrivalCity = r["ArrivalCity"].ToString(),
                            DepartureTime = (DateTime)r["DepartureTime"],
                            ArrivalTime = (DateTime)r["ArrivalTime"],
                            Price = (decimal)r["Price"]
                        });
                    }
                }
                AdminFlightsGrid.ItemsSource = list;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления таблицы: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Выбор рейса из таблицы для редактирования
        private void AdminFlightsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AdminFlightsGrid.SelectedItem is FlightRow f)
            {
                currentFlightId = f.FlightID;
                NumTxt.Text = f.FlightNumber;
                DepTxt.Text = f.DepartureCity;
                ArrTxt.Text = f.ArrivalCity;
                PriceTxt.Text = f.Price.ToString();
            }
        }

        // ОПЕРАЦИЯ: ДОБАВЛЕНИЕ (CREATE)
        private void Add_Click(object sender, RoutedEventArgs e)
        {
            // 1. Проверяем корректность ввода числовых значений
            if (!int.TryParse(PlaneIdTxt.Text, out int airplaneId))
            {
                MessageBox.Show("ID Самолета должно быть числом!", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(PriceTxt.Text, out decimal price))
            {
                MessageBox.Show("Цена должна быть числом!", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // 2. ПРОВЕРКА: Существует ли самолет с таким ID в базе?
                    string checkPlaneQuery = "SELECT COUNT(*) FROM Airplanes WHERE AirplaneID = @PlaneID";
                    using (SqlCommand checkCmd = new SqlCommand(checkPlaneQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@PlaneID", airplaneId);
                        int planeExists = (int)checkCmd.ExecuteScalar();

                        if (planeExists == 0)
                        {
                            MessageBox.Show($"Самолет с ID = {airplaneId} не найден в базе данных! Допустимые ID: 1, 2.",
                                            "Ошибка внешнего ключа", MessageBoxButton.OK, MessageBoxImage.Error);
                            return; // Прерываем добавление, защищая от падения
                        }
                    }

                    // 3. Если самолет существует — добавляем рейс
                    string insertQuery = @"INSERT INTO Flights (FlightNumber, DepartureCity, ArrivalCity, DepartureTime, ArrivalTime, Price, AirplaneID) 
                                   VALUES (@Num, @Dep, @Arr, @DepTime, @ArrTime, @Price, @Plane)";

                    using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.Add("@Num", SqlDbType.VarChar).Value = NumTxt.Text.Trim();
                        cmd.Parameters.Add("@Dep", SqlDbType.NVarChar).Value = DepTxt.Text.Trim();
                        cmd.Parameters.Add("@Arr", SqlDbType.NVarChar).Value = ArrTxt.Text.Trim();
                        cmd.Parameters.Add("@Price", SqlDbType.Decimal).Value = price;
                        cmd.Parameters.Add("@Plane", SqlDbType.Int).Value = airplaneId;

                        cmd.Parameters.Add("@DepTime", SqlDbType.DateTime).Value = DateTime.Now.AddDays(1); // вылет завтра
                        cmd.Parameters.Add("@ArrTime", SqlDbType.DateTime).Value = DateTime.Now.AddDays(1).AddHours(2);

                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Рейс успешно добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        // ОПЕРАЦИЯ: ИЗМЕНЕНИЕ (UPDATE)
        private void Update_Click(object sender, RoutedEventArgs e)
        {
            if (currentFlightId == -1)
            {
                MessageBox.Show("Выберите рейс из таблицы для изменения!");
                return;
            }

            string query = @"UPDATE Flights 
                             SET FlightNumber = @Num, DepartureCity = @Dep, ArrivalCity = @Arr, Price = @Price 
                             WHERE FlightID = @ID";
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.Add("@Num", SqlDbType.VarChar).Value = NumTxt.Text.Trim();
                        cmd.Parameters.Add("@Dep", SqlDbType.NVarChar).Value = DepTxt.Text.Trim();
                        cmd.Parameters.Add("@Arr", SqlDbType.NVarChar).Value = ArrTxt.Text.Trim();
                        cmd.Parameters.Add("@Price", SqlDbType.Decimal).Value = Convert.ToDecimal(PriceTxt.Text);
                        cmd.Parameters.Add("@ID", SqlDbType.Int).Value = currentFlightId;

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
                MessageBox.Show("Данные рейса обновлены!", "Успех");
                RefreshGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении: {ex.Message}", "Ошибка");
            }
        }

        // ОПЕРАЦИЯ: УДАЛЕНИЕ (DELETE)
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (currentFlightId == -1)
            {
                MessageBox.Show("Выберите рейс из таблицы для удаления!");
                return;
            }

            string query = "DELETE FROM Flights WHERE FlightID = @ID";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.Add("@ID", SqlDbType.Int).Value = currentFlightId;
                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
                MessageBox.Show("Рейс успешно удален!", "Успех");

                // Сбрасываем форму
                currentFlightId = -1;
                NumTxt.Clear();
                DepTxt.Clear();
                ArrTxt.Clear();
                PriceTxt.Clear();

                RefreshGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка");
            }
        }
    }
}

