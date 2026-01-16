using System.Windows;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using DigitalisationERP.Desktop.Models;
using DigitalisationERP.Desktop.Services;
using DigitalisationERP.Desktop.Services.IoT;
using DigitalisationERP.Desktop.Views;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace DigitalisationERP.Desktop;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ApiClient _apiClient;
    private RolePermissionService? _permissionService;
    private IotSimulationService? _iotSimulation;
    private IIotProvider? _iotProvider;
    private ConfigService? _configService;
    private LocalLogService? _logService;
    private BluetoothSerialClient? _bluetoothSerialClient;
    private object? _dashboardRootContent;

    // Chart Data Properties
    public ISeries[] ProductionSeries { get; set; } = null!;
    public Axis[] ProductionXAxes { get; set; } = null!;
    public Axis[] ProductionYAxes { get; set; } = null!;
    
    public ISeries[] InventorySeries { get; set; } = null!;
    
    public ISeries[] RevenueSeries { get; set; } = null!;
    public Axis[] RevenueXAxes { get; set; } = null!;
    public Axis[] RevenueYAxes { get; set; } = null!;

    // Constructeur sans param�tres pour le flow simple (SplashScreen -> LoginWindow)
    public MainWindow() : this(new ApiClient())
    {
    }

    public MainWindow(ApiClient apiClient)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== MainWindow Constructor Started ===");
            
            _apiClient = apiClient;
            System.Diagnostics.Debug.WriteLine("ApiClient assigned");

            System.Diagnostics.Debug.WriteLine("Calling InitializeComponent...");
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("InitializeComponent completed");

            _dashboardRootContent = ContentArea.Content;

            System.Diagnostics.Debug.WriteLine("Calling LoadUserInfo...");
            LoadUserInfo();
            System.Diagnostics.Debug.WriteLine("LoadUserInfo completed");
            
            System.Diagnostics.Debug.WriteLine("Calling InitializeCharts...");
            InitializeCharts();
            System.Diagnostics.Debug.WriteLine("InitializeCharts completed");
            
            System.Diagnostics.Debug.WriteLine("Setting DataContext...");
            DataContext = this;
            System.Diagnostics.Debug.WriteLine("DataContext set");
            
            System.Diagnostics.Debug.WriteLine("Calling SetActivePage...");
            SetActivePage("Dashboard");
            System.Diagnostics.Debug.WriteLine("SetActivePage completed");
            
            // Initialiser la simulation IoT
            System.Diagnostics.Debug.WriteLine("Initializing IoT Simulation...");
            InitializeIotSimulation();
            System.Diagnostics.Debug.WriteLine("IoT Simulation initialized");
            
            System.Diagnostics.Debug.WriteLine("=== MainWindow Constructor Finished Successfully ===");
        }
        catch (Exception ex)
        {
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop), 
                "erp_crash_log.txt");
            
            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] CRASH in MainWindow Constructor\n" +
                           $"Error: {ex.Message}\n" +
                           $"Stack Trace:\n{ex.StackTrace}\n" +
                           $"Inner Exception: {ex.InnerException?.Message}\n\n";
            
            System.IO.File.AppendAllText(logPath, logMessage);
            System.Diagnostics.Debug.WriteLine($"CRASH: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
            
            MessageBox.Show($"Error loading dashboard:\n{ex.Message}\n\nLog saved to: {logPath}", 
                          "Application Error", 
                          MessageBoxButton.OK, 
                          MessageBoxImage.Error);
            throw;
        }
    }

    /// <summary>
    /// Initialise le service de simulation IoT avec HAL (Hardware Abstraction Layer)
    /// </summary>
    private async void InitializeIotSimulation()
    {
        // Initialiser les services de config et logging
        _configService = new ConfigService();
        _logService = new LocalLogService();
        
        _iotSimulation = new IotSimulationService();

        // Provider selection:
        // Keep simulation running (UI + Post 2 simulated), and overlay Bluetooth serial triggers when enabled.
        var simulationProvider = new SimulationIotProvider(_iotSimulation);
        IIotProvider providerToUse = simulationProvider;

        try
        {
            var cfg = await _configService.GetConfigurationAsync();
            var providerType = (cfg.Provider ?? "simulation").Trim().ToLowerInvariant();

            if (cfg.Bluetooth?.Enabled == true && (providerType == "hybrid" || providerType == "bluetooth" || providerType == "simulation"))
            {
                _bluetoothSerialClient = new BluetoothSerialClient(cfg.Bluetooth.ComPort, cfg.Bluetooth.BaudRate, cfg.Bluetooth.NewLine);
                var bluetoothProvider = new BluetoothSerialIotProvider(_bluetoothSerialClient, ownsClient: false);
                providerToUse = new HybridIotProvider(simulationProvider, bluetoothProvider);
            }
        }
        catch
        {
            providerToUse = simulationProvider;
        }

        _iotProvider = providerToUse;

        var provider = _iotProvider;
        if (provider == null)
        {
            return;
        }
        
        // Abonner aux événements du provider
        provider.LogEventAdded += async (sender, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[IOT] {e.LogEvent.DisplayText}");
            
            // Enregistrer dans les logs locaux
            if (_logService != null)
            {
                await _logService.LogEventAsync(e.LogEvent.Level, e.LogEvent.Source, "IOT_EVENT", e.LogEvent.Message);
            }
        };

        provider.CriticalAlertRaised += async (sender, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[ALERT] {e.SensorId}: {e.Message}");
            
            // Enregistrer l'alerte critique
            if (_logService != null)
            {
                await _logService.LogCriticalAlertAsync(e.SensorId, e.Message, e.Value, e.Threshold, "Unknown");
            }
            
            // Si alerte critique, envoyer robot de maintenance automatiquement
            var robots = await provider.GetAllRobotsAsync();
            var maintenanceRobot = robots.FirstOrDefault(r => r.Type == RobotType.Maintenance)
                                 ?? robots.FirstOrDefault(r => r.RobotId != null && r.RobotId.Contains("MAINT", StringComparison.OrdinalIgnoreCase));
            if (maintenanceRobot != null)
            {
                // In simulation, the message is formatted like "POST-03: ...".
                // Prefer the post code as target location; fall back to sensor id.
                var targetLocation = e.SensorId;
                var message = e.Message;
                if (!string.IsNullOrEmpty(message))
                {
                    var colonIndex = message.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        var candidate = message.Substring(0, colonIndex).Trim();
                        if (!string.IsNullOrWhiteSpace(candidate))
                        {
                            targetLocation = candidate;
                        }
                    }
                }

                await provider.SendRobotCommandAsync(maintenanceRobot.RobotId, RobotCommand.GoToLocation, targetLocation);
            }
        };

        provider.RobotStateChanged += (sender, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[ROBOT] {e.State.DisplayText}");
        };

        // Démarrer la connexion IoT
        var connected = await provider.ConnectAsync();
        if (connected)
        {
            System.Diagnostics.Debug.WriteLine($"[IOT] Provider connecté: {provider.ProviderName}");
        }
    }

    /// <summary>
    /// Obtient le service de simulation IoT (legacy pour compatibilité)
    /// </summary>
    public IotSimulationService? GetIotSimulation() => _iotSimulation;
    
    /// <summary>
    /// Obtient le provider IoT abstrait (HAL)
    /// </summary>
    public IIotProvider? GetIotProvider() => _iotProvider;

    /// <summary>
    /// Optional shared Bluetooth serial client (used for both inbound triggers and outbound AGV messages).
    /// </summary>
    public BluetoothSerialClient? GetBluetoothSerialClient() => _bluetoothSerialClient;

    /// <summary>
    /// Ensures the shared Bluetooth serial client is created, opened and listening.
    /// This is used by both IoT pages and Production/Kanban (OF start/end messaging).
    /// </summary>
    public async Task<(bool Success, string? Port, string? Error)> EnsureBluetoothSerialConnectedAsync(string? portOverride = null)
    {
        try
        {
            _configService ??= new ConfigService();

            var cfg = await _configService.GetConfigurationAsync();
            var bt = cfg?.Bluetooth;

            var port = !string.IsNullOrWhiteSpace(portOverride)
                ? portOverride.Trim()
                : (bt?.ComPort ?? "COM3");
            var baud = bt?.BaudRate ?? 9600;
            var newLine = bt?.NewLine ?? "\n";

            // Recreate client if settings changed.
            if (_bluetoothSerialClient == null
                || !string.Equals(_bluetoothSerialClient.PortName, port, StringComparison.OrdinalIgnoreCase)
                || _bluetoothSerialClient.BaudRate != baud
                || !string.Equals(_bluetoothSerialClient.NewLine, newLine, StringComparison.Ordinal))
            {
                try { _bluetoothSerialClient?.Dispose(); } catch { /* ignore */ }
                _bluetoothSerialClient = new BluetoothSerialClient(port, baud, newLine);
            }

            await _bluetoothSerialClient.EnsureOpenAsync();
            await _bluetoothSerialClient.StartListeningAsync();

            return (true, _bluetoothSerialClient.PortName, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }
    
    /// <summary>
    /// Obtient le service de configuration IoT
    /// </summary>
    public ConfigService? GetConfigService() => _configService;
    
    /// <summary>
    /// Obtient le service de logging local
    /// </summary>
    public LocalLogService? GetLogService() => _logService;

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        try { (_iotProvider as IDisposable)?.Dispose(); } catch { /* ignore */ }
        try { _iotSimulation?.Dispose(); } catch { /* ignore */ }
        try { _bluetoothSerialClient?.Dispose(); } catch { /* ignore */ }
    }

    private void InitializeCharts()
    {
        // Production Trend Chart (Line Chart)
        ProductionSeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Name = "Completed Orders",
                Values = new double[] { 18, 22, 19, 25, 28, 24, 27 },
                Fill = null,
                Stroke = new SolidColorPaint(SKColor.Parse("#3498DB")) { StrokeThickness = 3 },
                GeometryFill = new SolidColorPaint(SKColor.Parse("#3498DB")),
                GeometryStroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 },
                GeometrySize = 10
            }
        };

        ProductionXAxes = new Axis[]
        {
            new Axis
            {
                Labels = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" },
                LabelsRotation = 0,
                TextSize = 12,
                SeparatorsPaint = new SolidColorPaint(SKColors.LightGray) { StrokeThickness = 1 }
            }
        };

        ProductionYAxes = new Axis[]
        {
            new Axis
            {
                Name = "Orders",
                NameTextSize = 14,
                TextSize = 12,
                SeparatorsPaint = new SolidColorPaint(SKColors.LightGray) { StrokeThickness = 1 }
            }
        };

        // Inventory Distribution Chart (Pie Chart)
        InventorySeries = new ISeries[]
        {
            new PieSeries<double>
            {
                Name = "In Stock",
                Values = new[] { 65.0 },
                Fill = new SolidColorPaint(SKColor.Parse("#27AE60")),
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsSize = 14,
                DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle
            },
            new PieSeries<double>
            {
                Name = "Low Stock",
                Values = new[] { 20.0 },
                Fill = new SolidColorPaint(SKColor.Parse("#E67E22")),
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsSize = 14,
                DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle
            },
            new PieSeries<double>
            {
                Name = "Out of Stock",
                Values = new[] { 15.0 },
                Fill = new SolidColorPaint(SKColor.Parse("#E74C3C")),
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsSize = 14,
                DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle
            }
        };

        // Revenue Analytics Chart (Column Chart)
        RevenueSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Name = "Revenue",
                Values = new double[] { 85000, 92000, 88000, 95000, 110000, 105000, 115000, 122000, 118000, 125000, 132000, 140000 },
                Fill = new SolidColorPaint(SKColor.Parse("#27AE60")),
                Stroke = null,
                MaxBarWidth = 40
            }
        };

        RevenueXAxes = new Axis[]
        {
            new Axis
            {
                Labels = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" },
                LabelsRotation = -45,
                TextSize = 11,
                SeparatorsPaint = new SolidColorPaint(SKColors.LightGray) { StrokeThickness = 1 }
            }
        };

        RevenueYAxes = new Axis[]
        {
            new Axis
            {
                Name = "Revenue ($)",
                NameTextSize = 14,
                TextSize = 12,
                Labeler = value => $"${value / 1000}K",
                SeparatorsPaint = new SolidColorPaint(SKColors.LightGray) { StrokeThickness = 1 }
            }
        };
    }

    private void LoadUserInfo()
    {
        // TODO: Load user info from ApiClient when available
        UserNameText.Text = "Development User";
        UserRoleText.Text = "Developer";
        WelcomeText.Text = "Welcome to DigitalisationERP!";
    }

    private void NavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string page)
        {
            NavigateToPage(page);
        }
    }

    public void NavigateToPage(string pageName)
    {
        if (pageName == "Reports")
        {
            _ = ExportReportAsync();
            return;
        }

        // Vérifier les permissions avant de naviguer
        if (_permissionService != null && !_permissionService.CanAccessPage(pageName))
        {
            MessageBox.Show("Vous n'avez pas accès à cette page", "Accès Restreint");
            return;
        }

        SetActivePage(pageName);
        LoadPageContent(pageName);
    }

    public void NavigateToProductionLine(string lineId)
    {
        NavigateToPage("Production");

        // Ensure the page has been created/navigated before selecting.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (PageFrame.Content is ProductionPage productionPage)
            {
                productionPage.FocusLine(lineId);
            }
        }), DispatcherPriority.Loaded);
    }

    private void DashboardQuickAction_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string tag)
        {
            if (tag == "QuickAction_ExportReport")
            {
                _ = ExportReportAsync();
                return;
            }

            var page = tag switch
            {
                "QuickAction_NewOrder" => "Production",
                "QuickAction_CheckStock" => "Inventory",
                "QuickAction_ManageUsers" => "UsersManagement",
                _ => "Dashboard"
            };

            NavigateToPage(page);
        }
    }

    private void SetActivePage(string pageName)
    {
        PageTitleText.Text = pageName switch
        {
            "IotConsole" => "IoT",
            _ => pageName
        };
        
        // Update button styles to highlight active page
        foreach (var child in ((StackPanel)((ScrollViewer)((Grid)((Border)((Grid)this.Content).Children[0]).Child).Children[1]).Content).Children)
        {
            if (child is Button btn)
            {
                if (btn.Tag?.ToString() == pageName)
                {
                    btn.Opacity = 1.0;
                    btn.FontWeight = FontWeights.Bold;
                }
                else
                {
                    btn.Opacity = 0.7;
                    btn.FontWeight = FontWeights.Normal;
                }
            }
        }
    }

    private void LoadPageContent(string pageName)
    {
        // Masquer le contenu par défaut
        if (FindName("DashboardContent") is FrameworkElement dashboard)
        {
            dashboard.Visibility = Visibility.Collapsed;
        }

        // Default: show frame for non-dashboard content
        if (pageName != "Dashboard")
        {
            PageFrame.Visibility = Visibility.Visible;
            ContentArea.Visibility = Visibility.Collapsed;
        }
        
        switch (pageName)
        {
            case "Dashboard":
                PageFrame.Visibility = Visibility.Collapsed;
                ContentArea.Visibility = Visibility.Visible;
                if (_dashboardRootContent != null)
                {
                    ContentArea.Content = _dashboardRootContent;
                }
                if (FindName("DashboardContent") is FrameworkElement dashboardContent)
                {
                    dashboardContent.Visibility = Visibility.Visible;
                }
                break;
            case "Production":
                PageFrame.Navigate(new ProductionPage(new LoginResponse { AccessToken = _apiClient.AuthToken }));
                break;
            case "Maintenance":
                if (_permissionService != null && _permissionService.CurrentRole is RolePermissionService.UserRole.S_USER
                    or RolePermissionService.UserRole.Z_MAINT_MANAGER
                    or RolePermissionService.UserRole.Z_MAINT_PLANNER
                    or RolePermissionService.UserRole.Z_MAINT_TECH)
                {
                    PageFrame.Navigate(new MaintenancePageAdvanced(_permissionService, _apiClient));
                }
                else
                {
                    PageFrame.Navigate(new MaintenanceGuestPage(_permissionService, _apiClient));
                }
                break;
            case "IotConsole":
                PageFrame.Navigate(new IoTTestPage());
                break;
            case "Inventory":
                PageFrame.Navigate(new InventoryPage(new LoginResponse { AccessToken = _apiClient.AuthToken }));
                break;
            case "UsersManagement":
                PageFrame.Navigate(new UsersManagementPage(_permissionService, _apiClient));
                break;
            case "MySchedule":
                PageFrame.Navigate(new MySchedulePage(_permissionService, _apiClient));
                break;
            case "MyTasks":
                PageFrame.Navigate(new MyTasksPage(_permissionService, _apiClient));
                break;
            case "ShiftPlanner":
                PageFrame.Navigate(new ShiftPlannerPage(_permissionService, _apiClient));
                break;
            case "TaskBoard":
                PageFrame.Navigate(new TaskBoardPage(_permissionService, _apiClient));
                break;
            case "Feedback":
                PageFrame.Navigate(new FeedbackInboxPage(_permissionService, _apiClient));
                break;
            case "Email":
                {
                    var emailPage = new EmailPage(new LoginResponse { AccessToken = _apiClient.AuthToken });
                    PageFrame.Navigate(emailPage);
                    break;
                }
            case "Reports":
                _ = ExportReportAsync();
                break;
            case "Configuration":
                PageFrame.Navigate(new ConfigurationPage());
                break;
            case "Meetings":
                if (_permissionService == null)
                {
                    PageFrame.Navigate(CreatePlaceholderContent(pageName, "Permissions not loaded."));
                    break;
                }

                PageFrame.Navigate(new MeetingsPage(_permissionService, _apiClient));
                break;
            case "Security":
                PageFrame.Navigate(new SecurityPage());
                break;
            case "Synchronization":
                PageFrame.Navigate(new SynchronizationPage());
                break;
            case "Analytics":
                PageFrame.Navigate(new AnalyticsPage());
                break;
            case "Notifications":
                PageFrame.Navigate(new NotificationsPage());
                break;
            case "Backup":
                PageFrame.Navigate(new BackupPage());
                break;
            case "Updates":
                PageFrame.Navigate(new UpdatesPage());
                break;
            case "Support":
                PageFrame.Navigate(new SupportPage());
                break;
            default:
                PageFrame.Navigate(CreatePlaceholderContent(pageName, "This area is not implemented yet."));
                break;
        }
    }

    public async Task ExportReportAsync()
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Title = "Exporter un rapport",
                FileName = $"DigitalisationERP_Report_{DateTime.Now:yyyyMMdd_HHmm}.txt",
                Filter = "Text report (*.txt)|*.txt|JSON report (*.json)|*.json",
                DefaultExt = ".txt",
                AddExtension = true
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var reportService = new ReportService();
            await reportService.ExportAsync(dialog.FileName);

            MessageBox.Show(
                $"Rapport exporté avec succès.\n\nFichier : {dialog.FileName}",
                "Export réussi",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Erreur lors de l'export du rapport :\n{ex.Message}",
                "Erreur",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private FrameworkElement CreatePlaceholderContent(string title, string message)
    {
        var stackPanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 100, 0, 0)
        };

        var icon = new MaterialDesignThemes.Wpf.PackIcon
        {
            Kind = MaterialDesignThemes.Wpf.PackIconKind.InformationOutline,
            Width = 80,
            Height = 80,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 20)
        };

        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 32,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var messageBlock = new TextBlock
        {
            Text = message,
            FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        stackPanel.Children.Add(icon);
        stackPanel.Children.Add(titleBlock);
        stackPanel.Children.Add(messageBlock);

        return stackPanel;
    }

    private bool _isDarkTheme = false;

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _isDarkTheme = !_isDarkTheme;
        ApplyTheme(_isDarkTheme);
    }

    private void NotificationsButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToPage("Email");
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Support: contactez l'équipe IT / Maintenance.\n\nPour les incidents production: utilisez la page Maintenance.",
            "Aide",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ApplyTheme(bool isDark)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"ApplyTheme called with isDark={isDark}");
            var paletteHelper = new MaterialDesignThemes.Wpf.PaletteHelper();
        
            if (isDark)
            {
                // GitHub Dark Theme
                var theme = MaterialDesignThemes.Wpf.Theme.Create(
                    MaterialDesignThemes.Wpf.BaseTheme.Dark,
                    Color.FromRgb(88, 166, 255),  // GitHub blue #58a6ff
                    Color.FromRgb(88, 166, 255)
                );
                paletteHelper.SetTheme(theme);
                
                // Apply GitHub dark colors to main window
                this.Background = new SolidColorBrush(Color.FromRgb(13, 17, 23));  // #0d1117
                
                // Update sidebar to darker GitHub color
                LeftSidebar.Background = new SolidColorBrush(Color.FromRgb(1, 4, 9)); // #010409
                LeftSidebar.BorderBrush = new SolidColorBrush(Color.FromRgb(48, 54, 61)); // #30363d
                
                // Update user info card background
                if (FindName("UserInfoCard") is System.Windows.Controls.Border userCard)
                {
                    userCard.Background = new SolidColorBrush(Color.FromRgb(22, 27, 34)); // #161b22
                }
            }
            else
            {
                // Light Theme (original colors)
                var theme = MaterialDesignThemes.Wpf.Theme.Create(
                    MaterialDesignThemes.Wpf.BaseTheme.Light,
                    Color.FromRgb(63, 81, 181),   // Indigo #3f51b5
                    Color.FromRgb(63, 81, 181)
                );
                paletteHelper.SetTheme(theme);
                
                // Restore light background
                this.Background = new SolidColorBrush(Color.FromRgb(250, 250, 250));
                
                // Restore sidebar to original deep indigo
                LeftSidebar.Background = new SolidColorBrush(Color.FromRgb(26, 35, 126)); // #1A237E
                LeftSidebar.BorderBrush = new SolidColorBrush(Color.FromRgb(13, 22, 66)); // #0D1642
                
                // Restore user info card background
                if (FindName("UserInfoCard") is System.Windows.Controls.Border userCard)
                {
                    userCard.Background = new SolidColorBrush(Color.FromRgb(40, 53, 147)); // #283593
                }
            }
            
            System.Diagnostics.Debug.WriteLine("ApplyTheme completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR in ApplyTheme: {ex.Message}");
            var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "erp_crash_log.txt");
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR in ApplyTheme: {ex.Message}\n{ex.StackTrace}\n\n");
            throw;
        }
    }

    private void SendEmail_Click(object sender, RoutedEventArgs e)
    {
        NavigateToPage("Email");
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to logout?",
            "Confirm Logout",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            // _authService.Logout();
            
            var loginWindow = new LoginWindow(null);
            loginWindow.Show();
            
            this.Close();
        }
    }

    private void HomeButton_Click(object sender, RoutedEventArgs e)
    {
        SetActivePage("Dashboard");
        LoadPageContent("Dashboard");
    }

    /// <summary>
    /// Définit le service de permissions pour l'utilisateur
    /// </summary>
    public void SetPermissionService(RolePermissionService permissionService)
    {
        _permissionService = permissionService;
        
        // Update User Info
        if (_permissionService != null)
        {
            UserNameText.Text = _permissionService.CurrentUserId;
            UserRoleText.Text = _permissionService.CurrentRole.ToString();
            
            // Update Welcome Text based on role
            WelcomeText.Text = $"Welcome back, {_permissionService.CurrentUserId}";
        }

        ApplyRoleBasedRestrictions();
        
        // Reload dashboard to apply role-specific content
        if (PageTitleText.Text == "Dashboard")
        {
            LoadPageContent("Dashboard");
        }
    }

    /// <summary>
    /// Applique les restrictions basées sur le rôle de l'utilisateur
    /// </summary>
    private void ApplyRoleBasedRestrictions()
    {
        if (_permissionService == null) return;

        // TODO: Afficher le rôle de l'utilisateur dans le XAML si disponible
        // System.Diagnostics.Debug.WriteLine($"Rôle: {_permissionService.GetRoleDisplayName()}");

        // Filtrer le menu selon les permissions
        FilterMenuByRole();

        // Restreindre l'accès aux pages
        RestrictPageAccess();
    }

    /// <summary>
    /// Filtre le menu de navigation selon le rôle
    /// </summary>
    private void FilterMenuByRole()
    {
        if (_permissionService == null) return;

        // Filter sidebar buttons based on Tag == pageName
        foreach (var child in NavigationMenuPanel.Children)
        {
            if (child is not Button btn) continue;
            if (btn.Tag is not string pageName) continue;

            // Keep Dashboard always visible.
            if (string.Equals(pageName, "Dashboard", StringComparison.OrdinalIgnoreCase))
            {
                btn.Visibility = Visibility.Visible;
                continue;
            }

            btn.Visibility = _permissionService.CanAccessPage(pageName)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // Quick-action buttons
        SendEmailButton.Visibility = _permissionService.CanAccessPage("Email")
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// Restreint l'accès aux pages selon les permissions
    /// </summary>
    private void RestrictPageAccess()
    {
        if (_permissionService == null) return;

        // Exemple: Désactiver certaines pages si l'utilisateur n'a pas les permissions
        // Cette logique peut être étendue selon les besoins
    }
}

