using System.Windows;
using System.Windows.Controls;

namespace DigitalisationERP.Desktop.Views;

public partial class SupportPage : Page
{
    public SupportPage()
    {
        InitializeComponent();
    }

    private void SendSupport_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Email client opened with pre-filled support email address.\n\nsupport@digitalisationerrp.com",
                       "Contact Support", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenResource_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            var resource = btn.Tag?.ToString() ?? "Resource";
            MessageBox.Show($"Opening {resource}...\n\nThis would open documentation in your browser.",
                           "Resources", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
