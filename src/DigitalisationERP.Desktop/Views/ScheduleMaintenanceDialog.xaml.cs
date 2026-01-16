using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace DigitalisationERP.Desktop.Views;

public partial class ScheduleMaintenanceDialog : Window
{
    public string StationName => StationNameTextBox.Text;
    public string TaskType => (TaskTypeComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString() ?? "Preventive";
    public string Priority => (PriorityComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString() ?? "Medium";
    public DateTime? ScheduledDate => ScheduledDatePicker.SelectedDate;
    public TimeSpan? ScheduledTime
    {
        get
        {
            if (ScheduledTimePicker.SelectedTime.HasValue)
            {
                var dt = ScheduledTimePicker.SelectedTime.Value;
                return new TimeSpan(dt.Hour, dt.Minute, dt.Second);
            }
            return null;
        }
    }
    public int EstimatedDuration => int.TryParse(EstimatedDurationTextBox.Text, out var val) ? val : 2;
    public string Technician => TechnicianTextBox.Text;
    public string Description => DescriptionTextBox.Text;
    public bool SendNotification => NotifyCheckBox.IsChecked ?? true;

    public ScheduleMaintenanceDialog(string stationName = "")
    {
        InitializeComponent();
        
        // Set default values
        StationNameTextBox.Text = stationName;
        ScheduledDatePicker.SelectedDate = DateTime.Now.AddDays(1);
        ScheduledTimePicker.SelectedTime = DateTime.Today.AddHours(9); // 9:00 AM
        EstimatedDurationTextBox.Text = "2";
        TechnicianTextBox.Text = "Unassigned";
    }

    private void NumberValidation_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        Regex regex = new Regex("[^0-9]+");
        e.Handled = regex.IsMatch(e.Text);
    }

    private void Schedule_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(StationName))
        {
            MessageBox.Show("Please enter a station/post name.", "Validation Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!ScheduledDate.HasValue)
        {
            MessageBox.Show("Please select a scheduled date.", "Validation Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
