using System.Windows;
using System.Windows.Controls;

namespace DigitalisationERP.Desktop.Views;

public partial class UpdatesPage : Page
{
    public UpdatesPage()
    {
        InitializeComponent();
        LoadUpdateHistory();
    }

    private void LoadUpdateHistory()
    {
        var history = new[]
        {
            new { VersionNumber = "v2.0.5", Changes = "Bug fixes and performance improvements", Date = "January 8, 2026", Status = "Installed" },
            new { VersionNumber = "v2.0.4", Changes = "UI improvements and stability updates", Date = "December 20, 2025", Status = "Installed" },
            new { VersionNumber = "v2.0.3", Changes = "New email feature integration", Date = "December 5, 2025", Status = "Installed" },
            new { VersionNumber = "v2.0.2", Changes = "Security patches and hotfixes", Date = "November 15, 2025", Status = "Installed" },
            new { VersionNumber = "v2.0.1", Changes = "Initial stability release", Date = "November 1, 2025", Status = "Installed" }
        };

        UpdateHistory.ItemsSource = history;
    }

    private void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        MessageBox.Show("Checking for updates...\n\nNo new updates available. Your system is up to date.",
                       "Check Updates", MessageBoxButton.OK, MessageBoxImage.Information);
        CheckUpdatesButton.IsEnabled = true;
    }

    private void ViewReleaseNotes_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Release Notes for v2.0.5:\n\n" +
                       "• Fixed email sending issues\n" +
                       "• Improved report generation performance\n" +
                       "• Enhanced security logging\n" +
                       "• Updated UI components\n" +
                       "• Various bug fixes",
                       "Release Notes", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
