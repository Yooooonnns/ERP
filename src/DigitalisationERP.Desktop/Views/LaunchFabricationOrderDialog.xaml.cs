using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DigitalisationERP.Desktop.Views
{
    public partial class LaunchFabricationOrderDialog : Window
    {
        public string OrderNumber => OrderNumberTextBox.Text.Trim();

        public string? SelectedLineId
            => (LineComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();

        public int Quantity
            => int.TryParse(QuantityTextBox.Text, out var q) ? q : 0;

        public LaunchFabricationOrderDialog()
        {
            InitializeComponent();
            QuantityTextBox.Text = "1";
        }

        public void SetLines(IEnumerable<(string lineId, string lineName)> lines)
        {
            LineComboBox.Items.Clear();
            foreach (var (id, name) in lines)
            {
                LineComboBox.Items.Add(new ComboBoxItem { Content = name, Tag = id });
            }

            LineComboBox.SelectedIndex = LineComboBox.Items.Count > 0 ? 0 : -1;
        }

        public void Prefill(string orderNumber, int quantity, string? preferredLineId)
        {
            OrderNumberTextBox.Text = orderNumber;
            QuantityTextBox.Text = quantity > 0 ? quantity.ToString() : "1";

            if (!string.IsNullOrWhiteSpace(preferredLineId))
            {
                for (int i = 0; i < LineComboBox.Items.Count; i++)
                {
                    if (LineComboBox.Items[i] is ComboBoxItem item &&
                        string.Equals(item.Tag?.ToString(), preferredLineId, System.StringComparison.OrdinalIgnoreCase))
                    {
                        LineComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        private void NumberValidation_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void Launch_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(OrderNumber))
            {
                MessageBox.Show("Enter an OF number.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedLineId))
            {
                MessageBox.Show("Select a production line.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Quantity <= 0)
            {
                MessageBox.Show("Enter a valid quantity (>= 1).", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
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
}
