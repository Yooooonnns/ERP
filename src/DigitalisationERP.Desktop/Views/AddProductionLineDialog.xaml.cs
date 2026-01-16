using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace DigitalisationERP.Desktop.Views
{
    public partial class AddProductionLineDialog : Window
    {
        public string LineId => LineIdTextBox.Text.Trim();
        public string LineName => LineNameTextBox.Text.Trim();
        public string Description => DescriptionTextBox.Text.Trim();
        public new bool IsActive => IsActiveCheckBox.IsChecked == true;

        public string OutputMaterialNumber => OutputMaterialNumberTextBox.Text.Trim();
        public string OutputMaterialDescription => OutputMaterialDescriptionTextBox.Text.Trim();
        public string OutputUnitOfMeasure => OutputUnitOfMeasureTextBox.Text.Trim();
        public decimal OutputInitialStock => decimal.TryParse(OutputInitialStockTextBox.Text.Trim(), out var v) ? v : 0m;

        public ObservableCollection<LineInputRow> Inputs { get; } = new();

        public AddProductionLineDialog()
        {
            InitializeComponent();
            DataContext = this;
            // basic default
            LineIdTextBox.Text = $"LINE-{DateTime.Now.Ticks % 1000:D3}";
            LineNameTextBox.Text = "New Production Line";

            OutputMaterialNumberTextBox.Text = $"PF-{DateTime.Now.Ticks % 1000:D3}";
            OutputMaterialDescriptionTextBox.Text = "Finished Product";
            OutputUnitOfMeasureTextBox.Text = "PC";
            OutputInitialStockTextBox.Text = "0";

            Inputs.Add(new LineInputRow
            {
                MaterialNumber = $"M-{DateTime.Now.Ticks % 1000:D3}",
                Description = "Raw Material",
                UnitOfMeasure = "PC",
                InitialStockQuantity = 0,
                QuantityPerUnit = 1
            });
        }

        private void AddLine_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(LineId) || string.IsNullOrWhiteSpace(LineName))
            {
                MessageBox.Show("Please enter Line ID and Line Name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputMaterialNumber))
            {
                MessageBox.Show("Please enter an output (finished product) Material Number.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void AddInput_Click(object sender, RoutedEventArgs e)
        {
            Inputs.Add(new LineInputRow
            {
                MaterialNumber = "",
                Description = "",
                UnitOfMeasure = "PC",
                InitialStockQuantity = 0,
                QuantityPerUnit = 1
            });
        }

        private void RemoveInput_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is LineInputRow row)
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

    public class LineInputRow
    {
        public string MaterialNumber { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal InitialStockQuantity { get; set; }
        public decimal QuantityPerUnit { get; set; } = 1;
        public string UnitOfMeasure { get; set; } = "PC";
    }
}
