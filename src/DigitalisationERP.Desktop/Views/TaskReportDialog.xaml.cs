using System;
using System.Windows;

namespace DigitalisationERP.Desktop.Views;

public partial class TaskReportDialog : Window
{
    public string TaskNumber { get; }
    public string TaskTitle { get; }

    public string SelectedStatus => (StatusCombo.SelectedItem as FrameworkElement)?.ToString() ?? StatusCombo.Text;

    public string ReportMessage => (ReportTextBox.Text ?? string.Empty).Trim();
    public string ShortNote => (ShortNoteTextBox.Text ?? string.Empty).Trim();

    public string Status
    {
        get
        {
            var raw = (StatusCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString();
            return string.IsNullOrWhiteSpace(raw) ? "ToDo" : raw;
        }
        set
        {
            foreach (var item in StatusCombo.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem cbi &&
                    string.Equals(cbi.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    StatusCombo.SelectedItem = item;
                    return;
                }
            }
        }
    }

    public TaskReportDialog(string taskNumber, string taskTitle, string currentStatus)
    {
        InitializeComponent();

        TaskNumber = taskNumber;
        TaskTitle = taskTitle;

        TitleText.Text = taskTitle;
        MetaText.Text = $"{taskNumber} · Statut actuel: {currentStatus}";

        Status = currentStatus;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Allow saving even with empty report; status updates still matter.
        DialogResult = true;
        Close();
    }
}
