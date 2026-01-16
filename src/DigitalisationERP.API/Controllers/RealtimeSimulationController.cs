using DigitalisationERP.Application.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DigitalisationERP.API.Controllers
{
    /// <summary>
    /// Contrôleur CENTRAL pour la simulation complète EN TEMPS RÉEL
    /// Intègre: Senseurs + Production + Maintenance
    /// 
    /// C'est le point d'entrée principal pour tester comment l'app gère 
    /// les données en entrée provenant de TOUS les modules
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class RealtimeSimulationController : ControllerBase
    {
        private readonly RealTimeSimulationIntegrator _integrator;

        public RealtimeSimulationController(
            SensorSimulationService sensorSimulation,
            ProductionSimulationService productionSimulation,
            MaintenanceAlertManager alertManager,
            MaintenanceHealthScoreCalculationService healthScore)
        {
            _integrator = new RealTimeSimulationIntegrator(
                sensorSimulation,
                productionSimulation,
                alertManager,
                healthScore);
        }

        /// <summary>
        /// Snapshot COMPLET d'une ligne EN TEMPS RÉEL
        /// Retourne: Senseurs + Production + Maintenance + Santé dans UN SEUL appel
        /// </summary>
        [HttpGet("line-snapshot")]
        public IActionResult GetLineSnapshot([FromQuery] int lineId = 1, [FromQuery] string postIds = "1,2,3,4,5,6,7")
        {
            try
            {
                if (lineId <= 0)
                    return BadRequest(new { error = "lineId doit être positif" });

                var postIdList = postIds.Split(',').Select(p => int.Parse(p.Trim())).ToList();
                var snapshot = _integrator.GenerateLineSnapshot(lineId, postIdList);

                return Ok(new
                {
                    data = MapSnapshotToDto(snapshot),
                    timestamp = DateTime.Now,
                    generatedAt = "Real-time"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Mise à jour DASHBOARD EN TEMPS RÉEL
        /// Optimisé pour WebSocket/SignalR
        /// Retourne UNIQUEMENT les changements détectés
        /// </summary>
        [HttpGet("dashboard-update")]
        public IActionResult GetDashboardUpdate([FromQuery] int lineId = 1, [FromQuery] string postIds = "1,2,3,4,5,6,7")
        {
            try
            {
                if (lineId <= 0)
                    return BadRequest(new { error = "lineId doit être positif" });

                var postIdList = postIds.Split(',').Select(p => int.Parse(p.Trim())).ToList();
                var update = _integrator.GenerateDashboardUpdate(lineId, postIdList);

                return Ok(new
                {
                    data = new
                    {
                        timestamp = update.Timestamp,
                        lineId = update.LineId,
                        snapshot = MapSnapshotToDto(update.CurrentSnapshot),
                        changes = update.Changes != null ? new
                        {
                            healthScoreChanges = update.Changes.HealthScoreChanges,
                            newAlerts = update.Changes.NewAlerts,
                            productionChanges = update.Changes.ProductionChanges,
                            newIncidents = update.Changes.NewIncidents,
                            hasAnyChanges = update.Changes.HasAnyChanges
                        } : null
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Stream continu d'ÉVÉNEMENTS EN TEMPS RÉEL
        /// Parfait pour:
        /// - WebSocket/SignalR streaming
        /// - Dashboard notifications
        /// - Alert panels
        /// </summary>
        [HttpGet("event-stream")]
        public IActionResult GetEventStream([FromQuery] int lineId = 1, [FromQuery] int eventCount = 10)
        {
            try
            {
                if (lineId <= 0)
                    return BadRequest(new { error = "lineId doit être positif" });

                var events = _integrator.GenerateEventStream(lineId, eventCount);

                return Ok(new
                {
                    data = events.Select(e => new
                    {
                        id = e.Id,
                        lineId = e.LineId,
                        postId = e.PostId,
                        eventType = e.EventType,
                        severity = e.Severity,
                        timestamp = e.Timestamp,
                        message = e.Message
                    }).ToList(),
                    count = events.Count,
                    generatedAt = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// RAPPORT COMPLET de simulation
        /// Synthèse de TOUS les données:
        /// - Production (items, qualité, temps)
        /// - Maintenance (alertes, santé, incidents)
        /// - Capteurs (statuts, anomalies)
        /// </summary>
        [HttpGet("complete-report")]
        public IActionResult GetCompleteReport([FromQuery] int lineId = 1, [FromQuery] string postIds = "1,2,3,4,5,6,7")
        {
            try
            {
                if (lineId <= 0)
                    return BadRequest(new { error = "lineId doit être positif" });

                var postIdList = postIds.Split(',').Select(p => int.Parse(p.Trim())).ToList();
                var report = _integrator.GenerateCompleteReport(lineId, postIdList);

                return Ok(new
                {
                    data = new
                    {
                        lineId = report.LineId,
                        reportTime = report.ReportTime,
                        snapshot = MapSnapshotToDto(report.Snapshot),
                        hourlyMetrics = report.HourlyMetrics,
                        healthStatus = report.HealthStatus,
                        alertStatus = report.AlertStatus,
                        incidentStatus = report.IncidentStatus
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Simulation multi-ligne SIMULTANÉE
        /// Teste comment l'app gère plusieurs chaînes en parallèle
        /// </summary>
        [HttpPost("multi-line-simulation")]
        public IActionResult GetMultiLineSimulation([FromBody] MultiLineSimulationRequest request)
        {
            try
            {
                if (request?.LineIds == null || !request.LineIds.Any())
                    return BadRequest(new { error = "Au moins une ligne requise" });

                var results = new List<object>();

                foreach (var lineId in request.LineIds)
                {
                    var postIds = Enumerable.Range(1, 7).ToList();
                    var snapshot = _integrator.GenerateLineSnapshot(lineId, postIds);
                    results.Add(new
                    {
                        lineId = lineId,
                        snapshot = MapSnapshotToDto(snapshot)
                    });
                }

                return Ok(new
                {
                    data = results,
                    totalLines = results.Count,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// STRESS TEST: Génère 100 mises à jour
        /// Teste la stabilité et performance du système
        /// </summary>
        [HttpGet("stress-test")]
        public IActionResult RunStressTest([FromQuery] int lineId = 1, [FromQuery] int iterations = 100)
        {
            try
            {
                if (lineId <= 0)
                    return BadRequest(new { error = "lineId doit être positif" });

                var postIds = Enumerable.Range(1, 7).ToList();
                var startTime = DateTime.Now;

                for (int i = 0; i < iterations; i++)
                {
                    _integrator.GenerateLineSnapshot(lineId, postIds);
                }

                var elapsed = DateTime.Now - startTime;
                var avgTime = elapsed.TotalMilliseconds / iterations;

                return Ok(new
                {
                    data = new
                    {
                        lineId = lineId,
                        iterationsRun = iterations,
                        totalTimeMs = elapsed.TotalMilliseconds,
                        avgTimePerIterationMs = Math.Round(avgTime, 2),
                        iterationsPerSecond = Math.Round(1000 / avgTime, 2),
                        performance = avgTime < 100 ? "Excellent" : (avgTime < 200 ? "Good" : "Needs optimization")
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// DEMO MODE: Génère 30 secondes de données
        /// Retourne un film des mises à jour
        /// </summary>
        [HttpGet("demo-30-seconds")]
        public IActionResult RunDemo30SecondSimulation([FromQuery] int lineId = 1)
        {
            try
            {
                if (lineId <= 0)
                    return BadRequest(new { error = "lineId doit être positif" });

                var postIds = Enumerable.Range(1, 7).ToList();
                var snapshots = new List<object>();

                // Génère 30 snapshots (1 par seconde pendant 30s)
                for (int second = 0; second < 30; second++)
                {
                    var snapshot = _integrator.GenerateLineSnapshot(lineId, postIds);
                    snapshots.Add(new
                    {
                        second = second,
                        timestamp = DateTime.Now.AddSeconds(second),
                        metrics = new
                        {
                            avgHealth = snapshot.LineMetrics.AverageHealthScore,
                            avgEfficiency = snapshot.LineMetrics.AverageEfficiency,
                            totalProduced = snapshot.LineMetrics.TotalItemsProduced,
                            alertCount = snapshot.MaintenanceAlerts.Count,
                            incidentCount = snapshot.Incidents.Count
                        }
                    });
                }

                return Ok(new
                {
                    data = snapshots,
                    duration = "30 seconds",
                    lineId = lineId,
                    totalSnapshots = snapshots.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ==================== HELPER METHODS ====================

        private object MapSnapshotToDto(dynamic snapshot)
        {
            // Convert to simple DTO instead of trying to use dynamic with lambdas
            var sensorslist = new List<dynamic>();
            var productionlist = new List<dynamic>();
            var alertslist = new List<dynamic>();
            var healthlist = new List<dynamic>();
            var incidentslist = new List<dynamic>();

            if (snapshot.SensorReadings != null)
            {
                foreach (var s in snapshot.SensorReadings)
                {
                    sensorslist.Add(new { postId = s.PostId, sensorType = s.SensorType, value = s.Value, isNormal = s.IsNormal, alertLevel = s.AlertLevel });
                }
            }

            if (snapshot.ProductionUpdates != null)
            {
                foreach (var p in snapshot.ProductionUpdates)
                {
                    productionlist.Add(new { postId = p.PostId, itemsProduced = p.ItemsProduced, efficiency = p.EfficiencyPercent, status = p.PostStatus, taktTime = p.TaktTime });
                }
            }

            if (snapshot.MaintenanceAlerts != null)
            {
                foreach (var a in snapshot.MaintenanceAlerts)
                {
                    alertslist.Add(new { postId = a.PostId, title = a.Title, severity = a.Severity });
                }
            }

            if (snapshot.HealthScores != null)
            {
                foreach (var h in snapshot.HealthScores)
                {
                    healthlist.Add(new { postId = h.PostId, score = h.Score, status = h.Status, color = h.Color });
                }
            }

            if (snapshot.Incidents != null)
            {
                foreach (var i in snapshot.Incidents)
                {
                    incidentslist.Add(new { postId = i.PostId, type = i.Type, severity = i.Severity, downtime = i.EstimatedDowntime });
                }
            }

            return new
            {
                lineId = snapshot.LineId,
                snapshotTime = snapshot.SnapshotTime,
                sensors = sensorslist,
                production = productionlist,
                maintenance = new
                {
                    alerts = alertslist,
                    health = healthlist,
                    incidents = incidentslist
                },
                metrics = new
                {
                    avgHealth = snapshot.LineMetrics.AverageHealthScore,
                    avgEfficiency = snapshot.LineMetrics.AverageEfficiency,
                    totalProduced = snapshot.LineMetrics.TotalItemsProduced,
                    quality = snapshot.LineMetrics.QualityRate,
                    status = snapshot.LineMetrics.LineStatus
                }
            };
        }
    }

    /// <summary>
    /// Request DTO pour multi-line simulation
    /// </summary>
    public class MultiLineSimulationRequest
    {
        public List<int> LineIds { get; set; } = new();
    }
}
