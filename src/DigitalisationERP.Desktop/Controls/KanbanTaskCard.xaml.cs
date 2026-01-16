using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DigitalisationERP.Desktop.Controls;

public partial class KanbanTaskCard : UserControl
{
    public KanbanTaskCard()
    {
        InitializeComponent();
    }

    public string TaskNumber
    {
        get => TaskNumberText.Text;
        set => TaskNumberText.Text = value;
    }

    public string TaskTitle
    {
        get => TitleText.Text;
        set => TitleText.Text = value;
    }

    private string _taskStatus = "ToDo";
    public string TaskStatus
    {
        get => _taskStatus;
        set
        {
            _taskStatus = value;
            StatusBadgeText.Text = value;
        }
    }

    private int _priority = 2;
    public int Priority
    {
        get => _priority;
        set
        {
            _priority = value;
            UpdatePriority();
        }
    }

    public string AssignedTo
    {
        get => AssigneeText.Text;
        set => AssigneeText.Text = value;
    }

    public int EstimatedHours
    {
        get
        {
            var raw = TimeText.Text
                .Replace("Qty:", "", StringComparison.OrdinalIgnoreCase)
                .Replace("pcs", "", StringComparison.OrdinalIgnoreCase)
                .Replace("h", "", StringComparison.OrdinalIgnoreCase)
                .Trim();

            return int.TryParse(raw, out var qty) ? qty : 0;
        }
        set
        {
            var qty = value < 0 ? 0 : value;
            TimeText.Text = $"Qty: {qty}";
        }
    }

    private int _producedCount;
    private int _totalCount;

    public void SetProgress(int produced, int total)
    {
        _producedCount = Math.Max(0, produced);
        _totalCount = Math.Max(0, total);

        var pct = 0.0;
        if (_totalCount > 0)
        {
            pct = Math.Min(100.0, (_producedCount * 100.0) / _totalCount);
        }

        OfProgressText.Text = $"{_producedCount}/{_totalCount}";
        OfProgressBar.Value = pct;

        // Color ramp: green -> orange -> red
        if (_totalCount > 0 && _producedCount < _totalCount)
        {
            OfProgressBar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
        }
        else
        {
            OfProgressBar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3"));
        }
    }

    private void UpdatePriority()
    {
        var (text, color, stripeColor) = Priority switch
        {
            1 => ("Low", "#4CAF50", "#4CAF50"),
            2 => ("Medium", "#2196F3", "#2196F3"),
            3 => ("High", "#FF9800", "#FF9800"),
            4 => ("Urgent", "#F44336", "#F44336"),
            _ => ("Medium", "#2196F3", "#2196F3")
        };

        PriorityText.Text = text;
        PriorityBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        PriorityStripe.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(stripeColor));
    }

    public void SetSelected(bool selected)
    {
        try
        {
            RootBorder.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
            RootBorder.BorderBrush = selected
                ? new SolidColorBrush(Color.FromRgb(14, 165, 233))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"));
        }
        catch
        {
            // ignore
        }
    }
}
