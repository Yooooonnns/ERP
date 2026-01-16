using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DigitalisationERP.Desktop.Services;

namespace DigitalisationERP.Desktop.Views;

public partial class ShiftPlannerPage : Page
{
    private readonly RolePermissionService? _permissionService;
    private readonly ApiClient _apiClient;

    private DateTime _weekStart;

    public ObservableCollection<ShiftEntry> Shifts { get; } = new();

    public ShiftPlannerPage(RolePermissionService? permissionService = null, ApiClient? apiClient = null)
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

    private async void ThisWeek_Click(object sender, RoutedEventArgs e)
    {
        _weekStart = GetWeekStart(DateTime.Today);
        await RefreshAsync();
    }

    private async void AddShift_Click(object sender, RoutedEventArgs e)
    {
        if (_permissionService != null && !_permissionService.CanPerformAction("ManageShifts"))
        {
            MessageBox.Show("Vous n'avez pas la permission de planifier des shifts.", "Accès Restreint");
            return;
        }

        var dialog = new AddShiftDialog(defaultEmployeeId: null)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true || dialog.CreatedShift == null)
        {
            return;
        }

        try
        {
            await _apiClient.CreateShiftAsync(dialog.CreatedShift);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible d'ajouter le shift.\n\n{ex.Message}", "Planning", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void DeleteShift_Click(object sender, RoutedEventArgs e)
    {
        if (ShiftsGrid.SelectedItem is not ShiftEntry selected) return;

        if (_permissionService != null && !_permissionService.CanPerformAction("ManageShifts"))
        {
            MessageBox.Show("Vous n'avez pas la permission de supprimer des shifts.", "Accès Restreint");
            return;
        }

        var confirm = MessageBox.Show(
            "Supprimer ce shift ?",
            "Confirmation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            await _apiClient.DeleteShiftAsync(selected.Id);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible de supprimer le shift.\n\n{ex.Message}", "Planning", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task RefreshAsync()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var weekEnd = _weekStart.AddDays(6);
        WeekRangeText.Text = $"Semaine du {_weekStart.ToString("dd MMM", culture)} au {weekEnd.ToString("dd MMM yyyy", culture)}";

        Shifts.Clear();
        try
        {
            var shifts = await _apiClient.GetShiftsForWeekAsync(_weekStart);
            foreach (var shift in shifts)
            {
                Shifts.Add(shift);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible de charger les shifts.\n\n{ex.Message}", "Planning", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }
}
