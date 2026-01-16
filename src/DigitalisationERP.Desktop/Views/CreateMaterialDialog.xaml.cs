using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace DigitalisationERP.Desktop.Views;

public partial class CreateMaterialDialog : Window
{
    private readonly int _materialType;

    public string MaterialNumber => MaterialNumberTextBox.Text.Trim();
    public string MaterialDescription => DescriptionTextBox.Text.Trim();
    public string UnitOfMeasure => UomTextBox.Text.Trim();
    public decimal InitialStock
    {
        get
        {
            if (decimal.TryParse(InitialStockTextBox.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
            {
                return v;
            }
            if (decimal.TryParse(InitialStockTextBox.Text.Trim(), out v))
            {
                return v;
            }
            return 0m;
        }
    }

    public CreateMaterialDialog(int materialType)
    {
        InitializeComponent();
        _materialType = materialType;

        HeaderText.Text = materialType switch
        {
            1 => "Create Raw Material",
            3 => "Create Finished Product",
            _ => "Create Material"
        };

        Title = HeaderText.Text;
        UomTextBox.Text = "PC";
        InitialStockTextBox.Text = "0";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(MaterialNumber))
        {
            MessageBox.Show("Material number is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(UnitOfMeasure))
        {
            MessageBox.Show("Unit of measure is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(InitialStockTextBox.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out _) &&
            !decimal.TryParse(InitialStockTextBox.Text.Trim(), out _))
        {
            MessageBox.Show("Initial stock must be a number.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private static readonly Regex _decimalRegex = new("^[0-9\\.,-]+$");

    private void DecimalValidation_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !_decimalRegex.IsMatch(e.Text);
    }
}
