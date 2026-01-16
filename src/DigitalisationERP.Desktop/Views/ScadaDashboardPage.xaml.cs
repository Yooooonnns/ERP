using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DigitalisationERP.Desktop.Models;
using DigitalisationERP.Desktop.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace DigitalisationERP.Desktop.Views;

public partial class ScadaDashboardPage : UserControl
{
    private readonly LoginResponse _loginResponse;
    private readonly ProductionDataService _dataService;
    private readonly DispatcherTimer _updateTimer;
    private readonly Random _random = new();
    private List<double> _productionData = new();
    private string? _selectedLineId = null;

    public ScadaDashboardPage(LoginResponse loginResponse)
    {
        InitializeComponent();
        _loginResponse = loginResponse;
        _dataService = ProductionDataService.Instance;

        // Initialize update timer
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();

        // Initial load
        Loaded += (s, e) => {
            _dataService.EnsureInitialized();
            InitializeLineSelector();
            LoadDashboard();
        };
    }

    private void InitializeLineSelector()
    {
        ScadaLineSelector.Items.Clear();
        ScadaLineSelector.Items.Add(new ComboBoxItem { Content = "🌍 Global SCADA - All Lines", Tag = "GLOBAL" });
        
        foreach (var line in _dataService.ProductionLines)
        {
            ScadaLineSelector.Items.Add(new ComboBoxItem { Content = $"📊 {line.LineName} SCADA", Tag = line.LineId });
        }

        // Select global by default
        ScadaLineSelector.SelectedIndex = 0;
    }

    private void ScadaLineSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ScadaLineSelector.SelectedItem is ComboBoxItem selectedItem)
        {
            var tag = selectedItem.Tag?.ToString();
            _selectedLineId = tag == "GLOBAL" ? null : tag;
            LoadDashboard();
            UpdateStatusText();
        }
    }

    private void UpdateStatusText()
    {
        if (_selectedLineId == null)
        {
            var totalActive = _dataService.Posts.Count(p => p.Status == "Active");
            StatusText.Text = $"System Online • {totalActive} Posts Active";
        }
        else
        {
            var line = _dataService.GetProductionLine(_selectedLineId);
            if (line != null)
            {
                var activePosts = line.Posts.Count(p => p.Status == "Active");
                StatusText.Text = $"{line.LineName} Online • {activePosts}/{line.TotalPosts} Posts Active";
            }
        }
    }

    private void LoadDashboard()
    {
        UpdateOEEGauge(85.3);
        LoadProductionPosts();
        LoadProductionTrend();
        LoadAlarms();
        LoadMessages();
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        // Simulate real-time updates
        var newOee = 85 + _random.NextDouble() * 5;
        UpdateOEEGauge(newOee);

        var newRate = 1200 + _random.Next(0, 100);
        ProductionRateText.Text = newRate.ToString("#,0");
        RateProgress.Value = (newRate / 1500.0) * 100;

        // Update production trend
        UpdateProductionTrend();
    }

    private void UpdateOEEGauge(double value)
    {
        OeeValueText.Text = $"{value:F1}%";

        // Create arc path for gauge
        var percentage = value / 100.0;
        var angle = 360 * percentage;

        var pathFigure = new PathFigure { StartPoint = new Point(100, 10) };
        var arcSegment = new ArcSegment
        {
            Point = CalculateArcEndPoint(100, 100, 90, angle),
            Size = new Size(90, 90),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = angle > 180
        };
        pathFigure.Segments.Add(arcSegment);

        var pathGeometry = new PathGeometry();
        pathGeometry.Figures.Add(pathFigure);

        OeeArc.Data = pathGeometry;

        // Change color based on value
        if (value >= 90)
            OeeArc.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
        else if (value >= 75)
            OeeArc.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800"));
        else
            OeeArc.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
    }

    private Point CalculateArcEndPoint(double centerX, double centerY, double radius, double angleDegrees)
    {
        var angleRadians = (angleDegrees - 90) * Math.PI / 180;
        var x = centerX + radius * Math.Cos(angleRadians);
        var y = centerY + radius * Math.Sin(angleRadians);
        return new Point(x, y);
    }

    private void LoadProductionPosts()
    {
        PostsStatusPanel.Children.Clear();

        // Filter posts by selected line
        var posts = _selectedLineId == null 
            ? _dataService.Posts 
            : _dataService.GetPostsForLine(_selectedLineId);

        foreach (var post in posts)
        {
            var load = post.StockCapacity > 0
                ? (int)((post.CurrentLoad / (double)post.StockCapacity) * 100)
                : 0;
            var temp = 20 + _random.Next(0, 30);
            var speed = post.Status == "Active" ? 1000 + _random.Next(0, 300) : 0;
            
            var postPanel = CreatePostStatusPanel(
                post.PostName, 
                post.Status == "Active" ? "Running" : "Idle", 
                load, 
                temp, 
                speed
            );
            PostsStatusPanel.Children.Add(postPanel);
        }
    }

    private Border CreatePostStatusPanel(string name, string status, int load, int temp, int speed)
    {
        var statusColor = status == "Running" ? "#4CAF50" : "#95A5A6";

        var border = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A252F")),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Post Name & Status
        var nameStack = new StackPanel();
        var nameText = new TextBlock
        {
            Text = name,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        };
        var statusText = new TextBlock
        {
            Text = status.ToUpper(),
            FontSize = 10,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(statusColor)),
            Margin = new Thickness(0, 2, 0, 0)
        };
        nameStack.Children.Add(nameText);
        nameStack.Children.Add(statusText);
        Grid.SetColumn(nameStack, 0);

        // Load Indicator
        var loadStack = CreateMetricPanel("LOAD", $"{load}%", load > 90 ? "#F44336" : "#00BCD4");
        Grid.SetColumn(loadStack, 1);

        // Temperature
        var tempStack = CreateMetricPanel("TEMP", $"{temp}°C", "#FF9800");
        Grid.SetColumn(tempStack, 2);

        // Speed
        var speedStack = CreateMetricPanel("SPEED", $"{speed}", "#4CAF50");
        Grid.SetColumn(speedStack, 3);

        grid.Children.Add(nameStack);
        grid.Children.Add(loadStack);
        grid.Children.Add(tempStack);
        grid.Children.Add(speedStack);

        border.Child = grid;
        return border;
    }

    private StackPanel CreateMetricPanel(string label, string value, string color)
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(15, 0, 0, 0)
        };

        var labelText = new TextBlock
        {
            Text = label,
            FontSize = 9,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6"))
        };

        var valueText = new TextBlock
        {
            Text = value,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
            Margin = new Thickness(0, 2, 0, 0)
        };

        stack.Children.Add(labelText);
        stack.Children.Add(valueText);
        return stack;
    }

    private void LoadProductionTrend()
    {
        // Initialize with mock data
        _productionData = new List<double> { 1150, 1180, 1210, 1190, 1230, 1250, 1220, 1200, 1190, 1210, 1240, 1247 };

        UpdateProductionTrendChart();
    }

    private void UpdateProductionTrend()
    {
        // Shift data and add new point
        _productionData.RemoveAt(0);
        _productionData.Add(1200 + _random.Next(-50, 50));

        UpdateProductionTrendChart();
    }

    private void UpdateProductionTrendChart()
    {
        ProductionTrendChart.Series = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = _productionData,
                Stroke = new SolidColorPaint(SKColors.Cyan) { StrokeThickness = 2 },
                Fill = new LinearGradientPaint(
                    new SKColor(0, 188, 212, 100),
                    new SKColor(0, 188, 212, 0),
                    new SKPoint(0, 0),
                    new SKPoint(0, 1)),
                GeometrySize = 0,
                LineSmoothness = 0.3
            }
        };

        ProductionTrendChart.XAxes = new[] { new Axis { IsVisible = false } };
        ProductionTrendChart.YAxes = new[] { new Axis { IsVisible = false } };
    }

    private void LoadAlarms()
    {
        AlarmsPanel.Children.Clear();

        var alarms = new[]
        {
            new { Severity = "Warning", Message = "Post 3 - Load exceeding 90% threshold", Time = "2 min ago" },
            new { Severity = "Info", Message = "Maintenance due for Post 1 in 24 hours", Time = "15 min ago" }
        };

        foreach (var alarm in alarms)
        {
            var alarmPanel = CreateAlarmPanel(alarm.Severity, alarm.Message, alarm.Time);
            AlarmsPanel.Children.Add(alarmPanel);
        }

        // Update alarm counts
        WarningAlarmsText.Text = "1";
        InfoAlarmsText.Text = "1";
    }

    private Border CreateAlarmPanel(string severity, string message, string time)
    {
        var severityColor = severity switch
        {
            "Critical" => "#F44336",
            "Warning" => "#FF9800",
            "Info" => "#2196F3",
            _ => "#95A5A6"
        };

        var border = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A252F")),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(severityColor)),
            BorderThickness = new Thickness(0, 0, 0, 2)
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var severityText = new TextBlock
        {
            Text = severity.ToUpper(),
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(severityColor))
        };
        Grid.SetColumn(severityText, 0);

        var timeText = new TextBlock
        {
            Text = time,
            FontSize = 10,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6"))
        };
        Grid.SetColumn(timeText, 1);

        headerGrid.Children.Add(severityText);
        headerGrid.Children.Add(timeText);
        Grid.SetRow(headerGrid, 0);

        var messageText = new TextBlock
        {
            Text = message,
            FontSize = 12,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        };
        Grid.SetRow(messageText, 1);

        grid.Children.Add(headerGrid);
        grid.Children.Add(messageText);

        border.Child = grid;
        return border;
    }

    private void LoadMessages()
    {
        MessagesPanel.Children.Clear();

        var messages = new[]
        {
            new { Type = "System", Message = "Shift handover completed successfully", Time = "5 min ago" },
            new { Type = "Production", Message = "Order #12345 completed - 1000 units", Time = "18 min ago" },
            new { Type = "Quality", Message = "Quality check passed for Batch B-2024-1202", Time = "32 min ago" }
        };

        foreach (var msg in messages)
        {
            var msgPanel = CreateMessagePanel(msg.Type, msg.Message, msg.Time);
            MessagesPanel.Children.Add(msgPanel);
        }
    }

    private Border CreateMessagePanel(string type, string message, string time)
    {
        var border = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A252F")),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var typeText = new TextBlock
        {
            Text = type.ToUpper(),
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00BCD4"))
        };
        Grid.SetColumn(typeText, 0);

        var timeText = new TextBlock
        {
            Text = time,
            FontSize = 10,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6"))
        };
        Grid.SetColumn(timeText, 1);

        headerGrid.Children.Add(typeText);
        headerGrid.Children.Add(timeText);
        Grid.SetRow(headerGrid, 0);

        var messageText = new TextBlock
        {
            Text = message,
            FontSize = 12,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        };
        Grid.SetRow(messageText, 1);

        grid.Children.Add(headerGrid);
        grid.Children.Add(messageText);

        border.Child = grid;
        return border;
    }

    private void AcknowledgeAllAlarms_Click(object sender, RoutedEventArgs e)
    {
        AlarmsPanel.Children.Clear();
        CriticalAlarmsText.Text = "0";
        WarningAlarmsText.Text = "0";
        InfoAlarmsText.Text = "0";

        var messageText = new TextBlock
        {
            Text = "All alarms acknowledged",
            FontSize = 12,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6")),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 20, 0, 0)
        };
        AlarmsPanel.Children.Add(messageText);
    }
}
