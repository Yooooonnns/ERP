using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DigitalisationERP.Desktop.Services;

namespace DigitalisationERP.Desktop.Views;

public partial class FeedbackInboxPage : Page
{
    private readonly RolePermissionService? _permissionService;
    private readonly ApiClient _apiClient;

    public ObservableCollection<FeedbackEntry> Items { get; } = new();

    public FeedbackInboxPage(RolePermissionService? permissionService = null, ApiClient? apiClient = null)
    {
        InitializeComponent();
        _permissionService = permissionService;
        _apiClient = apiClient ?? new ApiClient();

        FeedbackGrid.ItemsSource = Items;
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => await RefreshAsync();

    private async void Acknowledge_Click(object sender, RoutedEventArgs e)
    {
        if (!CanManageInbox())
        {
            MessageBox.Show("Vous n'avez pas la permission de traiter les feedbacks.", "Accès Restreint");
            return;
        }

        if (FeedbackGrid.SelectedItem is not FeedbackEntry selected) return;

        try
        {
            await _apiClient.UpdateTaskFeedbackStatusAsync(selected.Id, "Acknowledged");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible de mettre à jour le feedback.\n\n{ex.Message}", "Feedback", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Resolve_Click(object sender, RoutedEventArgs e)
    {
        if (!CanManageInbox())
        {
            MessageBox.Show("Vous n'avez pas la permission de traiter les feedbacks.", "Accès Restreint");
            return;
        }

        if (FeedbackGrid.SelectedItem is not FeedbackEntry selected) return;

        try
        {
            await _apiClient.UpdateTaskFeedbackStatusAsync(selected.Id, "Resolved");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible de mettre à jour le feedback.\n\n{ex.Message}", "Feedback", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task RefreshAsync()
    {
        Items.Clear();

        var filter = (StatusFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";

        try
        {
            var list = string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase)
                ? await _apiClient.GetTaskFeedbackAsync(status: null)
                : await _apiClient.GetTaskFeedbackAsync(status: filter);

            foreach (var item in list)
            {
                Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible de charger les feedbacks.\n\n{ex.Message}", "Feedback", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        CountText.Text = $"{Items.Count} élément(s)";
    }

    private bool CanManageInbox()
    {
        if (_permissionService == null) return true;

        // Managers/team leaders can manage feedback.
        return _permissionService.CurrentRole is RolePermissionService.UserRole.SAP_ALL
            or RolePermissionService.UserRole.S_USER
            or RolePermissionService.UserRole.Z_PROD_MANAGER
            or RolePermissionService.UserRole.Z_PROD_PLANNER
            or RolePermissionService.UserRole.Z_MAINT_MANAGER
            or RolePermissionService.UserRole.Z_MAINT_PLANNER
            or RolePermissionService.UserRole.Z_WM_MANAGER
            or RolePermissionService.UserRole.Z_QM_MANAGER;
    }
}
