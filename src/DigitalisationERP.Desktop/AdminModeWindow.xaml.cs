using System.Windows;
using System.Diagnostics;
using DigitalisationERP.Desktop.Services;

namespace DigitalisationERP.Desktop
{
    public partial class AdminModeWindow : Window
    {
        private readonly ApiClient _apiClient;
        private readonly string _userEmail;
        private readonly string _userRole;

        public AdminModeWindow(ApiClient apiClient, string userEmail)
        {
            InitializeComponent();
            _apiClient = apiClient;
            _userEmail = userEmail;
            
            // Déterminer le rôle à partir de l'email ou du token
            _userRole = DetermineUserRole(userEmail);
            UpdateUIForRole();
        }

        private string DetermineUserRole(string email)
        {
            // Logique simple basée sur l'email pour les tests
            if (email.Contains("admin.sap")) return "SAP_ALL";
            return "S_USER";
        }

        private void UpdateUIForRole()
        {
            if (_userRole == "SAP_ALL")
            {
                // Direction Générale
                WelcomeText.Text = "Bienvenue Direction Générale";
                WelcomeText.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#8b5cf6"));
                
                AppButtonText.Text = "📊 Application ERP";
                AppButtonDescription.Text = "Visualisation totale + Messages + Réunions";
                
                WebButtonText.Text = "🌐 Panneau Direction";
                WebButtonDescription.Text = "Créer réunions et envoyer messages";
            }
            else
            {
                // S_USER (comme avant)
                WelcomeText.Text = "Bienvenue Super Admin";
            }
        }

        private void AdminButton_Click(object sender, RoutedEventArgs e)
        {
            // Lancer l'application en mode normal
            MainWindow main = new MainWindow(_apiClient);
            
            // Configurer le service de permissions
            var permissionService = new RolePermissionService(_userRole, _userEmail);
            main.SetPermissionService(permissionService);
            
            main.Show();
            Close();
        }

        private void AdminPanelButton_Click(object sender, RoutedEventArgs e)
        {
            // Les pages web ont été supprimées: ouvrir la gestion des comptes directement dans l'app.
            var adminWindow = new AdminWindow(_apiClient);
            adminWindow.Show();
            Close();
        }
    }
}
