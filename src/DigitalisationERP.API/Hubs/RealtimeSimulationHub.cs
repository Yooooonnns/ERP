using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DigitalisationERP.Application.Services;
using DigitalisationERP.Core.Entities;

namespace DigitalisationERP.API.Hubs
{
    /// <summary>
    /// SignalR Hub for real-time production line monitoring and sensor data streaming
    /// Broadcasts production updates, alerts, and health scores to connected clients
    /// 
    /// Features:
    /// - Real-time line snapshot updates (<100ms)
    /// - Event streaming (sensor anomalies, production incidents, maintenance alerts)
    /// - Dashboard change detection (optimized for bandwidth)
    /// - Multi-line monitoring support
    /// - Automatic reconnection handling
    /// </summary>
    public class RealtimeSimulationHub : Hub
    {
        private readonly RealTimeSimulationIntegrator _integrator;
        private readonly ILogger<RealtimeSimulationHub> _logger;

        // Track active subscriptions per connection
        private static Dictionary<string, HashSet<int>> _activeSubscriptions = new();
        private static object _subscriptionLock = new object();

        public RealtimeSimulationHub(
            RealTimeSimulationIntegrator integrator,
            ILogger<RealtimeSimulationHub> logger)
        {
            _integrator = integrator;
            _logger = logger;
        }

        /// <summary>
        /// Called when a client connects
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            lock (_subscriptionLock)
            {
                if (!_activeSubscriptions.ContainsKey(Context.ConnectionId))
                {
                    _activeSubscriptions[Context.ConnectionId] = new HashSet<int>();
                }
            }

            _logger.LogInformation($"Client connected: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Called when a client disconnects
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            lock (_subscriptionLock)
            {
                if (_activeSubscriptions.ContainsKey(Context.ConnectionId))
                {
                    _activeSubscriptions.Remove(Context.ConnectionId);
                }
            }

            _logger.LogInformation($"Client disconnected: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Subscribe to real-time updates for a specific production line
        /// </summary>
        /// <param name="lineId">Production line ID to monitor</param>
        /// <param name="postIds">List of post IDs in the line (comma-separated)</param>
        public async Task SubscribeToLine(int lineId, string postIds = "")
        {
            try
            {
                var posts = string.IsNullOrWhiteSpace(postIds)
                    ? new List<int>()
                    : postIds.Split(',').Select(int.Parse).ToList();

                lock (_subscriptionLock)
                {
                    if (_activeSubscriptions.ContainsKey(Context.ConnectionId))
                    {
                        _activeSubscriptions[Context.ConnectionId].Add(lineId);
                    }
                }

                // Add to group for broadcasting
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Line_{lineId}");

                _logger.LogInformation($"Client {Context.ConnectionId} subscribed to Line {lineId}");

                // Send initial snapshot
                var snapshot = _integrator.GenerateLineSnapshot(lineId, posts);
                await Clients.Caller.SendAsync("InitialSnapshot", snapshot);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error subscribing to line: {ex.Message}");
                await Clients.Caller.SendAsync("Error", $"Failed to subscribe: {ex.Message}");
            }
        }

        /// <summary>
        /// Unsubscribe from a production line
        /// </summary>
        public async Task UnsubscribeFromLine(int lineId)
        {
            try
            {
                lock (_subscriptionLock)
                {
                    if (_activeSubscriptions.ContainsKey(Context.ConnectionId))
                    {
                        _activeSubscriptions[Context.ConnectionId].Remove(lineId);
                    }
                }

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Line_{lineId}");
                _logger.LogInformation($"Client {Context.ConnectionId} unsubscribed from Line {lineId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error unsubscribing: {ex.Message}");
            }
        }

        /// <summary>
        /// Request a fresh snapshot for a line (on-demand update)
        /// </summary>
        public async Task RequestSnapshot(int lineId, string postIds = "")
        {
            try
            {
                var posts = string.IsNullOrWhiteSpace(postIds)
                    ? new List<int>()
                    : postIds.Split(',').Select(int.Parse).ToList();

                var snapshot = _integrator.GenerateLineSnapshot(lineId, posts);
                await Clients.Caller.SendAsync("SnapshotUpdate", new
                {
                    lineId,
                    snapshot,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error requesting snapshot: {ex.Message}");
                await Clients.Caller.SendAsync("Error", $"Failed to get snapshot: {ex.Message}");
            }
        }

        /// <summary>
        /// Get real-time event stream (anomalies, incidents, alerts)
        /// Called by clients to pull events instead of being pushed
        /// </summary>
        public async Task RequestEventStream(int lineId, int eventCount = 5)
        {
            try
            {
                var events = _integrator.GenerateEventStream(lineId, eventCount);
                await Clients.Caller.SendAsync("EventStream", new
                {
                    lineId,
                    events,
                    count = events.Count,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting event stream: {ex.Message}");
                await Clients.Caller.SendAsync("Error", $"Failed to get events: {ex.Message}");
            }
        }

        /// <summary>
        /// Get comprehensive report for a line
        /// </summary>
        public async Task RequestCompleteReport(int lineId, string postIds = "")
        {
            try
            {
                var posts = string.IsNullOrWhiteSpace(postIds)
                    ? new List<int>()
                    : postIds.Split(',').Select(int.Parse).ToList();

                var report = _integrator.GenerateCompleteReport(lineId, posts);
                await Clients.Caller.SendAsync("CompleteReport", new
                {
                    lineId,
                    report,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting report: {ex.Message}");
                await Clients.Caller.SendAsync("Error", $"Failed to get report: {ex.Message}");
            }
        }

        /// <summary>
        /// Broadcast a line snapshot to all subscribers of that line
        /// Called by background service
        /// </summary>
        public async Task BroadcastLineSnapshot(int lineId, List<int> postIds)
        {
            try
            {
                var snapshot = _integrator.GenerateLineSnapshot(lineId, postIds);
                await Clients.Group($"Line_{lineId}").SendAsync("SnapshotUpdate", new
                {
                    lineId,
                    snapshot,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error broadcasting snapshot: {ex.Message}");
            }
        }

        /// <summary>
        /// Broadcast dashboard update (with change detection) to line subscribers
        /// More efficient than full snapshot
        /// </summary>
        public async Task BroadcastDashboardUpdate(int lineId, List<int> postIds)
        {
            try
            {
                var update = _integrator.GenerateDashboardUpdate(lineId, postIds);
                await Clients.Group($"Line_{lineId}").SendAsync("DashboardUpdate", new
                {
                    lineId,
                    update,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error broadcasting dashboard update: {ex.Message}");
            }
        }

        /// <summary>
        /// Broadcast alert to all subscribers (client-side filtering by line)
        /// </summary>
        public async Task BroadcastAlert(int lineId, dynamic alert)
        {
            try
            {
                await Clients.Group($"Line_{lineId}").SendAsync("NewAlert", new
                {
                    lineId,
                    alert,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error broadcasting alert: {ex.Message}");
            }
        }

        /// <summary>
        /// Broadcast incident to line subscribers
        /// </summary>
        public async Task BroadcastIncident(int lineId, dynamic incident)
        {
            try
            {
                await Clients.Group($"Line_{lineId}").SendAsync("NewIncident", new
                {
                    lineId,
                    incident,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error broadcasting incident: {ex.Message}");
            }
        }

        /// <summary>
        /// Get active subscriptions count (for monitoring)
        /// </summary>
        public async Task GetSubscriptionStatus()
        {
            try
            {
                lock (_subscriptionLock)
                {
                    var status = new
                    {
                        connectedClients = _activeSubscriptions.Count,
                        totalSubscriptions = _activeSubscriptions.Values.Sum(s => s.Count),
                        timestamp = DateTime.UtcNow
                    };
                }
                
                // Send status outside of lock
                var message = new
                {
                    connectedClients = _activeSubscriptions.Count,
                    totalSubscriptions = _activeSubscriptions.Values.Sum(s => s.Count),
                    timestamp = DateTime.UtcNow
                };
                await Clients.Caller.SendAsync("SubscriptionStatus", message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting subscription status: {ex.Message}");
            }
        }

        /// <summary>
        /// Health check endpoint
        /// </summary>
        public async Task Ping()
        {
            try
            {
                await Clients.Caller.SendAsync("Pong", new { timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in ping: {ex.Message}");
            }
        }
    }
}
