using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DigitalisationERP.Desktop.Controls;

public partial class ProductionPostControl : UserControl
{
    public ProductionPostControl()
    {
        InitializeComponent();
    }

    private bool _isInProcess;
    public bool IsInProcess
    {
        get => _isInProcess;
        set
        {
            _isInProcess = value;
            UpdateInProcessVisual();
        }
    }

    public string PostCode
    {
        get => CodeText.Text;
        set => CodeText.Text = value;
    }

    public string PostName
    {
        get => NameText.Text;
        set => NameText.Text = value;
    }

    public int StockCapacity { get; set; }

    private bool _isStockBlocked;
    public bool IsStockBlocked
    {
        get => _isStockBlocked;
        set
        {
            _isStockBlocked = value;
            UpdateStatus();
            UpdateMaintenanceHealth();
            UpdateStockCapacity();
        }
    }

    private int _currentLoad;
    public int CurrentLoad
    {
        get => _currentLoad;
        set
        {
            _currentLoad = value;
            UpdateStockCapacity();
        }
    }

    private int _materialLevel;
    public int MaterialLevel
    {
        get => _materialLevel;
        set
        {
            _materialLevel = value;
            UpdateMaterialLevel();
        }
    }

    private string _status = "Active";
    public string Status
    {
        get => _status;
        set
        {
            _status = value;
            UpdateStatus();
        }
    }

    private double _maintenanceHealthScore = 100.0;
    public double MaintenanceHealthScore
    {
        get => _maintenanceHealthScore;
        set
        {
            _maintenanceHealthScore = value;
            UpdateMaintenanceHealth();
        }
    }

    private string _maintenanceIssue = "No Issues";
    public string MaintenanceIssue
    {
        get => _maintenanceIssue;
        set
        {
            _maintenanceIssue = value;
            UpdateMaintenanceHealth();
        }
    }

    private void UpdateStockCapacity()
    {
        CapacityText.Text = $"{CurrentLoad}/{StockCapacity}";
        CapacityBar.Value = StockCapacity > 0 ? (CurrentLoad * 100.0 / StockCapacity) : 0;

        if (IsStockBlocked)
        {
            CapacityBar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336")); // Red
            return;
        }

        // Update color based on load
        if (CapacityBar.Value >= 90)
            CapacityBar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336")); // Red
        else if (CapacityBar.Value >= 70)
            CapacityBar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800")); // Orange
        else
            CapacityBar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3")); // Blue
    }

    private void UpdateMaterialLevel()
    {
        MaterialLevelText.Text = $"{MaterialLevel}%";
        MaterialBar.Value = MaterialLevel;

        // Update color based on level
        if (MaterialLevel < 30)
        {
            MaterialBar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336")); // Red
            MaterialLevelText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
        }
        else if (MaterialLevel < 50)
        {
            MaterialBar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800")); // Orange
            MaterialLevelText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800"));
        }
        else
        {
            MaterialBar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")); // Green
            MaterialLevelText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
        }
    }

    private void UpdateStatus()
    {
        StatusText.Text = Status;

        if (IsStockBlocked)
        {
            StatusText.Text = "Blocked";
            StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
            StatusIndicator.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
            UpdateMaintenanceHealth();
            return;
        }

        var (color, indicatorColor) = Status switch
        {
            "Active" => ("#4CAF50", "#4CAF50"),
            "Maintenance" => ("#FF9800", "#FF9800"),
            "Idle" => ("#9E9E9E", "#9E9E9E"),
            "Offline" => ("#F44336", "#F44336"),
            _ => ("#2196F3", "#2196F3")
        };

        StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        StatusIndicator.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(indicatorColor));

        // Update border and header based on maintenance health if not offline
        UpdateMaintenanceHealth();
    }

    private void UpdateMaintenanceHealth()
    {
        // Determine color based on maintenance health score
        string borderColor;
        string headerColor;
        
        if (IsStockBlocked)
        {
            borderColor = "#F44336"; // Red
            headerColor = "#F44336";
        }
        else if (Status == "Offline")
        {
            borderColor = "#F44336"; // Red
            headerColor = "#F44336";
        }
        else if (MaintenanceHealthScore >= 90)
        {
            borderColor = "#4CAF50"; // Green - Healthy
            headerColor = "#4CAF50";
        }
        else if (MaintenanceHealthScore >= 70)
        {
            borderColor = "#FFC107"; // Yellow - Small issue
            headerColor = "#FFC107";
        }
        else if (MaintenanceHealthScore >= 50)
        {
            borderColor = "#FF9800"; // Orange - Medium issue
            headerColor = "#FF9800";
        }
        else
        {
            borderColor = "#F44336"; // Red - Great issue
            headerColor = "#F44336";
        }

        RootBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(borderColor));
        HeaderBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(headerColor));

        UpdateInProcessVisual();
        
        // Update tooltip with maintenance info
        var tooltipText = $"{PostName} ({PostCode})\n" +
                         $"Status: {(IsStockBlocked ? "Blocked (Low stock)" : Status)}\n" +
                         $"Health Score: {MaintenanceHealthScore:F0}%\n" +
                         $"Issue: {MaintenanceIssue}";
        RootBorder.ToolTip = tooltipText;
    }

    private void UpdateInProcessVisual()
    {
        RootBorder.BorderThickness = _isInProcess ? new Thickness(4) : new Thickness(2);
    }
}
