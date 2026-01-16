using System.Windows;
using System.Diagnostics;
using DigitalisationERP.Desktop.Services;

namespace DigitalisationERP.Desktop
{
    public partial class MaintenanceModeWindow : Window
    {
        private readonly ApiClient _apiClient;
        private readonly string _userEmail;

        public MaintenanceModeWindow(ApiClient apiClient, string userEmail)
        {
            InitializeComponent();
            _apiClient = apiClient;
            _userEmail = userEmail;
        }

        private void DesktopConsolesButton_Click(object sender, RoutedEventArgs e)
        {
            // Lancer l'application desktop avec les consoles temps réel pour maintenance
            MainWindow mainWindow = new MainWindow(_apiClient);
            
            // Configurer le service de permissions pour Z_MAINT_TECH
            var permissionService = new RolePermissionService("Z_MAINT_TECH", _userEmail);
            mainWindow.SetPermissionService(permissionService);
            
            mainWindow.Show();
            Close();
        }

        private void WebDevToolsButton_Click(object sender, RoutedEventArgs e)
        {
            // Les pages web ont été supprimées: ouvrir directement les consoles desktop.
            DesktopConsolesButton_Click(sender, e);
        }
    }
}