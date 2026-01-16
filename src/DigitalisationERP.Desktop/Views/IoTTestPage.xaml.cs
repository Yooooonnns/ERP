using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DigitalisationERP.Desktop.Services;
using DigitalisationERP.Desktop.Services.IoT;

namespace DigitalisationERP.Desktop.Views
{
    public partial class IoTTestPage : UserControl
    {
        private LocalLogService? _logService;
        private readonly ProductionFlowSimulator _productionFlowSimulator = new(ProductionDataService.Instance);
        private CancellationTokenSource? _productionFlowCts;
        private TaskCompletionSource<bool>? _firstSensorTcs;

        private BluetoothSerialClient? _testSerialClient;
        private string? _testSerialPort;
        private int _testSerialBaud;
        private string? _testSerialNewLine;
        
        // Collections pour affichage
        public ObservableCollection<OutputEventItem> OutputEvents { get; set; }
        
        // Compteurs
        private int _inputCount = 0;
        private int _outputCount = 0;
        private int _robotCommandCount = 0;
        private int _warningCount = 0;
        private int _criticalCount = 0;

        public IoTTestPage()
        {
            InitializeComponent();
            
            OutputEvents = new ObservableCollection<OutputEventItem>();
            OutputListBox.ItemsSource = OutputEvents;
            
            Loaded += IoTTestPage_Loaded;
            Unloaded += IoTTestPage_Unloaded;
        }

        private void IoTTestPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Récupérer les services depuis MainWindow
            var mainWindow = Window.GetWindow(this) as MainWindow;
            _logService = mainWindow?.GetLogService();

            SimulateFirstSensorButton.IsEnabled = false;

            _ = RefreshSerialPortsAsync();
            UpdateSerialConnectionUi();
        }

        private void IoTTestPage_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Do not force-disconnect Bluetooth when navigating away.
                // The shared COM client is used by Production/Kanban to send OF start/end signals.
                if (_testSerialClient != null)
                {
                    _testSerialClient.LineReceived -= TestSerialClient_LineReceived;
                }
            }
            catch
            {
                // ignore
            }
        }

        private async Task RefreshSerialPortsAsync()
        {
            try
            {
                if (SerialPortComboBox == null)
                {
                    return;
                }

                var detected = SerialPort.GetPortNames();

                var mainWindow = Window.GetWindow(this) as MainWindow;
                var configService = mainWindow?.GetConfigService();
                var cfg = configService != null ? await configService.GetConfigurationAsync() : null;
                var preferred = cfg?.Bluetooth?.ComPort;

                var manual = NormalizeComPort(ManualPortTextBox?.Text);
                var selected = SerialPortComboBox.SelectedItem as string;

                var all = new List<string>(capacity: 16);
                if (detected != null) all.AddRange(detected);
                if (!string.IsNullOrWhiteSpace(preferred)) all.Add(preferred);
                if (!string.IsNullOrWhiteSpace(selected)) all.Add(selected);
                if (!string.IsNullOrWhiteSpace(_testSerialPort)) all.Add(_testSerialPort);
                if (!string.IsNullOrWhiteSpace(manual)) all.Add(manual);

                var ports = all
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                SerialPortComboBox.ItemsSource = ports;

                if (!string.IsNullOrWhiteSpace(preferred) && ports.Any(p => string.Equals(p, preferred, StringComparison.OrdinalIgnoreCase)))
                {
                    SerialPortComboBox.SelectedItem = ports.First(p => string.Equals(p, preferred, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    SerialPortComboBox.SelectedItem = ports.FirstOrDefault();
                }
            }
            catch
            {
                // ignore UI refresh errors
            }
        }

        private static string? NormalizeComPort(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var trimmed = text.Trim();
            if (trimmed.Length == 0) return null;

            // Accept inputs like "3" or "COM3" or "com 3".
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

        private async void UseManualPortButton_Click(object sender, RoutedEventArgs e)
        {
            if (SerialPortComboBox == null)
            {
                MessageBox.Show("ComboBox port non disponible", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var port = NormalizeComPort(ManualPortTextBox?.Text);
            if (string.IsNullOrWhiteSpace(port))
            {
                MessageBox.Show("Port manuel vide (ex: COM3)", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await RefreshSerialPortsAsync();

            try
            {
                var ports = SerialPortComboBox.ItemsSource as IEnumerable<string>;
                if (ports != null && ports.Any(p => string.Equals(p, port, StringComparison.OrdinalIgnoreCase)))
                {
                    SerialPortComboBox.SelectedItem = ports.First(p => string.Equals(p, port, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    // If ItemsSource wasn't enumerable (or refresh failed), still try to select by setting text.
                    SerialPortComboBox.Text = port;
                }
            }
            catch
            {
                SerialPortComboBox.Text = port;
            }

            AddOutputEvent(
                type: "SERIAL",
                title: "Port selected",
                message: $"Using {port}",
                borderColor: "#475569",
                eventType: "SYSTEM",
                typeColor: "#94a3b8");
        }

        private async void RefreshPortsButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshSerialPortsAsync();
        }

        private void UpdateSerialConnectionUi()
        {
            if (SerialStatusText == null || ConnectSerialButton == null)
            {
                return;
            }

            if (_testSerialClient?.IsOpen == true)
            {
                var portLabel = string.IsNullOrWhiteSpace(_testSerialPort) ? "" : $" ({_testSerialPort})";
                SerialStatusText.Text = $"Connected{portLabel}";
                ConnectSerialButton.Content = "⛔ Disconnect";
                ConnectSerialButton.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            }
            else
            {
                SerialStatusText.Text = "Disconnected";
                ConnectSerialButton.Content = "🔌 Connect";
                ConnectSerialButton.Background = new SolidColorBrush(Color.FromRgb(14, 165, 233));
            }
        }

        private void DisconnectTestSerial()
        {
            try
            {
                if (_testSerialClient != null)
                {
                    _testSerialClient.LineReceived -= TestSerialClient_LineReceived;
                }
            }
            catch
            {
                // ignore
            }

            try
            {
                // Do not dispose here: keep COM connection persistent across pages.
            }
            catch
            {
                // ignore
            }

            _testSerialClient = null;
            _testSerialPort = null;
            _testSerialBaud = 0;
            _testSerialNewLine = null;

            UpdateSerialConnectionUi();
        }

        private void TestSerialClient_LineReceived(object? sender, string line)
        {
            try
            {
                Dispatcher.InvokeAsync(() =>
                {
                    AddOutputEvent(
                        type: "SERIAL",
                        title: "RX",
                        message: "Received line",
                        borderColor: "#10b981",
                        eventType: "INPUT",
                        typeColor: "#10b981",
                        payload: line);
                });
            }
            catch
            {
                // ignore
            }
        }

        private async void ConnectSerialButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            var configService = mainWindow?.GetConfigService();

            try
            {
                var cfg = configService != null ? await configService.GetConfigurationAsync() : null;

                var manual = NormalizeComPort(ManualPortTextBox?.Text);
                var selectedPort = SerialPortComboBox?.SelectedItem as string;
                var port = !string.IsNullOrWhiteSpace(manual)
                    ? manual
                    : (!string.IsNullOrWhiteSpace(selectedPort) ? selectedPort : (cfg?.Bluetooth?.ComPort ?? "COM3"));

                var baud = cfg?.Bluetooth?.BaudRate ?? 9600;
                var newLine = cfg?.Bluetooth?.NewLine ?? "\n";

                // Use the shared MainWindow client so it stays connected for Kanban/OF.
                if (mainWindow != null)
                {
                    var result = await mainWindow.EnsureBluetoothSerialConnectedAsync(portOverride: port);
                    if (!result.Success)
                    {
                        throw new InvalidOperationException(result.Error ?? "Connect failed");
                    }
                }

                _testSerialPort = port;
                _testSerialBaud = baud;
                _testSerialNewLine = newLine;

                _testSerialClient = mainWindow?.GetBluetoothSerialClient();
                if (_testSerialClient != null)
                {
                    _testSerialClient.LineReceived -= TestSerialClient_LineReceived;
                    _testSerialClient.LineReceived += TestSerialClient_LineReceived;
                }

                UpdateSerialConnectionUi();

                AddOutputEvent(
                    type: "SERIAL",
                    title: "Connected",
                    message: $"Connected shared client on {port} @ {baud}.",
                    borderColor: "#0ea5e9",
                    eventType: "SYSTEM",
                    typeColor: "#0ea5e9");
            }
            catch (Exception ex)
            {
                DisconnectTestSerial();
                AddOutputEvent(
                    type: "SERIAL",
                    title: "Connect failed",
                    message: ex.Message,
                    borderColor: "#ef4444",
                    eventType: "ERROR",
                    typeColor: "#ef4444");
            }
        }
        #region PRODUCTION FLOW SIMULATION

        private async void SimulateOrderButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(QuantityTextBox.Text, out var quantity) || quantity <= 0)
            {
                MessageBox.Show("Quantité invalide", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TuSecondsTextBox.Text, out var transitSeconds) || transitSeconds < 0)
            {
                MessageBox.Show("Transit (secondes) invalide", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(FinishedStockTextBox.Text, out var finishedStockStart))
            {
                finishedStockStart = 0;
            }

            var request = new ProductionFlowRequest
            {
                OrderNumber = string.IsNullOrWhiteSpace(OrderNumberTextBox.Text) ? "OF-0001" : OrderNumberTextBox.Text.Trim(),
                LineId = string.IsNullOrWhiteSpace(LineIdTextBox.Text) ? "LINE-A" : LineIdTextBox.Text.Trim(),
                Quantity = quantity,
                // Default TU used only if the line posts do not have per-post TU configured.
                TuSeconds = 10,
                TransitSeconds = transitSeconds,
                Route = ParseRoute(RouteTextBox.Text),
                FinishedStockStart = finishedStockStart
            };

            try
            {
                ApplyPostsConfigIfProvided(request);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Configuration postes (JSON) invalide: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SimulateOrderButton.IsEnabled = false;
            SimulateFirstSensorButton.IsEnabled = true;
            _productionFlowCts?.Cancel();
            _productionFlowCts = new CancellationTokenSource();
            _firstSensorTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                await _productionFlowSimulator.RunAsync(
                    request,
                    ev => Dispatcher.InvokeAsync(() =>
                    {
                        bool isOutput = ev.Direction.Equals("output", StringComparison.OrdinalIgnoreCase);
                        string color = isOutput ? "#06b6d4" : "#22c55e";

                        if (!isOutput)
                        {
                            _inputCount++;
                            InputCountText.Text = _inputCount.ToString();
                        }
                        else
                        {
                            _outputCount++;
                            OutputCountText.Text = _outputCount.ToString();
                            LastOutputTimeText.Text = DateTime.Now.ToString("HH:mm:ss");

                            if (ev.EventType.Equals("stock-low", StringComparison.OrdinalIgnoreCase))
                            {
                                _warningCount++;
                                WarningCountText.Text = _warningCount.ToString();
                            }
                            else if (ev.EventType.Equals("stock-out", StringComparison.OrdinalIgnoreCase))
                            {
                                _criticalCount++;
                                CriticalCountText.Text = _criticalCount.ToString();
                            }
                        }

                        AddOutputEvent(
                            isOutput ? "OF OUTPUT" : "OF INPUT",
                            ev.Message,
                            $"{request.OrderNumber} · {ev.EventType}",
                            color,
                            isOutput ? "OUTPUT" : "INPUT",
                            color,
                            ev.JsonPayload);
                    }).Task,
                            waitForFirstPostDetection: ct => _firstSensorTcs!.Task.WaitAsync(ct),
                            cancellationToken: _productionFlowCts.Token);
            }
            catch (OperationCanceledException)
            {
                AddOutputEvent(
                    "SYSTEM",
                    "Simulation OF annulée",
                    "Arrêt demandé",
                    "#f59e0b",
                    "CANCEL",
                    "#f59e0b");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur simulation OF: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _productionFlowCts?.Dispose();
                _productionFlowCts = null;
                _firstSensorTcs = null;
                SimulateOrderButton.IsEnabled = true;
                SimulateFirstSensorButton.IsEnabled = false;
            }
        }

        private void SimulateFirstSensorButton_Click(object sender, RoutedEventArgs e)
        {
            _firstSensorTcs?.TrySetResult(true);
            SimulateFirstSensorButton.IsEnabled = false;
        }

        private IReadOnlyList<string> ParseRoute(string? routeText)
        {
            if (string.IsNullOrWhiteSpace(routeText))
            {
                return new[] { "POST-01", "POST-02", "POST-03" };
            }

            return routeText
                .Split(new[] { ',', ';', '>' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToArray();
        }

        #endregion

        #region UI HELPERS

        private void ClearOutputButton_Click(object sender, RoutedEventArgs e)
        {
            OutputEvents.Clear();
            _outputCount = 0;
            _inputCount = 0;
            _robotCommandCount = 0;
            _warningCount = 0;
            _criticalCount = 0;
            OutputCountText.Text = "0";
            RobotCommandCountText.Text = "0";
            InputCountText.Text = "0";
            WarningCountText.Text = "0";
            CriticalCountText.Text = "0";
            LastOutputTimeText.Text = "--:--:--";
        }

        private async void SendSerialButton_Click(object sender, RoutedEventArgs e)
        {
            var payload = SerialPayloadTextBox?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(payload))
            {
                MessageBox.Show("Payload vide", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var mainWindow = Window.GetWindow(this) as MainWindow;
            var configService = mainWindow?.GetConfigService();
            if (configService == null)
            {
                AddOutputEvent(
                    type: "SERIAL",
                    title: "TX failed",
                    message: "ConfigService non disponible (MainWindow).",
                    borderColor: "#ef4444",
                    eventType: "ERROR",
                    typeColor: "#ef4444",
                    payload: payload);
                return;
            }

            try
            {
                var cfg = await configService.GetConfigurationAsync();
                var manual = NormalizeComPort(ManualPortTextBox?.Text);
                var selectedPort = SerialPortComboBox?.SelectedItem as string;
                var port = !string.IsNullOrWhiteSpace(manual)
                    ? manual
                    : (!string.IsNullOrWhiteSpace(selectedPort) ? selectedPort : (cfg.Bluetooth?.ComPort ?? "COM3"));
                var baud = cfg.Bluetooth?.BaudRate ?? 9600;
                var newLine = cfg.Bluetooth?.NewLine ?? "\n";

                BluetoothSerialClient? client = null;
                var ownsClient = false;

                if (_testSerialClient?.IsOpen == true
                    && string.Equals(_testSerialPort, port, StringComparison.OrdinalIgnoreCase))
                {
                    client = _testSerialClient;
                    ownsClient = false;
                }
                else
                {
                    client = new BluetoothSerialClient(port, baud, newLine);
                    ownsClient = true;
                }

                try
                {
                    await client.SendLineAsync(payload);

                    _outputCount++;
                    OutputCountText.Text = _outputCount.ToString();

                    _robotCommandCount++;
                    RobotCommandCountText.Text = _robotCommandCount.ToString();

                    LastOutputTimeText.Text = DateTime.Now.ToString("HH:mm:ss");

                    AddOutputEvent(
                        type: "SERIAL",
                        title: "TX",
                        message: $"Sent to {port} @ {baud}",
                        borderColor: "#8b5cf6",
                        eventType: "OUTPUT",
                        typeColor: "#8b5cf6",
                        payload: payload);
                }
                catch (TimeoutException)
                {
                    AddOutputEvent(
                        type: "SERIAL",
                        title: "TX failed (timeout)",
                        message: $"Write timed out on {port}. This usually means the Bluetooth SPP link is not connected, or you picked the wrong COM port (often there is an Incoming and an Outgoing port). Close any serial monitor and try the other COM port.",
                        borderColor: "#ef4444",
                        eventType: "ERROR",
                        typeColor: "#ef4444",
                        payload: payload);
                }
                finally
                {
                    if (ownsClient)
                    {
                        client.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                AddOutputEvent(
                    type: "SERIAL",
                    title: "TX failed",
                    message: ex.Message,
                    borderColor: "#ef4444",
                    eventType: "ERROR",
                    typeColor: "#ef4444",
                    payload: payload);
            }
        }

        private void ApplyPostsConfigIfProvided(ProductionFlowRequest request)
        {
            var jsonText = PostsConfigTextBox?.Text;
            if (string.IsNullOrWhiteSpace(jsonText)) return;

            var route = request.Route ?? Array.Empty<string>();
            if (route.Count == 0)
            {
                route = new[] { "POST-01", "POST-02", "POST-03" };
            }

            var mapped = ParsePostsConfigJson(jsonText, route);
            if (mapped.Count == 0) return;

            ProductionDataService.Instance.EnsureInitialized();

            foreach (var kvp in mapped)
            {
                var postCode = kvp.Key;
                var config = kvp.Value;
                var tuSeconds = config.TuMs > 0 ? Math.Max(1, (int)Math.Round(config.TuMs / 1000.0)) : 0;

                ProductionDataService.Instance.UpdatePost(postCode, p =>
                {
                    p.StockCapacity = Math.Max(0, config.StockCapacity);
                    p.CurrentLoad = Math.Max(0, config.StockCapacity);
                    p.UtilityTimeSeconds = tuSeconds;
                });
            }

            // Log as an INPUT event (what the operator would "send" to IoT)
            _inputCount++;
            InputCountText.Text = _inputCount.ToString();
            AddOutputEvent(
                type: "OF INPUT",
                title: "Posts config received",
                message: $"Applied posts config to route ({mapped.Count} posts). Low-stock threshold is 0.2*s.",
                borderColor: "#22c55e",
                eventType: "INPUT",
                typeColor: "#22c55e",
                payload: jsonText.Trim());
        }

        private static Dictionary<string, PostConfig> ParsePostsConfigJson(string jsonText, IReadOnlyList<string> route)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var raw = JsonSerializer.Deserialize<Dictionary<string, PostConfigDto>>(jsonText, options)
                      ?? new Dictionary<string, PostConfigDto>(StringComparer.OrdinalIgnoreCase);

            var result = new Dictionary<string, PostConfig>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in raw)
            {
                var key = kvp.Key?.Trim();
                if (string.IsNullOrWhiteSpace(key)) continue;

                var dto = kvp.Value;
                if (dto == null) continue;

                var postCode = ResolvePostCode(key, route);
                if (postCode == null) continue;

                result[postCode] = new PostConfig(dto.S, dto.T);
            }

            return result;
        }

        private static string? ResolvePostCode(string key, IReadOnlyList<string> route)
        {
            // 1) If the key is already a post code (ex: "POST-01"), use it
            var direct = route.FirstOrDefault(p => p.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(direct)) return direct;

            // 2) Otherwise accept "p1", "p2", ... mapped to route index
            if (key.Length >= 2 && (key[0] == 'p' || key[0] == 'P'))
            {
                if (int.TryParse(key.Substring(1), out var index) && index >= 1 && index <= route.Count)
                {
                    return route[index - 1];
                }
            }

            return null;
        }

        private void AddOutputEvent(string type, string title, string message, string borderColor, string eventType, string typeColor, string? payload = null)
        {
            var item = new OutputEventItem
            {
                Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"),
                Type = eventType,
                Title = title,
                Message = message,
                BorderColor = borderColor,
                TypeColor = typeColor,
                Payload = payload,
                PayloadVisibility = string.IsNullOrEmpty(payload) ? Visibility.Collapsed : Visibility.Visible
            };

            OutputEvents.Insert(0, item); // Ajouter en haut

            // Limiter à 100 événements
            while (OutputEvents.Count > 100)
            {
                OutputEvents.RemoveAt(OutputEvents.Count - 1);
            }
        }

        #endregion
    }

    /// <summary>
    /// Modèle pour affichage des événements OUTPUT
    /// </summary>
    public class OutputEventItem
    {
        public string Timestamp { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string BorderColor { get; set; } = string.Empty;
        public string TypeColor { get; set; } = string.Empty;
        public string? Payload { get; set; }
        public Visibility PayloadVisibility { get; set; }
    }

    internal sealed class PostConfig
    {
        public int StockCapacity { get; }
        public int TuMs { get; }

        public PostConfig(int stockCapacity, int tuMs)
        {
            StockCapacity = stockCapacity;
            TuMs = tuMs;
        }
    }

    internal sealed class PostConfigDto
    {
        [JsonPropertyName("s")]
        public int S { get; set; }

        [JsonPropertyName("t")]
        public int T { get; set; }
    }
}
