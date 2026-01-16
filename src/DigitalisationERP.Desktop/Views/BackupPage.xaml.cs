using System.Windows;
using System.Windows.Controls;

namespace DigitalisationERP.Desktop.Views;

public partial class BackupPage : Page
{
    public BackupPage()
    {
        InitializeComponent();
        LoadBackupHistory();
    }

    private void LoadBackupHistory()
    {
        var backups = new[]
        {
            new { Name = "Full Backup - Weekly", Date = "January 8, 2026 03:15 AM", Size = "2.45 GB", Status = "Successful" },
            new { Name = "Daily Backup", Date = "January 7, 2026 02:30 AM", Size = "1.23 GB", Status = "Successful" },
            new { Name = "Daily Backup", Date = "January 6, 2026 02:30 AM", Size = "1.18 GB", Status = "Successful" },
            new { Name = "Full Backup - Weekly", Date = "January 1, 2026 03:15 AM", Size = "2.41 GB", Status = "Successful" },
            new { Name = "Daily Backup", Date = "December 31, 2025 02:30 AM", Size = "1.21 GB", Status = "Successful" }
        };

        BackupHistory.ItemsSource = backups;
    }

    private void BackupNow_Click(object sender, RoutedEventArgs e)
    {
        BackupNowButton.IsEnabled = false;
        MessageBox.Show("Backup in progress. This may take a few minutes.\n\nYou will be notified when complete.", 
                       "Backup Started", MessageBoxButton.OK, MessageBoxImage.Information);
        LoadBackupHistory();
        BackupNowButton.IsEnabled = true;
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Select a backup from the history above and click the restore icon to restore.",
                       "Restore Backup", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
