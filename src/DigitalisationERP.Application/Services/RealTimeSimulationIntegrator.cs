using DigitalisationERP.Application.Services;
using DigitalisationERP.Core.Entities;
using DigitalisationERP.Core.Entities.IoT;
using DigitalisationERP.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DigitalisationERP.Application.Services
{
    /// <summary>
    /// Service central pour intégrer TOUTES les simulations
    /// Combine: Production + Maintenance + Senseurs en TEMPS RÉEL
    /// 
    /// Flux: Senseurs → Données Production → Alertes Maintenance → Dashboard
    /// </summary>
    public class RealTimeSimulationIntegrator
    {
        private readonly SensorSimulationService _sensorSimulation;
        private readonly ProductionSimulationService _productionSimulation;
        private readonly MaintenanceAlertManager _alertManager;
        private readonly MaintenanceHealthScoreCalculationService _healthScore;

        private readonly Dictionary<int, LineFullSnapshot> _lineSnapshots = new();

        public RealTimeSimulationIntegrator(
            SensorSimulationService sensorSimulation,
            ProductionSimulationService productionSimulation,
            MaintenanceAlertManager alertManager,
            MaintenanceHealthScoreCalculationService healthScore)
        {
            _sensorSimulation = sensorSimulation;
            _productionSimulation = productionSimulation;
            _alertManager = alertManager;
            _healthScore = healthScore;
        }

        /// <summary>
        /// Génère un snapshot COMPLET en temps réel pour une ligne:
        /// 1. Données de senseurs
        /// 2. Mise à jour production
        /// 3. Alertes maintenance
        /// 4. Santé équipement
        /// </summary>
        public LineFullSnapshot GenerateLineSnapshot(int lineId, List<int> postIds)
        {
            var snapshot = new LineFullSnapshot
            {
                LineId = lineId,
                SnapshotTime = DateTime.Now,
                SensorReadings = new List<SensorReadingSnapshot>(),
                ProductionUpdates = new List<ProductionUpdateSnapshot>(),
                MaintenanceAlerts = new List<AlertSnapshot>(),
                HealthScores = new List<HealthScoreSnapshot>(),
                Incidents = new List<IncidentSnapshot>()
            };

            foreach (var postId in postIds)
            {
                // 1️⃣ CAPTEURS: Génère lectures de senseurs
                var sensorReadings = _sensorSimulation.GeneratePostSensorReadings(postId, $"EQ-{postId:D3}");
                snapshot.SensorReadings.AddRange(sensorReadings.Select(sr => new SensorReadingSnapshot
                {
                    PostId = sr.ProductionPostId,
                    SensorType = sr.SensorType.ToString(),
                    Value = sr.Value,
                    IsNormal = sr.IsNormal,
                    AlertLevel = sr.AlertLevel?.ToString() ?? string.Empty,
                    Timestamp = sr.Timestamp
                }));

                // 2️⃣ PRODUCTION: Mise à jour production
                var prodUpdate = _productionSimulation.GenerateProductionUpdate(postId, lineId);
                snapshot.ProductionUpdates.Add(new ProductionUpdateSnapshot
                {
                    PostId = prodUpdate.PostId,
                    ItemsProduced = prodUpdate.ItemsProduced,
                    DefectCount = prodUpdate.DefectCount,
                    EfficiencyPercent = prodUpdate.EfficiencyPercent,
                    PostStatus = prodUpdate.PostStatus,
                    TaktTime = prodUpdate.TaktTimeSecond,
                    Downtime = prodUpdate.DowntimeSeconds
                });

                // 3️⃣ MAINTENANCE: Génère alertes basées sur senseurs
                var mockPost = new Core.Entities.ProductionPost { Id = postId, PostCode = $"POST-{postId}" };
                var alerts = _alertManager.GenerateAlertsForPost(mockPost, null, sensorReadings);
                snapshot.MaintenanceAlerts.AddRange(alerts.Select(a => new AlertSnapshot
                {
                    PostId = a.PostId,
                    Title = a.Title,
                    Severity = a.Severity,
                    DueDate = a.DueDate
                }));

                // 4️⃣ SANTÉ: Calcule score de santé
                var healthScore = _healthScore.CalculateHealthScore(mockPost);
                var (icon, status, description) = _healthScore.GetHealthStatus(healthScore);
                snapshot.HealthScores.Add(new HealthScoreSnapshot
                {
                    PostId = postId,
                    Score = healthScore,
                    Status = status,
                    Color = _healthScore.GetHealthStatusColor(healthScore),
                    Icon = icon
                });

                // 5️⃣ INCIDENTS: Incident aléatoire (5% chance)
                var incident = _productionSimulation.GenerateIncident(postId, lineId, probability: 0.05);
                if (incident != null)
                {
                    snapshot.Incidents.Add(new IncidentSnapshot
                    {
                        PostId = incident.PostId,
                        Type = incident.IncidentType,
                        Severity = incident.Severity,
                        EstimatedDowntime = incident.EstimatedDowntimeMinutes,
                        Timestamp = incident.Timestamp
                    });
                }
            }

            // Calcule métriques agrégées
            snapshot.LineMetrics = CalculateLineMetrics(snapshot);

            return snapshot;
        }

        /// <summary>
        /// Cycle complet pour le DASHBOARD EN TEMPS RÉEL
        /// Retourne tous les changements détectés
        /// </summary>
        public DashboardRealtimeUpdate GenerateDashboardUpdate(int lineId, List<int> postIds)
        {
            var snapshot = GenerateLineSnapshot(lineId, postIds);
            var previousSnapshot = GetPreviousSnapshot(lineId);

            var update = new DashboardRealtimeUpdate
            {
                Timestamp = DateTime.Now,
                LineId = lineId,
                CurrentSnapshot = snapshot
            };

            // Détecte les changements
            if (previousSnapshot != null)
            {
                update.Changes = DetectChanges(previousSnapshot, snapshot);
            }

            // Stocke le snapshot pour la prochaine comparaison
            _lineSnapshots[lineId] = snapshot;

            return update;
        }

        /// <summary>
        /// Stream continu d'événements
        /// Parfait pour WebSocket/SignalR
        /// </summary>
        public List<RealtimeEvent> GenerateEventStream(int lineId, int eventCount = 10)
        {
            var events = new List<RealtimeEvent>();
            var random = new Random();

            for (int i = 0; i < eventCount; i++)
            {
                var eventType = (RealtimeEventType)random.Next(0, 5);
                var severity = random.NextDouble() < 0.7 ? "Info" : (random.NextDouble() < 0.9 ? "Warning" : "Critical");

                events.Add(new RealtimeEvent
                {
                    Id = Guid.NewGuid(),
                    LineId = lineId,
                    EventType = eventType.ToString(),
                    Severity = severity,
                    Timestamp = DateTime.Now.AddSeconds(-random.Next(0, 60)),
                    Message = GenerateEventMessage(eventType),
                    PostId = random.Next(1, 8)
                });
            }

            return events;
        }

        /// <summary>
        /// Rapport complet de simulation
        /// </summary>
        public CompleteSimulationReport GenerateCompleteReport(int lineId, List<int> postIds)
        {
            var snapshot = GenerateLineSnapshot(lineId, postIds);
            var hourlyReport = _productionSimulation.GenerateHourlyReport(lineId, postIds);

            return new CompleteSimulationReport
            {
                LineId = lineId,
                ReportTime = DateTime.Now,
                Snapshot = snapshot,
                HourlyMetrics = new
                {
                    totalProduced = hourlyReport.TotalItemsProduced,
                    totalDefects = hourlyReport.TotalDefects,
                    avgEfficiency = hourlyReport.AverageEfficiency,
                    qualityRate = hourlyReport.QualityRate,
                    downtimeMinutes = hourlyReport.TotalDowntimeMinutes
                },
                HealthStatus = new
                {
                    avgHealthScore = snapshot.HealthScores.Average(h => h.Score),
                    criticalPosts = snapshot.HealthScores.Where(h => h.Score < 50).Count(),
                    warningPosts = snapshot.HealthScores.Where(h => h.Score >= 50 && h.Score < 70).Count(),
                    okPosts = snapshot.HealthScores.Where(h => h.Score >= 70).Count()
                },
                AlertStatus = new
                {
                    totalAlerts = snapshot.MaintenanceAlerts.Count,
                    criticalAlerts = snapshot.MaintenanceAlerts.Where(a => a.Severity == "Critical").Count(),
                    warningAlerts = snapshot.MaintenanceAlerts.Where(a => a.Severity == "Warning").Count()
                },
                IncidentStatus = new
                {
                    activeIncidents = snapshot.Incidents.Count,
                    totalDowntime = snapshot.Incidents.Sum(i => i.EstimatedDowntime),
                    avgDowntime = snapshot.Incidents.Any() ? snapshot.Incidents.Average(i => i.EstimatedDowntime) : 0
                }
            };
        }

        // ==================== PRIVATE METHODS ====================

        private LineMetrics CalculateLineMetrics(LineFullSnapshot snapshot)
        {
            var avgHealth = snapshot.HealthScores.Any() ? snapshot.HealthScores.Average(h => h.Score) : 0;
            var avgEfficiency = snapshot.ProductionUpdates.Any() ? snapshot.ProductionUpdates.Average(p => p.EfficiencyPercent) : 0;
            var criticalAlerts = snapshot.MaintenanceAlerts.Count(a => a.Severity == "Critical");
            var totalDowntime = snapshot.Incidents.Sum(i => i.EstimatedDowntime);

            return new LineMetrics
            {
                AverageHealthScore = Math.Round(avgHealth, 2),
                AverageEfficiency = Math.Round(avgEfficiency, 2),
                TotalItemsProduced = snapshot.ProductionUpdates.Sum(p => p.ItemsProduced),
                TotalDefects = snapshot.ProductionUpdates.Sum(p => p.DefectCount),
                QualityRate = snapshot.ProductionUpdates.Sum(p => p.ItemsProduced) > 0
                    ? Math.Round((1 - snapshot.ProductionUpdates.Sum(p => p.DefectCount) / (double)snapshot.ProductionUpdates.Sum(p => p.ItemsProduced)) * 100, 2)
                    : 100,
                CriticalAlertCount = criticalAlerts,
                LineStatus = avgHealth > 85 ? "Excellent" : (avgHealth > 70 ? "Good" : (avgHealth > 50 ? "Warning" : "Critical")),
                TotalDowntimeMinutes = totalDowntime
            };
        }

        private LineFullSnapshot? GetPreviousSnapshot(int lineId)
        {
            return _lineSnapshots.ContainsKey(lineId) ? _lineSnapshots[lineId] : null;
        }

        private SnapshotChanges DetectChanges(LineFullSnapshot previous, LineFullSnapshot current)
        {
            var changes = new SnapshotChanges();

            // Détecte changements de santé
            var healthChanges = current.HealthScores.Where(h =>
                !previous.HealthScores.Any(ph =>
                    ph.PostId == h.PostId && Math.Abs(ph.Score - h.Score) < 1)).ToList();
            changes.HealthScoreChanges = healthChanges.Count;

            // Détecte nouvelles alertes
            var newAlerts = current.MaintenanceAlerts.Where(a =>
                !previous.MaintenanceAlerts.Any(pa =>
                    pa.PostId == a.PostId && pa.Title == a.Title)).ToList();
            changes.NewAlerts = newAlerts.Count;

            // Détecte changements production
            var prodChanges = current.ProductionUpdates.Where(p =>
                !previous.ProductionUpdates.Any(pp =>
                    pp.PostId == p.PostId && Math.Abs(pp.EfficiencyPercent - p.EfficiencyPercent) < 5)).ToList();
            changes.ProductionChanges = prodChanges.Count;

            // Détecte incidents
            changes.NewIncidents = current.Incidents.Count - previous.Incidents.Count;

            changes.HasAnyChanges = changes.HealthScoreChanges > 0 ||
                                   changes.NewAlerts > 0 ||
                                   changes.ProductionChanges > 0 ||
                                   changes.NewIncidents > 0;

            return changes;
        }

        private string GenerateEventMessage(RealtimeEventType eventType)
        {
            return eventType switch
            {
                RealtimeEventType.SensorAlert => "Capteur: Anomalie détectée",
                RealtimeEventType.ProductionUpdate => "Production: Mise à jour en temps réel",
                RealtimeEventType.MaintenanceAlert => "Maintenance: Alerte générée",
                RealtimeEventType.IncidentDetected => "Incident: Détection de panne",
                RealtimeEventType.QualityIssue => "Qualité: Problème de contrôle",
                _ => "Événement système"
            };
        }
    }

    // ==================== SNAPSHOT & DTO MODELS ====================

    public class LineFullSnapshot
    {
        public int LineId { get; set; }
        public DateTime SnapshotTime { get; set; }
        public List<SensorReadingSnapshot> SensorReadings { get; set; } = new();
        public List<ProductionUpdateSnapshot> ProductionUpdates { get; set; } = new();
        public List<AlertSnapshot> MaintenanceAlerts { get; set; } = new();
        public List<HealthScoreSnapshot> HealthScores { get; set; } = new();
        public List<IncidentSnapshot> Incidents { get; set; } = new();
        public LineMetrics LineMetrics { get; set; } = new();
    }

    public class SensorReadingSnapshot
    {
        public int PostId { get; set; }
        public string SensorType { get; set; } = string.Empty;
        public double Value { get; set; }
        public bool IsNormal { get; set; }
        public string AlertLevel { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class ProductionUpdateSnapshot
    {
        public int PostId { get; set; }
        public int ItemsProduced { get; set; }
        public int DefectCount { get; set; }
        public double EfficiencyPercent { get; set; }
        public string PostStatus { get; set; } = string.Empty;
        public double TaktTime { get; set; }
        public int Downtime { get; set; }
    }

    public class AlertSnapshot
    {
        public int PostId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
    }

    public class HealthScoreSnapshot
    {
        public int PostId { get; set; }
        public double Score { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }

    public class IncidentSnapshot
    {
        public int PostId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public int EstimatedDowntime { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class LineMetrics
    {
        public double AverageHealthScore { get; set; }
        public double AverageEfficiency { get; set; }
        public int TotalItemsProduced { get; set; }
        public int TotalDefects { get; set; }
        public double QualityRate { get; set; }
        public int CriticalAlertCount { get; set; }
        public string LineStatus { get; set; } = string.Empty;
        public int TotalDowntimeMinutes { get; set; }
    }

    public class DashboardRealtimeUpdate
    {
        public DateTime Timestamp { get; set; }
        public int LineId { get; set; }
        public LineFullSnapshot CurrentSnapshot { get; set; } = new();
        public SnapshotChanges Changes { get; set; } = new();
    }

    public class SnapshotChanges
    {
        public int HealthScoreChanges { get; set; }
        public int NewAlerts { get; set; }
        public int ProductionChanges { get; set; }
        public int NewIncidents { get; set; }
        public bool HasAnyChanges { get; set; }
    }

    public class RealtimeEvent
    {
        public Guid Id { get; set; }
        public int LineId { get; set; }
        public int PostId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class CompleteSimulationReport
    {
        public int LineId { get; set; }
        public DateTime ReportTime { get; set; }
        public LineFullSnapshot Snapshot { get; set; } = new();
        public dynamic? HourlyMetrics { get; set; }
        public dynamic? HealthStatus { get; set; }
        public dynamic? AlertStatus { get; set; }
        public dynamic? IncidentStatus { get; set; }
    }

    public enum RealtimeEventType
    {
        SensorAlert,
        ProductionUpdate,
        MaintenanceAlert,
        IncidentDetected,
        QualityIssue
    }
}
