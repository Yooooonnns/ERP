using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using System.Diagnostics;

namespace DigitalisationERP.Desktop.Services
{
    /// <summary>
    /// WebSocket/SignalR client for connecting to real-time simulation streaming hub
    /// Handles connection lifecycle, subscriptions, and event reception
    /// 
    /// Features:
    /// - Automatic reconnection with exponential backoff
    /// - Subscription management for production lines
    /// - Event-based notification system
    /// - Comprehensive error handling and logging
    /// - Connection state tracking
    /// </summary>
    public class RealtimeWebSocketClient : IAsyncDisposable
    {
        private HubConnection? _connection;
        private readonly string _hubUrl;
        private readonly object? _logger; // Simplified logger placeholder

        // Events for client consumers
        public event Func<dynamic, Task>? OnSnapshotUpdate;
        public event Func<dynamic, Task>? OnDashboardUpdate;
        public event Func<dynamic, Task>? OnEventStream;
        public event Func<dynamic, Task>? OnCompleteReport;
        public event Func<dynamic, Task>? OnNewAlert;
        public event Func<dynamic, Task>? OnNewIncident;
        public event Func<dynamic?, Task>? OnConnectionStateChanged;
        public event Func<string, Task>? OnError;

        // Connection state
        public HubConnectionState ConnectionState => _connection?.State ?? HubConnectionState.Disconnected;
        public bool IsConnected => ConnectionState == HubConnectionState.Connected;

        public RealtimeWebSocketClient(string apiBaseUrl, object? logger = null)
        {
            _hubUrl = $"{apiBaseUrl.TrimEnd('/')}/hubs/realtime-simulation";
            _logger = logger;
        }

        /// <summary>
        /// Connect to the WebSocket hub
        /// </summary>
        public async Task ConnectAsync()
        {
            try
            {
                if (_connection != null)
                {
                    await _connection.DisposeAsync();
                }

                _connection = new HubConnectionBuilder()
                    .WithUrl(_hubUrl)
                    .WithAutomaticReconnect(new ExponentialBackoffRetryPolicy())
                    .Build();

                // Register event handlers
                RegisterEventHandlers();

                // Set up reconnection event
                _connection.Reconnecting += async error =>
                {
                    // _logger?.LogWarning($"Reconnecting to hub: {error?.Message}");
                    await NotifyConnectionStateChanged();
                };

                _connection.Reconnected += async connectionId =>
                {
                    // _logger?.LogInformation($"Reconnected to hub with connection ID: {connectionId}");
                    await NotifyConnectionStateChanged();
                };

                _connection.Closed += async error =>
                {
                    // _logger?.LogWarning($"Hub connection closed: {error?.Message}");
                    await NotifyConnectionStateChanged();
                };

                // Connect
                await _connection.StartAsync();
                // _logger?.LogInformation($"Connected to WebSocket hub: {_hubUrl}");
                await NotifyConnectionStateChanged();
            }
            catch (Exception ex)
            {
                // _logger?.LogError($"Error connecting to hub: {ex.Message}");
                var handler = OnError;
                if (handler != null)
                {
                    await handler.Invoke($"Connection failed: {ex.Message}");
                }
                throw;
            }
        }

        /// <summary>
        /// Disconnect from the WebSocket hub
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                if (_connection != null && IsConnected)
                {
                    await _connection.StopAsync();
                    // _logger?.LogInformation("Disconnected from WebSocket hub");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                // _logger?.LogError($"Error disconnecting: {ex.Message}");
            }
            finally
            {
                await NotifyConnectionStateChanged();
            }
        }

        /// <summary>
        /// Register all event handlers from the hub
        /// </summary>
        private void RegisterEventHandlers()
        {
            if (_connection == null) return;

            // Snapshot events
            _connection.On<dynamic>("SnapshotUpdate", async snapshot =>
            {
                // _logger?.LogDebug("Received snapshot update");
                var handler = OnSnapshotUpdate;
                if (handler != null)
                {
                    await handler.Invoke(snapshot);
                }
            });

            _connection.On<dynamic>("InitialSnapshot", async snapshot =>
            {
                // _logger?.LogDebug("Received initial snapshot");
                var handler = OnSnapshotUpdate;
                if (handler != null)
                {
                    await handler.Invoke(snapshot);
                }
            });

            // Dashboard events
            _connection.On<dynamic>("DashboardUpdate", async update =>
            {
                // _logger?.LogDebug("Received dashboard update");
                var handler = OnDashboardUpdate;
                if (handler != null)
                {
                    await handler.Invoke(update);
                }
            });

            // Event stream
            _connection.On<dynamic>("EventStream", async eventStream =>
            {
                // _logger?.LogDebug("Received event stream");
                var handler = OnEventStream;
                if (handler != null)
                {
                    await handler.Invoke(eventStream);
                }
            });

            // Complete report
            _connection.On<dynamic>("CompleteReport", async report =>
            {
                // _logger?.LogDebug("Received complete report");
                var handler = OnCompleteReport;
                if (handler != null)
                {
                    await handler.Invoke(report);
                }
            });

            // Alert events
            _connection.On<dynamic>("NewAlert", async alert =>
            {
                // _logger?.LogDebug("Received new alert");
                var handler = OnNewAlert;
                if (handler != null)
                {
                    await handler.Invoke(alert);
                }
            });

            // Incident events
            _connection.On<dynamic>("NewIncident", async incident =>
            {
                // _logger?.LogDebug("Received new incident");
                var handler = OnNewIncident;
                if (handler != null)
                {
                    await handler.Invoke(incident);
                }
            });

            // Error notifications
            _connection.On<string>("Error", async error =>
            {
                // _logger?.LogError($"Hub error: {error}");
                var handler = OnError;
                if (handler != null)
                {
                    await handler.Invoke(error);
                }
            });

            // Pong response
            _connection.On<dynamic>("Pong", pong => { });
        }

        /// <summary>
        /// Subscribe to real-time updates for a production line
        /// </summary>
        public async Task SubscribeToLineAsync(int lineId, List<int>? postIds = null)
        {
            try
            {
                if (!IsConnected)
                {
                    await ConnectAsync();
                }

                if (_connection == null)
                {
                    throw new InvalidOperationException("Failed to establish connection");
                }

                var postIdsStr = postIds != null && postIds.Count > 0
                    ? string.Join(",", postIds)
                    : "";

                await _connection.InvokeAsync("SubscribeToLine", lineId, postIdsStr);
                // _logger?.LogInformation($"Subscribed to Line {lineId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                // _logger?.LogError($"Error subscribing to line: {ex.Message}");
                var handler = OnError;
                if (handler != null)
                {
                    await handler.Invoke($"Subscribe failed: {ex.Message}");
                }
                throw;
            }
        }

        /// <summary>
        /// Unsubscribe from a production line
        /// </summary>
        public async Task UnsubscribeFromLineAsync(int lineId)
        {
            try
            {
                if (IsConnected && _connection != null)
                {
                    await _connection.InvokeAsync("UnsubscribeFromLine", lineId);
                    // _logger?.LogInformation($"Unsubscribed from Line {lineId}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                // _logger?.LogError($"Error unsubscribing from line: {ex.Message}");
            }
        }

        /// <summary>
        /// Request an on-demand snapshot
        /// </summary>
        public async Task RequestSnapshotAsync(int lineId, List<int>? postIds = null)
        {
            try
            {
                if (!IsConnected)
                {
                    throw new InvalidOperationException("Not connected to hub");
                }

                if (_connection == null)
                {
                    throw new InvalidOperationException("Connection not available");
                }

                var postIdsStr = postIds != null && postIds.Count > 0
                    ? string.Join(",", postIds)
                    : "";

                await _connection.InvokeAsync("RequestSnapshot", lineId, postIdsStr);
                // _logger?.LogDebug($"Requested snapshot for Line {lineId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                // _logger?.LogError($"Error requesting snapshot: {ex.Message}");
                var handler = OnError;
                if (handler != null)
                {
                    await handler.Invoke($"Snapshot request failed: {ex.Message}");
                }
                throw;
            }
        }

        /// <summary>
        /// Request event stream
        /// </summary>
        public async Task RequestEventStreamAsync(int lineId, int eventCount = 5)
        {
            try
            {
                if (!IsConnected)
                {
                    throw new InvalidOperationException("Not connected to hub");
                }

                if (_connection == null)
                {
                    throw new InvalidOperationException("Connection not available");
                }

                await _connection.InvokeAsync("RequestEventStream", lineId, eventCount);
                // _logger?.LogDebug($"Requested event stream for Line {lineId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                // _logger?.LogError($"Error requesting event stream: {ex.Message}");
                var handler = OnError;
                if (handler != null)
                {
                    await handler.Invoke($"Event stream request failed: {ex.Message}");
                }
                throw;
            }
        }

        /// <summary>
        /// Request complete report
        /// </summary>
        public async Task RequestCompleteReportAsync(int lineId, List<int>? postIds = null)
        {
            try
            {
                if (!IsConnected)
                {
                    throw new InvalidOperationException("Not connected to hub");
                }

                if (_connection == null)
                {
                    throw new InvalidOperationException("Connection not available");
                }

                var postIdsStr = postIds != null && postIds.Count > 0
                    ? string.Join(",", postIds)
                    : "";

                await _connection.InvokeAsync("RequestCompleteReport", lineId, postIdsStr);
                // _logger?.LogDebug($"Requested complete report for Line {lineId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                // _logger?.LogError($"Error requesting complete report: {ex.Message}");
                var handler = OnError;
                if (handler != null)
                {
                    await handler.Invoke($"Report request failed: {ex.Message}");
                }
                throw;
            }
        }

        /// <summary>
        /// Health check ping
        /// </summary>
        public async Task PingAsync()
        {
            try
            {
                if (IsConnected && _connection != null)
                {
                    await _connection.InvokeAsync("Ping");
                    // _logger?.LogDebug("Sent ping");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                // _logger?.LogError($"Error sending ping: {ex.Message}");
            }
        }

        /// <summary>
        /// Get subscription status
        /// </summary>
        public async Task GetSubscriptionStatusAsync()
        {
            try
            {
                if (IsConnected && _connection != null)
                {
                    await _connection.InvokeAsync("GetSubscriptionStatus");
                    // _logger?.LogDebug("Requested subscription status");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                // _logger?.LogError($"Error getting subscription status: {ex.Message}");
            }
        }

        /// <summary>
        /// Notify listeners of connection state change
        /// </summary>
        private async Task NotifyConnectionStateChanged()
        {
            var handler = OnConnectionStateChanged;
            if (handler != null)
            {
                await handler.Invoke(null);
            }
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_connection != null)
                {
                    await DisconnectAsync();
                    await _connection.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                // _logger?.LogError($"Error disposing WebSocket client: {ex.Message}");
            }
        }

        /// <summary>
        /// Custom retry policy with exponential backoff
        /// </summary>
        private class ExponentialBackoffRetryPolicy : IRetryPolicy
        {
            private readonly TimeSpan[] _retryDelays = new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(500),
                TimeSpan.FromMilliseconds(1000),
                TimeSpan.FromMilliseconds(2000),
                TimeSpan.FromMilliseconds(5000)
            };

            public TimeSpan? NextRetryDelay(RetryContext context)
            {
                return context.PreviousRetryCount < _retryDelays.Length
                    ? _retryDelays[context.PreviousRetryCount]
                    : null;
            }
        }
    }
}
