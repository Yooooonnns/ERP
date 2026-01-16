using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DigitalisationERP.Desktop.Views;

public partial class RecordTransactionDialog : Window
{
    public string TransactionType => (TransactionTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Receipt";
    public string MaterialCode => MaterialCodeTextBox.Text;
    public string MaterialName => MaterialNameTextBox.Text;
    public int Quantity => int.TryParse(QuantityTextBox.Text, out var val) ? val : 0;
    public string Unit => (UnitComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "pcs";
    public string FromLocation => (FromLocationComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
    public string ToLocation => (ToLocationComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
    public string Reference => ReferenceTextBox.Text;
    public string Notes => NotesTextBox.Text;

    public RecordTransactionDialog()
    {
        InitializeComponent();
        
        // Set default values based on Receipt transaction
        MaterialCodeTextBox.Text = $"MAT-{DateTime.Now.Ticks % 10000:D4}";
        QuantityTextBox.Text = "100";
        ReferenceTextBox.Text = $"PO-{DateTime.Now.Year}-{DateTime.Now.Month:D2}-{DateTime.Now.Day:D2}";
        
        UpdateLocationVisibility();
    }

    private void TransactionType_Changed(object sender, SelectionChangedEventArgs e)
    {
        UpdateLocationVisibility();
    }

    private void UpdateLocationVisibility()
    {
        if (TransactionTypeComboBox.SelectedItem is ComboBoxItem item)
        {
            string type = item.Content.ToString() ?? "";
            
            // Configure locations based on transaction type
            switch (type)
            {
                case "Receipt":
                    FromLocationComboBox.IsEnabled = true;
                    ToLocationComboBox.IsEnabled = true;
                    if (FromLocationComboBox.Items.Count > 8)
                        FromLocationComboBox.SelectedIndex = 8; // External Supplier
                    if (ToLocationComboBox.Items.Count > 0)
                        ToLocationComboBox.SelectedIndex = 0; // Receiving
                    break;
                    
                case "Consumption":
                    FromLocationComboBox.IsEnabled = true;
                    ToLocationComboBox.IsEnabled = true;
                    if (ToLocationComboBox.Items.Count > 8)
                        ToLocationComboBox.SelectedIndex = 8; // Production
                    break;
                    
                case "Transfer":
                    FromLocationComboBox.IsEnabled = true;
                    ToLocationComboBox.IsEnabled = true;
                    break;
                    
                case "Adjustment":
                    FromLocationComboBox.IsEnabled = false;
                    ToLocationComboBox.IsEnabled = true;
                    break;
                    
                case "Return":
                case "Scrap":
                    FromLocationComboBox.IsEnabled = true;
                    ToLocationComboBox.IsEnabled = false;
                    break;
            }
        }
    }

    private void NumberValidation_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        Regex regex = new Regex("[^0-9]+");
        e.Handled = regex.IsMatch(e.Text);
    }

    private void Record_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(MaterialCode) || string.IsNullOrWhiteSpace(MaterialName))
        {
            MessageBox.Show("Please enter Material Code and Material Name.", "Validation Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (Quantity <= 0)
        {
            MessageBox.Show("Please enter a valid quantity greater than 0.", "Validation Error", 
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
