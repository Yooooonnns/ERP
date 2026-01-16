using DigitalisationERP.Core.Entities;
using DigitalisationERP.Core.Entities.IoT;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DigitalisationERP.Application.Services
{
    /// <summary>
    /// Service pour générer automatiquement les alertes de maintenance
    /// Basé sur: HealthScore, senseurs, tâches en retard
    /// </summary>
    public class MaintenanceAlertManager
    {
        private readonly List<MaintenanceAlert> _alerts = new();
        private readonly MaintenanceHealthScoreCalculationService _healthScoreService;

        public MaintenanceAlertManager(MaintenanceHealthScoreCalculationService healthScoreService)
        {
            _healthScoreService = healthScoreService;
        }

        /// <summary>
        /// Génère les alertes pour un poste basé sur sa santé actuelle
        /// </summary>
        public List<MaintenanceAlert> GenerateAlertsForPost(ProductionPost post, MaintenanceSchedule? latestMaintenance, List<SensorReading> sensorReadings)
        {
            var postAlerts = new List<MaintenanceAlert>();
            double healthScore = _healthScoreService.CalculateHealthScore(post);

            // Alerte 1: Santé critique (🔴 < 50)
            if (healthScore < 50)
            {
                postAlerts.Add(new MaintenanceAlert
                {
                    AlertId = Guid.NewGuid().ToString(),
                    PostCode = post?.PostCode ?? "",
                    PostName = post?.PostName ?? "",
                    AlertType = "HealthCritical",
                    Severity = "Critical",
                    Icon = "🔴",
                    Title = "MAINTENANCE CRITIQUE REQUISE",
                    Description = $"Santé du poste: {healthScore:F1}% - Maintenance immédiate nécessaire",
                    CreatedAt = DateTime.Now,
                    DueDate = DateTime.Now.AddHours(4),
                    Status = "Active",
                    RequiredAction = "Arrêter le poste et effectuer maintenance préventive complète",
                    EstimatedDuration = 240 // minutes
                });
            }

            // Alerte 2: Santé dégradée (🟠 50-70)
            else if (healthScore < 70)
            {
                postAlerts.Add(new MaintenanceAlert
                {
                    AlertId = Guid.NewGuid().ToString(),
                    PostCode = post.PostCode,
                    PostName = post.PostName,
                    AlertType = "HealthDegraded",
                    Severity = "High",
                    Icon = "🟠",
                    Title = "Maintenance planifiée recommandée",
                    Description = $"Santé du poste: {healthScore:F1}% - Planifier maintenance dans 48h",
                    CreatedAt = DateTime.Now,
                    DueDate = DateTime.Now.AddDays(2),
                    Status = "Active",
                    RequiredAction = "Planifier visite maintenance",
                    EstimatedDuration = 120 // minutes
                });
            }

            // Alerte 3: Santé moyenne (🟡 70-85)
            else if (healthScore < 85)
            {
                postAlerts.Add(new MaintenanceAlert
                {
                    AlertId = Guid.NewGuid().ToString(),
                    PostCode = post.PostCode,
                    PostName = post.PostName,
                    AlertType = "HealthWarning",
                    Severity = "Medium",
                    Icon = "🟡",
                    Title = "Suivi maintenance suggéré",
                    Description = $"Santé du poste: {healthScore:F1}% - Suivi recommandé",
                    CreatedAt = DateTime.Now,
                    DueDate = DateTime.Now.AddDays(7),
                    Status = "Active",
                    RequiredAction = "Programmer inspection",
                    EstimatedDuration = 60 // minutes
                });
            }

            // Alerte 4: Maintenance en retard
            if (latestMaintenance != null && latestMaintenance.Status == MaintenanceStatusEnum.Overdue)
            {
                DateTime scheduledDate = latestMaintenance.ScheduledDate ?? DateTime.Now;
                int daysOverdue = (int)(DateTime.Now - scheduledDate).TotalDays;
                postAlerts.Add(new MaintenanceAlert
                {
                    AlertId = Guid.NewGuid().ToString(),
                    PostCode = post?.PostCode ?? string.Empty,
                    PostName = post?.PostName ?? string.Empty,
                    AlertType = "MaintenanceOverdue",
                    Severity = "Critical",
                    Icon = "⏰",
                    Title = $"MAINTENANCE EN RETARD ({daysOverdue}j)",
                    Description = $"Maintenance prévue le {scheduledDate:dd/MM/yyyy} - {daysOverdue} jours de retard",
                    CreatedAt = DateTime.Now,
                    DueDate = DateTime.Now,
                    Status = "Active",
                    RequiredAction = "Effectuer maintenance immédiatement",
                    EstimatedDuration = latestMaintenance.EstimatedDurationMinutes ?? 120
                });
            }

            // Alerte 5: Seuils senseurs dépassés
            var criticalSensorReadings = sensorReadings
                .Where(s => s.AlertLevel == AlertLevel.Critical && 
                           s.Timestamp >= DateTime.Now.AddDays(-1))
                .ToList();

            if (criticalSensorReadings.Any())
            {
                var sensorAlert = criticalSensorReadings.First();
                postAlerts.Add(new MaintenanceAlert
                {
                    AlertId = Guid.NewGuid().ToString(),
                    PostCode = post?.PostCode ?? "",
                    PostName = post?.PostName ?? "",
                    AlertType = "SensorCritical",
                    Severity = "Critical",
                    Icon = "📊",
                    Title = "Lecture senseur critique",
                    Description = $"Senseur [{sensorAlert.SensorType}]: {sensorAlert.Value} {sensorAlert.Unit} (seuil min: {sensorAlert.ThresholdMin}, max: {sensorAlert.ThresholdMax})",
                    CreatedAt = DateTime.Now,
                    DueDate = DateTime.Now.AddHours(2),
                    Status = "Active",
                    RequiredAction = "Inspecter et calibrer senseur ou vérifier équipement",
                    EstimatedDuration = 90
                });
            }

            return postAlerts;
        }

        /// <summary>
        /// Génère toutes les alertes pour une ligne de production
        /// </summary>
        public List<MaintenanceAlert> GenerateAlertsForLine(
            int lineId, 
            List<ProductionPost> posts,
            List<MaintenanceSchedule> schedules,
            List<SensorReading> sensorReadings)
        {
            var allAlerts = new List<MaintenanceAlert>();

            var linePosts = posts.Where(p => p.ProductionLineId == lineId).ToList();

            foreach (var post in linePosts)
            {
                var latestMaintenance = schedules
                    .Where(s => s.ProductionPostId == post.Id)
                    .OrderByDescending(s => s.ScheduledDate)
                    .FirstOrDefault();

                var postSensorReadings = sensorReadings
                    .Where(s => s.ProductionPostId == post.Id && s.Timestamp >= DateTime.Now.AddDays(-1))
                    .ToList();

                var postAlerts = GenerateAlertsForPost(post, latestMaintenance, postSensorReadings);
                allAlerts.AddRange(postAlerts);
            }

            return allAlerts;
        }

        /// <summary>
        /// Filtre les alertes par sévérité
        /// </summary>
        public List<MaintenanceAlert> GetAlertsBySeverity(List<MaintenanceAlert> alerts, string severity)
        {
            return alerts.Where(a => a.Severity == severity).OrderByDescending(a => a.CreatedAt).ToList();
        }

        /// <summary>
        /// Compte les alertes par sévérité
        /// </summary>
        public AlertCountByType GetAlertCounts(List<MaintenanceAlert> alerts)
        {
            return new AlertCountByType
            {
                CriticalCount = alerts.Count(a => a.Severity == "Critical"),
                HighCount = alerts.Count(a => a.Severity == "High"),
                MediumCount = alerts.Count(a => a.Severity == "Medium"),
                LowCount = alerts.Count(a => a.Severity == "Low"),
                TotalCount = alerts.Count
            };
        }

        /// <summary>
        /// Marque une alerte comme acquittée (acknowledged)
        /// </summary>
        public void AcknowledgeAlert(string alertId)
        {
            var alert = _alerts.FirstOrDefault(a => a.AlertId == alertId);
            if (alert != null)
            {
                alert.Status = "Acknowledged";
                alert.AcknowledgedAt = DateTime.Now;
            }
        }

        /// <summary>
        /// Enregistre une alerte
        /// </summary>
        public void RecordAlert(MaintenanceAlert alert)
        {
            _alerts.Add(alert);
        }

        /// <summary>
        /// Récupère les alertes actives
        /// </summary>
        public List<MaintenanceAlert> GetActiveAlerts()
        {
            return _alerts
                .Where(a => a.Status == "Active" || a.Status == "Acknowledged")
                .OrderByDescending(a => a.CreatedAt)
                .ToList();
        }

        /// <summary>
        /// Récupère les alertes pour un poste
        /// </summary>
        public List<MaintenanceAlert> GetAlertsForPost(string postCode)
        {
            return _alerts
                .Where(a => a.PostCode == postCode && a.Status == "Active")
                .OrderByDescending(a => a.CreatedAt)
                .ToList();
        }

        /// <summary>
        /// Efface une alerte résolvée
        /// </summary>
        public void ResolveAlert(string alertId)
        {
            var alert = _alerts.FirstOrDefault(a => a.AlertId == alertId);
            if (alert != null)
            {
                alert.Status = "Resolved";
                alert.ResolvedAt = DateTime.Now;
            }
        }
    }

    /// <summary>
    /// Représente une alerte de maintenance
    /// </summary>
    public class MaintenanceAlert
    {
        public string AlertId { get; set; } = string.Empty;
        public int PostId { get; set; }                   // Added missing property
        public string PostCode { get; set; } = string.Empty;
        public string PostName { get; set; } = string.Empty;
        public string AlertType { get; set; } = string.Empty; // HealthCritical, MaintenanceOverdue, SensorCritical, etc.
        public string Severity { get; set; } = string.Empty; // Critical, High, Medium, Low
        public string Icon { get; set; } = string.Empty; // 🔴, 🟠, 🟡, 🟢
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = string.Empty; // Active, Acknowledged, Resolved
        public DateTime? AcknowledgedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string RequiredAction { get; set; } = string.Empty;
        public int EstimatedDuration { get; set; } // minutes
    }

    /// <summary>
    /// Comptage des alertes par type
    /// </summary>
    public class AlertCountByType
    {
        public int CriticalCount { get; set; }
        public int HighCount { get; set; }
        public int MediumCount { get; set; }
        public int LowCount { get; set; }
        public int TotalCount { get; set; }
    }
}
