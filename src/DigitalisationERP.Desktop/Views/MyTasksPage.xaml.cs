using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using DigitalisationERP.Desktop.Models.InternalMessaging;
using DigitalisationERP.Desktop.Services;

namespace DigitalisationERP.Desktop.Views
{
    public partial class MyTasksPage : Page
    {
        private readonly RolePermissionService? _permissionService;
        private readonly ApiClient _apiClient;
        private readonly ApiService _internalApi;

        public ObservableCollection<TaskItemDetail> Tasks { get; set; } = new();

        public MyTasksPage()
        {
            InitializeComponent();
            _apiClient = new ApiClient();

            _internalApi = new ApiService();
            LoadTasks();
        }

        public MyTasksPage(RolePermissionService? permissionService = null, ApiClient? apiClient = null)
        {
            InitializeComponent();
            _permissionService = permissionService;
            _apiClient = apiClient ?? new ApiClient();

            _internalApi = new ApiService();
            if (!string.IsNullOrWhiteSpace(_apiClient.AuthToken))
            {
                _internalApi.SetAccessToken(_apiClient.AuthToken);
            }

            LoadTasks();
        }

        private void LoadTasks()
        {
            Tasks = new ObservableCollection<TaskItemDetail>
            {
                new TaskItemDetail
                {
                    TaskId = "MY-0001",
                    Title = "Contrôle Qualité Lot-A145",
                    Description = "Vérifier la conformité des 150 unités produites sur POST-02",
                    Priority = "Haute",
                    PriorityColor = new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                    DueDate = "Aujourd'hui 14:00",
                    AssignedBy = "Sophie Martin",
                    IsCompleted = false
                },
                new TaskItemDetail
                {
                    TaskId = "MY-0002",
                    Title = "Nettoyage Zone B",
                    Description = "Nettoyage complet après maintenance",
                    Priority = "Moyenne",
                    PriorityColor = new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                    DueDate = "Aujourd'hui 16:30",
                    AssignedBy = "Marc Dupont",
                    IsCompleted = false
                },
                new TaskItemDetail
                {
                    TaskId = "MY-0003",
                    Title = "Formation Sécurité",
                    Description = "Participer à la session de formation mensuelle obligatoire",
                    Priority = "Moyenne",
                    PriorityColor = new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                    DueDate = "16 Jan 10:00",
                    AssignedBy = "Direction RH",
                    IsCompleted = false
                },
                new TaskItemDetail
                {
                    TaskId = "MY-0004",
                    Title = "Rapport Production Hebdo",
                    Description = "Compiler les métriques de production de la semaine",
                    Priority = "Basse",
                    PriorityColor = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                    DueDate = "17 Jan 17:00",
                    AssignedBy = "Emma Petit",
                    IsCompleted = false
                },
                new TaskItemDetail
                {
                    TaskId = "MY-0005",
                    Title = "Inventaire Pièces",
                    Description = "Compter les pièces détachées en stock Zone C",
                    Priority = "Haute",
                    PriorityColor = new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                    DueDate = "15 Jan 12:00",
                    AssignedBy = "Julie Bernard",
                    IsCompleted = true,
                    CompletedAtUtc = DateTime.UtcNow.AddDays(-1)
                },
                new TaskItemDetail
                {
                    TaskId = "MY-0006",
                    Title = "Calibration POST-05",
                    Description = "Vérifier et ajuster les paramètres de calibration",
                    Priority = "Moyenne",
                    PriorityColor = new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                    DueDate = "14 Jan 15:00",
                    AssignedBy = "Pierre Durand",
                    IsCompleted = true,
                    CompletedAtUtc = DateTime.UtcNow.AddDays(-2)
                }
            };

            TasksItemsControl.ItemsSource = Tasks;

            // Update stats
            int total = Tasks.Count;
            int completed = 0;
            int active = 0;

            foreach (var task in Tasks)
            {
                if (task.IsCompleted) completed++;
                else active++;
            }

            TotalTasksText.Text = total.ToString();
            ActiveTasksText.Text = active.ToString();
            CompletedTasksText.Text = completed.ToString();
        }

        private void UpdateStats()
        {
            if (Tasks == null) return;

            int total = Tasks.Count;
            int completed = 0;
            int active = 0;

            foreach (var task in Tasks)
            {
                if (task.IsCompleted) completed++;
                else active++;
            }

            TotalTasksText.Text = total.ToString();
            ActiveTasksText.Text = active.ToString();
            CompletedTasksText.Text = completed.ToString();
        }

        private void TaskDetails_Click(object sender, RoutedEventArgs e)
        {
            var task = (sender as FrameworkElement)?.DataContext as TaskItemDetail;
            if (task == null) return;

            // Use the first "details" view as a lightweight "started" signal for autofill.
            if (!task.IsCompleted && task.StartedAtUtc == null)
            {
                task.StartedAtUtc = DateTime.UtcNow;
            }

            MessageBox.Show(
                $"{task.Title}\n\n{task.Description}\n\nPriorité: {task.Priority}\nÉchéance: {task.DueDate}\nAssigné par: {task.AssignedBy}",
                "Task details",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async void CompleteTask_Click(object sender, RoutedEventArgs e)
        {
            var task = (sender as FrameworkElement)?.DataContext as TaskItemDetail;
            if (task == null) return;

            if (!task.IsCompleted)
            {
                task.IsCompleted = true;
                task.CompletedAtUtc ??= DateTime.UtcNow;
                UpdateStats();

                var view = CollectionViewSource.GetDefaultView(TasksItemsControl.ItemsSource);
                view?.Refresh();

                var confirm = MessageBox.Show(
                    "Générer un rapport de fin de tâche et l'envoyer au manager/leader via la messagerie interne ?",
                    "Rapport de fin de tâche",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm == MessageBoxResult.Yes)
                {
                    var dialog = new TaskReportDialog(task.TaskId, task.Title, "Done")
                    {
                        Owner = Window.GetWindow(this)
                    };
                    dialog.Status = "Done";

                    if (dialog.ShowDialog() == true)
                    {
                        var operatorName = _permissionService?.CurrentUserId ?? "Unknown";
                        var startUtc = task.StartedAtUtc;
                        var endUtc = task.CompletedAtUtc ?? DateTime.UtcNow;
                        var duration = startUtc.HasValue ? (endUtc - startUtc.Value) : (TimeSpan?)null;

                        await SendMyTaskCompletionReportAsync(task, operatorName, startUtc, endUtc, duration, dialog.ShortNote, dialog.ReportMessage);
                    }
                }
            }
        }

        private async Task SendMyTaskCompletionReportAsync(
            TaskItemDetail task,
            string operatorName,
            DateTime? startedAtUtc,
            DateTime completedAtUtc,
            TimeSpan? duration,
            string shortNote,
            string reportMessage)
        {
            if (!_internalApi.IsAuthenticated)
            {
                MessageBox.Show("Not authenticated; cannot send internal report.", "Internal Messaging", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var workersResp = await _internalApi.GetAsync<List<WorkerDto>>("/api/InternalEmail/workers");
            if (!workersResp.Success || workersResp.Data == null)
            {
                MessageBox.Show(
                    "Failed to load contacts for recipient selection.\n\n" + string.Join("\n", workersResp.Errors ?? new List<string>()),
                    "Internal Messaging",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            var workers = workersResp.Data;
            var candidates = PickManagerCandidates(workers, _permissionService?.CurrentRole.ToString());
            if (candidates.Count == 0)
            {
                candidates = workers.OrderBy(w => w.Department).ThenBy(w => w.Name).ToList();
            }

            var picker = new RecipientPickerDialog(candidates)
            {
                Owner = Window.GetWindow(this)
            };

            if (picker.ShowDialog() != true)
                return;

            var recipients = picker.SelectedRecipientIds;
            if (recipients.Count == 0)
            {
                MessageBox.Show("Please select at least one recipient.", "Internal Messaging", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var reportFile = CreateMyTaskReportFile(task, operatorName, startedAtUtc, completedAtUtc, duration, shortNote, reportMessage);
            try
            {
                var uploadResp = await _internalApi.UploadFileAsync<InternalEmailAttachmentDto>("/api/InternalEmail/attachments/upload", reportFile);
                if (!uploadResp.Success || uploadResp.Data == null)
                {
                    MessageBox.Show(
                        "Failed to upload report attachment.\n\n" + string.Join("\n", uploadResp.Errors ?? new List<string>()),
                        "Internal Messaging",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                var attachment = uploadResp.Data;
                var subject = $"Task Completion Report - {task.TaskId} - {task.Title}";
                var body = BuildMyTaskBody(task, operatorName, startedAtUtc, completedAtUtc, duration, shortNote, reportMessage);

                var sendRequest = new SendInternalEmailRequest
                {
                    RecipientIds = recipients,
                    Subject = subject,
                    Body = body,
                    Attachments = new List<InternalEmailAttachmentDto> { attachment }
                };

                var sendResp = await _internalApi.PostAsync<SendInternalEmailRequest, object>("/api/InternalEmail/send", sendRequest);
                if (!sendResp.Success)
                {
                    MessageBox.Show(
                        "Failed to send internal report.\n\n" + string.Join("\n", sendResp.Errors ?? new List<string>()),
                        "Internal Messaging",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                MessageBox.Show("Rapport envoyé via la messagerie interne.", "Rapport de fin de tâche", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                try { System.IO.File.Delete(reportFile); } catch { /* ignore */ }
            }
        }

        private static List<WorkerDto> PickManagerCandidates(List<WorkerDto> workers, string? currentRole)
        {
            static bool IsManagerLike(string? role)
            {
                if (string.IsNullOrWhiteSpace(role)) return false;
                return role.Contains("S_USER", StringComparison.OrdinalIgnoreCase)
                       || role.Contains("MANAGER", StringComparison.OrdinalIgnoreCase)
                       || role.Contains("LEADER", StringComparison.OrdinalIgnoreCase)
                       || role.Contains("PLANNER", StringComparison.OrdinalIgnoreCase);
            }

            string? family = null;
            if (!string.IsNullOrWhiteSpace(currentRole))
            {
                if (currentRole.StartsWith("Z_MAINT_", StringComparison.OrdinalIgnoreCase)) family = "MAINT";
                else if (currentRole.StartsWith("Z_PROD_", StringComparison.OrdinalIgnoreCase)) family = "PROD";
                else if (currentRole.StartsWith("Z_WM_", StringComparison.OrdinalIgnoreCase) || currentRole.StartsWith("Z_MM_", StringComparison.OrdinalIgnoreCase)) family = "WM";
                else if (currentRole.StartsWith("Z_QM_", StringComparison.OrdinalIgnoreCase)) family = "QM";
            }

            var managerLike = workers.Where(w => IsManagerLike(w.Role)).ToList();
            if (family == null) return managerLike;

            var preferred = managerLike.Where(w => w.Role.StartsWith("Z_" + family + "_", StringComparison.OrdinalIgnoreCase)).ToList();
            return preferred.Count > 0 ? preferred : managerLike;
        }

        private static string CreateMyTaskReportFile(
            TaskItemDetail task,
            string operatorName,
            DateTime? startedAtUtc,
            DateTime completedAtUtc,
            TimeSpan? duration,
            string shortNote,
            string reportMessage)
        {
            var safeTask = string.Concat((task.TaskId ?? "TASK").Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
            var fileName = $"TaskCompletionReport_{safeTask}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), fileName);
            var body = BuildMyTaskBody(task, operatorName, startedAtUtc, completedAtUtc, duration, shortNote, reportMessage);
            System.IO.File.WriteAllText(path, body);
            return path;
        }

        private static string BuildMyTaskBody(
            TaskItemDetail task,
            string operatorName,
            DateTime? startedAtUtc,
            DateTime completedAtUtc,
            TimeSpan? duration,
            string shortNote,
            string reportMessage)
        {
            var startedLocal = startedAtUtc?.ToLocalTime();
            var completedLocal = completedAtUtc.ToLocalTime();
            var durationText = duration.HasValue ? $"{(int)duration.Value.TotalMinutes} min" : "N/A";

            return
                "Task Completion Report\n" +
                "====================\n" +
                $"Task: {task.TaskId}\n" +
                $"Title: {task.Title}\n" +
                $"Priority: {task.Priority}\n" +
                $"Due: {task.DueDate}\n" +
                $"Assigned by: {task.AssignedBy}\n" +
                $"Operator: {operatorName}\n" +
                $"Started: {(startedLocal.HasValue ? startedLocal.Value.ToString("yyyy-MM-dd HH:mm") : "N/A")}\n" +
                $"Completed: {completedLocal:yyyy-MM-dd HH:mm}\n" +
                $"Duration: {durationText}\n" +
                "\nDescription:\n" + task.Description + "\n" +
                (string.IsNullOrWhiteSpace(shortNote) ? string.Empty : $"\nShort note:\n{shortNote}\n") +
                (string.IsNullOrWhiteSpace(reportMessage) ? string.Empty : $"\nDetails:\n{reportMessage}\n");
        }
    }

    public class TaskItemDetail
    {
        public string TaskId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public SolidColorBrush PriorityColor { get; set; } = Brushes.Gray;
        public string DueDate { get; set; } = string.Empty;
        public string AssignedBy { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }

        public DateTime? StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
    }
}
