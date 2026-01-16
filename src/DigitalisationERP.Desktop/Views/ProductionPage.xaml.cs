using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DigitalisationERP.Desktop.Models;
using DigitalisationERP.Desktop.Controls;
using DigitalisationERP.Desktop.Services;
using DigitalisationERP.Desktop.Services.IoT;
using MaterialDesignThemes.Wpf;

namespace DigitalisationERP.Desktop.Views;

public partial class ProductionPage : UserControl
{
    private AgvRobotMessenger? _agvRobotMessenger;
    private IReadOnlyList<string> _currentRoute = Array.Empty<string>();
    private readonly LoginResponse? _loginResponse;
    private readonly ApiClient _apiClient;
    private readonly ProductionDataService _dataService;
    private Point _dragStartPoint;
    private UIElement? _draggedElement;
    private bool _isDragging;
    private Dictionary<string, ProductionPostControl> _postControls = new();
    private List<KanbanTaskCard> _tasks = new();
    private double _currentZoom = 1.0;
    private bool _isPanning = false;
    private Point _panStart;
    private string? _selectedLineId = null;
    private bool _allLinesMode => _selectedLineId == null;
    private string? _pendingLineIdToSelect;

    private readonly ProductionFlowSimulator _productionFlowSimulator = new(ProductionDataService.Instance);
    private CancellationTokenSource? _ofCancellation;
    private bool _isOfPaused;
    private bool _isOfRunning;
    private string? _highlightedPostCode;

    // Sensor gating (one trigger releases one piece).
    private readonly object _sensorGateSync = new();
    private readonly Dictionary<string, SemaphoreSlim> _sensorGateByIndex = new(StringComparer.OrdinalIgnoreCase);
    private IIotProvider? _sensorGateProvider;
    private EventHandler<SensorReadingEventArgs>? _sensorGateHandler;
    private BluetoothSerialClient? _sensorGateSerialClient;
    private EventHandler<string>? _sensorGateSerialHandler;

    // Kanban lifecycle for the active OF.
    private KanbanTaskCard? _activeKanbanCard;
    private string? _activeKanbanOrderNumber;
    private bool _activeKanbanCompleted;

    private KanbanOfPayload? _activeKanbanPayload;

    private KanbanTaskCard? _selectedKanbanCard;

    // Guards to avoid duplicated Drop bubbling (StackPanel + ScrollViewer) and duplicated launch prompts.
    private readonly HashSet<KanbanTaskCard> _kanbanPromptingCards = new();

    // Stock blocking (when a post reaches <=20% of its capacity).
    private readonly HashSet<string> _stockBlockedPosts = new(StringComparer.OrdinalIgnoreCase);
    private bool _isBlockedByStock;

    private readonly Dictionary<int, FrameworkElement> _pieceMarkers = new();
    private readonly Dictionary<int, ScaleTransform> _pieceMarkerTransforms = new();
    private readonly Dictionary<int, string> _pieceLastPostById = new();

    public ProductionPage(LoginResponse? loginResponse)
    {
        InitializeComponent();
        _loginResponse = loginResponse;
        _apiClient = new ApiClient { AuthToken = _loginResponse?.AccessToken };
        _dataService = ProductionDataService.Instance;
        
        // Set initial view
        FlowViewPanel.Visibility = Visibility.Visible;
        KanbanViewPanel.Visibility = Visibility.Collapsed;
        
        // Subscribe to data service events
        _dataService.PostAdded += OnPostAdded;
        _dataService.PostUpdated += OnPostUpdated;
        _dataService.LineAdded += OnLineAdded;
        
        // Load initial data after UI is ready
        Loaded += async (_, _) =>
        {
            _dataService.EnsureInitialized();
            await SyncLinesFromApiAsync();
            InitializeLineSelector();
            LoadExistingPosts();

            await RefreshKanbanComPortsAsync();

            if (!string.IsNullOrWhiteSpace(_pendingLineIdToSelect))
            {
                SelectLine(_pendingLineIdToSelect);
            }

            if (EditLineLinksButton != null)
            {
                EditLineLinksButton.IsEnabled = !_allLinesMode && !string.IsNullOrWhiteSpace(_selectedLineId);
            }

            if (ResetFinishedProductsButton != null)
            {
                ResetFinishedProductsButton.IsEnabled = !_allLinesMode && !string.IsNullOrWhiteSpace(_selectedLineId);
            }

            OrderNumberTextBox.Text = $"OF-{DateTime.Now:yyyyMMdd-HHmmss}";
        };
    }

    private async Task SyncLinesFromApiAsync()
    {
        // If not authenticated (e.g. test page), keep local-only mode.
        if (string.IsNullOrWhiteSpace(_apiClient.AuthToken))
        {
            return;
        }

        try
        {
            var apiLines = await _apiClient.GetProductionLinesAsync();
            foreach (var apiLine in apiLines)
            {
                var id = (apiLine.lineId ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(id)) continue;

                var existing = _dataService.ProductionLines.FirstOrDefault(l =>
                    l.LineId.Equals(id, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    _dataService.AddProductionLine(new ProductionLineData
                    {
                        LineId = id,
                        LineName = string.IsNullOrWhiteSpace(apiLine.lineName) ? id : apiLine.lineName,
                        Description = apiLine.description ?? string.Empty,
                        IsActive = apiLine.isActive,
                        CreatedDate = DateTime.Now
                    });
                }
                else
                {
                    _dataService.UpdateProductionLine(id, l =>
                    {
                        l.LineName = string.IsNullOrWhiteSpace(apiLine.lineName) ? l.LineName : apiLine.lineName;
                        l.Description = apiLine.description ?? l.Description;
                        l.IsActive = apiLine.isActive;
                    });
                }
            }
        }
        catch
        {
            // Best-effort: Production can still run with local posts even if API is temporarily unreachable.
        }
    }

    private void InitializeLineSelector()
    {
        if (ProductionLineSelector == null) return;

        var selectedTag = (ProductionLineSelector.SelectedItem as ComboBoxItem)?.Tag?.ToString();

        ProductionLineSelector.Items.Clear();
        ProductionLineSelector.Items.Add(new ComboBoxItem { Content = "🌍 All Lines - Global View", Tag = "GLOBAL" });

        foreach (var line in _dataService.ProductionLines.OrderBy(l => l.LineName))
        {
            ProductionLineSelector.Items.Add(new ComboBoxItem
            {
                Content = $"{line.LineName} ({line.LineId})",
                Tag = line.LineId
            });
        }

        // Restore previous selection if possible.
        if (!string.IsNullOrWhiteSpace(selectedTag))
        {
            SelectLine(selectedTag);
        }
        else
        {
            ProductionLineSelector.SelectedIndex = 0;
        }
    }

    private void OnLineAdded(object? sender, ProductionLineData line)
    {
        Dispatcher.Invoke(() =>
        {
            InitializeLineSelector();

            if (!string.IsNullOrWhiteSpace(line?.LineId))
            {
                SelectLine(line.LineId);
            }
        });
    }

    private void ProductionLineSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProductionLineSelector.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        var tag = item.Tag?.ToString();

        if (string.Equals(tag, "GLOBAL", StringComparison.OrdinalIgnoreCase))
        {
            _selectedLineId = null;
            AllLinesScrollViewer.Visibility = Visibility.Visible;
            LoadExistingPosts();
            UpdateStatistics();

            if (EditLineLinksButton != null)
            {
                EditLineLinksButton.IsEnabled = false;
            }

            if (ResetFinishedProductsButton != null)
            {
                ResetFinishedProductsButton.IsEnabled = false;
            }
            return;
        }

        _selectedLineId = string.IsNullOrWhiteSpace(tag) ? null : tag;
        AllLinesScrollViewer.Visibility = Visibility.Collapsed;

        if (EditLineLinksButton != null)
        {
            EditLineLinksButton.IsEnabled = !_allLinesMode && !string.IsNullOrWhiteSpace(_selectedLineId);
        }

        if (ResetFinishedProductsButton != null)
        {
            ResetFinishedProductsButton.IsEnabled = !_allLinesMode && !string.IsNullOrWhiteSpace(_selectedLineId);
        }

        LoadExistingPosts();
        UpdateStatistics();
    }

    private void AddLine_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Production lines are created from Inventory (Stock Diagram).\n\nGo to Inventory → Add Line, then come back here to configure posts.",
            "Create Line",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ResetFinishedProducts_Click(object sender, RoutedEventArgs e)
    {
        if (_allLinesMode || string.IsNullOrWhiteSpace(_selectedLineId))
        {
            MessageBox.Show("Select a line first.", "Reset PF", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            "Reset finished product count (PF) for this line?",
            "Reset PF",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        _dataService.UpdateProductionLine(_selectedLineId, l => l.FinishedProductCount = 0);
        FinishedProductsText.Text = "0";
        UpdateStatistics();
    }

    private async void EditLineLinks_Click(object sender, RoutedEventArgs e)
    {
        if (_allLinesMode || string.IsNullOrWhiteSpace(_selectedLineId))
        {
            MessageBox.Show("Select a line first.", "Line Links", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(_apiClient.AuthToken))
        {
            MessageBox.Show("Not authenticated. Please login again before editing line links.", "Line Links", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var lineId = _selectedLineId.Trim();
            var line = await _apiClient.GetProductionLineAsync(lineId);
            if (line == null)
            {
                MessageBox.Show("Line not found in API.", "Line Links", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new EditProductionLineLinksDialog(line) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() != true) return;

            var req = new UpsertProductionLineRequest
            {
                lineId = lineId,
                lineName = dlg.LineName,
                description = dlg.Description,
                isActive = dlg.IsActive,
                outputMaterial = new MaterialRefRequest
                {
                    materialNumber = dlg.OutputMaterialNumber,
                    description = dlg.OutputMaterialDescription,
                    materialType = 3,
                    unitOfMeasure = string.IsNullOrWhiteSpace(dlg.OutputUnitOfMeasure) ? null : dlg.OutputUnitOfMeasure,
                    initialStockQuantity = dlg.OutputInitialStock
                },
                inputs = dlg.Inputs
                    .Where(i => !string.IsNullOrWhiteSpace(i.MaterialNumber))
                    .Select(i => new LineInputRequest
                    {
                        material = new MaterialRefRequest
                        {
                            materialNumber = i.MaterialNumber,
                            description = i.Description,
                            materialType = 1,
                            unitOfMeasure = string.IsNullOrWhiteSpace(i.UnitOfMeasure) ? null : i.UnitOfMeasure,
                            initialStockQuantity = i.InitialStockQuantity <= 0 ? null : i.InitialStockQuantity
                        },
                        quantityPerUnit = i.QuantityPerUnit <= 0 ? 1 : i.QuantityPerUnit,
                        unitOfMeasure = string.IsNullOrWhiteSpace(i.UnitOfMeasure) ? null : i.UnitOfMeasure
                    })
                    .ToList()
            };

            var updated = await _apiClient.UpdateProductionLineAsync(lineId, req);
            if (updated == null)
            {
                MessageBox.Show("Update failed.", "Line Links", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Keep local line metadata in sync (posts are local and must be preserved).
            _dataService.UpdateProductionLine(lineId, l =>
            {
                l.LineName = string.IsNullOrWhiteSpace(updated.lineName) ? l.LineName : updated.lineName;
                l.Description = updated.description ?? l.Description;
                l.IsActive = updated.isActive;
            });

            MessageBox.Show("Line links saved.", "Line Links", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to update line links.\n\n{ex.Message}", "Line Links", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void FlowViewButton_Click(object sender, RoutedEventArgs e)
    {
        FlowViewPanel.Visibility = Visibility.Visible;
        KanbanViewPanel.Visibility = Visibility.Collapsed;
    }

    private void KanbanViewButton_Click(object sender, RoutedEventArgs e)
    {
        KanbanViewPanel.Visibility = Visibility.Visible;
        FlowViewPanel.Visibility = Visibility.Collapsed;
    }

    public void FocusLine(string lineId)
    {
        if (string.IsNullOrWhiteSpace(lineId))
        {
            return;
        }

        if (IsLoaded)
        {
            SelectLine(lineId);
            return;
        }

        _pendingLineIdToSelect = lineId;
    }

    private async void StartOf_Click(object sender, RoutedEventArgs e)
    {
        if (_allLinesMode || string.IsNullOrWhiteSpace(_selectedLineId))
        {
            MessageBox.Show("Select a line before starting an OF.", "Select Line", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var orderNumber = string.IsNullOrWhiteSpace(OrderNumberTextBox?.Text)
            ? $"OF-{DateTime.Now:yyyyMMdd-HHmmss}"
            : OrderNumberTextBox.Text.Trim();

        if (!int.TryParse(QuantityTextBox?.Text, out var quantity) || quantity <= 0)
        {
            MessageBox.Show("Invalid quantity.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            await LaunchOfAsync(orderNumber, quantity, source: "manual");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start OF: {ex.Message}", "OF", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LaunchOfFromKanbanAsync(KanbanTaskCard card, KanbanOfPayload payload)
    {
        if (payload == null) return;

        var orderNumber = string.IsNullOrWhiteSpace(payload.OrderNumber)
            ? $"OF-{DateTime.Now:yyyyMMdd-HHmmss}"
            : payload.OrderNumber.Trim();
        var total = Math.Max(1, payload.Quantity);
        var produced = Math.Max(0, payload.Produced);
        var remaining = Math.Max(0, total - produced);

        // If the OF was previously completed, allow relaunch by resetting progress.
        if (remaining == 0)
        {
            payload.Produced = 0;
            produced = 0;
            remaining = total;
            try { card.SetProgress(produced, total); } catch { }
        }

        try
        {
            await LaunchOfAsync(orderNumber, remaining, source: "kanban", kanbanCard: card);
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                OfStatusText.Text = $"OF failed: {ex.Message}";
            });
        }
    }

    private async Task<bool> PromptAndLaunchOfFromKanbanAsync(KanbanTaskCard card, KanbanOfPayload payload)
    {
        if (payload == null) return false;

        _dataService.EnsureInitialized();

        var lines = _dataService.ProductionLines
            .OrderBy(l => l.LineName)
            .Select(l => (lineId: l.LineId, lineName: $"{l.LineName} ({l.LineId})"))
            .ToList();

        if (lines.Count == 0)
        {
            MessageBox.Show("No production lines available. Create a line in Inventory first.",
                "Launch OF",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        var dialog = new LaunchFabricationOrderDialog
        {
            Owner = Window.GetWindow(this)
        };
        dialog.SetLines(lines);

        var orderNumber = string.IsNullOrWhiteSpace(payload.OrderNumber)
            ? $"OF-{DateTime.Now:yyyyMMdd-HHmmss}"
            : payload.OrderNumber.Trim();

        var total = Math.Max(1, payload.Quantity);
        var produced = Math.Max(0, payload.Produced);
        var remaining = Math.Max(0, total - produced);

        // If previously completed, default to relaunch full quantity.
        var quantity = Math.Max(1, remaining == 0 ? total : remaining);

        dialog.Prefill(orderNumber, quantity, preferredLineId: _selectedLineId);

        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(dialog.SelectedLineId))
        {
            SelectLine(dialog.SelectedLineId);
        }

        await LaunchOfAsync(dialog.OrderNumber, dialog.Quantity, source: "kanban", kanbanCard: card);
        return true;
    }

    private async Task LaunchOfAsync(string orderNumber, int quantity, string source, KanbanTaskCard? kanbanCard = null)
    {
        if (_allLinesMode || string.IsNullOrWhiteSpace(_selectedLineId))
        {
            throw new InvalidOperationException("A production line must be selected to launch an OF.");
        }

        _dataService.EnsureInitialized();

        var postsForLine = _dataService
            .GetPostsForLine(_selectedLineId)
            .OrderBy(p => p.Position)
            .ToList();

        if (postsForLine.Count == 0)
        {
            throw new InvalidOperationException("Selected line has no posts.");
        }

        // Apply optional per-post config JSON (stock + TU) before starting.
        var postsConfigJson = PostsConfigTextBox?.Text;
        if (!string.IsNullOrWhiteSpace(postsConfigJson))
        {
            ApplyPostsConfigToLine(_selectedLineId, postsConfigJson);
        }

        var finishedStart = _dataService.GetProductionLine(_selectedLineId)?.FinishedProductCount ?? 0;

        var request = new ProductionFlowRequest
        {
            OrderNumber = orderNumber,
            LineId = _selectedLineId,
            Quantity = quantity,
            TransitSeconds = 2,
            TuSeconds = 10,
            Route = postsForLine.Select(p => p.PostCode).ToArray(),
            FinishedStockStart = finishedStart,
            // Requirement: pipeline flow; piece N+1 can start as soon as post 1 is available.
            RequirePfBeforeNextPiece = false
        };

        // Reset per-run sensor gates.
        lock (_sensorGateSync)
        {
            foreach (var sem in _sensorGateByIndex.Values)
            {
                try { sem.Dispose(); } catch { /* ignore */ }
            }

            _sensorGateByIndex.Clear();
        }

        // Track active Kanban card so we can move it to Done on completion, or back to To Do when stopped.
        _activeKanbanCard = kanbanCard;
        _activeKanbanOrderNumber = orderNumber;
        _activeKanbanCompleted = false;
        _activeKanbanPayload = kanbanCard?.Tag as KanbanOfPayload;

        if (_activeKanbanCard != null && _activeKanbanPayload != null)
        {
            try { _activeKanbanCard.SetProgress(_activeKanbanPayload.Produced, _activeKanbanPayload.Quantity); } catch { }
        }

        // Keep selection on the running card so "Continue selected" works after a stop.
        SetSelectedKanbanCard(kanbanCard);

        _currentRoute = request.Route;

        _ofCancellation?.Cancel();
        _ofCancellation?.Dispose();
        _ofCancellation = new CancellationTokenSource();
        var ct = _ofCancellation.Token;

        _isOfRunning = true;

        Dispatcher.Invoke(() =>
        {
            OfStatusText.Text = $"Launching {orderNumber} (qty {quantity}) via {source}...";
        });

        _agvRobotMessenger?.Dispose();
        _agvRobotMessenger = await TryCreateAgvMessengerAsync(request.Route);

        var startedSignalSent = false;

        try
        {
            startedSignalSent = await TrySendOfSignalAsync(cmd: "start", cancellationToken: ct);

            await _productionFlowSimulator.RunAsync(
                request,
                onEvent: ev => Dispatcher.InvokeAsync(() => HandleProductionFlowEvent(ev)).Task,
                waitForFirstPostDetection: null,
                waitForPostDetection: (postCode, piece, token) => WaitForRequiredPostDetectionAsync(postCode, token),
                waitWhilePaused: token => WaitWhilePausedAsync(token),
                cancellationToken: ct);

            // Requirement: when quantity is built, send {"cmd":"end"} (even if the start send failed).
            await TrySendOfSignalAsync(cmd: "end", cancellationToken: CancellationToken.None);

            _activeKanbanCompleted = true;
            TryMoveActiveKanbanToDone();

            await TryPostProductionToApiAsync(orderNumber, quantity);
        }
        catch (OperationCanceledException)
        {
            Dispatcher.Invoke(() =>
            {
                OfStatusText.Text = "Cancelled.";
            });

            // Allow relaunch: if stopped before completion, keep the task out of Done.
            if (!_activeKanbanCompleted)
            {
                TryMoveActiveKanbanBackToInProgress();
            }
            return;
        }
        finally
        {
            // Only send {"cmd":"end"} when the requested quantity is fully completed.
            // (No-op here; end is handled only on normal completion above.)

            _isOfRunning = false;
            try { _ofCancellation?.Dispose(); } catch { }
            _ofCancellation = null;
        }
    }

    private void TryMoveActiveKanbanToDone()
    {
        try
        {
            if (_activeKanbanCard == null) return;
            if (DoneColumn == null) return;

            Dispatcher.Invoke(() =>
            {
                var old = _activeKanbanCard.Parent as Panel;
                old?.Children.Remove(_activeKanbanCard);
                DoneColumn.Children.Add(_activeKanbanCard);
                _activeKanbanCard.TaskStatus = "Done";
                UpdateKanbanCounts();
            });
        }
        catch
        {
            // ignore
        }
    }

    private void TryMoveActiveKanbanBackToToDo()
    {
        try
        {
            if (_activeKanbanCard == null) return;
            if (ToDoColumn == null) return;

            Dispatcher.Invoke(() =>
            {
                var old = _activeKanbanCard.Parent as Panel;
                old?.Children.Remove(_activeKanbanCard);
                ToDoColumn.Children.Add(_activeKanbanCard);
                _activeKanbanCard.TaskStatus = "ToDo";
                UpdateKanbanCounts();
            });
        }
        catch
        {
            // ignore
        }
    }

    private void TryMoveActiveKanbanBackToInProgress()
    {
        try
        {
            if (_activeKanbanCard == null) return;
            if (InProgressColumn == null) return;

            Dispatcher.Invoke(() =>
            {
                var old = _activeKanbanCard.Parent as Panel;
                old?.Children.Remove(_activeKanbanCard);
                InProgressColumn.Children.Add(_activeKanbanCard);
                _activeKanbanCard.TaskStatus = "InProgress";
                UpdateKanbanCounts();

                // Preserve selection for quick relaunch.
                SetSelectedKanbanCard(_activeKanbanCard);
            });
        }
        catch
        {
            // ignore
        }
    }

    private async Task<bool> TrySendOfSignalAsync(string cmd, CancellationToken cancellationToken)
    {
        try
        {
            var normalized = (cmd ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized != "start" && normalized != "end")
            {
                return false;
            }

            // Default payloads (can be overridden by config).
            var payload = normalized == "start"
                ? "{\"cmd\" : \"start\"}"
                : "{\"cmd\":\"end\"}";

            // Prefer using the already-created messenger (so we reuse the same COM port client).
            if (_agvRobotMessenger != null)
            {
                await _agvRobotMessenger.SendRawAsync(payload, cancellationToken);
                try
                {
                    var client = System.Windows.Application.Current?.MainWindow is MainWindow mw
                        ? mw.GetBluetoothSerialClient()
                        : null;
                    var port = client?.PortName;
                    Dispatcher.Invoke(() =>
                    {
                        OfStatusText.Text = string.IsNullOrWhiteSpace(port)
                            ? $"OF signal '{normalized}' sent."
                            : $"OF signal '{normalized}' sent to {port}.";
                    });
                }
                catch { /* ignore */ }
                return true;
            }

            if (System.Windows.Application.Current?.MainWindow is not MainWindow mainWindow)
            {
                return false;
            }

            var shared = mainWindow.GetBluetoothSerialClient();
            if (shared != null)
            {
                if (!shared.IsOpen)
                {
                    // Make sure the shared client is actually opened.
                    await mainWindow.EnsureBluetoothSerialConnectedAsync();
                }
                await shared.SendLineAsync(payload, cancellationToken);
                try
                {
                    var port = shared.PortName;
                    Dispatcher.Invoke(() =>
                    {
                        OfStatusText.Text = string.IsNullOrWhiteSpace(port)
                            ? $"OF signal '{normalized}' sent."
                            : $"OF signal '{normalized}' sent to {port}.";
                    });
                }
                catch { /* ignore */ }
                return true;
            }

            var configService = mainWindow.GetConfigService();
            if (configService == null)
            {
                return false;
            }

            var cfg = await configService.GetConfigurationAsync();
            var bt = cfg.Bluetooth;
            if (bt == null || bt.Enabled != true)
            {
                return false;
            }

            // Use configured payloads if present (supports strict receivers that compare strings).
            if (normalized == "start" && !string.IsNullOrWhiteSpace(bt.OfStartPayload))
            {
                payload = bt.OfStartPayload;
            }
            else if (normalized == "end" && !string.IsNullOrWhiteSpace(bt.OfEndPayload))
            {
                payload = bt.OfEndPayload;
            }

            using var temp = new BluetoothSerialClient(bt.ComPort, bt.BaudRate, bt.NewLine);
            await temp.SendLineAsync(payload, cancellationToken);

            try
            {
                Dispatcher.Invoke(() =>
                {
                    OfStatusText.Text = $"OF signal '{normalized}' sent to {bt.ComPort}.";
                });
            }
            catch
            {
                // ignore
            }
            return true;
        }
        catch (Exception ex)
        {
            try
            {
                if (System.Windows.Application.Current?.MainWindow is MainWindow mainWindow)
                {
                    var logService = mainWindow.GetLogService();
                    if (logService != null)
                    {
                        await logService.LogEventAsync(
                            level: "ERROR",
                            source: "OF",
                            eventType: "OF_SIGNAL_SEND_FAILED",
                            message: $"cmd={cmd}; error={ex.Message}");
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    // Keep it concise; the flow can still run even if the external receiver is not connected.
                    OfStatusText.Text = $"OF signal '{cmd}' not sent: {ex.Message}";
                });
            }
            catch
            {
                // ignore secondary failures
            }
            return false;
        }
    }

    private static readonly Regex DigitsOnlyRegex = new("^[0-9]+$");

    private static string? NormalizeComPort(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var trimmed = text.Trim();
        if (trimmed.Length == 0) return null;

        trimmed = trimmed.Replace(" ", "", StringComparison.OrdinalIgnoreCase);
        if (trimmed.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.ToUpperInvariant();
        }

        if (int.TryParse(trimmed, out var n) && n > 0)
        {
            return "COM" + n;
        }

        return trimmed.ToUpperInvariant();
    }

    private async Task RefreshKanbanComPortsAsync()
    {
        try
        {
            if (KanbanComPortComboBox == null)
            {
                return;
            }

            var detected = SerialPort.GetPortNames();

            var mainWindow = System.Windows.Application.Current?.MainWindow as MainWindow;
            var configService = mainWindow?.GetConfigService();
            var cfg = configService != null ? await configService.GetConfigurationAsync() : null;
            var preferred = cfg?.Bluetooth?.ComPort;

            var selected = KanbanComPortComboBox.SelectedItem as string;
            var all = new List<string>(capacity: 16);
            if (detected != null) all.AddRange(detected);
            if (!string.IsNullOrWhiteSpace(preferred)) all.Add(preferred);
            if (!string.IsNullOrWhiteSpace(selected)) all.Add(selected);

            var ports = all
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            KanbanComPortComboBox.ItemsSource = ports;

            if (!string.IsNullOrWhiteSpace(preferred) && ports.Any(p => string.Equals(p, preferred, StringComparison.OrdinalIgnoreCase)))
            {
                KanbanComPortComboBox.SelectedItem = ports.First(p => string.Equals(p, preferred, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                KanbanComPortComboBox.SelectedItem = ports.FirstOrDefault();
            }
        }
        catch
        {
            // ignore
        }
    }

    private async void KanbanRefreshPortsButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshKanbanComPortsAsync();
    }

    private async void KanbanConnectComButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var mainWindow = System.Windows.Application.Current?.MainWindow as MainWindow;
            if (mainWindow == null)
            {
                KanbanComStatusText.Text = "Disconnected (no MainWindow)";
                return;
            }

            var selected = KanbanComPortComboBox?.SelectedItem as string;
            var typed = KanbanComPortComboBox?.Text;
            var port = NormalizeComPort(!string.IsNullOrWhiteSpace(typed) ? typed : selected);

            var result = await mainWindow.EnsureBluetoothSerialConnectedAsync(portOverride: port);
            if (!result.Success)
            {
                KanbanComStatusText.Text = $"Disconnected ({result.Error})";
                return;
            }

            KanbanComStatusText.Text = $"Connected ({result.Port})";
        }
        catch (Exception ex)
        {
            try { KanbanComStatusText.Text = $"Disconnected ({ex.Message})"; } catch { }
        }
    }

    private async void KanbanStopProductionButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Requirement: when stopping mid-production, preserve current progress and piece positions.
            // Implementation: pause the running OF (do not cancel), so in-flight pieces keep their state.
            if (!_isOfRunning)
            {
                Dispatcher.Invoke(() => { OfStatusText.Text = "No OF running."; });
                return;
            }

            _isOfPaused = true;

            // Freeze visuals at the last known detected positions (stop any ongoing animations).
            Dispatcher.Invoke(() => SnapAllPieceMarkersToLastKnownPosts());

            // Best-effort stop signal to external receiver.
            await TrySendOfSignalAsync(cmd: "end", cancellationToken: CancellationToken.None);

            Dispatcher.Invoke(() =>
            {
                OfStatusText.Text = "OF paused (end signal sent).";
            });
        }
        catch (Exception ex)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    OfStatusText.Text = $"Stop production failed: {ex.Message}";
                });
            }
            catch
            {
                // ignore
            }
        }
    }

    private async void KanbanEndOfButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_isOfRunning && !_isOfPaused)
            {
                Dispatcher.Invoke(() => { OfStatusText.Text = "No OF running."; });
                return;
            }

            var confirm = MessageBox.Show(
                "End the current OF?\n\nThis will stop the run immediately. The current in-process pieces will be cleared, and the Kanban progress will be reset (Produced=0) and moved back to ToDo.",
                "End OF",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            _isOfPaused = false;

            // Best-effort end signal to external receiver.
            await TrySendOfSignalAsync(cmd: "end", cancellationToken: CancellationToken.None);

            // Cancel the running loop.
            _ofCancellation?.Cancel();

            // Clear UI markers (we are ending the run).
            Dispatcher.Invoke(() =>
            {
                ClearHighlight();
                ClearPieceMarkers();
                OfStatusText.Text = "OF ended.";
            });

            // Reset Kanban progress and move card back to ToDo.
            try
            {
                if (_activeKanbanPayload != null)
                {
                    _activeKanbanPayload.Produced = 0;
                }

                if (_activeKanbanCard != null && _activeKanbanPayload != null)
                {
                    _activeKanbanCard.SetProgress(_activeKanbanPayload.Produced, _activeKanbanPayload.Quantity);
                }

                TryMoveActiveKanbanBackToToDo();
            }
            catch
            {
                // ignore
            }
        }
        catch (Exception ex)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    OfStatusText.Text = $"End OF failed: {ex.Message}";
                });
            }
            catch
            {
                // ignore
            }
        }
    }

    private void NumberValidation_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = string.IsNullOrWhiteSpace(e.Text) || !DigitsOnlyRegex.IsMatch(e.Text);
    }

    private async Task TryPostProductionToApiAsync(string orderNumber, int quantity)
    {
        if (string.IsNullOrWhiteSpace(_selectedLineId)) return;
        if (string.IsNullOrWhiteSpace(_loginResponse?.AccessToken)) return;

        try
        {
            await _apiClient.PostProductionAsync(_selectedLineId, orderNumber, quantity);
        }
        catch (Exception ex)
        {
            // Keep OF simulation as the source of truth for UI; stock posting is best-effort.
            Dispatcher.Invoke(() =>
            {
                OfStatusText.Text = $"Complete (stock sync failed): {ex.Message}";
            });
        }
    }

    private sealed class KanbanOfPayload
    {
        public string OrderNumber { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int Produced { get; set; }
    }

    private sealed class PostConfigDto
    {
        [JsonPropertyName("s")]
        public int StockCapacity { get; set; }

        [JsonPropertyName("t")]
        public int TuMs { get; set; }
    }

    private void ApplyPostsConfigToLine(string lineId, string json)
    {
        var parsed = JsonSerializer.Deserialize<Dictionary<string, PostConfigDto>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (parsed == null || parsed.Count == 0)
        {
            throw new InvalidOperationException("Config JSON is empty.");
        }

        var posts = _dataService.GetPostsForLine(lineId).OrderBy(p => p.Position).ToList();
        if (posts.Count == 0)
        {
            throw new InvalidOperationException("Selected line has no posts.");
        }

        foreach (var (key, cfg) in parsed)
        {
            if (cfg == null) continue;
            if (cfg.StockCapacity <= 0) continue;

            var normalized = key?.Trim() ?? "";
            var postCode = ResolvePostCodeFromConfigKey(normalized, posts);
            if (postCode == null) continue;

            var tuSeconds = (int)Math.Round(cfg.TuMs / 1000.0);
            tuSeconds = Math.Max(1, tuSeconds);

            _dataService.UpdatePost(postCode, p =>
            {
                p.StockCapacity = cfg.StockCapacity;
                p.CurrentLoad = cfg.StockCapacity; // reset full at start
                p.UtilityTimeSeconds = tuSeconds;
            });
        }

        UpdateStatistics();
    }

    private static string? ResolvePostCodeFromConfigKey(string key, List<ProductionPostData> orderedPosts)
    {
        // Accept p1/p2/p3 mapping to route index (1-based)
        if (key.Length >= 2 && (key[0] == 'p' || key[0] == 'P') && int.TryParse(key[1..], out var idx))
        {
            var zeroBased = idx - 1;
            if (zeroBased >= 0 && zeroBased < orderedPosts.Count)
            {
                return orderedPosts[zeroBased].PostCode;
            }
            return null;
        }

        // Accept direct post code keys
        var match = orderedPosts.FirstOrDefault(p => p.PostCode.Equals(key, StringComparison.OrdinalIgnoreCase));
        return match?.PostCode;
    }

    private void HandleProductionFlowEvent(ProductionFlowEvent ev)
    {
        if (ev == null) return;

        if (ev.EventType.Equals("order-launch", StringComparison.OrdinalIgnoreCase))
        {
            ClearHighlight();
            ClearPieceMarkers();
            OfStatusText.Text = ev.Message;
            return;
        }

        // Stock alerts: turn the post red and block production (even if sensors keep sending).
        if (ev.EventType.Equals("stock-low", StringComparison.OrdinalIgnoreCase)
            || ev.EventType.Equals("stock-out", StringComparison.OrdinalIgnoreCase))
        {
            string postCode = string.Empty;

            try
            {
                using var doc = JsonDocument.Parse(ev.JsonPayload);
                if (doc.RootElement.TryGetProperty("PostCode", out var postCodeProp))
                {
                    postCode = (postCodeProp.GetString() ?? string.Empty).Trim();
                }
            }
            catch
            {
                // ignore parse errors
            }

            if (!string.IsNullOrWhiteSpace(postCode))
            {
                _stockBlockedPosts.Add(postCode);
                if (_postControls.TryGetValue(postCode, out var postCtrl))
                {
                    postCtrl.IsStockBlocked = true;
                }
            }

            _isBlockedByStock = true;

            // Notify AGV robot with the expected payload (best-effort).
            if (_agvRobotMessenger != null && !string.IsNullOrWhiteSpace(postCode))
            {
                try
                {
                    var idx = GetPostIndex1Based(postCode);
                    if (idx.HasValue)
                    {
                        _agvRobotMessenger.SetRawMaterialNeeded(true);
                        _agvRobotMessenger.SetPostMessage(idx.Value, ev.Message);
                        _ = _agvRobotMessenger.SendAsync();
                    }
                }
                catch
                {
                    // ignore robot notification failures
                }
            }

            // Hard stop the current OF.
            if (_isOfRunning)
            {
                _ofCancellation?.Cancel();
            }

            OfStatusText.Text = ev.Message;

            try
            {
                MessageBox.Show(ev.Message, "Stock Alert", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch
            {
                // ignore
            }

            return;
        }
        
        if (ev.EventType.Equals("piece-detected", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var doc = JsonDocument.Parse(ev.JsonPayload);
                if (doc.RootElement.TryGetProperty("PostCode", out var postCodeEl))
                {
                    var postCode = postCodeEl.GetString();
                    if (!string.IsNullOrWhiteSpace(postCode))
                    {
                        HighlightPost(postCode);
                        if (doc.RootElement.TryGetProperty("Piece", out var pieceEl) && pieceEl.TryGetInt32(out var piece))
                        {
                            _pieceLastPostById[piece] = postCode;

                            var step = doc.RootElement.TryGetProperty("Step", out var stepEl) && stepEl.TryGetInt32(out var s)
                                ? s
                                : 0;

                            // Requirement: pieces appear ONLY when detected at post 1.
                            // For later posts, we only move an existing marker.
                            if (step <= 1)
                            {
                                EnsurePieceMarker(piece);
                            }

                            if (_pieceMarkers.ContainsKey(piece))
                            {
                                MovePieceToPost(piece, postCode);
                            }
                            StartTuPulse(piece);
                            OfStatusText.Text = $"Piece {piece} processing at {postCode}";
                        }
                        else
                        {
                            OfStatusText.Text = $"Processing at {postCode}";
                        }
                    }
                }
            }
            catch
            {
                // ignore parsing errors
            }
        }
        else if (ev.EventType.Equals("transit", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var doc = JsonDocument.Parse(ev.JsonPayload);
                if (!doc.RootElement.TryGetProperty("Piece", out var pieceEl) || !pieceEl.TryGetInt32(out var piece))
                {
                    OfStatusText.Text = ev.Message;
                    return;
                }

                if (!doc.RootElement.TryGetProperty("From", out var fromEl) || !doc.RootElement.TryGetProperty("To", out var toEl))
                {
                    OfStatusText.Text = ev.Message;
                    return;
                }

                var from = fromEl.GetString();
                var to = toEl.GetString();
                if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                {
                    OfStatusText.Text = ev.Message;
                    return;
                }

                var transitMs = 1000;
                if (doc.RootElement.TryGetProperty("TransitMs", out var transitEl) && transitEl.TryGetInt32(out var t))
                {
                    transitMs = Math.Max(0, t);
                }

                // Do not create a piece marker on transit.
                // Markers are created only when post 1 detects the piece.
                if (!_pieceMarkers.ContainsKey(piece))
                {
                    OfStatusText.Text = ev.Message;
                    return;
                }
                AnimatePieceTransit(piece, from, to, transitMs);
                OfStatusText.Text = $"Piece {piece} transiting {from} -> {to}";
            }
            catch
            {
                OfStatusText.Text = ev.Message;
            }
        }
        else if (ev.EventType.Equals("raw-material-consumed", StringComparison.OrdinalIgnoreCase))
        {
            // Stock UI updates are driven by ProductionDataService.PostUpdated.
            // Keep status informative.
            OfStatusText.Text = ev.Message;
        }
        else if (ev.EventType.Equals("tu-complete", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var doc = JsonDocument.Parse(ev.JsonPayload);
                if (doc.RootElement.TryGetProperty("Piece", out var pieceEl) && pieceEl.TryGetInt32(out var piece))
                {
                    StopTuPulse(piece);
                    BlinkPieceMarker(piece);
                }
            }
            catch
            {
                // ignore
            }
            try
            {
                using var doc = JsonDocument.Parse(ev.JsonPayload);
                var piece = doc.RootElement.TryGetProperty("Piece", out var pieceEl) && pieceEl.TryGetInt32(out var p) ? p : 0;
                var post = doc.RootElement.TryGetProperty("PostCode", out var postEl) ? postEl.GetString() : null;

                if (piece > 0)
                {
                    BlinkPieceMarker(piece);
                }

                if (piece > 0 && !string.IsNullOrWhiteSpace(post))
                {
                    OfStatusText.Text = $"Piece {piece} TU complete at {post}";
                }
                else
                {
                    OfStatusText.Text = ev.Message;
                }
            }
            catch
            {
                OfStatusText.Text = ev.Message;
            }
        }
        else if (ev.EventType.Equals("final-product-count", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var doc = JsonDocument.Parse(ev.JsonPayload);
                if (doc.RootElement.TryGetProperty("Piece", out var pieceEl) && pieceEl.TryGetInt32(out var piece) && piece > 0)
                {
                    RemovePieceMarker(piece);
                }
                if (doc.RootElement.TryGetProperty("FinalProductCount", out var countEl))
                {
                    var count = countEl.GetInt32();
                    if (!string.IsNullOrWhiteSpace(_selectedLineId))
                    {
                        _dataService.UpdateProductionLine(_selectedLineId, l => l.FinishedProductCount = count);
                    }
                    FinishedProductsText.Text = count.ToString();
                }

                if (_activeKanbanCard != null && _activeKanbanPayload != null)
                {
                    var evOrder = doc.RootElement.TryGetProperty("OrderNumber", out var orderEl)
                        ? (orderEl.GetString() ?? string.Empty)
                        : string.Empty;

                    if (string.IsNullOrWhiteSpace(_activeKanbanOrderNumber) || string.Equals(evOrder, _activeKanbanOrderNumber, StringComparison.OrdinalIgnoreCase))
                    {
                        _activeKanbanPayload.Produced = Math.Min(_activeKanbanPayload.Quantity, _activeKanbanPayload.Produced + 1);
                        _activeKanbanCard.SetProgress(_activeKanbanPayload.Produced, _activeKanbanPayload.Quantity);
                    }
                }
            }
            catch
            {
                // ignore
            }

            UpdateStatistics();
        }
        else if (ev.EventType.Equals("order-complete", StringComparison.OrdinalIgnoreCase))
        {
            OfStatusText.Text = "Complete.";
            ClearHighlight();
            ClearPieceMarkers();
            UpdateStatistics();
        }
    }

    private int? GetPostIndex1Based(string postCode)
    {
        if (string.IsNullOrWhiteSpace(postCode) || _currentRoute.Count == 0) return null;
        for (var i = 0; i < _currentRoute.Count; i++)
        {
            if (string.Equals(_currentRoute[i], postCode, StringComparison.OrdinalIgnoreCase))
            {
                return i + 1;
            }
        }
        return null;
    }

    private async Task<AgvRobotMessenger?> TryCreateAgvMessengerAsync(IReadOnlyList<string> route)
    {
        try
        {
            if (System.Windows.Application.Current?.MainWindow is not MainWindow mainWindow)
            {
                return null;
            }

            var configService = mainWindow.GetConfigService();
            if (configService == null)
            {
                return null;
            }

            var cfg = await configService.GetConfigurationAsync();
            if (cfg.Bluetooth?.Enabled != true)
            {
                return null;
            }

            // IMPORTANT: share a single COM-port client across the app (IoT inbound triggers + outbound AGV messages).
            var shared = mainWindow.GetBluetoothSerialClient();
            var client = shared ?? new BluetoothSerialClient(cfg.Bluetooth.ComPort, cfg.Bluetooth.BaudRate, cfg.Bluetooth.NewLine);
            var builder = new AgvRobotMessageBuilder(route);
            return new AgvRobotMessenger(client, builder, ownsClient: shared == null);
        }
        catch
        {
            return null;
        }
    }

    private async Task WaitWhilePausedAsync(CancellationToken ct)
    {
        while (_isOfPaused)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(100, ct);
        }
    }

    private async Task WaitForRequiredPostDetectionAsync(string postCode, CancellationToken ct)
    {
        // Requirement:
        // - post 1: piece appears only after a real sensor message
        // - post 2: piece appears only after a real sensor message
        // - post 3: piece appears only after a real sensor message
        // One sensor message should release only ONE piece.

        if (_isBlockedByStock)
        {
            Dispatcher.Invoke(() =>
            {
                OfStatusText.Text = "Blocked by low stock. Production paused.";
            });

            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }

        if (System.Windows.Application.Current?.MainWindow is not MainWindow mainWindow)
        {
            return;
        }

        // We gate based on the shared Bluetooth serial client so it works even if the IoT provider is in simulation mode.
        // This avoids cases where the COM port was connected but the IoT provider wasn't rebuilt.
        var client = mainWindow.GetBluetoothSerialClient();
        if (client == null || !client.IsOpen)
        {
            // Try to auto-connect using current config.
            var result = await mainWindow.EnsureBluetoothSerialConnectedAsync(portOverride: null);
            if (!result.Success)
            {
                // Do not fail the whole run: stay blocked until the operator connects COM or stops the OF.
                Dispatcher.Invoke(() =>
                {
                    OfStatusText.Text = $"Waiting for COM connection (needed for sensor gating): {result.Error}";
                });

                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }

            client = mainWindow.GetBluetoothSerialClient();
        }

        if (client == null)
        {
            Dispatcher.Invoke(() =>
            {
                OfStatusText.Text = "Waiting for COM client (needed for sensor gating)...";
            });

            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return;
        }

        var idx = GetPostIndex1Based(postCode);
        if (!idx.HasValue) return;
        if (idx.Value is < 1 or > 3) return;

        // IMPORTANT: This workflow is sensor-driven.
        // Gate post1/post2/post3 on real serial sensor triggers.

        SemaphoreSlim gate;
        lock (_sensorGateSync)
        {
            // Use the post index as the gate key so Bluetooth provider ("1"/"3") and route post codes both map.
            var key = idx.Value.ToString();
            if (!_sensorGateByIndex.TryGetValue(key, out gate!))
            {
                gate = new SemaphoreSlim(0, int.MaxValue);
                _sensorGateByIndex[key] = gate;
            }

            // Ensure we are subscribed to the active Bluetooth serial client.
            if (!ReferenceEquals(_sensorGateSerialClient, client))
            {
                if (_sensorGateSerialClient != null && _sensorGateSerialHandler != null)
                {
                    try { _sensorGateSerialClient.LineReceived -= _sensorGateSerialHandler; } catch { /* ignore */ }
                }

                _sensorGateSerialClient = client;
                _sensorGateSerialHandler = (sender, line) =>
                {
                    try
                    {
                        if (_isOfPaused) return;

                        if (!TryParseSensorLineForPostIndex(line, out var postIndex1Based))
                        {
                            return;
                        }

                        var rp = postIndex1Based.ToString();
                        if (rp != "1" && rp != "2" && rp != "3") return;

                        lock (_sensorGateSync)
                        {
                            if (_sensorGateByIndex.TryGetValue(rp, out var sem))
                            {
                                sem.Release(1);
                            }
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                };

                client.LineReceived += _sensorGateSerialHandler;
            }
        }

        Dispatcher.Invoke(() =>
        {
            OfStatusText.Text = $"Waiting for sensor trigger at post {idx.Value}...";
        });

        while (true)
        {
            await WaitWhilePausedAsync(ct);
            await gate.WaitAsync(ct);

            // Discard any triggers that arrived while paused.
            if (_isOfPaused) continue;
            break;
        }
    }

    private static bool TryParseSensorLineForPostIndex(string line, out int postIndex1Based)
    {
        postIndex1Based = 0;
        if (string.IsNullOrWhiteSpace(line)) return false;

        var trimmed = line.Trim();
        if (trimmed.Length == 0) return false;

        // Preferred sensor JSON format:
        // {"poste":1,"etat":"piece _detectee"}
        // Only treat it as a trigger when etat indicates a detection.
        if (!trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            // Ignore non-JSON lines to avoid false positives (noise/echo/AT responses).
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;

            if (!root.TryGetProperty("etat", out var etatEl) || etatEl.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var etat = (etatEl.GetString() ?? string.Empty).Trim().ToLowerInvariant();
            var normalized = System.Text.RegularExpressions.Regex.Replace(etat, @"\s+", " ");
            normalized = normalized.Replace("_", " ").Trim();
            if (!normalized.Contains("piece") || !normalized.Contains("detect"))
            {
                return false;
            }

            if (root.TryGetProperty("poste", out var posteEl) && posteEl.ValueKind == JsonValueKind.Number && posteEl.TryGetInt32(out var p))
            {
                postIndex1Based = p;
                return postIndex1Based is >= 1 and <= 3;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private void RemovePieceMarker(int piece)
    {
        if (piece <= 0) return;
        if (PieceMarkersCanvas == null) return;
        if (!_pieceMarkers.TryGetValue(piece, out var marker)) return;

        PieceMarkersCanvas.Children.Remove(marker);
        _pieceMarkers.Remove(piece);
        _pieceMarkerTransforms.Remove(piece);
    }

    private void BlinkPieceMarker(int piece)
    {
        if (piece <= 0) return;
        if (!_pieceMarkers.TryGetValue(piece, out var marker)) return;

        var anim = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 0.95,
            To = 0.55,
            Duration = new Duration(TimeSpan.FromMilliseconds(150)),
            AutoReverse = true
        };

        marker.BeginAnimation(OpacityProperty, anim);
    }

    private void EnsurePieceMarker(int piece)
    {
        if (piece <= 0) return;
        if (_pieceMarkers.ContainsKey(piece)) return;
        if (PieceMarkersCanvas == null) return;

        var scale = new ScaleTransform(1.0, 1.0);
        var marker = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(15),
            Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(2),
            RenderTransform = scale,
            RenderTransformOrigin = new Point(0.5, 0.5),
            Child = new TextBlock
            {
                Text = piece.ToString(),
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            },
            Opacity = 0.95
        };

        Panel.SetZIndex(marker, 60);
        Canvas.SetLeft(marker, 0);
        Canvas.SetTop(marker, 0);
        PieceMarkersCanvas.Children.Add(marker);
        _pieceMarkers[piece] = marker;
        _pieceMarkerTransforms[piece] = scale;
    }

    private void ClearPieceMarkers()
    {
        if (PieceMarkersCanvas != null)
        {
            PieceMarkersCanvas.Children.Clear();
        }
        _pieceMarkers.Clear();
        _pieceMarkerTransforms.Clear();
        _pieceLastPostById.Clear();
    }
    
    private void SnapAllPieceMarkersToLastKnownPosts()
    {
        foreach (var kvp in _pieceLastPostById)
        {
            if (_pieceMarkers.ContainsKey(kvp.Key))
            {
                MovePieceToPost(kvp.Key, kvp.Value);
            }
        }
    }

    private Point? GetPostCenter(string postCode)
    {
        if (string.IsNullOrWhiteSpace(postCode)) return null;

        // Prefer actual canvas container position to avoid any lag between drag operations and persisted data.
        var container = FlowCanvas.Children
            .OfType<Grid>()
            .FirstOrDefault(g => string.Equals(g.Tag?.ToString(), postCode, StringComparison.OrdinalIgnoreCase));

        if (container != null)
        {
            var left = Canvas.GetLeft(container);
            var top = Canvas.GetTop(container);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;
            var w = container.Width > 0 ? container.Width : 180;
            var h = container.Height > 0 ? container.Height : 120;
            return new Point(left + (w / 2), top + (h / 2));
        }

        // Fallback to persisted coordinates.
        var post = _dataService.Posts.FirstOrDefault(p => p.PostCode.Equals(postCode, StringComparison.OrdinalIgnoreCase));
        if (post == null) return null;
        return new Point(post.X + 90, post.Y + 60);
    }


    private void MovePieceToPost(int piece, string postCode)
    {
        if (!_pieceMarkers.TryGetValue(piece, out var marker)) return;
        var center = GetPostCenter(postCode);
        if (center == null) return;

        marker.BeginAnimation(Canvas.LeftProperty, null);
        marker.BeginAnimation(Canvas.TopProperty, null);
        Canvas.SetLeft(marker, center.Value.X - (marker.Width / 2));
        Canvas.SetTop(marker, center.Value.Y - (marker.Height / 2));
    }

    private void StartTuPulse(int piece)
    {
        if (piece <= 0) return;
        if (!_pieceMarkerTransforms.TryGetValue(piece, out var scale)) return;

        var pulse = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 1.0,
            To = 1.25,
            Duration = new Duration(TimeSpan.FromMilliseconds(350)),
            AutoReverse = true,
            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
        };

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
    }

    private void StopTuPulse(int piece)
    {
        if (piece <= 0) return;
        if (!_pieceMarkerTransforms.TryGetValue(piece, out var scale)) return;

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        scale.ScaleX = 1.0;
        scale.ScaleY = 1.0;
    }

    private void AnimatePieceTransit(int piece, string fromPost, string toPost, int transitMs)
    {
        if (!_pieceMarkers.TryGetValue(piece, out var marker)) return;
        var from = GetPostCenter(fromPost);
        var to = GetPostCenter(toPost);
        if (from == null || to == null) return;

        // Ensure we start at the origin.
        Canvas.SetLeft(marker, from.Value.X - (marker.Width / 2));
        Canvas.SetTop(marker, from.Value.Y - (marker.Height / 2));

        var duration = new Duration(TimeSpan.FromMilliseconds(Math.Max(50, transitMs)));

        var leftAnim = new System.Windows.Media.Animation.DoubleAnimation
        {
            To = to.Value.X - (marker.Width / 2),
            Duration = duration,
            EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut }
        };

        var topAnim = new System.Windows.Media.Animation.DoubleAnimation
        {
            To = to.Value.Y - (marker.Height / 2),
            Duration = duration,
            EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut }
        };

        marker.BeginAnimation(Canvas.LeftProperty, leftAnim);
        marker.BeginAnimation(Canvas.TopProperty, topAnim);
    }

    private void HighlightPost(string postCode)
    {
        if (string.IsNullOrWhiteSpace(postCode)) return;

        if (_highlightedPostCode != null && _postControls.TryGetValue(_highlightedPostCode, out var prev))
        {
            prev.IsInProcess = false;
        }

        _highlightedPostCode = postCode;
        if (_postControls.TryGetValue(postCode, out var current))
        {
            current.IsInProcess = true;
        }
    }

    private void ClearHighlight()
    {
        if (_highlightedPostCode != null && _postControls.TryGetValue(_highlightedPostCode, out var prev))
        {
            prev.IsInProcess = false;
        }

        _highlightedPostCode = null;
    }

    #region Flow Map - Drag and Drop

    private void AddPost_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_allLinesMode)
            {
                MessageBox.Show("Select a line before adding a post.", "Select Line", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dialog = new AddProductionPostDialog
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                // Generate health score and issue
                var healthScore = Random.Shared.Next(40, 101);
                var issueType = healthScore switch
                {
                    >= 90 => "No Issues",
                    >= 70 => "Minor Wear on Bearings",
                    >= 50 => "Hydraulic Pressure Low",
                    _ => "Critical Motor Failure"
                };

                var postData = new ProductionPostData
                {
                    PostCode = dialog.PostCode,
                    PostName = dialog.PostName,
                    X = dialog.XPosition,
                    Y = dialog.YPosition,
                    LineId = _selectedLineId ?? string.Empty,
                    CurrentLoad = dialog.CurrentLoad,
                    MaterialLevel = dialog.MaterialLevel,
                    UtilityTimeSeconds = dialog.UtilityTimeSeconds,
                    StockCapacity = dialog.StockCapacity,
                    Status = dialog.Status == "Active" && healthScore < 70 ? "Maintenance" : dialog.Status,
                    MaintenanceHealthScore = healthScore,
                    MaintenanceIssue = issueType
                };

                // Add to shared data service (will trigger PostAdded event)
                _dataService.AddPost(postData);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error adding post: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddPostBeforeOrAfter(string referencePostCode, bool isBefore)
    {
        try
        {
            if (_allLinesMode)
            {
                MessageBox.Show("Select a line before adding a post.", "Select Line", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            // Get the reference post
            var referencePost = _dataService.Posts.FirstOrDefault(p => p.PostCode == referencePostCode);
            if (referencePost == null) return;

            var dialog = new AddProductionPostDialog
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                // Generate health score and issue
                var healthScore = Random.Shared.Next(40, 101);
                var issueType = healthScore switch
                {
                    >= 90 => "No Issues",
                    >= 70 => "Minor Wear on Bearings",
                    >= 50 => "Hydraulic Pressure Low",
                    _ => "Critical Motor Failure"
                };

                // Calculate position (before or after reference post)
                var newPosition = isBefore ? referencePost.Position : referencePost.Position + 1;

                // Calculate X, Y coordinates - will be repositioned automatically
                var newX = referencePost.X;
                var newY = referencePost.Y;

                var postData = new ProductionPostData
                {
                    PostCode = dialog.PostCode,
                    PostName = dialog.PostName,
                    X = newX,
                    Y = newY,
                    LineId = _selectedLineId ?? referencePost.LineId,
                    CurrentLoad = dialog.CurrentLoad,
                    MaterialLevel = dialog.MaterialLevel,
                    UtilityTimeSeconds = dialog.UtilityTimeSeconds,
                    StockCapacity = dialog.StockCapacity,
                    Status = dialog.Status == "Active" && healthScore < 70 ? "Maintenance" : dialog.Status,
                    MaintenanceHealthScore = healthScore,
                    MaintenanceIssue = issueType,
                    Position = newPosition
                };

                // Add to shared data service with position
                _dataService.AddPostToLine(referencePost.LineId, postData);
                
                // Rearrange all posts in the line with proper spacing
                RearrangePostsInLine(referencePost.LineId);
                
                MessageBox.Show($"Post {postData.PostCode} added {(isBefore ? "before" : "after")} {referencePostCode}!", 
                    "Post Added", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error adding post: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RearrangePostsInLine(string lineId)
    {
        try
        {
            // Get all posts in this line ordered by position
            var linePosts = _dataService.GetPostsForLine(lineId).OrderBy(p => p.Position).ToList();
            
            if (linePosts.Count == 0) return;

            // Calculate spacing
            const double startX = 100;
            const double startY = 300;
            const double horizontalSpacing = 250; // Space between posts (180 width + 70 gap)

            // Reposition each post
            for (int i = 0; i < linePosts.Count; i++)
            {
                var post = linePosts[i];
                var newX = startX + (i * horizontalSpacing);
                var newY = startY;

                // Update position in data service
                _dataService.UpdatePost(post.PostCode, p => 
                { 
                    p.X = newX; 
                    p.Y = newY; 
                });

                // Update visual position if container exists
                var container = FlowCanvas.Children.OfType<Grid>().FirstOrDefault(g => g.Tag?.ToString() == post.PostCode);
                if (container != null)
                {
                    Canvas.SetLeft(container, newX);
                    Canvas.SetTop(container, newY);
                }
            }

            // Update connection lines after rearranging
            UpdateConnectionLines();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error rearranging posts: {ex.Message}");
        }
    }

    private ProductionPostControl CreateProductionPostFromData(ProductionPostData data)
    {
        var postControl = new ProductionPostControl
        {
            PostCode = data.PostCode,
            PostName = data.PostName,
            StockCapacity = data.StockCapacity,
            CurrentLoad = data.CurrentLoad,
            MaterialLevel = data.MaterialLevel,
            Status = data.Status,
            MaintenanceHealthScore = data.MaintenanceHealthScore,
            MaintenanceIssue = data.MaintenanceIssue
        };

        postControl.IsStockBlocked = _stockBlockedPosts.Contains(data.PostCode);

        // Create container for post + add buttons
        var container = new Grid
        {
            Width = 180,
            Height = 120,
            Background = Brushes.Transparent
        };
        // Make container pass through mouse events except for the buttons
        container.IsHitTestVisible = true;

        // Add the post control
        container.Children.Add(postControl);

        // Create "Add Before" button (left side)
        var addBeforeButton = new Button
        {
            Content = "➕",
            Width = 28,
            Height = 28,
            FontSize = 16,
            Background = new SolidColorBrush(Color.FromArgb(220, 33, 150, 243)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(-14, 0, 0, 0),
            Visibility = Visibility.Collapsed,
            Cursor = Cursors.Hand,
            Style = (Style)FindResource("MaterialDesignFloatingActionMiniButton"),
            ToolTip = "Add post before this one"
        };
        addBeforeButton.Click += (s, e) =>
        {
            AddPostBeforeOrAfter(data.PostCode, isBefore: true);
            e.Handled = true;
        };
        Panel.SetZIndex(addBeforeButton, 100);
        container.Children.Add(addBeforeButton);

        // Create "Add After" button (right side)
        var addAfterButton = new Button
        {
            Content = "➕",
            Width = 28,
            Height = 28,
            FontSize = 16,
            Background = new SolidColorBrush(Color.FromArgb(220, 76, 175, 80)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, -14, 0),
            Visibility = Visibility.Collapsed,
            Cursor = Cursors.Hand,
            Style = (Style)FindResource("MaterialDesignFloatingActionMiniButton"),
            ToolTip = "Add post after this one"
        };
        addAfterButton.Click += (s, e) =>
        {
            AddPostBeforeOrAfter(data.PostCode, isBefore: false);
            e.Handled = true;
        };
        Panel.SetZIndex(addAfterButton, 100);
        container.Children.Add(addAfterButton);

        // Show/hide buttons on hover
        postControl.MouseEnter += (s, e) =>
        {
            addBeforeButton.Visibility = Visibility.Visible;
            addAfterButton.Visibility = Visibility.Visible;
        };

        postControl.MouseLeave += (s, e) =>
        {
            if (!addBeforeButton.IsMouseOver && !addAfterButton.IsMouseOver)
            {
                addBeforeButton.Visibility = Visibility.Collapsed;
                addAfterButton.Visibility = Visibility.Collapsed;
            }
        };

        // Keep buttons visible when hovering over them
        addBeforeButton.MouseLeave += (s, e) =>
        {
            if (!postControl.IsMouseOver && !addAfterButton.IsMouseOver)
            {
                addBeforeButton.Visibility = Visibility.Collapsed;
                addAfterButton.Visibility = Visibility.Collapsed;
            }
        };

        addAfterButton.MouseLeave += (s, e) =>
        {
            if (!postControl.IsMouseOver && !addBeforeButton.IsMouseOver)
            {
                addBeforeButton.Visibility = Visibility.Collapsed;
                addAfterButton.Visibility = Visibility.Collapsed;
            }
        };

        Canvas.SetLeft(container, data.X);
        Canvas.SetTop(container, data.Y);
        Canvas.SetZIndex(container, 10);

        // Attach drag handlers to postControl AND container for drag operations
        postControl.MouseLeftButtonDown += Post_MouseLeftButtonDown;
        postControl.MouseMove += Post_MouseMove;
        postControl.MouseLeftButtonUp += Post_MouseLeftButtonUp;
        postControl.MouseRightButtonUp += Post_MouseRightButtonUp;

        // Store container reference with post code for drag operations
        container.Tag = data.PostCode;
        postControl.Tag = data.PostCode;

        FlowCanvas.Children.Add(container);
        
        // Add to sidebar list
        AddPostToSidebar(data);
        
        return postControl;
    }

    private void AddPostToSidebar(ProductionPostData data)
    {
        var card = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(15, 12, 15, 12),
            Cursor = Cursors.Hand
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Status indicator
        var statusDot = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = data.Status == "Active" 
                ? new SolidColorBrush(Color.FromRgb(76, 175, 80)) 
                : new SolidColorBrush(Color.FromRgb(158, 158, 158)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        Grid.SetColumn(statusDot, 0);

        // Post info
        var infoStack = new StackPanel();
        var codeText = new TextBlock
        {
            Text = data.PostCode,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(33, 33, 33))
        };
        var nameText = new TextBlock
        {
            Text = data.PostName,
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(117, 117, 117)),
            Margin = new Thickness(0, 2, 0, 0)
        };
        var loadText = new TextBlock
        {
            Text = $"Stock: {data.CurrentLoad}/{data.StockCapacity}",
            FontSize = 9,
            Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 3, 0, 0)
        };
        infoStack.Children.Add(codeText);
        infoStack.Children.Add(nameText);
        infoStack.Children.Add(loadText);
        Grid.SetColumn(infoStack, 1);

        grid.Children.Add(statusDot);
        grid.Children.Add(infoStack);
        card.Child = grid;

        // Click to focus on canvas
        card.MouseLeftButtonDown += (s, e) =>
        {
            if (_postControls.TryGetValue(data.PostCode, out var postControl))
            {
                var x = Canvas.GetLeft(postControl);
                var y = Canvas.GetTop(postControl);
                CanvasScrollViewer.ScrollToHorizontalOffset(x - 200);
                CanvasScrollViewer.ScrollToVerticalOffset(y - 200);
            }
        };

        PostsListPanel.Children.Add(card);
    }

    private void Post_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ProductionPostControl post)
        {
            _draggedElement = post;
            _dragStartPoint = e.GetPosition(FlowCanvas);
            _isDragging = false;
            post.CaptureMouse();
            e.Handled = true;
        }
    }

    private void ShowPostDetails(ProductionPostControl post)
    {
        var postData = _dataService.Posts.FirstOrDefault(p => p.PostCode == post.PostCode);
        if (postData == null) return;

        var details = $"Production Post Details\n\n" +
                     $"Code: {postData.PostCode}\n" +
                     $"Name: {postData.PostName}\n" +
                     $"Line: {postData.LineId}\n" +
                     $"Position: {postData.Position}\n\n" +
                     $"Status: {postData.Status}\n" +
                     $"Stock Capacity: {postData.StockCapacity}\n" +
                     $"Current Stock: {postData.CurrentLoad}\n" +
                     $"Utility Time (TU): {postData.UtilityTimeSeconds}s\n" +
                     $"Material Level: {postData.MaterialLevel}%\n\n" +
                     $"Maintenance Health: {postData.MaintenanceHealthScore:F1}%\n" +
                     $"Issue: {postData.MaintenanceIssue}";
        
        MessageBox.Show(details, "Post Details", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Post_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggedElement != null && e.LeftButton == MouseButtonState.Pressed)
        {
            Point currentPosition = e.GetPosition(FlowCanvas);
            Vector offset = currentPosition - _dragStartPoint;

            if (!_isDragging && (Math.Abs(offset.X) > 5 || Math.Abs(offset.Y) > 5))
            {
                _isDragging = true;
            }

            if (_isDragging)
            {
                // Find the container for this post
                var postCode = (_draggedElement as ProductionPostControl)?.PostCode;
                var container = FlowCanvas.Children.OfType<Grid>().FirstOrDefault(g => g.Tag?.ToString() == postCode);
                
                if (container != null)
                {
                    double newLeft = Canvas.GetLeft(container) + offset.X;
                    double newTop = Canvas.GetTop(container) + offset.Y;

                    // Keep within bounds
                    newLeft = Math.Max(0, Math.Min(newLeft, FlowCanvas.Width - 180));
                    newTop = Math.Max(0, Math.Min(newTop, FlowCanvas.Height - 120));

                    Canvas.SetLeft(container, newLeft);
                    Canvas.SetTop(container, newTop);

                    _dragStartPoint = currentPosition;
                    UpdateConnectionLines();
                }
            }
        }
    }

    private void Post_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggedElement != null)
        {
            // If we didn't drag, show details on click
            if (!_isDragging && sender is ProductionPostControl post)
            {
                if (e.ClickCount >= 2)
                {
                    EditPost(post);
                }
                else
                {
                    ShowPostDetails(post);
                }
            }
            // Sync position back to data service
            else if (_draggedElement is ProductionPostControl draggedPost)
            {
                // Find the container for this post
                var container = FlowCanvas.Children.OfType<Grid>().FirstOrDefault(g => g.Tag?.ToString() == draggedPost.PostCode);
                
                if (container != null)
                {
                    var x = Canvas.GetLeft(container);
                    var y = Canvas.GetTop(container);
                    _dataService.UpdatePost(draggedPost.PostCode, p => 
                    { 
                        p.X = x; 
                        p.Y = y; 
                    });
                }
            }
            
            _draggedElement.ReleaseMouseCapture();
            _draggedElement = null;
            _isDragging = false;
        }
    }

    private void Post_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is ProductionPostControl post)
        {
            var contextMenu = new ContextMenu();
            
            var viewDetailsItem = new MenuItem { Header = "View Details" };
            viewDetailsItem.Click += (s, args) => ShowPostDetails(post);
            contextMenu.Items.Add(viewDetailsItem);
            
            contextMenu.Items.Add(new Separator());
            
            var editMenuItem = new MenuItem { Header = "Edit Post" };
            editMenuItem.Click += (s, args) => EditPost(post);
            contextMenu.Items.Add(editMenuItem);
            
            var deleteMenuItem = new MenuItem { Header = "Delete Post" };
            deleteMenuItem.Click += (s, args) => DeletePost(post);
            contextMenu.Items.Add(deleteMenuItem);
            
            contextMenu.IsOpen = true;
            contextMenu.PlacementTarget = post;
            contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        }
    }

    private void EditPost(ProductionPostControl post)
    {
        try
        {
            var postData = _dataService.GetPost(post.PostCode);
            if (postData == null) return;

            var dialog = new AddProductionPostDialog
            {
                Owner = Window.GetWindow(this)
            };

            dialog.LoadFrom(postData);
            dialog.ConfigureForEdit();

            if (dialog.ShowDialog() == true)
            {
                _dataService.UpdatePost(postData.PostCode, p =>
                {
                    p.PostName = dialog.PostName;
                    p.CurrentLoad = dialog.CurrentLoad;
                    p.MaterialLevel = dialog.MaterialLevel;
                    p.UtilityTimeSeconds = dialog.UtilityTimeSeconds;
                    p.StockCapacity = dialog.StockCapacity;
                    p.Status = dialog.Status;
                    p.X = dialog.XPosition;
                    p.Y = dialog.YPosition;
                });

                var container = FlowCanvas.Children.OfType<Grid>().FirstOrDefault(g => g.Tag?.ToString() == postData.PostCode);
                if (container != null)
                {
                    Canvas.SetLeft(container, postData.X);
                    Canvas.SetTop(container, postData.Y);
                    UpdateConnectionLines();
                }

                if (_allLinesMode)
                {
                    RenderAllLinesView();
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error editing post: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeletePost(ProductionPostControl post)
    {
        var result = MessageBox.Show(
            $"Are you sure you want to delete {post.PostCode} - {post.PostName}?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _dataService.RemovePost(post.PostCode);
            
            if (_postControls.ContainsKey(post.PostCode))
            {
                var container = FlowCanvas.Children.OfType<Grid>().FirstOrDefault(g => g.Tag?.ToString() == post.PostCode);
                if (container != null)
                {
                    FlowCanvas.Children.Remove(container);
                }
                _postControls.Remove(post.PostCode);
                UpdateConnectionLines();
                UpdateStatistics();
            }
        }
    }

    private void FlowCanvas_Drop(object sender, DragEventArgs e)
    {
        // Handle drop from sidebar
    }

    private void FlowCanvas_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void FlowCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isPanning && e.LeftButton == MouseButtonState.Pressed)
        {
            Point currentPosition = e.GetPosition(CanvasScrollViewer);
            double deltaX = _panStart.X - currentPosition.X;
            double deltaY = _panStart.Y - currentPosition.Y;

            CanvasScrollViewer.ScrollToHorizontalOffset(CanvasScrollViewer.HorizontalOffset + deltaX);
            CanvasScrollViewer.ScrollToVerticalOffset(CanvasScrollViewer.VerticalOffset + deltaY);

            _panStart = currentPosition;
        }
    }

    private void FlowCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Only pan if clicking directly on canvas (not on a child element like a post)
        if (e.OriginalSource == FlowCanvas || e.OriginalSource == sender)
        {
            _isPanning = true;
            _panStart = e.GetPosition(CanvasScrollViewer);
            FlowCanvas.Cursor = Cursors.Hand;
            FlowCanvas.CaptureMouse();
            e.Handled = true;
        }
    }

    private void FlowCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            FlowCanvas.Cursor = Cursors.Arrow;
            FlowCanvas.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void UpdateConnectionLines()
    {
        try
        {
            // Connection lines are drawn on the dedicated overlay canvas.
            ConnectionLinesCanvas.Children.Clear();

            // Get posts ordered by position for the selected line
            var postsData = _selectedLineId == null 
                ? _dataService.Posts.OrderBy(p => p.Position).ToList()
                : _dataService.GetPostsForLine(_selectedLineId).OrderBy(p => p.Position).ToList();
            
            System.Diagnostics.Debug.WriteLine($"UpdateConnectionLines: Found {postsData.Count} posts for line {_selectedLineId ?? "GLOBAL"}");
            
            if (postsData.Count < 2)
            {
                System.Diagnostics.Debug.WriteLine("Not enough posts to draw lines (need at least 2)");
                return;
            }

            for (int i = 0; i < postsData.Count - 1; i++)
            {
                var startPostData = postsData[i];
                var endPostData = postsData[i + 1];

                // Get the container elements from canvas
                var allContainers = FlowCanvas.Children.OfType<Grid>().ToList();
                System.Diagnostics.Debug.WriteLine($"Total containers on canvas: {allContainers.Count}");
                
                var startContainer = allContainers.FirstOrDefault(g => g.Tag?.ToString() == startPostData.PostCode);
                var endContainer = allContainers.FirstOrDefault(g => g.Tag?.ToString() == endPostData.PostCode);

                if (startContainer == null || endContainer == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Container not found: Start={startPostData.PostCode}({startContainer != null}), End={endPostData.PostCode}({endContainer != null})");
                    continue;
                }
                
                var startLeft = Canvas.GetLeft(startContainer);
                var startTop = Canvas.GetTop(startContainer);
                var endLeft = Canvas.GetLeft(endContainer);
                var endTop = Canvas.GetTop(endContainer);
                
                // Check for NaN values
                if (double.IsNaN(startLeft) || double.IsNaN(startTop) || double.IsNaN(endLeft) || double.IsNaN(endTop))
                {
                    System.Diagnostics.Debug.WriteLine($"NaN coordinates detected: Start({startLeft},{startTop}) End({endLeft},{endTop})");
                    continue;
                }

                var startX = startLeft + 180; // Right edge of post
                var startY = startTop + 60;   // Middle height
                var endX = endLeft;           // Left edge of next post
                var endY = endTop + 60;       // Middle height
                
                System.Diagnostics.Debug.WriteLine($"Drawing line from {startPostData.PostCode} ({startX},{startY}) to {endPostData.PostCode} ({endX},{endY})");

                var line = new Line
                {
                    X1 = startX,
                    Y1 = startY,
                    X2 = endX,
                    Y2 = endY,
                    Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3")),
                    StrokeThickness = 3
                };

                // Add arrow head
                var arrowSize = 10;
                var angle = Math.Atan2(endY - startY, endX - startX);
                var arrowPoint1 = new Point(
                    endX - arrowSize * Math.Cos(angle - Math.PI / 6),
                    endY - arrowSize * Math.Sin(angle - Math.PI / 6)
                );
                var arrowPoint2 = new Point(
                    endX - arrowSize * Math.Cos(angle + Math.PI / 6),
                    endY - arrowSize * Math.Sin(angle + Math.PI / 6)
                );

                var arrow = new Polygon
                {
                    Points = new PointCollection { new Point(endX, endY), arrowPoint1, arrowPoint2 },
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3"))
                };

                // Set Z-index lower than posts (which are at 10)
                Canvas.SetZIndex(line, 1);
                Canvas.SetZIndex(arrow, 1);

                ConnectionLinesCanvas.Children.Add(line);
                ConnectionLinesCanvas.Children.Add(arrow);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating connection lines: {ex.Message}");
            MessageBox.Show($"Error drawing connection lines: {ex.Message}", "Debug", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void UpdateStatistics()
    {
        var posts = _dataService.Posts;
        var activePosts = posts.Count(p => p.Status == "Active");
        var totalCapacity = posts.Sum(p => p.StockCapacity);
        var totalLoad = posts.Sum(p => p.CurrentLoad);
        var loadPercentage = totalCapacity > 0 ? (totalLoad * 100.0 / totalCapacity) : 0;

        var finished = _allLinesMode
            ? _dataService.ProductionLines.Sum(l => l.FinishedProductCount)
            : _dataService.GetProductionLine(_selectedLineId ?? "")?.FinishedProductCount ?? 0;

        ActivePostsCount.Text = activePosts.ToString();
        TotalLoadText.Text = $"{loadPercentage:F0}%";
        FinishedProductsText.Text = finished.ToString();
    }

    #endregion

    #region Kanban Board - Drag and Drop
    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new AddKanbanTaskDialog
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                var orderNumber = dialog.TaskTitle.Trim();
                var quantity = Math.Max(1, dialog.EstimatedHours);
                var payload = new KanbanOfPayload { OrderNumber = orderNumber, Quantity = quantity, Produced = 0 };

                var task = CreateKanbanTask(
                    orderNumber,
                    $"Qty: {quantity}",
                    dialog.Status,
                    dialog.Priority,
                    dialog.AssignedTo,
                    estimatedHours: quantity
                );

                task.Tag = payload;
                task.SetProgress(payload.Produced, payload.Quantity);
                
                _tasks.Add(task);

                var column = dialog.Status switch
                {
                    "Backlog" => BacklogColumn,
                    "ToDo" => ToDoColumn,
                    "InProgress" => InProgressColumn,
                    "QualityCheck" => QualityCheckColumn,
                    "Done" => DoneColumn,
                    _ => BacklogColumn
                };

                column.Children.Add(task);
                UpdateKanbanCounts();

                // If a task is created directly in InProgress, show the Launch OF page.
                if (ReferenceEquals(column, InProgressColumn) && task.Tag is KanbanOfPayload p)
                {
                    _ = Dispatcher.InvokeAsync(async () =>
                    {
                        if (_isOfRunning && !_isOfPaused)
                        {
                            MessageBox.Show("An OF is already running. Stop it first.", "Launch OF", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }

                        await PromptAndLaunchOfFromKanbanAsync(task, p);
                    });
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error adding task: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TryAutoLaunchKanbanCard(KanbanTaskCard card, KanbanOfPayload payload)
    {
        if (card == null || payload == null) return;

        if (_allLinesMode || string.IsNullOrWhiteSpace(_selectedLineId))
        {
            MessageBox.Show("Select a production line before launching an OF.", "Launch OF", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            _ = LaunchOfFromKanbanAsync(card, payload);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to launch OF: {ex.Message}", "Launch OF", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private KanbanTaskCard CreateKanbanTask(string taskNumber, string title, string status, 
        int priority = 2, string assignedTo = "Unassigned", int estimatedHours = 4)
    {
        var card = new KanbanTaskCard
        {
            TaskNumber = taskNumber,
            TaskTitle = title,
            TaskStatus = status,
            Priority = priority,
            AssignedTo = assignedTo,
            EstimatedHours = estimatedHours,
            Margin = new Thickness(0, 0, 0, 10)
        };

        card.MouseLeftButtonDown += TaskCard_MouseLeftButtonDown;
        card.MouseMove += TaskCard_MouseMove;
        card.MouseLeftButtonUp += TaskCard_MouseLeftButtonUp;
        card.MouseDoubleClick += TaskCard_MouseDoubleClick;

        return card;
    }

    private void TaskCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is KanbanTaskCard card)
        {
            SetSelectedKanbanCard(card);
            _draggedElement = card;
            _dragStartPoint = e.GetPosition(null);
            card.CaptureMouse();
            card.Opacity = 0.7;
            Panel.SetZIndex(card, 1000);
            e.Handled = true;
        }
    }

    private void SetSelectedKanbanCard(KanbanTaskCard? card)
    {
        try
        {
            if (!ReferenceEquals(_selectedKanbanCard, card))
            {
                _selectedKanbanCard?.SetSelected(false);
            }

            _selectedKanbanCard = card;
            _selectedKanbanCard?.SetSelected(true);
        }
        catch
        {
            // ignore
        }
    }

    private async void KanbanContinueSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // If the current OF is paused, this button resumes it.
            if (_isOfPaused)
            {
                // If the OF is still running, resume in-place.
                if (_isOfRunning)
                {
                    _isOfPaused = false;
                    await TrySendOfSignalAsync(cmd: "start", cancellationToken: CancellationToken.None);
                    Dispatcher.Invoke(() => { OfStatusText.Text = "OF resumed (start signal sent)."; });
                    return;
                }

                // Stale pause flag (no run active): clear it and fall through to normal continue.
                _isOfPaused = false;
            }

            if (_isOfRunning)
            {
                MessageBox.Show("An OF is already running. Stop it first.", "Continue", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var card = _selectedKanbanCard;
            if (card == null)
            {
                MessageBox.Show("Select an InProgress card first.", "Continue", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var parent = card.Parent as Panel;
            if (!ReferenceEquals(parent, InProgressColumn))
            {
                MessageBox.Show("Only cards in InProgress can be continued. Drag it to InProgress first.", "Continue", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (card.Tag is not KanbanOfPayload payload)
            {
                MessageBox.Show("This card has no OF payload.", "Continue", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await LaunchOfFromKanbanAsync(card, payload);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to continue: {ex.Message}", "Continue", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TaskCard_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggedElement is KanbanTaskCard card && e.LeftButton == MouseButtonState.Pressed)
        {
            Point currentPosition = e.GetPosition(null);
            Vector offset = currentPosition - _dragStartPoint;

            if (Math.Abs(offset.X) > 5 || Math.Abs(offset.Y) > 5)
            {
                DragDrop.DoDragDrop(card, card, DragDropEffects.Move);
            }
        }
    }

    private void TaskCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggedElement is KanbanTaskCard card)
        {
            card.ReleaseMouseCapture();
            card.Opacity = 1.0;
            Panel.SetZIndex(card, 0);
            _draggedElement = null;
        }
    }

    private async void TaskCard_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not KanbanTaskCard card) return;
        if (e.ChangedButton != MouseButton.Left) return;

        // Explicit launch: double-click a card in InProgress.
        var parent = card.Parent as Panel;
        if (!ReferenceEquals(parent, InProgressColumn)) return;

        if (card.Tag is not KanbanOfPayload payload) return;

        if (_allLinesMode || string.IsNullOrWhiteSpace(_selectedLineId))
        {
            MessageBox.Show("Select a production line before launching an OF.", "Launch OF", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            await LaunchOfFromKanbanAsync(card, payload);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to launch OF: {ex.Message}", "Launch OF", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void KanbanColumn_Drop(object sender, DragEventArgs e)
    {
        if (e.Handled) return;

        var targetColumn = sender switch
        {
            StackPanel sp => sp,
            ScrollViewer sv when sv.Content is StackPanel sp => sp,
            _ => null
        };

        if (e.Data.GetData(typeof(KanbanTaskCard)) is KanbanTaskCard card && targetColumn != null)
        {
            // Remove from old column
            var oldColumn = card.Parent as Panel;
            var oldStatus = card.TaskStatus;
            oldColumn?.Children.Remove(card);

            // Add to new column
            targetColumn.Children.Add(card);

            // Update status
            card.TaskStatus = targetColumn.Name.Replace("Column", "");

            card.Opacity = 1.0;
            Panel.SetZIndex(card, 0);

            UpdateKanbanCounts();

            // Dragging into InProgress should prompt the OF launch page.
            if (ReferenceEquals(targetColumn, InProgressColumn) && card.Tag is KanbanOfPayload payload)
            {
                // If already running (and not paused), don't prompt another launch.
                if (_isOfRunning && !_isOfPaused)
                {
                    MessageBox.Show("An OF is already running. Stop it first.", "Launch OF", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                try
                {
                    // Prevent duplicated prompts when Drop bubbles from inner panel to ScrollViewer.
                    lock (_kanbanPromptingCards)
                    {
                        if (_kanbanPromptingCards.Contains(card))
                        {
                            e.Handled = true;
                            return;
                        }
                        _kanbanPromptingCards.Add(card);
                    }

                    await PromptAndLaunchOfFromKanbanAsync(card, payload);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to launch OF: {ex.Message}", "Launch OF", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    lock (_kanbanPromptingCards)
                    {
                        _kanbanPromptingCards.Remove(card);
                    }
                }
            }

            e.Handled = true;
        }
    }

    private void KanbanColumn_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void UpdateKanbanCounts()
    {
        BacklogCount.Text = $"{BacklogColumn.Children.Count} tasks";
        ToDoCount.Text = $"{ToDoColumn.Children.Count} tasks";
        InProgressCount.Text = $"{InProgressColumn.Children.Count} tasks";
        QualityCheckCount.Text = $"{QualityCheckColumn.Children.Count} tasks";
        DoneCount.Text = $"{DoneColumn.Children.Count} tasks";
    }

    #endregion

    #region Data Loading

    private void LoadExistingPosts()
    {
        try
        {
            if (_allLinesMode)
            {
                RenderAllLinesView();
                return;
            }
            // Clear existing posts
            // IMPORTANT: FlowCanvas contains XAML-defined overlay layers.
            // Keep them and remove only dynamically-added children (posts/lines/arrows).
            var keep = new HashSet<UIElement>();
            if (ConnectionLinesCanvas != null) keep.Add(ConnectionLinesCanvas);
            if (PieceMarkersCanvas != null) keep.Add(PieceMarkersCanvas);

            var toRemove = FlowCanvas.Children.Cast<UIElement>()
                .Where(c => !keep.Contains(c))
                .ToList();

            foreach (var child in toRemove)
            {
                FlowCanvas.Children.Remove(child);
            }

            ClearHighlight();
            ClearPieceMarkers();
            PostsListPanel.Children.Clear();
            _postControls.Clear();

            // Filter posts by selected line
            var postsToLoad = _selectedLineId == null 
                ? _dataService.Posts.OrderBy(p => p.Position).ToList()
                : _dataService.GetPostsForLine(_selectedLineId).OrderBy(p => p.Position).ToList();

            // If posts don't have proper X positions, arrange them
            if (postsToLoad.Any() && postsToLoad.All(p => p.X <= 0))
            {
                const double startX = 100;
                const double startY = 300;
                const double horizontalSpacing = 250;

                for (int i = 0; i < postsToLoad.Count; i++)
                {
                    var post = postsToLoad[i];
                    _dataService.UpdatePost(post.PostCode, p => 
                    { 
                        p.X = startX + (i * horizontalSpacing); 
                        p.Y = startY; 
                    });
                }
            }

            // Load posts from shared data service
            foreach (var postData in postsToLoad)
            {
                try
                {
                    var postControl = CreateProductionPostFromData(postData);
                    _postControls[postData.PostCode] = postControl;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error creating post {postData.PostCode}: {ex.Message}\n\n{ex.StackTrace}", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    throw;
                }
            }

            // Force layout update before drawing lines
            FlowCanvas.UpdateLayout();
            
            // Delay line drawing to ensure containers are fully rendered
            Dispatcher.InvokeAsync(() =>
            {
                UpdateConnectionLines();
            }, System.Windows.Threading.DispatcherPriority.Loaded);
            
            UpdateStatistics();

            // No mock Kanban tasks (real data only)
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading posts: {ex.Message}\n\n{ex.StackTrace}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RenderAllLinesView()
    {
        AllLinesStack.Children.Clear();

        foreach (var line in _dataService.ProductionLines)
        {
            var lineBorder = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var stack = new StackPanel();

            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            headerGrid.Children.Add(new TextBlock
            {
                Text = $"{line.LineName} ({line.LineId})",
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(33, 33, 33))
            });

            var openLineButton = new Button
            {
                Content = "Open Line",
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(10, 0, 0, 0),
                Style = (Style)FindResource("MaterialDesignOutlinedButton")
            };
            openLineButton.Click += (s, e) => SelectLine(line.LineId);
            Grid.SetColumn(openLineButton, 1);
            headerGrid.Children.Add(openLineButton);

            stack.Children.Add(headerGrid);

            var posts = _dataService.GetPostsForLine(line.LineId).ToList();
            if (!posts.Any())
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "No posts yet. Use Add Post after selecting this line.",
                    Foreground = new SolidColorBrush(Color.FromRgb(117, 117, 117)),
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 0, 8)
                });
            }
            else
            {
                foreach (var post in posts)
                {
                    var postCard = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(10),
                        Margin = new Thickness(0, 0, 0, 6)
                    };

                    postCard.MouseLeftButtonDown += (s, e) =>
                    {
                        if (e.ClickCount >= 2)
                        {
                            EditPostByCode(post.PostCode);
                            e.Handled = true;
                        }
                    };

                    postCard.MouseRightButtonUp += (s, e) =>
                    {
                        var contextMenu = new ContextMenu();

                        var viewDetailsItem = new MenuItem { Header = "View Details" };
                        viewDetailsItem.Click += (s2, e2) => ShowPostDetailsByCode(post.PostCode);
                        contextMenu.Items.Add(viewDetailsItem);

                        contextMenu.Items.Add(new Separator());

                        var editItem = new MenuItem { Header = "Edit Post" };
                        editItem.Click += (s2, e2) => EditPostByCode(post.PostCode);
                        contextMenu.Items.Add(editItem);

                        var deleteItem = new MenuItem { Header = "Delete Post" };
                        deleteItem.Click += (s2, e2) => DeletePostByCode(post.PostCode);
                        contextMenu.Items.Add(deleteItem);

                        contextMenu.PlacementTarget = postCard;
                        contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                        contextMenu.IsOpen = true;
                        e.Handled = true;
                    };

                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    grid.Children.Add(new Ellipse
                    {
                        Width = 10,
                        Height = 10,
                        Fill = post.Status == "Active"
                            ? new SolidColorBrush(Color.FromRgb(34, 197, 94))
                            : new SolidColorBrush(Color.FromRgb(248, 180, 0)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 8, 0)
                    });

                    var textStack = new StackPanel();
                    textStack.Children.Add(new TextBlock { Text = post.PostCode, FontWeight = FontWeights.SemiBold, FontSize = 12 });
                    textStack.Children.Add(new TextBlock { Text = post.PostName, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)) });
                    textStack.Children.Add(new TextBlock { Text = $"Stock {post.CurrentLoad}/{post.StockCapacity} | TU {post.UtilityTimeSeconds}s", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)) });
                    Grid.SetColumn(textStack, 1);
                    grid.Children.Add(textStack);

                    postCard.Child = grid;
                    stack.Children.Add(postCard);
                }
            }

            lineBorder.Child = stack;
            AllLinesStack.Children.Add(lineBorder);
        }
    }

    private void SelectLine(string lineId)
    {
        for (int i = 0; i < ProductionLineSelector.Items.Count; i++)
        {
            if (ProductionLineSelector.Items[i] is ComboBoxItem item
                && string.Equals(item.Tag?.ToString(), lineId, StringComparison.OrdinalIgnoreCase))
            {
                ProductionLineSelector.SelectedIndex = i;
                return;
            }
        }
    }

    private void ShowPostDetailsByCode(string postCode)
    {
        var postControl = new ProductionPostControl { PostCode = postCode };
        ShowPostDetails(postControl);
    }

    private void EditPostByCode(string postCode)
    {
        var postControl = new ProductionPostControl { PostCode = postCode };
        EditPost(postControl);
    }

    private void DeletePostByCode(string postCode)
    {
        var postControl = new ProductionPostControl { PostCode = postCode };
        DeletePost(postControl);
    }

    private void OnPostAdded(object? sender, ProductionPostData postData)
    {
        Dispatcher.Invoke(() =>
        {
            var postControl = CreateProductionPostFromData(postData);
            _postControls[postData.PostCode] = postControl;
            UpdateConnectionLines();
            UpdateStatistics();

            if (_allLinesMode)
            {
                RenderAllLinesView();
            }
        });
    }

    private void OnPostUpdated(object? sender, ProductionPostData postData)
    {
        Dispatcher.Invoke(() =>
        {
            if (_postControls.TryGetValue(postData.PostCode, out var postControl))
            {
                postControl.PostName = postData.PostName;
                postControl.StockCapacity = postData.StockCapacity;
                postControl.MaintenanceHealthScore = postData.MaintenanceHealthScore;
                postControl.MaintenanceIssue = postData.MaintenanceIssue;
                postControl.Status = postData.Status;
                postControl.CurrentLoad = postData.CurrentLoad;
                postControl.MaterialLevel = postData.MaterialLevel;
                postControl.IsStockBlocked = _stockBlockedPosts.Contains(postData.PostCode);
            }

            UpdateStatistics();

            if (_allLinesMode)
            {
                RenderAllLinesView();
            }
        });
    }

    #endregion

    #region Zoom and Pan

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        _currentZoom = Math.Min(_currentZoom * 1.2, 3.0);
        CanvasScaleTransform.ScaleX = _currentZoom;
        CanvasScaleTransform.ScaleY = _currentZoom;
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        _currentZoom = Math.Max(_currentZoom / 1.2, 0.3);
        CanvasScaleTransform.ScaleX = _currentZoom;
        CanvasScaleTransform.ScaleY = _currentZoom;
    }

    private void ResetZoom_Click(object sender, RoutedEventArgs e)
    {
        _currentZoom = 1.0;
        CanvasScaleTransform.ScaleX = 1.0;
        CanvasScaleTransform.ScaleY = 1.0;
        CanvasScrollViewer.ScrollToHorizontalOffset(0);
        CanvasScrollViewer.ScrollToVerticalOffset(0);
    }

    private void CanvasScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Zoom with Ctrl key only
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Delta > 0)
            {
                _currentZoom = Math.Min(_currentZoom * 1.1, 3.0);
            }
            else
            {
                _currentZoom = Math.Max(_currentZoom / 1.1, 0.5);
            }
            CanvasScaleTransform.ScaleY = _currentZoom;
            e.Handled = true;
        }
        // Otherwise allow normal scroll behavior
    }

    #endregion
}
