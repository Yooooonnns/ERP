using Microsoft.AspNetCore.SignalR;
using DigitalisationERP.API.Hubs;
using DigitalisationERP.Application.Services;

namespace DigitalisationERP.API.Services
{
    /// <summary>
    /// Background service for continuously streaming real-time data to connected clients
    /// Runs every configurable interval and broadcasts:
    /// - Line snapshots (complete state)
    /// - Dashboard updates (optimized with change detection)
    /// - Event streams (anomalies, incidents, alerts)
    /// 
    /// Features:
    /// - Configurable streaming interval (default: 500ms)
    /// - Monitored lines configuration (which lines to stream)
    /// - Graceful shutdown and reconnection
    /// - Error logging and recovery
    /// </summary>
    public class RealtimeStreamingService : BackgroundService
    {
        private readonly IHubContext<RealtimeSimulationHub> _hubContext;
        private readonly RealTimeSimulationIntegrator _integrator;
        private readonly ILogger<RealtimeStreamingService> _logger;
        private readonly IConfiguration _config;

        // Configuration
        private int _streamingIntervalMs = 500; // Default: 2 updates per second
        private List<(int LineId, List<int> PostIds)> _monitoredLines = new();

        // State tracking
        private Dictionary<int, object> _lastSnapshots = new();
        private bool _isRunning = false;

        public RealtimeStreamingService(
            IHubContext<RealtimeSimulationHub> hubContext,
            RealTimeSimulationIntegrator integrator,
            ILogger<RealtimeStreamingService> logger,
            IConfiguration config)
        {
            _hubContext = hubContext;
            _integrator = integrator;
            _logger = logger;
            _config = config;

            InitializeConfiguration();
        }

        /// <summary>
        /// Load configuration from appsettings.json
        /// </summary>
        private void InitializeConfiguration()
        {
            try
            {
                // Load streaming interval (in milliseconds)
                var interval = _config.GetValue<int?>("RealtimeStreaming:IntervalMs");
                if (interval.HasValue && interval > 0)
                {
                    _streamingIntervalMs = interval.Value;
                }

                // Load monitored lines configuration
                var linesConfig = _config.GetSection("RealtimeStreaming:MonitoredLines");
                if (linesConfig.Exists())
                {
                    foreach (var lineSection in linesConfig.GetChildren())
                    {
                        if (int.TryParse(lineSection["LineId"], out var lineId))
                        {
                            var postsStr = lineSection["PostIds"] ?? "";
                            var posts = string.IsNullOrWhiteSpace(postsStr)
                                ? new List<int>()
                                : postsStr.Split(',').Select(int.Parse).ToList();

                            _monitoredLines.Add((lineId, posts));
                        }
                    }
                }

                // Default: monitor Line 1 if no config provided
                if (_monitoredLines.Count == 0)
                {
                    _monitoredLines.Add((1, new List<int> { 1, 2, 3, 4 }));
                }

                _logger.LogInformation(
                    $"RealtimeStreamingService initialized. Interval: {_streamingIntervalMs}ms, Monitored lines: {_monitoredLines.Count}"
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error loading streaming config, using defaults: {ex.Message}");
                _monitoredLines.Add((1, new List<int> { 1, 2, 3, 4 }));
            }
        }

        /// <summary>
        /// Main execution loop - broadcasts updates at configured interval
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _isRunning = true;
            _logger.LogInformation("RealtimeStreamingService started");

            try
            {
                while (!stoppingToken.IsCancellationRequested && _isRunning)
                {
                    try
                    {
                        // Stream each monitored line
                        foreach (var (lineId, postIds) in _monitoredLines)
                        {
                            await BroadcastLineUpdates(lineId, postIds);
                        }

                        // Wait for next interval
                        await Task.Delay(_streamingIntervalMs, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Service is stopping
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error in streaming loop: {ex.Message}");
                        // Continue on error instead of crashing
                        await Task.Delay(1000, stoppingToken);
                    }
                }
            }
            finally
            {
                _isRunning = false;
                _logger.LogInformation("RealtimeStreamingService stopped");
            }
        }

        /// <summary>
        /// Broadcast updates for a single line to all connected subscribers
        /// </summary>
        private async Task BroadcastLineUpdates(int lineId, List<int> postIds)
        {
            try
            {
                // Generate current snapshot
                var currentSnapshot = _integrator.GenerateLineSnapshot(lineId, postIds);

                // Check if we have a previous snapshot for change detection
                if (_lastSnapshots.TryGetValue(lineId, out var lastSnapshot))
                {
                    // For efficiency: send dashboard update (with change detection)
                    // instead of full snapshot
                    var update = _integrator.GenerateDashboardUpdate(lineId, postIds);

                    // Only broadcast if there are meaningful changes
                    if (update.Changes?.HasAnyChanges == true || _streamingIntervalMs > 1000)
                    {
                        await _hubContext.Clients
                            .Group($"Line_{lineId}")
                            .SendAsync("DashboardUpdate", new
                            {
                                lineId,
                                update,
                                timestamp = DateTime.UtcNow
                            });
                    }
                }
                else
                {
                    // First update: send full snapshot
                    await _hubContext.Clients
                        .Group($"Line_{lineId}")
                        .SendAsync("InitialSnapshot", new
                        {
                            lineId,
                            snapshot = currentSnapshot,
                            timestamp = DateTime.UtcNow
                        });
                }

                // Update cached snapshot
                _lastSnapshots[lineId] = currentSnapshot;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error broadcasting line {lineId} updates: {ex.Message}");
            }
        }

        /// <summary>
        /// Update monitored lines at runtime
        /// </summary>
        public void UpdateMonitoredLines(List<(int LineId, List<int> PostIds)> lines)
        {
            _monitoredLines = lines;
            _lastSnapshots.Clear(); // Clear cache to force full snapshots on next update
            _logger.LogInformation($"Monitored lines updated: {lines.Count} lines");
        }

        /// <summary>
        /// Add a line to monitoring
        /// </summary>
        public void AddMonitoredLine(int lineId, List<int> postIds)
        {
            if (!_monitoredLines.Any(l => l.LineId == lineId))
            {
                _monitoredLines.Add((lineId, postIds));
                _logger.LogInformation($"Line {lineId} added to monitoring");
            }
        }

        /// <summary>
        /// Remove a line from monitoring
        /// </summary>
        public void RemoveMonitoredLine(int lineId)
        {
            _monitoredLines.RemoveAll(l => l.LineId == lineId);
            if (_lastSnapshots.ContainsKey(lineId))
            {
                _lastSnapshots.Remove(lineId);
            }
            _logger.LogInformation($"Line {lineId} removed from monitoring");
        }

        /// <summary>
        /// Update streaming interval (in milliseconds)
        /// </summary>
        public void SetStreamingInterval(int intervalMs)
        {
            if (intervalMs > 0)
            {
                _streamingIntervalMs = intervalMs;
                _logger.LogInformation($"Streaming interval updated to {intervalMs}ms");
            }
        }

        /// <summary>
        /// Get current status
        /// </summary>
        public object GetStatus()
        {
            return new
            {
                isRunning = _isRunning,
                streamingIntervalMs = _streamingIntervalMs,
                monitoredLinesCount = _monitoredLines.Count,
                monitoredLines = _monitoredLines.Select(l => new { l.LineId, PostCount = l.PostIds.Count }).ToList(),
                cachedSnapshotsCount = _lastSnapshots.Count
            };
        }

        /// <summary>
        /// Graceful shutdown
        /// </summary>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _isRunning = false;
            _logger.LogInformation("RealtimeStreamingService stopping...");
            await base.StopAsync(cancellationToken);
        }
    }
}
