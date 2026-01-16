using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DigitalisationERP.Desktop.Services;

namespace DigitalisationERP.Desktop.Views
{
    public partial class MySchedulePage : Page
    {
        private readonly RolePermissionService? _permissionService;
        private readonly ApiClient _apiClient;

        private DateTime _weekStart;

        public MySchedulePage(RolePermissionService? permissionService = null, ApiClient? apiClient = null)
        {
            InitializeComponent();

            _permissionService = permissionService;
            _apiClient = apiClient ?? new ApiClient();
            _weekStart = GetWeekStart(DateTime.Today);

            Loaded += async (_, _) => await RenderWeekAsync();
        }

        private async void PrevWeek_Click(object sender, RoutedEventArgs e)
        {
            _weekStart = _weekStart.AddDays(-7);
            await RenderWeekAsync();
        }

        private async void NextWeek_Click(object sender, RoutedEventArgs e)
        {
            _weekStart = _weekStart.AddDays(7);
            await RenderWeekAsync();
        }

        private async void Today_Click(object sender, RoutedEventArgs e)
        {
            _weekStart = GetWeekStart(DateTime.Today);
            await RenderWeekAsync();
        }

        private async void RequestLeave_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new LeaveRequestDialog
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var userId = GetUserId();
                await _apiClient.CreateLeaveRequestAsync(new LeaveRequestEntry
                {
                    UserId = userId,
                    StartDate = dialog.StartDate!.Value.Date,
                    EndDate = dialog.EndDate!.Value.Date,
                    Reason = dialog.Reason
                });

                MessageBox.Show("Demande envoyée.\n\nVotre demande de congé a été transmise au serveur.", "Congé", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossible d'envoyer la demande de congé.\n\n{ex.Message}", "Congé", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RenderWeekAsync()
        {
            var culture = CultureInfo.GetCultureInfo("fr-FR");

            var weekEnd = _weekStart.AddDays(6);
            WeekRangeText.Text = $"Semaine du {_weekStart.ToString("dd MMM", culture)} au {weekEnd.ToString("dd MMM yyyy", culture)}";

            var dayDates = Enumerable.Range(0, 7)
                .Select(i => _weekStart.AddDays(i))
                .ToArray();

            Day1Date.Text = dayDates[0].ToString("dd MMM", culture);
            Day2Date.Text = dayDates[1].ToString("dd MMM", culture);
            Day3Date.Text = dayDates[2].ToString("dd MMM", culture);
            Day4Date.Text = dayDates[3].ToString("dd MMM", culture);
            Day5Date.Text = dayDates[4].ToString("dd MMM", culture);
            Day6Date.Text = dayDates[5].ToString("dd MMM", culture);
            Day7Date.Text = dayDates[6].ToString("dd MMM", culture);

            await RenderScheduleGridAsync();
        }

        private async Task RenderScheduleGridAsync()
        {
            ScheduleGrid.Children.Clear();

            var userId = GetUserId();
            ShiftEntry[] shifts;
            try
            {
                var list = await _apiClient.GetShiftsForWeekAsync(_weekStart, userId);
                shifts = list.ToArray();
            }
            catch (Exception ex)
            {
                HoursThisWeekText.Text = "--";
                MessageBox.Show($"Impossible de charger le planning.\n\n{ex.Message}", "Planning", MessageBoxButton.OK, MessageBoxImage.Error);
                shifts = Array.Empty<ShiftEntry>();
            }

            // Time label column
            AddCellBorder(0, 0, 1, 1, CreateLabel("Matin"));
            AddCellBorder(1, 0, 1, 1, CreateLabel("Après-midi"));
            AddCellBorder(2, 0, 1, 1, CreateLabel("Nuit"));

            // Base grid cells + shifts
            for (var dayIndex = 0; dayIndex < 7; dayIndex++)
            {
                var date = _weekStart.AddDays(dayIndex);
                var col = dayIndex + 1;

                var dayShifts = shifts.Where(s => s.Date.Date == date.Date).ToList();

                // Weekend with no shifts: show "Repos" across all rows.
                if ((date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) && dayShifts.Count == 0)
                {
                    AddCellBorder(0, col, 3, 1, CreateDayOffCell());
                    continue;
                }

                for (var row = 0; row < 3; row++)
                {
                    var segment = (ShiftSegment)row;
                    var shift = dayShifts.FirstOrDefault(s => s.Segment == segment);
                    AddCellBorder(row, col, 1, 1, CreateShiftCell(shift, segment));
                }
            }

            // Hours summary
            var total = shifts
                .Select(TryGetDuration)
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .Aggregate(TimeSpan.Zero, (acc, value) => acc + value);

            HoursThisWeekText.Text = $"{(int)total.TotalHours}h{total.Minutes:00}";
        }

        private static TimeSpan? TryGetDuration(ShiftEntry entry)
        {
            if (entry == null) return null;
            if (!TimeSpan.TryParse(entry.StartTime, out var start)) return null;
            if (!TimeSpan.TryParse(entry.EndTime, out var end)) return null;

            var duration = end - start;
            if (duration < TimeSpan.Zero)
            {
                duration = duration + TimeSpan.FromDays(1);
            }

            return duration;
        }

        private static DateTime GetWeekStart(DateTime date)
        {
            // ISO-like week start: Monday
            var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.Date.AddDays(-diff);
        }

        private string GetUserId()
        {
            var userId = _permissionService?.CurrentUserId;
            if (!string.IsNullOrWhiteSpace(userId)) return userId;
            return "Development User";
        }

        private static UIElement CreateLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static UIElement CreateDayOffCell()
        {
            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            stack.Children.Add(new TextBlock
            {
                Text = "Repos",
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 195, 74)),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            return stack;
        }

        private static UIElement CreateShiftCell(ShiftEntry? shift, ShiftSegment segment)
        {
            if (shift == null)
            {
                return new Grid();
            }

            var (bg, fg) = segment switch
            {
                ShiftSegment.Morning => (Color.FromRgb(227, 242, 253), Color.FromRgb(33, 150, 243)),
                ShiftSegment.Afternoon => (Color.FromRgb(255, 243, 224), Color.FromRgb(255, 152, 0)),
                ShiftSegment.Night => (Color.FromRgb(243, 229, 245), Color.FromRgb(156, 39, 176)),
                _ => (Colors.White, Colors.Black)
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = segment switch
                {
                    ShiftSegment.Morning => "Shift Matin",
                    ShiftSegment.Afternoon => "Shift Après-midi",
                    ShiftSegment.Night => "Shift Nuit",
                    _ => "Shift"
                },
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = new SolidColorBrush(fg)
            });
            stack.Children.Add(new TextBlock
            {
                Text = $"{shift.StartTime} - {shift.EndTime}",
                FontSize = 11,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 4, 0, 0)
            });
            stack.Children.Add(new TextBlock
            {
                Text = shift.Location,
                FontSize = 10,
                Foreground = Brushes.Gray
            });

            return new Border
            {
                Background = new SolidColorBrush(bg),
                Padding = new Thickness(8),
                Child = stack
            };
        }

        private void AddCellBorder(int row, int col, int rowSpan, int colSpan, UIElement content)
        {
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(0, 0, col == 7 ? 0 : 1, row == 2 && rowSpan == 1 ? 0 : 1),
                Padding = new Thickness(10),
                Child = content
            };

            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            Grid.SetRowSpan(border, rowSpan);
            Grid.SetColumnSpan(border, colSpan);
            ScheduleGrid.Children.Add(border);
        }
    }
}
