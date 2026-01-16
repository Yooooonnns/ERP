using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using DigitalisationERP.Domain.Entities;
using DigitalisationERP.Desktop.Models;

namespace DigitalisationERP.Desktop.Views
{
    public partial class ConfigurationPage : UserControl
    {
        private ObservableCollection<ProductionPost> _posts;

        public ConfigurationPage()
        {
            InitializeComponent();
            _posts = new ObservableCollection<ProductionPost>();
            PostsGrid.ItemsSource = _posts;
            InitializeSliders();
        }

        private void InitializeSliders()
        {
            MinAvailabilitySlider.ValueChanged += (s, e) => 
                MinAvailabilityValue.Text = $"{(int)e.NewValue}%";
            MinPerformanceSlider.ValueChanged += (s, e) => 
                MinPerformanceValue.Text = $"{(int)e.NewValue}%";
            MinQualitySlider.ValueChanged += (s, e) => 
                MinQualityValue.Text = $"{(int)e.NewValue}%";

            MinAvailabilitySlider.ValueChanged += (s, e) => UpdateMinOEEDisplay();
            MinPerformanceSlider.ValueChanged += (s, e) => UpdateMinOEEDisplay();
            MinQualitySlider.ValueChanged += (s, e) => UpdateMinOEEDisplay();
        }

        private void UpdateMinOEEDisplay()
        {
            var availability = MinAvailabilitySlider.Value / 100;
            var performance = MinPerformanceSlider.Value / 100;
            var quality = MinQualitySlider.Value / 100;
            var minOEE = (availability * performance * quality) * 100;
            MinOEEDisplay.Text = $"Min OEE: {minOEE:F2}%";
        }

        private void AddPostButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Post configuration under reconstruction", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Configuration saving under reconstruction - services need to be integrated", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.NavigateToPage("Dashboard");
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
            {
                var loginWindow = new LoginWindow(null);
                loginWindow.Show();
                mainWindow.Close();
            }
        }
    }
}
