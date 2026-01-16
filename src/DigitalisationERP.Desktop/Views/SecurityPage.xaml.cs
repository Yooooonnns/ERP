using System.Windows;
using System.Windows.Controls;

namespace DigitalisationERP.Desktop.Views;

public partial class SecurityPage : Page
{
    public SecurityPage()
    {
        InitializeComponent();
        LoadAuditLog();
    }

    private void LoadAuditLog()
    {
        var logs = new[]
        {
            new { Action = "User 'admin' logged in", Timestamp = DateTime.Now.AddHours(-1).ToString("yyyy-MM-dd HH:mm") },
            new { Action = "Permission changed for user 'operator1'", Timestamp = DateTime.Now.AddHours(-2).ToString("yyyy-MM-dd HH:mm") },
            new { Action = "Access denied: user 'guest' attempted secure area", Timestamp = DateTime.Now.AddHours(-3).ToString("yyyy-MM-dd HH:mm") },
            new { Action = "User 'manager1' exported report", Timestamp = DateTime.Now.AddHours(-4).ToString("yyyy-MM-dd HH:mm") },
            new { Action = "Security policy updated", Timestamp = DateTime.Now.AddHours(-5).ToString("yyyy-MM-dd HH:mm") }
        };

        AuditLog.ItemsSource = logs;
    }
}
