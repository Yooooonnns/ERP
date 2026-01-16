using System.Windows;
using System.Windows.Controls;
using DigitalisationERP.Desktop.Services;
using DigitalisationERP.Desktop.Models;

namespace DigitalisationERP.Desktop.Views
{
    public partial class LoginPage : UserControl
    {
        private readonly AuthService _authService;

        public LoginPage()
        {
            InitializeComponent();
            _authService = new AuthService(new ApiService());
        }

        private void LoginModeButton_Click(object sender, RoutedEventArgs e)
        {
            LoginPanel.Visibility = Visibility.Visible;
            RegisterPanel.Visibility = Visibility.Collapsed;
            LoginModeButton.Background = (System.Windows.Media.Brush)FindResource("PrimaryBrush") ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightBlue);
            RegisterModeButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGray);
        }

        private void RegisterModeButton_Click(object sender, RoutedEventArgs e)
        {
            LoginPanel.Visibility = Visibility.Collapsed;
            RegisterPanel.Visibility = Visibility.Visible;
            RegisterModeButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGreen);
            LoginModeButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGray);
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            LoginErrorMessage.Text = "";
            ShowLoading(true);

            try
            {
                var email = LoginEmailInput.Text.Trim();
                var password = LoginPasswordInput.Password;

                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                {
                    LoginErrorMessage.Text = "Please enter email and password";
                    return;
                }

                var result = await _authService.LoginAsync(email, password);

                if (result?.Success == true && result.Data != null)
                {
                    // User authenticated - navigate to dashboard
                    NavigateToDashboard();
                }
                else
                {
                    LoginErrorMessage.Text = result?.Message ?? "Login failed";
                }
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            RegisterErrorMessage.Text = "";
            ShowLoading(true);

            try
            {
                var firstName = RegisterFirstNameInput.Text.Trim();
                var lastName = RegisterLastNameInput.Text.Trim();
                var email = RegisterEmailInput.Text.Trim();
                var password = RegisterPasswordInput.Password;

                if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) ||
                    string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                {
                    RegisterErrorMessage.Text = "All fields are required";
                    return;
                }

                var result = await _authService.RegisterAsync(new RegisterRequest 
                { 
                    Email = email, 
                    Password = password
                });

                if (result?.Success == true && result.Data != null)
                {
                    // User registered successfully - navigate to dashboard
                    NavigateToDashboard();
                }
                else
                {
                    RegisterErrorMessage.Text = result?.Message ?? "Registration failed";
                }
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private void ShowLoading(bool isLoading)
        {
            LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            LoadingSpinner.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        }

        private void NavigateToDashboard()
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.NavigateToDashboard();
            }
        }
    }
}
