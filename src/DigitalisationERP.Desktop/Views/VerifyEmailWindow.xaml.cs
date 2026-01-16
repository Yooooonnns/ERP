using System.Windows;
using DigitalisationERP.Desktop.Services;
using DigitalisationERP.Desktop.ViewModels;

namespace DigitalisationERP.Desktop.Views;

public partial class VerifyEmailWindow : Window
{
    private readonly VerifyEmailViewModel _viewModel;

    public VerifyEmailWindow(AuthService authService)
    {
        InitializeComponent();
        
        _viewModel = new VerifyEmailViewModel(authService);
        DataContext = _viewModel;

        _viewModel.VerificationSuccessful += OnVerificationSuccessful;
    }

    private void OnVerificationSuccessful(object? sender, EventArgs e)
    {
        MessageBox.Show(
            "Email verified successfully! You can now login.",
            "Success",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
