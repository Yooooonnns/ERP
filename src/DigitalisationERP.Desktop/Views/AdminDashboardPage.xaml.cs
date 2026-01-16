using System.Windows;
using System.Windows.Controls;

namespace DigitalisationERP.Desktop.Views
{
    public partial class AdminDashboardPage : Page
    {
        public AdminDashboardPage()
        {
            InitializeComponent();
            UpdateKpiValues();
        }

        private void UpdateKpiValues()
        {
            // Update KPIs with simulated data
            ActiveUsersCount.Text = "248";
            ProductionPostsCount.Text = "15";
            AverageOeeValue.Text = "78.5%";
            IoTSensorsCount.Text = "47";
            IoTStatusText.Text = "45 opérationnels";
            WelcomeText.Text = "Vue globale de toutes les opérations";
        }

        private MainWindow? GetMainWindow() => System.Windows.Application.Current?.MainWindow as MainWindow;

        private void CreateUser_Click(object sender, RoutedEventArgs e)
        {
            GetMainWindow()?.NavigateToPage("UsersManagement");
        }

        private void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = GetMainWindow();
            if (mainWindow != null)
            {
                _ = mainWindow.ExportReportAsync();
            }
        }

        private void ConfigSystem_Click(object sender, RoutedEventArgs e)
        {
            GetMainWindow()?.NavigateToPage("Configuration");
        }

        private void ViewAllAlerts_Click(object sender, RoutedEventArgs e)
        {
            GetMainWindow()?.NavigateToPage("Maintenance");
        }
    }
}
