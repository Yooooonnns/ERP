using System.Windows;

namespace DigitalisationERP.Desktop.Views;

public partial class PromptDialog : Window
{
    public string Value => ValueTextBox.Text.Trim();

    public PromptDialog(string title, string label)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        LabelText.Text = label;
        ValueTextBox.Focus();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Value))
        {
            MessageBox.Show("Value is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }
}
