using System;
using System.Windows;

namespace DigitalisationERP.Desktop.Views;

public partial class LeaveRequestDialog : Window
{
    public DateTime? StartDate => StartDatePicker.SelectedDate;
    public DateTime? EndDate => EndDatePicker.SelectedDate;
    public string Reason => ReasonTextBox.Text?.Trim() ?? string.Empty;

    public LeaveRequestDialog()
    {
        InitializeComponent();
        StartDatePicker.SelectedDate = DateTime.Today;
        EndDatePicker.SelectedDate = DateTime.Today;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Submit_Click(object sender, RoutedEventArgs e)
    {
        if (StartDate is null || EndDate is null)
        {
            MessageBox.Show("Veuillez choisir une date de début et une date de fin.", "Validation");
            return;
        }

        if (StartDate.Value.Date > EndDate.Value.Date)
        {
            MessageBox.Show("La date de fin doit être après la date de début.", "Validation");
            return;
        }

        if (string.IsNullOrWhiteSpace(Reason))
        {
            MessageBox.Show("Veuillez indiquer une raison (même courte).", "Validation");
            return;
        }

        DialogResult = true;
        Close();
    }
}
