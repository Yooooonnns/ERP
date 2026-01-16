using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using DigitalisationERP.Desktop.Services;

namespace DigitalisationERP.Desktop.Views;

public partial class AddProductionPostDialog : Window
{
    private bool _isEditMode;

    public string PostCode => PostCodeTextBox.Text;
    public string PostName => PostNameTextBox.Text;
    public int CurrentLoad => int.TryParse(CurrentLoadTextBox.Text, out var val) ? val : 0;
    public int MaterialLevel => int.TryParse(MaterialLevelTextBox.Text, out var val) ? val : 100;
    public int UtilityTimeSeconds
    {
        get
        {
            if (!int.TryParse(CycleTimeTextBox.Text, out var raw)) return 60;
            if (raw <= 0) return 1;

            // Heuristic: values like 5000 are likely milliseconds, while 5/10/60 are seconds.
            if (raw >= 1000)
            {
                return Math.Max(1, (int)Math.Round(raw / 1000.0));
            }

            return raw;
        }
    }
    public int StockCapacity => int.TryParse(BufferStockTextBox.Text, out var val) ? val : 0;
    public string Status => (StatusComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString() ?? "Active";
    public double XPosition => double.TryParse(XPositionTextBox.Text, out var val) ? val : 100;
    public double YPosition => double.TryParse(YPositionTextBox.Text, out var val) ? val : 100;

    public void ConfigureForEdit()
    {
        _isEditMode = true;
        Title = "Edit Production Post";
        if (HeaderTextBlock != null)
        {
            HeaderTextBlock.Text = "Edit Production Post";
        }
        if (PrimaryActionButton != null)
        {
            PrimaryActionButton.Content = "SAVE";
            PrimaryActionButton.Width = 100;
        }

        PostCodeTextBox.IsReadOnly = true;
        PostCodeTextBox.IsEnabled = false;
    }

    public void LoadFrom(ProductionPostData data)
    {
        PostCodeTextBox.Text = data.PostCode;
        PostNameTextBox.Text = data.PostName;
        CurrentLoadTextBox.Text = data.CurrentLoad.ToString();
        MaterialLevelTextBox.Text = data.MaterialLevel.ToString();
        CycleTimeTextBox.Text = data.UtilityTimeSeconds.ToString();
        BufferStockTextBox.Text = data.StockCapacity.ToString();
        StatusComboBox.SelectedIndex = data.Status switch
        {
            "Active" => 0,
            "Idle" => 1,
            "Maintenance" => 2,
            "Offline" => 3,
            _ => 0
        };
        XPositionTextBox.Text = data.X.ToString("F0");
        YPositionTextBox.Text = data.Y.ToString("F0");
    }

    public AddProductionPostDialog()
    {
        InitializeComponent();
        
        // Set default values
        var postNumber = DateTime.Now.Ticks % 1000;
        PostCodeTextBox.Text = $"POST-{postNumber:D3}";
        PostNameTextBox.Text = $"Workstation {postNumber}";
        CurrentLoadTextBox.Text = "0";
        MaterialLevelTextBox.Text = "100";
        CycleTimeTextBox.Text = "60";
        BufferStockTextBox.Text = "0";
        XPositionTextBox.Text = "100";
        YPositionTextBox.Text = "100";
    }

    private void NumberValidation_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        Regex regex = new Regex("[^0-9]+");
        e.Handled = regex.IsMatch(e.Text);
    }

    private void AddPost_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PostCode) || string.IsNullOrWhiteSpace(PostName))
        {
            MessageBox.Show("Please fill in Post Code and Post Name.", "Validation Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!_isEditMode && string.IsNullOrWhiteSpace(PostCode))
        {
            MessageBox.Show("Please fill in Post Code.", "Validation Error",
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
