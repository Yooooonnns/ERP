using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DigitalisationERP.Desktop.Services;

namespace DigitalisationERP.Desktop.Services.IoT;

public sealed class ProductionFlowRequest
{
    public string OrderNumber { get; set; } = "OF-0001";
    public string LineId { get; set; } = "LINE-A";
    public int Quantity { get; set; }
    public int TuSeconds { get; set; } = 60;
    public int TransitSeconds { get; set; } = 2;
    public IReadOnlyList<string> Route { get; set; } = Array.Empty<string>();
    public int FinishedStockStart { get; set; }

    /// <summary>
    /// If true, piece N+1 will not start until piece N becomes a finished product
    /// (i.e. last post completed including TU). This matches "PF before next piece".
    /// </summary>
    public bool RequirePfBeforeNextPiece { get; set; } = false;
}

public sealed class ProductionFlowEvent
{
    public string Direction { get; init; } = "input"; // input/output
    public string EventType { get; init; } = "info";
    public string Message { get; init; } = "";
    public string JsonPayload { get; init; } = "{}";
}

public sealed class ProductionFlowSimulator
{
    private readonly ProductionDataService _productionDataService;

    public ProductionFlowSimulator(ProductionDataService? productionDataService = null)
    {
        _productionDataService = productionDataService ?? ProductionDataService.Instance;
    }

    public async Task RunAsync(
        ProductionFlowRequest request,
        Func<ProductionFlowEvent, Task> onEvent,
        Func<CancellationToken, Task>? waitForFirstPostDetection = null,
        Func<string, int, CancellationToken, Task>? waitForPostDetection = null,
        Func<CancellationToken, Task>? waitWhilePaused = null,
        CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (onEvent == null) throw new ArgumentNullException(nameof(onEvent));

        _productionDataService.EnsureInitialized();

        var route = ResolveRoute(request);
        var postsByCode = _productionDataService.Posts
            .Where(p => p.LineId == request.LineId)
            .ToDictionary(p => p.PostCode, StringComparer.OrdinalIgnoreCase);

        var lowStockTriggered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var outOfStockTriggered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var sync = new object();

        var fallbackTuMs = Math.Max(1, request.TuSeconds) * 1000;
        var transitMs = Math.Max(0, request.TransitSeconds) * 1000;
        var finishedStock = request.FinishedStockStart;

        // One-at-a-time capacity per post to enable real pipeline behavior.
        var postLocks = route
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(p => p, _ => new SemaphoreSlim(1, 1), StringComparer.OrdinalIgnoreCase);

        // 1) Order launch (input)
        await onEvent(MakeEvent(
            direction: "input",
            eventType: "order-launch",
            message: $"Order {request.OrderNumber} launched (qty {request.Quantity})",
            payload: new
            {
                request.OrderNumber,
                request.LineId,
                request.Quantity,
                request.TransitSeconds,
                DefaultTuSeconds = request.TuSeconds,
                Route = route
            }));

        // 2) Wait for first post detection (sensor input)
        var firstPost = route[0];
        await onEvent(MakeEvent(
            direction: "input",
            eventType: "await-first-detection",
            message: $"Waiting for sensor detection at {firstPost}",
            payload: new
            {
                request.OrderNumber,
                request.LineId,
                FirstPost = firstPost
            }));

        var firstDetectionWaitedViaPostCallback = false;

        if (waitForFirstPostDetection != null)
        {
            await waitForFirstPostDetection(cancellationToken);
        }
        else if (waitForPostDetection != null)
        {
            // If callers only provide per-post detection, use it to gate the very first detection (piece 1 @ first post)
            // so the OF doesn't start without a real sensor trigger.
            await waitForPostDetection(firstPost, 1, cancellationToken);
            firstDetectionWaitedViaPostCallback = true;
        }

        await onEvent(MakeEvent(
            direction: "input",
            eventType: "first-detection",
            message: $"Sensor detected piece at {firstPost}",
            payload: new
            {
                request.OrderNumber,
                request.LineId,
                PostCode = firstPost
            }));

        async Task WaitIfPausedAsync()
        {
            if (waitWhilePaused != null)
            {
                await waitWhilePaused(cancellationToken);
            }
        }

        async Task DelayWithPauseAsync(int totalMs)
        {
            if (totalMs <= 0) return;

            const int chunkMs = 100;
            var remaining = totalMs;
            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await WaitIfPausedAsync();

                var step = remaining > chunkMs ? chunkMs : remaining;
                await Task.Delay(step, cancellationToken);
                remaining -= step;
            }
        }

        async Task RunPieceAsync(int unitIndex)
        {
            for (int stepIndex = 0; stepIndex < route.Count; stepIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var postCode = route[stepIndex];

                // Sensor gating + transit ordering:
                // - A sensor message for the destination post must unlock the animation.
                // - Animation starts when the sensor signal is received.
                // - Piece markers must not appear/move before detection.
                if (stepIndex == 0)
                {
                    if (waitForPostDetection != null)
                    {
                        // Avoid double-waiting for the very first piece at the first post when we already gated it above.
                        if (!(firstDetectionWaitedViaPostCallback && unitIndex == 1))
                        {
                            await waitForPostDetection(postCode, unitIndex, cancellationToken);
                        }
                    }
                }
                else
                {
                    var prevPost = route[stepIndex - 1];

                    // Gate on real sensor first (destination post).
                    if (waitForPostDetection != null)
                    {
                        await waitForPostDetection(postCode, unitIndex, cancellationToken);
                    }

                    // Always provide a non-zero TransitMs so UI animation is visible.
                    var legTransitMs = transitMs;

                    await onEvent(MakeEvent(
                        direction: "input",
                        eventType: "transit",
                        message: $"Piece {unitIndex}/{request.Quantity} transiting {prevPost} -> {postCode}",
                        payload: new
                        {
                            request.OrderNumber,
                            request.LineId,
                            Piece = unitIndex,
                            From = prevPost,
                            To = postCode,
                            TransitMs = legTransitMs
                        }));

                    if (legTransitMs > 0)
                    {
                        await DelayWithPauseAsync(legTransitMs);
                    }
                }

                // One-at-a-time on each post
                if (postLocks.TryGetValue(postCode, out var gate))
                {
                    await gate.WaitAsync(cancellationToken);
                }

                try
                {
                    // INPUT: piece detected at post (entry)
                    await onEvent(MakeEvent(
                        direction: "input",
                        eventType: "piece-detected",
                        message: $"Piece {unitIndex}/{request.Quantity} detected at {postCode}",
                        payload: new
                        {
                            request.OrderNumber,
                            request.LineId,
                            Piece = unitIndex,
                            Step = stepIndex + 1,
                            PostCode = postCode
                        }));

                    int tuMs;
                    int? stockBefore = null;
                    int? stockAfter = null;
                    int? capacityValue = null;

                    lock (sync)
                    {
                        // OUTPUT: consume raw material stock for this post (1 unit per piece)
                        if (postsByCode.TryGetValue(postCode, out var post))
                        {
                            stockBefore = post.CurrentLoad;
                            stockAfter = Math.Max(0, post.CurrentLoad - 1);
                            capacityValue = post.StockCapacity;

                            if (stockAfter.Value != stockBefore.Value)
                            {
                                _productionDataService.UpdatePost(postCode, p => p.CurrentLoad = stockAfter.Value);
                            }

                            // TU processing time for this post.
                            // Sensor triggers (including last post) are treated as "piece detected at entry",
                            // so we always apply TU unless explicitly configured elsewhere.
                            tuMs = fallbackTuMs;
                            if (post.UtilityTimeSeconds > 0)
                            {
                                tuMs = post.UtilityTimeSeconds * 1000;
                            }

                            // Low/out-of-stock alerts based on capacity (threshold = 0.2*s)
                            var capacity = Math.Max(0, post.StockCapacity);
                            if (capacity > 0)
                            {
                                var lowThreshold = capacity * 0.2;

                                if (stockAfter.Value <= lowThreshold && !lowStockTriggered.Contains(postCode))
                                {
                                    lowStockTriggered.Add(postCode);
                                    _ = onEvent(MakeEvent(
                                        direction: "output",
                                        eventType: "stock-low",
                                        message: $"Low stock at {postCode}: {stockAfter}/{capacity} (threshold {lowThreshold:0.##})",
                                        payload: new
                                        {
                                            request.OrderNumber,
                                            request.LineId,
                                            Piece = unitIndex,
                                            PostCode = postCode,
                                            StockAfter = stockAfter,
                                            StockCapacity = capacity,
                                            LowStockThreshold = lowThreshold
                                        }));
                                }

                                if (stockAfter.Value <= 0 && !outOfStockTriggered.Contains(postCode))
                                {
                                    outOfStockTriggered.Add(postCode);
                                    _ = onEvent(MakeEvent(
                                        direction: "output",
                                        eventType: "stock-out",
                                        message: $"Out of stock at {postCode}: {stockAfter}/{capacity}",
                                        payload: new
                                        {
                                            request.OrderNumber,
                                            request.LineId,
                                            Piece = unitIndex,
                                            PostCode = postCode,
                                            StockAfter = stockAfter,
                                            StockCapacity = capacity
                                        }));
                                }
                            }
                        }
                        else
                        {
                            tuMs = fallbackTuMs;
                        }
                    }

                    if (stockBefore.HasValue && stockAfter.HasValue)
                    {
                        await onEvent(MakeEvent(
                            direction: "output",
                            eventType: "raw-material-consumed",
                            message: $"{postCode} stock consumed: {stockBefore} -> {stockAfter}",
                            payload: new
                            {
                                request.OrderNumber,
                                request.LineId,
                                Piece = unitIndex,
                                PostCode = postCode,
                                Consumed = 1,
                                StockBefore = stockBefore.Value,
                                StockAfter = stockAfter.Value,
                                StockCapacity = capacityValue ?? 0
                            }));
                    }
                    else
                    {
                        await onEvent(MakeEvent(
                            direction: "output",
                            eventType: "raw-material-consumed",
                            message: $"{postCode} stock consumed (post not found in line data)",
                            payload: new
                            {
                                request.OrderNumber,
                                request.LineId,
                                Piece = unitIndex,
                                PostCode = postCode,
                                Consumed = 1
                            }));
                    }

                    if (tuMs > 0)
                    {
                        await DelayWithPauseAsync(tuMs);
                    }

                    await onEvent(MakeEvent(
                        direction: "output",
                        eventType: "tu-complete",
                        message: $"TU complete at {postCode}",
                        payload: new
                        {
                            request.OrderNumber,
                            request.LineId,
                            Piece = unitIndex,
                            Step = stepIndex + 1,
                            PostCode = postCode,
                            TUms = tuMs
                        }));
                }
                finally
                {
                    if (postLocks.TryGetValue(postCode, out var gateToRelease))
                    {
                        gateToRelease.Release();
                    }
                }
            }

            // OUTPUT: final product count increments only after last post TU
            var newFinished = Interlocked.Increment(ref finishedStock);
            await onEvent(MakeEvent(
                direction: "output",
                eventType: "final-product-count",
                message: $"Final product count incremented ({newFinished})",
                payload: new
                {
                    request.OrderNumber,
                    request.LineId,
                    Piece = unitIndex,
                    FinalProductCount = newFinished
                }));
        }

        if (request.RequirePfBeforeNextPiece)
        {
            for (int unitIndex = 1; unitIndex <= request.Quantity; unitIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await RunPieceAsync(unitIndex);
            }
        }
        else
        {
            var pieceTasks = new List<Task>(capacity: Math.Max(0, request.Quantity));
            for (int unitIndex = 1; unitIndex <= request.Quantity; unitIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                pieceTasks.Add(RunPieceAsync(unitIndex));
            }

            await Task.WhenAll(pieceTasks);
        }

        await onEvent(MakeEvent(
            direction: "output",
            eventType: "order-complete",
            message: $"Order {request.OrderNumber} complete",
            payload: new
            {
                request.OrderNumber,
                request.LineId,
                request.Quantity,
                FinalProductCount = finishedStock
            }));

        foreach (var sem in postLocks.Values)
        {
            sem.Dispose();
        }
    }

    private IReadOnlyList<string> ResolveRoute(ProductionFlowRequest request)
    {
        var route = request.Route ?? Array.Empty<string>();
        if (route.Count > 0) return route;

        var linePosts = _productionDataService.GetPostsForLine(request.LineId).ToList();
        if (linePosts.Count > 0)
        {
            return linePosts.Select(p => p.PostCode).ToArray();
        }

        return new[] { "POST-01", "POST-02", "POST-03" };
    }

    private static ProductionFlowEvent MakeEvent(string direction, string eventType, string message, object payload)
    {
        return new ProductionFlowEvent
        {
            Direction = direction,
            EventType = eventType,
            Message = message,
            JsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false })
        };
    }
}
