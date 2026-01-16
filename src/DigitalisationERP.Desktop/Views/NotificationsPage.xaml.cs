using System.Windows;
using System.Windows.Controls;

namespace DigitalisationERP.Desktop.Views;

public partial class NotificationsPage : Page
{
    public NotificationsPage()
    {
        InitializeComponent();
        LoadNotifications();
    }

    private void LoadNotifications()
    {
        var notifications = new[]
        {
            new { Icon = "AlertCircle", Title = "Critical: Production Line 3 Error", Message = "Production line 3 has encountered a critical error. Immediate action required.", Timestamp = "2 minutes ago" },
            new { Icon = "AlertCircle", Title = "Security Alert", Message = "Unauthorized access attempt detected from IP 192.168.1.50", Timestamp = "15 minutes ago" },
            new { Icon = "AlertOctagon", Title = "Inventory Low Stock", Message = "Component XYZ-001 stock below reorder point", Timestamp = "1 hour ago" },
            new { Icon = "AlertOctagon", Title = "Maintenance Due", Message = "Equipment EMQ-42 maintenance due in 3 days", Timestamp = "3 hours ago" },
            new { Icon = "AlertOctagon", Title = "Backup Status", Message = "Database backup completed successfully", Timestamp = "5 hours ago" },
            new { Icon = "Information", Title = "System Update Available", Message = "New system update v2.1.0 is available", Timestamp = "6 hours ago" },
            new { Icon = "CheckCircle", Title = "Report Generated", Message = "Monthly production report has been generated", Timestamp = "8 hours ago" },
            new { Icon = "CheckCircle", Title = "Sync Completed", Message = "All data synchronized successfully", Timestamp = "12 hours ago" }
        };

        NotificationsList.ItemsSource = notifications;
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        NotificationsList.ItemsSource = null;
        MessageBox.Show("All notifications have been cleared.", "Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
