using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using DigitalisationERP.Desktop.Services;

namespace DigitalisationERP.Desktop.Views
{
    public partial class EditProductionLineLinksDialog : Window
    {
        public string LineId => LineIdTextBox.Text.Trim();
        public string LineName => LineNameTextBox.Text.Trim();
        public string Description => DescriptionTextBox.Text.Trim();
        public new bool IsActive => IsActiveCheckBox.IsChecked == true;

        public string OutputMaterialNumber => OutputMaterialNumberTextBox.Text.Trim();
        public string OutputMaterialDescription => OutputMaterialDescriptionTextBox.Text.Trim();
        public string OutputUnitOfMeasure => OutputUnitOfMeasureTextBox.Text.Trim();
        public decimal? OutputInitialStock
            => decimal.TryParse(OutputInitialStockTextBox.Text.Trim(), out var v) ? v : null;

        public ObservableCollection<EditLineInputRow> Inputs { get; } = new();

        public EditProductionLineLinksDialog(ProductionLineDto line)
        {
            InitializeComponent();
            DataContext = this;

            LineIdTextBox.Text = line.lineId ?? string.Empty;
            LineNameTextBox.Text = line.lineName ?? (line.lineId ?? string.Empty);
            DescriptionTextBox.Text = line.description ?? string.Empty;
            IsActiveCheckBox.IsChecked = line.isActive;

            OutputMaterialNumberTextBox.Text = line.output?.materialNumber ?? string.Empty;
            OutputMaterialDescriptionTextBox.Text = line.output?.description ?? string.Empty;
            OutputUnitOfMeasureTextBox.Text = line.output?.unitOfMeasure ?? "PC";
            OutputInitialStockTextBox.Text = string.Empty;

            foreach (var input in line.inputs ?? new())
            {
                Inputs.Add(new EditLineInputRow
                {
                    MaterialNumber = input.materialNumber ?? string.Empty,
                    Description = input.description ?? string.Empty,
                    UnitOfMeasure = string.IsNullOrWhiteSpace(input.unitOfMeasure) ? "PC" : input.unitOfMeasure,
                    InitialStockQuantity = 0,
                    QuantityPerUnit = input.quantityPerUnit <= 0 ? 1 : input.quantityPerUnit
                });
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(LineId) || string.IsNullOrWhiteSpace(LineName))
            {
                MessageBox.Show("Line ID and Line Name are required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputMaterialNumber))
            {
                MessageBox.Show("Output (finished product) Material Number is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Remove empty input rows
            var empties = Inputs.Where(i => string.IsNullOrWhiteSpace(i.MaterialNumber)).ToList();
            foreach (var row in empties) Inputs.Remove(row);

            DialogResult = true;
            Close();
        }

        private void AddInput_Click(object sender, RoutedEventArgs e)
        {
            Inputs.Add(new EditLineInputRow
            {
                MaterialNumber = string.Empty,
                Description = string.Empty,
                UnitOfMeasure = "PC",
                InitialStockQuantity = 0,
                QuantityPerUnit = 1
            });
        }

        private void RemoveInput_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is EditLineInputRow row)
            {
                Inputs.Remove(row);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class EditLineInputRow
    {
        public string MaterialNumber { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal InitialStockQuantity { get; set; }
        public decimal QuantityPerUnit { get; set; } = 1;
        public string UnitOfMeasure { get; set; } = "PC";
    }
}
