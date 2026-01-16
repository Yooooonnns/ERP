using System.Windows;
using System.Windows.Controls;
using DigitalisationERP.Desktop.Services;
using DigitalisationERP.Desktop.ViewModels;

namespace DigitalisationERP.Desktop.Views;

public partial class RegisterWindow : Window
{
    private readonly RegisterViewModel _viewModel;

    public RegisterWindow(AuthService authService)
    {
        InitializeComponent();
        
        _viewModel = new RegisterViewModel(authService);
        DataContext = _viewModel;

        _viewModel.RegistrationSuccessful += OnRegistrationSuccessful;
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            _viewModel.Password = passwordBox.Password;
        }
    }

    private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            _viewModel.ConfirmPassword = passwordBox.Password;
        }
    }

    private void OnRegistrationSuccessful(object? sender, EventArgs e)
    {
        // Show success message and optionally close after a delay
        MessageBox.Show(
            "Registration successful! Please check your email to verify your account.",
            "Success",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
