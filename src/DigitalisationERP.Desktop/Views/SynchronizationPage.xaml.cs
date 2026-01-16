using System.Windows;
using System.Windows.Controls;

namespace DigitalisationERP.Desktop.Views;

public partial class SynchronizationPage : Page
{
    public SynchronizationPage()
    {
        InitializeComponent();
        LoadSyncHistory();
    }

    private void LoadSyncHistory()
    {
        var history = new[]
        {
            new { Description = "Full sync completed", Timestamp = DateTime.Now.AddMinutes(-5).ToString("yyyy-MM-dd HH:mm") },
            new { Description = "Inventory sync", Timestamp = DateTime.Now.AddMinutes(-15).ToString("yyyy-MM-dd HH:mm") },
            new { Description = "Production data sync", Timestamp = DateTime.Now.AddMinutes(-25).ToString("yyyy-MM-dd HH:mm") },
            new { Description = "User data sync", Timestamp = DateTime.Now.AddHours(-1).ToString("yyyy-MM-dd HH:mm") },
            new { Description = "Maintenance logs sync", Timestamp = DateTime.Now.AddHours(-2).ToString("yyyy-MM-dd HH:mm") }
        };

        SyncHistory.ItemsSource = history;
    }

    private void Sync_Click(object sender, RoutedEventArgs e)
    {
        SyncButton.IsEnabled = false;
        MessageBox.Show("Synchronization started. All data sources are being synced.", 
                       "Sync In Progress", MessageBoxButton.OK, MessageBoxImage.Information);
        LoadSyncHistory();
        SyncButton.IsEnabled = true;
    }
}
