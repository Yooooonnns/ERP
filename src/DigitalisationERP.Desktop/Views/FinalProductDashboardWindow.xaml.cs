using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DigitalisationERP.Desktop.Services;

namespace DigitalisationERP.Desktop.Views;

public partial class FinalProductDashboardWindow : Window
{
    private readonly ApiClient _apiClient;
    private readonly string _materialNumber;

    public FinalProductDashboardWindow(ApiClient apiClient, string materialNumber)
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
            // Prefer showing GR postings for the finished product.
            MovementsGrid.ItemsSource = movements
                .Where(m => string.Equals(m.movementType, "101", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.postingDate)
                .ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Finished Product Dashboard", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
