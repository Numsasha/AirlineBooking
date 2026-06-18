using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Data;
using System.Text.RegularExpressions;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.Data.SqlClient;

namespace WpfApp2
{
    /// <summary>
    /// Логика взаимодействия для PassengerWindow.xaml
    /// </summary>
    public partial class PassengerWindow : System.Windows.Window
    {
        // Укажите вашу строку подключения к MS SQL Server
        private readonly string connectionString = @"Server=510EC13\SQLEXPRESS;Database=AirlineDB;Trusted_Connection=True;TrustServerCertificate=True;";
        private int selectedFlightId = -1;
        private FlightRow selectedFlightData = null;

        public PassengerWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadFlights();
            LoadMyBookings();
            ResetSeatMap();
        }

        private void LoadFlights()
        {
            List<FlightRow> list = new List<FlightRow>();
            string query = "SELECT FlightID, FlightNumber, DepartureCity, ArrivalCity, DepartureTime, ArrivalTime, Price FROM Flights";

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
            FlightsGrid.ItemsSource = list;
        }

        private void LoadMyBookings()
        {
            List<BookingRow> list = new List<BookingRow>();
            string q = @"SELECT b.BookingID, f.FlightNumber, b.SeatNumber, b.BookingDate 
                         FROM Bookings b JOIN Flights f ON b.FlightID = f.FlightID 
                         WHERE b.UserID = @UID";
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                SqlCommand cmd = new SqlCommand(q, conn);
                cmd.Parameters.AddWithValue("@UID", UserSession.UserID);
                conn.Open();
                SqlDataReader r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new BookingRow
                    {
                        BookingID = (int)r["BookingID"],
                        FlightNumber = r["FlightNumber"].ToString(),
                        SeatNumber = r["SeatNumber"].ToString(),
                        BookingDate = (DateTime)r["BookingDate"]
                    });
                }
            }
            MyBookingsGrid.ItemsSource = list;
        }

        private void FlightsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FlightsGrid.SelectedItem is FlightRow f)
            {
                selectedFlightId = f.FlightID;
                selectedFlightData = f;
                UpdateSeatMap(f.FlightID);
            }
            else
            {
                selectedFlightId = -1;
                selectedFlightData = null;
                ResetSeatMap();
            }
        }

        private void ResetSeatMap()
        {
            UpdateSeatMap(-1);
        }

        private void UpdateSeatMap(int flightId)
        {
            HashSet<string> occupiedSeats = new HashSet<string>();

            if (flightId != -1)
            {
                string query = "SELECT SeatNumber FROM Bookings WHERE FlightID = @FID AND StatusID != 3";
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@FID", flightId);
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            occupiedSeats.Add(reader["SeatNumber"].ToString().Trim());
                        }
                    }
                }
            }

            List<SeatItem> seatMap = new List<SeatItem>();
            for (int i = 1; i <= 6; i++)
            {
                string[] letters = { "A", "B", "C", "D", "E", "F" };
                foreach (var l in letters)
                {
                    string seatNum = $"{i}{l}";
                    seatMap.Add(new SeatItem
                    {
                        SeatNumber = seatNum,
                        IsOccupied = occupiedSeats.Contains(seatNum)
                    });
                }
            }
            SeatsListBox.ItemsSource = seatMap;
        }

        private void Book_Click(object sender, RoutedEventArgs e)
        {
            if (selectedFlightId == -1 || SeatsListBox.SelectedItem == null)
            {
                MessageBox.Show("Пожалуйста, выберите рейс и свободное место!");
                return;
            }

            SeatItem selectedSeat = (SeatItem)SeatsListBox.SelectedItem;
            string seat = selectedSeat.SeatNumber;

            string q = "INSERT INTO Bookings (UserID, FlightID, BookingDate, SeatNumber, StatusID) VALUES (@UID, @FID, GETDATE(), @Seat, 1)";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    SqlCommand cmd = new SqlCommand(q, conn);
                    cmd.Parameters.AddWithValue("@UID", UserSession.UserID);
                    cmd.Parameters.AddWithValue("@FID", selectedFlightId);
                    cmd.Parameters.AddWithValue("@Seat", seat);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }

                MessageBox.Show("Успешно забронировано! Формируем маршрутную квитанцию...");
                GeneratePdfTicket(selectedFlightData, seat);

                UpdateSeatMap(selectedFlightId);
                LoadMyBookings();
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка при бронировании: {ex.Message}"); }
        }

        private void CancelBooking_Click(object sender, RoutedEventArgs e)
        {
            if (MyBookingsGrid.SelectedItem is BookingRow b)
            {
                string q = "DELETE FROM Bookings WHERE BookingID = @BID";
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    SqlCommand cmd = new SqlCommand(q, conn);
                    cmd.Parameters.AddWithValue("@BID", b.BookingID);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                MessageBox.Show("Бронирование отменено.");
                LoadMyBookings();
                if (selectedFlightId != -1) UpdateSeatMap(selectedFlightId);
            }
        }

        private void GeneratePdfTicket(FlightRow flight, string seat)
        {
            string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Tickets");
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            string filePath = Path.Combine(folderPath, $"Ticket_{flight.FlightNumber}_{seat}.pdf");
            Document doc = new Document(PageSize.A5, 30, 30, 30, 30);

            try
            {
                PdfWriter.GetInstance(doc, new FileStream(filePath, FileMode.Create));
                doc.Open();

                string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                BaseFont bf = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.NOT_EMBEDDED);
                Font fontTitle = new Font(bf, 16, Font.BOLD);
                Font fontBody = new Font(bf, 11, Font.NORMAL);
                Font fontBold = new Font(bf, 11, Font.BOLD);

                doc.Add(new iTextSharp.text.Paragraph("МАРШРУТНАЯ КВИТАНЦИЯ (ЭЛЕКТРОННЫЙ БИЛЕТ)", fontTitle));
                doc.Add(new iTextSharp.text.Paragraph("=========================================================", fontBody));
                doc.Add(new iTextSharp.text.Paragraph($"Пассажир: {UserSession.FullName}", fontBody));
                doc.Add(new iTextSharp.text.Paragraph($"Номер рейса: {flight.FlightNumber}", fontBold));
                doc.Add(new iTextSharp.text.Paragraph($"Место в салоне: {seat}", fontBold));
                doc.Add(new iTextSharp.text.Paragraph("---------------------------------------------------------", fontBody));

                doc.Add(new iTextSharp.text.Paragraph($"Аэропорт отправления: {flight.DepartureCity}", fontBody));
                doc.Add(new iTextSharp.text.Paragraph($"Время вылета: {flight.DepartureTime:dd.MM.yyyy HH:mm}", fontBody));
                doc.Add(new iTextSharp.text.Paragraph($"Аэропорт назначения: {flight.ArrivalCity}", fontBody));
                doc.Add(new iTextSharp.text.Paragraph($"Время прилета: {flight.ArrivalTime:dd.MM.yyyy HH:mm}", fontBody));

                doc.Add(new iTextSharp.text.Paragraph("---------------------------------------------------------", fontBody));
                doc.Add(new iTextSharp.text.Paragraph($"Итоговая стоимость: {flight.Price:C}", fontBody));
                doc.Add(new iTextSharp.text.Paragraph($"Дата оформления: {DateTime.Now:dd.MM.yyyy HH:mm}", fontBody));
                doc.Add(new iTextSharp.text.Paragraph("=========================================================", fontBody)); var footer = new iTextSharp.text.Paragraph("Благодарим за выбор нашей авиакомпании!", fontBold); footer.Alignment = Element.ALIGN_CENTER; doc.Add(footer); MessageBox.Show($"Билет сохранен на Рабочий стол в папку 'Tickets'!", "PDF Сгенерирован");
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка сохранения PDF: {ex.Message}"); }
            finally { doc.Close(); }
        }
    }// ВСЕ НЕОБХОДИМЫЕ КЛАССЫ МОДЕЛЕЙ ДАННЫХ ВНУТРИ ПРОСТРАНСТВА ИМЕН
    public class FlightRow{public int FlightID { get; set; }
    public string FlightNumber { get; set; }
        public string DepartureCity { get; set; }
        public string ArrivalCity { get; set; }
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public decimal Price { get; set; }}
    public class BookingRow{public int BookingID { get; set; }
        public string FlightNumber { get; set; }
        public string SeatNumber { get; set; }
        public DateTime BookingDate { get; set; }}
    public class SeatItem{public string SeatNumber { get; set; }
     public bool IsOccupied { get; set; }}}




