using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using DigitalisationERP.Application.Services;
using DigitalisationERP.Core.Configuration;
using DigitalisationERP.Core.Entities;
using DigitalisationERP.Core.Entities.IoT;
using DigitalisationERP.Desktop.Models.InternalMessaging;
using DigitalisationERP.Desktop.Services;
using DigitalisationERP.Desktop.Services.IoT;

namespace DigitalisationERP.Desktop.Views
{
    public partial class MaintenancePageAdvanced : UserControl
    {
        private readonly RolePermissionService? _permissionService;
        private readonly ApiClient _apiClient;
        private readonly ApiService _internalApi;

        private readonly ProductionDataService _productionDataService = ProductionDataService.Instance;
        private MaintenanceHealthScoreCalculationService _healthScoreService = null!;
        private MaintenanceAlertManager _alertManager = null!;
        private PlannedMaintenanceService _plannedMaintenanceService = null!;
        private SensorSimulationService _sensorSimulationService = null!;
        private RealTimeSimulationIntegrator _realtimeIntegrator = null!;
        private RealtimeWebSocketClient? _webSocketClient;
        private DispatcherTimer _refreshTimer = null!;
        private string? _selectedLineKey;
        private int _selectedLineNumeric = 0;
        private readonly Dictionary<string, int> _lineIdMap = new();
        private int _refreshCount = 0;
        private Random _anomalyRandom = new Random();
        private bool _isReady;

        // Mock data storage
        private List<ProductionPost> _allPosts = new();
        private List<MaintenanceSchedule> _schedules = new();
        private List<SensorReading> _sensorReadings = new();
        private List<MaintenanceAlert> _currentAlerts = new();
        private readonly List<MaintenanceAlert> _injectedAlerts = new();
        private List<PlannedMaintenanceTask> _plannedTasks = new();

        // Configuration
        private bool _useWebSocket = false; // Toggle between local simulation and WebSocket streaming
        private string _apiBaseUrl = ErpRuntimeConfig.ApiBaseUrl; // API base URL for WebSocket connection

        public MaintenancePageAdvanced() : this(null, null)
        {
        }

        public MaintenancePageAdvanced(RolePermissionService? permissionService, ApiClient? apiClient)
        {
            InitializeComponent();

            _permissionService = permissionService;
            _apiClient = apiClient ?? new ApiClient();
            _internalApi = new ApiService();
            if (!string.IsNullOrWhiteSpace(_apiClient.AuthToken))
            {
                _internalApi.SetAccessToken(_apiClient.AuthToken);
            }

            InitializeServices();
            LoadProductionLines();
            LoadDataFromProduction();
            InitializeInjectionSelectors();
            InitializeTimers();
            HookRobotFeed();
            _isReady = true;
            RefreshData();
        }

        private void InitializeInjectionSelectors()
        {
            InjectTroubleTypeSelector.Items.Clear();
            InjectTroubleTypeSelector.Items.Add(new ComboBoxItem { Content = "Overheat", Tag = "overheat" });
            InjectTroubleTypeSelector.Items.Add(new ComboBoxItem { Content = "Vibration", Tag = "vibration_spike" });
            InjectTroubleTypeSelector.Items.Add(new ComboBoxItem { Content = "Oil low", Tag = "low_level" });
            InjectTroubleTypeSelector.Items.Add(new ComboBoxItem { Content = "Pressure drop", Tag = "pressure_drop" });
            InjectTroubleTypeSelector.SelectedIndex = InjectTroubleTypeSelector.Items.Count > 0 ? 0 : -1;

            UpdateInjectPostSelector();
        }

        private void UpdateInjectPostSelector()
        {
            InjectPostSelector.Items.Clear();

            var relevantPosts = _selectedLineNumeric == 0
                ? _allPosts
                : _allPosts.Where(p => p.ProductionLineId == _selectedLineNumeric).ToList();

            foreach (var post in relevantPosts.OrderBy(p => p.SequenceOrder))
            {
                InjectPostSelector.Items.Add(new ComboBoxItem
                {
                    Content = $"{post.PostCode}",
                    Tag = post.PostCode
                });
            }

            InjectPostSelector.SelectedIndex = InjectPostSelector.Items.Count > 0 ? 0 : -1;
        }

        private static string? GetComboTagAsString(ComboBox comboBox)
        {
            return (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        }

        private void EmitMaintenanceSignal(string eventType, MaintenanceAlert alert)
        {
            var payload = JsonSerializer.Serialize(new
            {
                type = eventType,
                postCode = alert.PostCode,
                troubleType = alert.AlertType,
                status = alert.Status,
                timestamp = DateTimeOffset.Now
            });

            AppendRobotLog(payload);
        }

        private void InitializeServices()
        {
            _productionDataService.EnsureInitialized();
            _healthScoreService = new MaintenanceHealthScoreCalculationService();
            _alertManager = new MaintenanceAlertManager(_healthScoreService);
            _sensorSimulationService = new SensorSimulationService();
            _realtimeIntegrator = new RealTimeSimulationIntegrator(
                _sensorSimulationService,
                new ProductionSimulationService(),
                _alertManager,
                _healthScoreService
            );
            _schedules = new List<MaintenanceSchedule>();
            _plannedMaintenanceService = new PlannedMaintenanceService(_schedules);
            _sensorReadings = new List<SensorReading>();
            _currentAlerts = new List<MaintenanceAlert>();
            _plannedTasks = new List<PlannedMaintenanceTask>();

            // Initialize WebSocket client (optional, based on configuration)
            if (_useWebSocket)
            {
                InitializeWebSocketClient();
            }
        }

        /// <summary>
        /// Initialize WebSocket client for real-time streaming
        /// </summary>
        private void InitializeWebSocketClient()
        {
            try
            {
                _webSocketClient = new RealtimeWebSocketClient(_apiBaseUrl, null);

                // Register event handlers
                _webSocketClient.OnSnapshotUpdate += HandleSnapshotUpdate;
                _webSocketClient.OnDashboardUpdate += HandleDashboardUpdate;
                _webSocketClient.OnNewAlert += HandleNewAlert;
                _webSocketClient.OnNewIncident += HandleNewIncident;
                _webSocketClient.OnConnectionStateChanged += HandleConnectionStateChanged;
                _webSocketClient.OnError += HandleWebSocketError;

                System.Diagnostics.Debug.WriteLine("✓ WebSocket client initialized");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error initializing WebSocket: {ex.Message}");
                _useWebSocket = false;
            }
        }

        private void LoadProductionLines()
        {
            LineSelector.Items.Clear();
            LineSelector.Items.Add(new ComboBoxItem { Content = "All lines", Tag = "ALL" });

            _lineIdMap.Clear();
            var lines = _productionDataService.ProductionLines.ToList();
            for (int i = 0; i < lines.Count; i++)
            {
                _lineIdMap[lines[i].LineId] = i + 1;
                LineSelector.Items.Add(new ComboBoxItem { Content = lines[i].LineName, Tag = lines[i].LineId });
            }

            if (LineSelector.Items.Count > 0)
            {
                LineSelector.SelectedIndex = 0;
                _selectedLineKey = "ALL";
                _selectedLineNumeric = 0;
            }
        }

        private void LoadDataFromProduction()
        {
            _allPosts.Clear();
            foreach (var post in _productionDataService.Posts)
            {
                var mapped = new ProductionPost
                {
                    Id = _allPosts.Count + 1,
                    PostCode = post.PostCode,
                    PostName = post.PostName,
                    Status = post.Status switch
                    {
                        "Maintenance" => PostStatus.Maintenance,
                        "Offline" => PostStatus.Offline,
                        _ => PostStatus.Active
                    },
                    Capacity = post.StockCapacity,
                    CurrentLoad = post.CurrentLoad,
                    ProductionLineId = _lineIdMap.TryGetValue(post.LineId, out var numeric) ? numeric : 0,
                    SequenceOrder = post.Position
                };
                _allPosts.Add(mapped);
            }

            _plannedTasks = _allPosts.Take(2).Select((p, idx) => new PlannedMaintenanceTask
            {
                Id = (idx + 1).ToString(),
                PostId = (int)p.Id,
                PostCode = p.PostCode,
                Title = "Preventive check",
                ScheduledStartDate = DateTime.Now.AddDays(idx + 1),
                ScheduledEndDate = DateTime.Now.AddDays(idx + 1).AddHours(1),
                MaintenanceType = "Preventive",
                Priority = idx == 0 ? "High" : "Normal",
                Status = "Scheduled",
                EstimatedDurationMinutes = 60
            }).ToList();

            _schedules.Clear();
            foreach (var task in _plannedTasks)
            {
                _schedules.Add(new MaintenanceSchedule
                {
                    ProductionPostId = task.PostId,
                    Title = task.Title,
                    Status = MaintenanceStatusEnum.Scheduled,
                    ScheduledDate = task.ScheduledStartDate,
                    EstimatedDurationMinutes = task.EstimatedDurationMinutes
                });
            }

            foreach (var post in _allPosts)
            {
                _healthScoreService.AddMaintenanceRecord(new MaintenanceSchedule
                {
                    ProductionPostId = post.Id,
                    CompletedDate = DateTime.Now.AddDays(-10)
                });
            }

            RefreshData();
        }

        private void InitializeTimers()
        {
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(5); // Refresh every 5 seconds
            _refreshTimer.Tick += (s, e) => RefreshData();
            _refreshTimer.Start();
        }

        private void LineSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isReady) return;

            if (LineSelector.SelectedItem is ComboBoxItem item)
            {
                var tag = item.Tag?.ToString();
                _selectedLineKey = tag == "ALL" ? null : tag;
                _selectedLineNumeric = _selectedLineKey != null && _lineIdMap.TryGetValue(_selectedLineKey, out var numeric) ? numeric : 0;
                UpdateInjectPostSelector();
                RefreshData();
            }
        }

        private void RefreshData()
        {
            _refreshCount++;

            // Génère des lectures de senseurs simulées
            GenerateSimulatedSensorData();
            
            GenerateAlerts();
            RenderPostsPanel();
            UpdateAlertsList();
            UpdateKPIs();
            UpdatePlannedMaintenanceGrid();
            UpdateIncidentsList();
        }

        /// <summary>
        /// Génère des données de senseurs simulées pour tester les alertes
        /// </summary>
        private void GenerateSimulatedSensorData()
        {
            _sensorReadings.Clear();

            var lineId = _selectedLineNumeric;
            var relevantPosts = lineId == 0
                ? _allPosts
                : _allPosts.Where(p => p.ProductionLineId == lineId).ToList();

            foreach (var post in relevantPosts)
            {
                // Génère les readings normales pour tous les senseurs du poste
                var readings = _sensorSimulationService.GeneratePostSensorReadings(
                    (int)post.Id,
                    post.PostCode
                );
                _sensorReadings.AddRange(readings);

                // Injection d'anomalies aléatoires (15% de chance tous les 10 appels)
                if (_refreshCount % 10 == 0 && _anomalyRandom.NextDouble() < 0.15)
                {
                    var sensorTypes = new[]
                    {
                        SensorType.MotorTemperature,
                        SensorType.Vibration,
                        SensorType.OilLevel,
                        SensorType.PowerConsumption
                    };

                    var anomalyTypes = new[] { "overheat", "vibration_spike", "low_level", "pressure_drop" };

                    var randomSensor = sensorTypes[_anomalyRandom.Next(sensorTypes.Length)];
                    var randomAnomaly = anomalyTypes[_anomalyRandom.Next(anomalyTypes.Length)];

                    var anomaly = _sensorSimulationService.GenerateAnomalySensorReading(
                        (int)post.Id,
                        post.PostCode,
                        randomSensor,
                        randomAnomaly
                    );
                    
                    _sensorReadings.Add(anomaly);
                    
                    // Log de l'anomalie pour déboguer
                    System.Diagnostics.Debug.WriteLine(
                        $"🔴 ANOMALY: {post.PostName} - {anomaly.Status} at {DateTime.Now:HH:mm:ss}"
                    );
                }
            }
        }

        private void GenerateAlerts()
        {
            _currentAlerts.Clear();

            var lineId = _selectedLineNumeric;
            var relevantPosts = lineId == 0 
                ? _allPosts 
                : _allPosts.Where(p => p.ProductionLineId == lineId).ToList();

            foreach (var post in relevantPosts)
            {
                var latestMaintenance = _schedules
                    .Where(s => s.ProductionPostId == post.Id)
                    .OrderByDescending(s => s.ScheduledDate)
                    .FirstOrDefault();

                var postAlerts = _alertManager.GenerateAlertsForPost(post, latestMaintenance, _sensorReadings);
                _currentAlerts.AddRange(postAlerts);
            }

            // Keep injected alerts visible until operator resolves them.
            foreach (var injected in _injectedAlerts)
            {
                if (string.Equals(injected.Status, "Fixed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (_selectedLineNumeric != 0)
                {
                    var post = _allPosts.FirstOrDefault(p => p.PostCode == injected.PostCode);
                    if (post == null || post.ProductionLineId != _selectedLineNumeric)
                    {
                        continue;
                    }
                }

                _currentAlerts.Add(injected);
            }
        }

        
        private void RenderPostsPanel()
        {
            PostsPanel.Children.Clear();

            var relevantPosts = _selectedLineNumeric == 0
                ? _allPosts
                : _allPosts.Where(p => p.ProductionLineId == _selectedLineNumeric).ToList();

            foreach (var post in relevantPosts.OrderBy(p => p.SequenceOrder))
            {
                double healthScore = _healthScoreService.CalculateHealthScore(post);
                string healthColor = _healthScoreService.GetHealthStatusColor(healthScore);
                var (icon, status, description) = _healthScoreService.GetHealthStatus(healthScore);

                var sourcePost = _productionDataService.GetPost(post.PostCode);

                var card = new Border
                {
                    Width = 210,
                    Height = 110,
                    Margin = new Thickness(6),
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(Color.FromRgb(26, 35, 58)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(37, 50, 74)),
                    BorderThickness = new Thickness(1)
                };

                var stack = new StackPanel { Margin = new Thickness(10, 8, 10, 8) };
                stack.Children.Add(new TextBlock { Text = post.PostCode, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold });
                stack.Children.Add(new TextBlock { Text = post.PostName, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), FontSize = 12 });
                stack.Children.Add(new TextBlock { Text = $"Status: {status}", Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), FontSize = 12, Margin = new Thickness(0,4,0,0) });
                stack.Children.Add(new TextBlock { Text = $"Stock {post.CurrentLoad}/{post.Capacity}", Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250)), FontSize = 12 });
                stack.Children.Add(new TextBlock { Text = $"Health {healthScore:F0}%", Foreground = new SolidColorBrush(Color.FromRgb(234, 179, 8)), FontSize = 12 });
                card.Child = stack;

                PostsPanel.Children.Add(card);
            }

            LineSummaryText.Text = _selectedLineNumeric == 0
                ? $"{_allPosts.Count} posts"
                : $"{relevantPosts.Count} posts on {_selectedLineKey}";
        }

        private void UpdateAlertsList()
        {
            AlertsList.Items.Clear();
            var alerts = _currentAlerts
                .OrderByDescending(a => a.CreatedAt)
                .Take(10)
                .ToList();

            foreach (var alert in alerts)
            {
                var line = $"{alert.Icon} {alert.PostCode} | {alert.Title} | {alert.Severity} | {alert.Status}";
                AlertsList.Items.Add(new ListBoxItem { Content = line, Tag = alert });
            }

            var counts = _alertManager.GetAlertCounts(_currentAlerts);
            CriticalAlertsText.Text = counts.TotalCount.ToString();
        }

        private void UpdateKPIs()
        {
            var relevantPosts = _selectedLineNumeric == 0
                ? _allPosts
                : _allPosts.Where(p => p.ProductionLineId == _selectedLineNumeric).ToList();

            ActivePostsText.Text = relevantPosts.Count(p => p.Status == PostStatus.Active).ToString();
            CriticalAlertsText.Text = _currentAlerts.Count(a => a.Severity == "Critical").ToString();
        }

        private void UpdatePlannedMaintenanceGrid()
        {
            var lineId = _selectedLineNumeric;
            var relevantTasks = lineId == 0
                ? _plannedTasks
                : _plannedTasks.Where(t => _allPosts.FirstOrDefault(p => p.Id == t.PostId)?.ProductionLineId == lineId).ToList();

            PlannedTasksList.Items.Clear();
            foreach (var task in relevantTasks.OrderBy(t => t.ScheduledStartDate))
            {
                PlannedTasksList.Items.Add($"{task.PostCode} - {task.Title} ({task.ScheduledStartDate:dd/MM HH:mm})");
            }
        }

        private void UpdateIncidentsList()
        {
            IncidentsList.Items.Clear();
            foreach (var alert in _currentAlerts.OrderByDescending(a => a.CreatedAt).Take(6))
            {
                IncidentsList.Items.Add($"{alert.Icon} {alert.PostCode} - {alert.Title}");
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshData();
        }

        private void InjectPanneButton_Click(object sender, RoutedEventArgs e)
        {
            var targetPostCode = GetComboTagAsString(InjectPostSelector);
            var target = targetPostCode != null ? _allPosts.FirstOrDefault(p => p.PostCode == targetPostCode) : _allPosts.FirstOrDefault();
            if (target == null) return;

            var troubleType = GetComboTagAsString(InjectTroubleTypeSelector) ?? "injected_panne";

            var injected = new MaintenanceAlert
            {
                AlertId = Guid.NewGuid().ToString(),
                PostCode = target.PostCode,
                PostName = target.PostName,
                AlertType = troubleType,
                Severity = "Critical",
                Icon = "⚠",
                Title = $"Panne simulée ({troubleType})",
                Description = "Incident injecté pour test maintenance",
                CreatedAt = DateTime.Now,
                DueDate = DateTime.Now.AddHours(1),
                Status = "Active",
                RequiredAction = "Envoyer robot de maintenance",
                EstimatedDuration = 45
            };

            _injectedAlerts.Add(injected);
            EmitMaintenanceSignal("trouble-detected", injected);

            UpdateAlertsList();
            UpdateIncidentsList();
        }

        private MaintenanceAlert? GetSelectedAlert()
        {
            return (AlertsList.SelectedItem as ListBoxItem)?.Tag as MaintenanceAlert;
        }

        private void SetSelectedAlertStatus(string status)
        {
            var alert = GetSelectedAlert();
            if (alert == null) return;

            alert.Status = status;

            if (string.Equals(status, "InProgress", StringComparison.OrdinalIgnoreCase) && alert.AcknowledgedAt == null)
            {
                alert.AcknowledgedAt = DateTime.Now;
            }

            if (string.Equals(status, "Fixed", StringComparison.OrdinalIgnoreCase))
            {
                alert.ResolvedAt = DateTime.Now;
            }

            EmitMaintenanceSignal("operator-status", alert);

            UpdateAlertsList();
            UpdateIncidentsList();
        }

        private void MarkInProgressButton_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedAlertStatus("InProgress");
        }

        private void MarkFixedButton_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedAlertStatus("Fixed");

            var alert = GetSelectedAlert();
            if (alert == null) return;

            var confirm = MessageBox.Show(
                "Générer un rapport d'intervention et l'envoyer au manager/leader via la messagerie interne ?",
                "Rapport d'intervention",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                _ = SendMaintenanceInterventionReportAsync(alert);
            }
        }

        private void MarkCouldNotFixButton_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedAlertStatus("CouldNotFix");
        }

        private async Task SendMaintenanceInterventionReportAsync(MaintenanceAlert alert)
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
                candidates = workers.OrderBy(w => w.Department).ThenBy(w => w.Name).ToList();

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

            var operatorName = _permissionService?.CurrentUserId ?? "Unknown";
            var started = alert.AcknowledgedAt;
            var completed = alert.ResolvedAt ?? DateTime.Now;
            TimeSpan? duration = started.HasValue ? (completed - started.Value) : null;

            var reportFile = CreateMaintenanceReportFile(alert, operatorName, started, completed, duration);
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

                var subject = $"Maintenance Intervention Report - {alert.PostCode} - {alert.Title}";
                var body = BuildMaintenanceBody(alert, operatorName, started, completed, duration);

                var sendRequest = new SendInternalEmailRequest
                {
                    RecipientIds = recipients,
                    Subject = subject,
                    Body = body,
                    Attachments = new List<InternalEmailAttachmentDto> { uploadResp.Data }
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

        private static string CreateMaintenanceReportFile(
            MaintenanceAlert alert,
            string operatorName,
            DateTime? startedAtLocal,
            DateTime completedAtLocal,
            TimeSpan? duration)
        {
            var safePost = string.Concat((alert.PostCode ?? "POST").Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
            var fileName = $"MaintenanceReport_{safePost}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), fileName);
            var body = BuildMaintenanceBody(alert, operatorName, startedAtLocal, completedAtLocal, duration);
            System.IO.File.WriteAllText(path, body);
            return path;
        }

        private static string BuildMaintenanceBody(
            MaintenanceAlert alert,
            string operatorName,
            DateTime? startedAtLocal,
            DateTime completedAtLocal,
            TimeSpan? duration)
        {
            var startedText = startedAtLocal.HasValue ? startedAtLocal.Value.ToString("yyyy-MM-dd HH:mm") : "N/A";
            var completedText = completedAtLocal.ToString("yyyy-MM-dd HH:mm");
            var durationText = duration.HasValue ? $"{(int)duration.Value.TotalMinutes} min" : (alert.EstimatedDuration > 0 ? $"~{alert.EstimatedDuration} min" : "N/A");

            return
                "Maintenance Intervention Report\n" +
                "==============================\n" +
                $"Post: {alert.PostCode} ({alert.PostName})\n" +
                $"Type: {alert.AlertType}\n" +
                $"Severity: {alert.Severity}\n" +
                $"Title: {alert.Title}\n" +
                $"Status: {alert.Status}\n" +
                $"Operator: {operatorName}\n" +
                $"Detected: {alert.CreatedAt:yyyy-MM-dd HH:mm}\n" +
                $"Started: {startedText}\n" +
                $"Completed: {completedText}\n" +
                $"Duration: {durationText}\n" +
                (string.IsNullOrWhiteSpace(alert.RequiredAction) ? string.Empty : $"\nRequired action:\n{alert.RequiredAction}\n") +
                (string.IsNullOrWhiteSpace(alert.Description) ? string.Empty : $"\nDescription:\n{alert.Description}\n");
        }

        private void HookRobotFeed()
        {
            if (System.Windows.Application.Current?.MainWindow is MainWindow main && main.GetIotProvider() is IIotProvider provider)
            {
                provider.RobotStateChanged += (s, e) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        RobotStatusText.Text = e.State.DisplayText;
                        AppendRobotLog(e.State.DisplayText);
                    });
                };

                provider.LogEventAdded += (s, e) =>
                {
                    Dispatcher.Invoke(() => AppendRobotLog(e.LogEvent.DisplayText));
                };
            }
        }

        private void AppendRobotLog(string message)
        {
            var line = $"{DateTime.Now:HH:mm:ss} - {message}";
            RobotLogList.Items.Insert(0, line);
            while (RobotLogList.Items.Count > 20)
            {
                RobotLogList.Items.RemoveAt(RobotLogList.Items.Count - 1);
            }
        }

        private void AddMaintenance_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Add Maintenance dialog would open here", "Feature Preview");
        }

        private void ViewCalendar_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Calendar view would open here", "Feature Preview");
        }

        private void RefreshData_Click(object sender, RoutedEventArgs e)
        {
            RefreshData();
            MessageBox.Show("Data refreshed!", "Success");
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _refreshTimer?.Stop();
            
            // Disconnect WebSocket if connected
            if (_webSocketClient != null)
            {
                Task.Run(async () => await _webSocketClient.DisconnectAsync());
                Task.Run(async () => await _webSocketClient.DisposeAsync());
            }
        }

        #region WebSocket Event Handlers

        /// <summary>
        /// Handle snapshot update from WebSocket
        /// </summary>
        private async Task HandleSnapshotUpdate(dynamic snapshot)
        {
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    // Update UI with snapshot data
                    System.Diagnostics.Debug.WriteLine("📊 Snapshot received from WebSocket");
                    
                    // Extract data from snapshot
                    if (snapshot.snapshot != null)
                    {
                        // Update alerts
                        if (snapshot.snapshot.alerts != null)
                        {
                            _currentAlerts.Clear();
                            foreach (var alert in snapshot.snapshot.alerts)
                            {
                                _currentAlerts.Add(new MaintenanceAlert
                                {
                                    Title = alert.title,
                                    Severity = alert.severity,
                                    CreatedAt = DateTime.UtcNow
                                });
                            }
                        }

                        // Refresh UI
                        UpdateAlertsList();
                        RenderPostsPanel();
                        UpdateKPIs();
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error handling snapshot: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle dashboard update from WebSocket (optimized with change detection)
        /// </summary>
        private async Task HandleDashboardUpdate(dynamic update)
        {
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    System.Diagnostics.Debug.WriteLine("🔄 Dashboard update received from WebSocket");
                    
                    if (update.update?.changes?.hasAnyChanges == true)
                    {
                        // Only update what changed
                        var changes = update.update.changes;
                        
                        if (changes.newAlerts != null && changes.newAlerts.Count > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"  → {changes.newAlerts.Count} new alerts");
                        }
                        
                        if (changes.healthScoreChanges != null && changes.healthScoreChanges.Count > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"  → Health scores changed");
                        }
                        
                        if (changes.productionChanges != null && changes.productionChanges.Count > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"  → Production data updated");
                        }

                        // Refresh visualization
                        RenderPostsPanel();
                        UpdateAlertsList();
                        UpdateKPIs();
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error handling dashboard update: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle new alert from WebSocket
        /// </summary>
        private async Task HandleNewAlert(dynamic alert)
        {
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"🚨 New alert: {alert.alert?.title}");
                    
                    var newAlert = new MaintenanceAlert
                    {
                        Title = alert.alert?.title ?? "Unknown Alert",
                        Severity = alert.alert?.severity ?? "Medium",
                        CreatedAt = DateTime.UtcNow
                    };

                    _currentAlerts.Add(newAlert);
                    UpdateAlertsList();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error handling new alert: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle new incident from WebSocket
        /// </summary>
        private async Task HandleNewIncident(dynamic incident)
        {
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ New incident: {incident.incident?.type}");
                    
                    // Add incident as alert
                    var alert = new MaintenanceAlert
                    {
                        Title = $"Incident: {incident.incident?.type ?? "Equipment Issue"}",
                        Severity = incident.incident?.severity ?? "High",
                        CreatedAt = DateTime.UtcNow
                    };

                    _currentAlerts.Add(alert);
                    UpdateAlertsList();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error handling incident: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle WebSocket connection state changes
        /// </summary>
        private async Task HandleConnectionStateChanged(dynamic? data)
        {
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    var state = _webSocketClient?.ConnectionState ?? Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Disconnected;
                    var stateText = state switch
                    {
                        Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected => "✓ Connected",
                        Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connecting => "⟳ Connecting...",
                        Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Disconnected => "✗ Disconnected",
                        Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Reconnecting => "⟳ Reconnecting...",
                        _ => "? Unknown"
                    };

                    System.Diagnostics.Debug.WriteLine($"WebSocket state: {stateText}");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error handling connection state: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle WebSocket errors
        /// </summary>
        private async Task HandleWebSocketError(string error)
        {
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"🔴 WebSocket error: {error}");
                    MessageBox.Show($"WebSocket Error: {error}", "Connection Error");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error handling WebSocket error: {ex.Message}");
            }
        }

        /// <summary>
        /// Connect to WebSocket hub
        /// </summary>
        public async Task ConnectToWebSocketAsync()
        {
            try
            {
                if (_webSocketClient == null)
                {
                    InitializeWebSocketClient();
                }

                if (_webSocketClient != null)
                {
                    await _webSocketClient.ConnectAsync();
                    
                    // Subscribe to the selected line
                    var lineId = _selectedLineNumeric == 0 ? 1 : _selectedLineNumeric;
                    var postIds = _allPosts
                        .Where(p => p.ProductionLineId == lineId)
                        .Select(p => (int)p.Id)
                        .ToList();

                    await _webSocketClient.SubscribeToLineAsync(lineId, postIds);
                    System.Diagnostics.Debug.WriteLine($"✓ Connected to WebSocket and subscribed to Line {lineId}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error connecting to WebSocket: {ex.Message}");
                MessageBox.Show($"Failed to connect to WebSocket: {ex.Message}", "Connection Error");
            }
        }

        #endregion
    }
}
