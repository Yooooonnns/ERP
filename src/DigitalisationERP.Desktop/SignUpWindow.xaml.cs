using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using DigitalisationERP.Desktop.Services;
using DigitalisationERP.Desktop.ViewModels;

namespace DigitalisationERP.Desktop
{
    public partial class SignUpWindow : Window
    {
        private readonly ApiClient _apiClient;
        private SignUpViewModel _viewModel = null!;

        public SignUpWindow(ApiClient apiClient)
        {
            InitializeComponent();
            _apiClient = apiClient ?? new ApiClient();
            
            _viewModel = new SignUpViewModel(_apiClient);
            _viewModel.SignUpSucceeded += (s, e) =>
            {
                // Just show message and return to login
                MessageBox.Show(
                    "Compte créé avec succès!\n\nUn S-User doit valider votre demande d'inscription.\n\nVeuillez revenir ultérieurement pour vous connecter.",
                    "Inscription Réussie",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                
                // Return to login
                LoginWindow loginWindow = new LoginWindow(_apiClient);
                loginWindow.Show();
                Close();
            };
            
            DataContext = _viewModel;
            
            // Bind PasswordBox to ViewModel
            PasswordBox.PasswordChanged += (s, e) =>
            {
                if (_viewModel != null)
                    _viewModel.Password = PasswordBox.Password;
            };
            
            ConfirmPasswordBox.PasswordChanged += (s, e) =>
            {
                if (_viewModel != null)
                    _viewModel.ConfirmPassword = ConfirmPasswordBox.Password;
            };
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            LoginWindow loginWindow = new LoginWindow(_apiClient);
            loginWindow.Show();
            Close();
        }
    }
}
