using System.Windows;
using DigitalisationERP.Desktop.Services;

namespace DigitalisationERP.Desktop
{
    public partial class LoginWindow : Window
    {
        private readonly ApiClient _apiClient;

        public LoginWindow(ApiClient? apiClient)
        {
            InitializeComponent();
            _apiClient = apiClient ?? new ApiClient();
            
            // Force window to front
            this.Loaded += (s, e) =>
            {
                this.Topmost = true;
                this.Focus();
                this.Activate();
                Dispatcher.BeginInvoke(() => this.Topmost = false);
            };
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailBox.Text;
            string password = PasswordBox.Password;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Remplissez tous les champs", "Erreur");
                return;
            }

            try
            {
                var response = await _apiClient.LoginAsync(email, password);
                
                if (response?.AccessToken != null)
                {
                    _apiClient.AuthToken = response.AccessToken;

                    // Récupérer le rôle de l'utilisateur depuis la réponse
                    string userRole = response.Role ?? "Z_PROD_OPERATOR"; // Par défaut opérateur
                    string userId = response.UserId ?? "";

                    // Créer le service de permissions
                    var permissionService = new RolePermissionService(userRole, userId);

                    // Account management center is restricted: S_USER + Maintenance IT only.
                    var isMaintenanceIt = userRole == "Z_MAINT_TECH" && email.Contains("it", StringComparison.OrdinalIgnoreCase);
                    if (userRole == "S_USER" || isMaintenanceIt)
                    {
                        var adminWindow = new AdminWindow(_apiClient, userRole, userId);
                        adminWindow.Show();
                    }
                    else
                    {
                        var mainWindow = new MainWindow(_apiClient);
                        mainWindow.SetPermissionService(permissionService);
                        mainWindow.Show();
                    }
                    Close();
                }
                else
                {
                    MessageBox.Show("Identifiants invalides", "Erreur");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur: {ex.Message}", "Erreur");
            }
        }

        private void SignUpButton_Click(object sender, RoutedEventArgs e)
        {
            SignUpWindow signUpWindow = new SignUpWindow(_apiClient);
            signUpWindow.Show();
            Close();
        }
    }
}
