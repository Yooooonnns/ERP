using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DigitalisationERP.Desktop.Services;

namespace DigitalisationERP.Desktop.Views;

public partial class MaintenanceGuestPage : Page
{
    private readonly RolePermissionService? _permissionService;
    private readonly ApiClient _apiClient;

    private DateTime _weekStart;

    public ObservableCollection<MaintenanceShiftRow> Shifts { get; } = new();

    public MaintenanceGuestPage(RolePermissionService? permissionService = null, ApiClient? apiClient = null)
    {
        InitializeComponent();
        _permissionService = permissionService;
        _apiClient = apiClient ?? new ApiClient();

        _weekStart = GetWeekStart(DateTime.Today);
        ShiftsGrid.ItemsSource = Shifts;

        Loaded += async (_, _) => await RefreshAsync();
    }

    private async void PrevWeek_Click(object sender, RoutedEventArgs e)
    {
        _weekStart = _weekStart.AddDays(-7);
        await RefreshAsync();
    }

    private async void NextWeek_Click(object sender, RoutedEventArgs e)
    {
        _weekStart = _weekStart.AddDays(7);
        await RefreshAsync();
    }

    private async void Today_Click(object sender, RoutedEventArgs e)
    {
        _weekStart = GetWeekStart(DateTime.Today);
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var weekEnd = _weekStart.AddDays(6);
        WeekRangeText.Text = $"Semaine du {_weekStart.ToString("dd MMM", culture)} au {weekEnd.ToString("dd MMM yyyy", culture)}";

        await LoadMaintenanceShiftsAsync();
        LoadInterventions();
    }

    private async Task LoadMaintenanceShiftsAsync()
    {
        Shifts.Clear();

        // "Maintenance IT" + team sample list.
        var maintenanceUsers = new[]
        {
            "maint.it@test.com",
            "maint.tech@test.com",
            "maint.planner@test.com",
            "maint.manager@test.com"
        };

        try
        {
            var all = new ObservableCollection<MaintenanceShiftRow>();

            foreach (var user in maintenanceUsers)
            {
                // API seeds a default week when employeeId is provided and nothing exists.
                var shifts = await _apiClient.GetShiftsForWeekAsync(_weekStart, user);
                foreach (var s in shifts)
                {
                    all.Add(new MaintenanceShiftRow
                    {
                        Date = s.Date.ToString("dd/MM/yyyy"),
                        EmployeeId = s.EmployeeId,
                        Segment = s.Segment.ToString(),
                        TimeRange = $"{s.StartTime}-{s.EndTime}",
                        Location = s.Location,
                        Notes = s.Notes
                    });
                }
            }

            foreach (var row in all
                         .OrderBy(r => r.Date)
                         .ThenBy(r => r.EmployeeId)
                         .ThenBy(r => r.Segment))
            {
                Shifts.Add(row);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible de charger le planning maintenance.\n\n{ex.Message}", "Maintenance", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadInterventions()
    {
        // Simple seeded data for guest visualization (read-only)
        var items = new[]
        {
            new MaintenanceWorkItem("PM-2201", "Inspection POST-05", "POST-05", _weekStart.AddDays(1).AddHours(9), "Planned", "maint.it@test.com"),
            new MaintenanceWorkItem("PM-2202", "Graissage convoyeur ligne A", "LINE-A", _weekStart.AddDays(2).AddHours(14), "Planned", "maint.tech@test.com"),
            new MaintenanceWorkItem("PM-2203", "Remplacement capteur vibration", "POST-02", _weekStart.AddDays(0).AddHours(10), "Ongoing", "maint.tech@test.com"),
            new MaintenanceWorkItem("PM-2204", "Diagnostic alarme température", "POST-03", _weekStart.AddDays(0).AddHours(11), "Ongoing", "maint.it@test.com"),
        };

        PlannedList.ItemsSource = items
            .Where(i => i.Status == "Planned")
            .Select(i => i.ToDisplay())
            .ToList();

        OngoingList.ItemsSource = items
            .Where(i => i.Status == "Ongoing")
            .Select(i => i.ToDisplay())
            .ToList();
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }
}

public sealed class MaintenanceShiftRow
{
    public string Date { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string Segment { get; set; } = string.Empty;
    public string TimeRange { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

internal sealed class MaintenanceWorkItem
{
    public MaintenanceWorkItem(string id, string title, string asset, DateTime when, string status, string assignedTo)
    {
        Id = id;
        Title = title;
        Asset = asset;
        When = when;
        Status = status;
        AssignedTo = assignedTo;
    }

    public string Id { get; }
    public string Title { get; }
    public string Asset { get; }
    public DateTime When { get; }
    public string Status { get; }
    public string AssignedTo { get; }

    public string ToDisplay()
        => $"{Id} • {Title} • {Asset} • {When:dd/MM HH:mm} • {AssignedTo}";
}
