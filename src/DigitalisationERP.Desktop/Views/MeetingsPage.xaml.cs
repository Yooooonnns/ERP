using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DigitalisationERP.Desktop.Services;

namespace DigitalisationERP.Desktop.Views;

public partial class MeetingsPage : Page
{
    private readonly RolePermissionService _permissionService;
    private readonly ApiService _api;
    private readonly ObservableCollection<MeetingListItem> _upcoming = new();
    private readonly ObservableCollection<MeetingListItem> _today = new();

    private List<WorkerDto> _cachedWorkers = new();

    public MeetingsPage(RolePermissionService permissionService, ApiClient apiClient)
    {
        _permissionService = permissionService;
        _api = new ApiService();
        if (!string.IsNullOrWhiteSpace(apiClient.AuthToken))
        {
            _api.SetAccessToken(apiClient.AuthToken);
        }

        InitializeComponent();

        MeetingsList.ItemsSource = _upcoming;
        TodayMeetingsList.ItemsSource = _today;

        NewMeetingButton.Visibility = CanCreateMeetings(_permissionService) ? Visibility.Visible : Visibility.Collapsed;

        Loaded += async (_, _) => await LoadMeetingsAsync();
    }

    private static bool CanCreateMeetings(RolePermissionService permissionService)
    {
        var role = permissionService.CurrentRole;
        return role == RolePermissionService.UserRole.S_USER
               || role == RolePermissionService.UserRole.SAP_ALL
               || role == RolePermissionService.UserRole.Z_PROD_MANAGER
               || role == RolePermissionService.UserRole.Z_PROD_PLANNER
               || role == RolePermissionService.UserRole.Z_MAINT_MANAGER
               || role == RolePermissionService.UserRole.Z_MAINT_PLANNER
               || role == RolePermissionService.UserRole.Z_WM_MANAGER
               || role == RolePermissionService.UserRole.Z_QM_MANAGER;
    }

    private async Task LoadMeetingsAsync()
    {
        var response = await _api.GetAsync<List<MeetingItem>>("/api/meetings/mine");
        if (!response.Success)
        {
            MessageBox.Show(
                "Failed to load meetings.\n\n" + string.Join("\n", response.Errors ?? new List<string>()),
                "Meetings",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var nowLocal = DateTime.Now;
        var meetings = response.Data ?? new List<MeetingItem>();
        var list = meetings
            .OrderBy(m => m.StartUtc)
            .Select(m => new MeetingListItem
            {
                Id = m.Id,
                Title = m.Title,
                DateTime = m.StartUtc.ToLocalTime(),
                Attendees = $"{m.AttendeeIds?.Count ?? 0} people",
                CreatedBy = m.CreatedByName
            })
            .ToList();

        _upcoming.Clear();
        foreach (var item in list.Where(m => m.DateTime >= nowLocal.AddMinutes(-30)))
            _upcoming.Add(item);

        _today.Clear();
        foreach (var item in list.Where(m => m.DateTime.Date == nowLocal.Date))
            _today.Add(item);
    }

    private void NewMeeting_Click(object sender, RoutedEventArgs e)
    {
        _ = CreateMeetingFlowAsync();
    }

    private async Task CreateMeetingFlowAsync()
    {
        try
        {
            if (!CanCreateMeetings(_permissionService))
            {
                MessageBox.Show("You are not allowed to create meetings.", "Meetings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_cachedWorkers.Count == 0)
            {
                var workersResponse = await _api.GetAsync<List<WorkerDto>>("/api/InternalEmail/workers");
                if (workersResponse.Success)
                    _cachedWorkers = workersResponse.Data ?? new List<WorkerDto>();
            }

            var dialog = new NewMeetingDialog(_cachedWorkers);
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() != true)
                return;

            var startLocal = dialog.StartLocal;
            var endLocal = dialog.EndLocal;

            var request = new CreateMeetingRequest
            {
                Title = dialog.MeetingTitle,
                Description = dialog.Description,
                StartUtc = DateTime.SpecifyKind(startLocal, DateTimeKind.Local).ToUniversalTime(),
                EndUtc = DateTime.SpecifyKind(endLocal, DateTimeKind.Local).ToUniversalTime(),
                AttendeeIds = dialog.SelectedAttendeeIds
            };

            var createResponse = await _api.PostAsync<CreateMeetingRequest, MeetingItem>("/api/meetings", request);
            if (!createResponse.Success)
            {
                MessageBox.Show(
                    "Failed to create meeting.\n\n" + string.Join("\n", createResponse.Errors ?? new List<string>()),
                    "Meetings",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            await LoadMeetingsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Meetings", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private sealed class MeetingListItem
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public DateTime DateTime { get; init; }
        public string Attendees { get; init; } = string.Empty;
        public string CreatedBy { get; init; } = string.Empty;
    }

    private sealed class CreateMeetingRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public List<int> AttendeeIds { get; set; } = new();
    }

    private sealed class MeetingItem
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public int CreatedByUserId { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public List<int> AttendeeIds { get; set; } = new();
        public List<int> AcknowledgedBy { get; set; } = new();
    }

    private sealed class WorkerDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
    }

    private sealed class NewMeetingDialog : Window
    {
        private readonly TextBox _title;
        private readonly TextBox _description;
        private readonly DatePicker _date;
        private readonly TextBox _startTime;
        private readonly TextBox _endTime;
        private readonly ListBox _attendees;

        public string MeetingTitle => _title.Text.Trim();
        public string Description => _description.Text.Trim();

        public DateTime StartLocal => ParseDateTime(_date.SelectedDate, _startTime.Text, DateTime.Now.AddHours(1));
        public DateTime EndLocal => ParseDateTime(_date.SelectedDate, _endTime.Text, DateTime.Now.AddHours(2));

        public List<int> SelectedAttendeeIds
            => _attendees.SelectedItems.OfType<WorkerDto>().Select(w => w.Id).ToList();

        public NewMeetingDialog(List<WorkerDto> workers)
        {
            Title = "New Meeting";
            Width = 520;
            Height = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var root = new Grid { Margin = new Thickness(16) };
            for (int i = 0; i < 8; i++)
            {
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            root.Children.Add(MakeLabel("Title", 0, 0));
            _title = new TextBox { MinWidth = 200, Margin = new Thickness(0, 6, 0, 10) };
            Grid.SetRow(_title, 1);
            Grid.SetColumnSpan(_title, 2);
            root.Children.Add(_title);

            root.Children.Add(MakeLabel("Description", 2, 0));
            _description = new TextBox { AcceptsReturn = true, Height = 70, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 10) };
            Grid.SetRow(_description, 3);
            Grid.SetColumnSpan(_description, 2);
            root.Children.Add(_description);

            root.Children.Add(MakeLabel("Date", 4, 0));
            _date = new DatePicker { SelectedDate = DateTime.Today, Margin = new Thickness(0, 6, 8, 10) };
            Grid.SetRow(_date, 5);
            Grid.SetColumn(_date, 0);
            root.Children.Add(_date);

            root.Children.Add(MakeLabel("Start (HH:mm)", 4, 1));
            _startTime = new TextBox { Text = DateTime.Now.AddHours(1).ToString("HH:mm"), Margin = new Thickness(0, 6, 0, 10) };
            Grid.SetRow(_startTime, 5);
            Grid.SetColumn(_startTime, 1);
            root.Children.Add(_startTime);

            root.Children.Add(MakeLabel("End (HH:mm)", 6, 1));
            _endTime = new TextBox { Text = DateTime.Now.AddHours(2).ToString("HH:mm"), Margin = new Thickness(0, 6, 0, 10) };
            Grid.SetRow(_endTime, 7);
            Grid.SetColumn(_endTime, 1);
            root.Children.Add(_endTime);

            root.Children.Add(MakeLabel("Attendees (Ctrl/Shift multi-select)", 6, 0));
            _attendees = new ListBox
            {
                SelectionMode = SelectionMode.Extended,
                Margin = new Thickness(0, 6, 0, 12),
                ItemsSource = workers.OrderBy(w => w.Department).ThenBy(w => w.Name).ToList(),
                DisplayMemberPath = nameof(WorkerDto.Name)
            };

            var view = (CollectionView)CollectionViewSource.GetDefaultView(_attendees.ItemsSource);
            view.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(WorkerDto.Department), System.ComponentModel.ListSortDirection.Ascending));
            view.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(WorkerDto.Name), System.ComponentModel.ListSortDirection.Ascending));

            Grid.SetRow(_attendees, 8);
            Grid.SetColumnSpan(_attendees, 2);
            root.Children.Add(_attendees);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = new Button { Content = "Cancel", Width = 90, Margin = new Thickness(0, 0, 8, 0) };
            cancel.Click += (_, _) => { DialogResult = false; Close(); };
            var ok = new Button { Content = "Create", Width = 90 };
            ok.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(MeetingTitle))
                {
                    MessageBox.Show("Title is required.", "New Meeting", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (EndLocal <= StartLocal)
                {
                    MessageBox.Show("End time must be after start time.", "New Meeting", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DialogResult = true;
                Close();
            };
            buttons.Children.Add(cancel);
            buttons.Children.Add(ok);
            Grid.SetRow(buttons, 9);
            Grid.SetColumnSpan(buttons, 2);
            root.Children.Add(buttons);

            Content = root;
        }

        private static TextBlock MakeLabel(string text, int row, int col)
        {
            var tb = new TextBlock { Text = text, FontWeight = FontWeights.SemiBold };
            Grid.SetRow(tb, row);
            Grid.SetColumn(tb, col);
            return tb;
        }

        private static DateTime ParseDateTime(DateTime? date, string timeText, DateTime fallback)
        {
            var d = date ?? fallback.Date;
            if (TimeSpan.TryParse(timeText, out var t))
            {
                return new DateTime(d.Year, d.Month, d.Day, t.Hours, t.Minutes, 0);
            }
            return fallback;
        }
    }
}
