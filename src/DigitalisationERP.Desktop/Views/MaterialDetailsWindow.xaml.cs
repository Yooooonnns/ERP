using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DigitalisationERP.Desktop.Services;

namespace DigitalisationERP.Desktop.Views;

public partial class MaterialDetailsWindow : Window
{
    private readonly ApiClient _apiClient;
    private readonly string _materialNumber;

    public MaterialDetailsWindow(ApiClient apiClient, string materialNumber)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _materialNumber = materialNumber;

        Loaded += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            HeaderText.Text = _materialNumber;

            var all = await _apiClient.GetMaterialsAsync();
            var material = all.FirstOrDefault(m => string.Equals(m.materialNumber, _materialNumber, StringComparison.OrdinalIgnoreCase));
            if (material != null)
            {
                SubHeaderText.Text = $"{material.description}  •  Stock: {material.stockQuantity:0.##} {material.unitOfMeasure}";
            }

            var movements = await _apiClient.GetMaterialMovementsAsync(_materialNumber, take: 300);
            MovementsGrid.ItemsSource = movements
                .OrderByDescending(m => m.postingDate)
                .ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Material Details", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Receive_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(ReceiveQtyTextBox.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var qty) &&
            !decimal.TryParse(ReceiveQtyTextBox.Text.Trim(), out qty))
        {
            MessageBox.Show("Quantity must be a number.", "Receive", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (qty <= 0)
        {
            MessageBox.Show("Quantity must be > 0.", "Receive", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            await _apiClient.ReceiveMaterialAsync(_materialNumber, qty, documentNumber: DocNumberTextBox.Text.Trim());
            ReceiveQtyTextBox.Text = "";
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Receive", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static readonly Regex _decimalRegex = new("^[0-9\\.,-]+$");

    private void DecimalValidation_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !_decimalRegex.IsMatch(e.Text);
    }
}
