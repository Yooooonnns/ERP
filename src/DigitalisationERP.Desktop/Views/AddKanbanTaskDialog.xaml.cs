using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace DigitalisationERP.Desktop.Views;

public partial class AddKanbanTaskDialog : Window
{
    public string TaskTitle => TaskTitleTextBox.Text;
    public string TaskDescription => TaskDescriptionTextBox.Text;
    public string Status => (StatusComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString() ?? "Backlog";
    public int Priority => PriorityComboBox.SelectedIndex + 1;
    public string AssignedTo => AssignedToTextBox.Text;
    public int EstimatedHours => int.TryParse(EstimatedHoursTextBox.Text, out var val) ? val : 4;
    public DateTime? DueDate => DueDatePicker.SelectedDate;

    public AddKanbanTaskDialog()
    {
        InitializeComponent();
        
        // Set default values
        AssignedToTextBox.Text = "Unassigned";
        EstimatedHoursTextBox.Text = "1";
        DueDatePicker.SelectedDate = DateTime.Now.AddDays(7);
    }

    private void NumberValidation_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        Regex regex = new Regex("[^0-9]+");
        e.Handled = regex.IsMatch(e.Text);
    }

    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TaskTitle))
        {
            MessageBox.Show("Please enter an OF number.", "Validation Error", 
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
