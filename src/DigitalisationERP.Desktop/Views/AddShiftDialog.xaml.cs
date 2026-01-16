using System;
using System.Windows;
using DigitalisationERP.Desktop.Services;

namespace DigitalisationERP.Desktop.Views;

public partial class AddShiftDialog : Window
{
    public ShiftEntry? CreatedShift { get; private set; }

    public AddShiftDialog(string? defaultEmployeeId = null)
    {
        InitializeComponent();

        EmployeeTextBox.Text = defaultEmployeeId ?? string.Empty;
        DatePicker.SelectedDate = DateTime.Today;
        StartTextBox.Text = "07:00";
        EndTextBox.Text = "15:00";
        LocationTextBox.Text = "POST-01";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var employee = (EmployeeTextBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(employee))
        {
            MessageBox.Show("Veuillez renseigner l'employé.", "Validation");
            return;
        }

        if (DatePicker.SelectedDate is not DateTime date)
        {
            MessageBox.Show("Veuillez choisir une date.", "Validation");
            return;
        }

        var segment = SegmentCombo.SelectedIndex switch
        {
            0 => ShiftSegment.Morning,
            1 => ShiftSegment.Afternoon,
            2 => ShiftSegment.Night,
            _ => ShiftSegment.Morning
        };

        var start = (StartTextBox.Text ?? string.Empty).Trim();
        var end = (EndTextBox.Text ?? string.Empty).Trim();
        if (!TimeSpan.TryParse(start, out _) || !TimeSpan.TryParse(end, out _))
        {
            MessageBox.Show("Format d'heure invalide. Exemple: 07:00", "Validation");
            return;
        }

        var location = (LocationTextBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(location))
        {
            MessageBox.Show("Veuillez renseigner le poste / lieu.", "Validation");
            return;
        }

        CreatedShift = new ShiftEntry
        {
            EmployeeId = employee,
            Date = date.Date,
            Segment = segment,
            StartTime = start,
            EndTime = end,
            Location = location,
            Notes = (NotesTextBox.Text ?? string.Empty).Trim()
        };

        DialogResult = true;
        Close();
    }
}
