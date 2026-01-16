using System.Windows;
using DigitalisationERP.Desktop.Services;
using DigitalisationERP.Desktop.ViewModels;

namespace DigitalisationERP.Desktop.Views;

public partial class AdminDashboardWindow : Window
{
    private readonly CreateUserViewModel _viewModel;
    private readonly ApiClient _apiClient;

    public AdminDashboardWindow(ApiClient apiClient)
    {
        InitializeComponent();
        
        _apiClient = apiClient;
        _viewModel = new CreateUserViewModel(_apiClient);
        DataContext = _viewModel;

        _viewModel.UserCreated += OnUserCreated;
    }

    private void OnUserCreated(object? sender, EventArgs e)
    {
        // User created successfully - message is already shown in the UI
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to logout?",
            "Logout",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _apiClient.Logout();
            
            // Show login window
            var loginWindow = new LoginWindow(_apiClient);
            loginWindow.Show();
            
            Close();
        }
    }
}
