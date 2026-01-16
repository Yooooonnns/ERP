using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DigitalisationERP.Core.Configuration;
using DigitalisationERP.Desktop.Controls;
using DigitalisationERP.Desktop.Models.InternalMessaging;
using DigitalisationERP.Desktop.Services;

namespace DigitalisationERP.Desktop.Views;

public partial class TaskBoardPage : Page
{
    private readonly RolePermissionService? _permissionService;
    private readonly ApiClient _apiClient;
    private readonly ApiService _internalApi;

    private readonly List<TaskBoardTask> _tasks = new();

    public TaskBoardPage(RolePermissionService? permissionService = null, ApiClient? apiClient = null)
    {
        InitializeComponent();
        _permissionService = permissionService;
        _apiClient = apiClient ?? new ApiClient();

        _internalApi = new ApiService();
        if (!string.IsNullOrWhiteSpace(_apiClient.AuthToken))
        {
            _internalApi.SetAccessToken(_apiClient.AuthToken);
        }

        SeedTasks();
        Loaded += (_, _) => Render();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Render();

    private void SeedTasks()
    {
        if (_tasks.Count > 0) return;

        _tasks.AddRange(new[]
        {
            new TaskBoardTask("TASK-0101", "Vérifier POST-02 (lot A145)", "prod.operator@test.com", 4, 4, "ToDo"),
            new TaskBoardTask("TASK-0102", "Nettoyage zone B après maintenance", "prod.operator@test.com", 3, 2, "InProgress"),
            new TaskBoardTask("TASK-0103", "Calibration POST-05", "maint.tech@test.com", 3, 3, "ToDo"),
            new TaskBoardTask("TASK-0104", "Inventaire pièces zone C", "wm.clerk@test.com", 2, 2, "Done"),
            new TaskBoardTask("TASK-0105", "Formation sécurité mensuelle", "prod.operator@test.com", 2, 1, "Blocked")
        });
    }

    private void Render()
    {
        var userId = _permissionService?.CurrentUserId ?? "Development User";
        var role = _permissionService?.CurrentRole.ToString() ?? "";
        UserText.Text = $"Utilisateur: {userId} {(!string.IsNullOrWhiteSpace(role) ? $"({role})" : string.Empty)}";

        TodoPanel.Children.Clear();
        InProgressPanel.Children.Clear();
        DonePanel.Children.Clear();
        BlockedPanel.Children.Clear();

        foreach (var task in _tasks)
        {
            var card = new KanbanTaskCard
            {
                Margin = new Thickness(0, 0, 0, 10)
            };
            card.TaskNumber = task.TaskNumber;
            card.TaskTitle = task.Title;
            card.AssignedTo = task.AssignedTo;
            card.Priority = task.Priority;
            card.TaskStatus = task.Status;
            card.EstimatedHours = task.EstimatedHours;

            card.MouseLeftButtonUp += async (_, _) => await OpenTaskDialogAsync(task);

            var target = task.Status switch
            {
                "ToDo" => TodoPanel,
                "InProgress" => InProgressPanel,
                "Done" => DonePanel,
                "Blocked" => BlockedPanel,
                _ => TodoPanel
            };
            target.Children.Add(card);
        }
    }

    private async Task OpenTaskDialogAsync(TaskBoardTask task)
    {
        var dialog = new TaskReportDialog(task.TaskNumber, task.Title, task.Status)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var newStatus = dialog.Status;
        if (!string.Equals(newStatus, task.Status, StringComparison.OrdinalIgnoreCase))
        {
            // Track start/end times for duration autofill.
            if (string.Equals(newStatus, "InProgress", StringComparison.OrdinalIgnoreCase) && task.StartedAtUtc == null)
            {
                task.StartedAtUtc = DateTime.UtcNow;
            }

            if (string.Equals(newStatus, "Done", StringComparison.OrdinalIgnoreCase) && task.CompletedAtUtc == null)
            {
                task.CompletedAtUtc = DateTime.UtcNow;
            }

            task.Status = newStatus;
        }

        var message = dialog.ReportMessage;
        if (!string.IsNullOrWhiteSpace(message))
        {
            var submittedBy = _permissionService?.CurrentUserId ?? "Development User";

            try
            {
                await _apiClient.CreateTaskFeedbackAsync(new FeedbackEntry
                {
                    TaskNumber = task.TaskNumber,
                    TaskTitle = task.Title,
                    SubmittedBy = submittedBy,
                    Message = message,
                    Status = "New"
                });

                MessageBox.Show("Feedback envoyé aux managers (serveur).", "Feedback", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossible d'envoyer le feedback.\n\n{ex.Message}", "Feedback", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // When a task is marked Done, prompt to generate a report and send it internally.
        if (string.Equals(task.Status, "Done", StringComparison.OrdinalIgnoreCase))
        {
            var operatorName = _permissionService?.CurrentUserId ?? "Unknown";
            var startUtc = task.StartedAtUtc;
            var endUtc = task.CompletedAtUtc ?? DateTime.UtcNow;
            var duration = startUtc.HasValue ? (endUtc - startUtc.Value) : (TimeSpan?)null;

            var confirm = MessageBox.Show(
                "Générer un rapport d'intervention et l'envoyer au manager/leader via la messagerie interne ?",
                "Rapport d'intervention",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                await SendInterventionReportAsync(task, operatorName, startUtc, endUtc, duration, dialog.ShortNote, dialog.ReportMessage);
            }
        }

        Render();
    }

    private async Task SendInterventionReportAsync(
        TaskBoardTask task,
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

        // Fetch contacts and default to managers/leads/planners.
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

        var reportFile = CreateInterventionReportFile(task, operatorName, startedAtUtc, completedAtUtc, duration, shortNote, reportMessage);
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

            var subject = $"Intervention Report - {task.TaskNumber} - {task.Title}";
            var body = BuildInterventionBody(task, operatorName, startedAtUtc, completedAtUtc, duration, shortNote, reportMessage);

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

            MessageBox.Show("Rapport envoyé via la messagerie interne.", "Rapport d'intervention", MessageBoxButton.OK, MessageBoxImage.Information);
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

        // Prefer same department family when possible.
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

    private static string CreateInterventionReportFile(
        TaskBoardTask task,
        string operatorName,
        DateTime? startedAtUtc,
        DateTime completedAtUtc,
        TimeSpan? duration,
        string shortNote,
        string reportMessage)
    {
        var safeTask = string.Concat(task.TaskNumber.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
        var fileName = $"InterventionReport_{safeTask}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var path = Path.Combine(Path.GetTempPath(), fileName);
        var body = BuildInterventionBody(task, operatorName, startedAtUtc, completedAtUtc, duration, shortNote, reportMessage);
        System.IO.File.WriteAllText(path, body);
        return path;
    }

    private static string BuildInterventionBody(
        TaskBoardTask task,
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
            "Maintenance/Intervention Report\n" +
            "==============================\n" +
            $"Task: {task.TaskNumber}\n" +
            $"Title: {task.Title}\n" +
            $"Status: {task.Status}\n" +
            $"Operator: {operatorName}\n" +
            $"Started: {(startedLocal.HasValue ? startedLocal.Value.ToString("yyyy-MM-dd HH:mm") : "N/A")}\n" +
            $"Completed: {completedLocal:yyyy-MM-dd HH:mm}\n" +
            $"Duration: {durationText}\n" +
            (string.IsNullOrWhiteSpace(shortNote) ? string.Empty : $"\nShort note:\n{shortNote}\n") +
            (string.IsNullOrWhiteSpace(reportMessage) ? string.Empty : $"\nDetails:\n{reportMessage}\n");
    }
}

public sealed class TaskBoardTask
{
    public TaskBoardTask(string taskNumber, string title, string assignedTo, int priority, int estimatedHours, string status)
    {
        TaskNumber = taskNumber;
        Title = title;
        AssignedTo = assignedTo;
        Priority = priority;
        EstimatedHours = estimatedHours;
        Status = status;
    }

    public string TaskNumber { get; }
    public string Title { get; }
    public string AssignedTo { get; }
    public int Priority { get; }
    public int EstimatedHours { get; }
    public string Status { get; set; }

    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
