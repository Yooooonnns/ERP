using System;

namespace DigitalisationERP.Core.Entities;

public class DashboardWidget
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string DashboardName { get; set; } = string.Empty;
    
    public WidgetTypeEnum WidgetType { get; set; }
    public string Title { get; set; } = string.Empty;
    
    // Layout
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    
    // Configuration (JSON)
    public string ConfigurationJson { get; set; } = "{}";
    
    // Data source
    public string? DataSourceQuery { get; set; }
    public int? RefreshIntervalSeconds { get; set; }
    
    public bool IsVisible { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum WidgetTypeEnum
{
    KpiCard,
    LineChart,
    BarChart,
    PieChart,
    GaugeChart,
    Table,
    Heatmap,
    Timeline,
    Sankey,
    AlertList,
    ProductionCounter,
    OeeGauge,
    StatusIndicator,
    TrendChart,
    TaskList,
    Chat,
    IssueList
}
