using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DigitalisationERP.Domain.Entities;
using DigitalisationERP.Application.Interfaces;

namespace DigitalisationERP.Application.Services
{
    /// <summary>
    /// Service de gestion des alertes automatiques basées sur les seuils
    /// </summary>
    public class AlertService : IAlertService
    {
        private static readonly List<Alert> _alerts = new();
        private static int _nextAlertId = 1;

        /// <summary>
        /// Vérifie les seuils et crée les alertes nécessaires
        /// </summary>
        public async Task<List<Alert>> CheckAndCreateAlertsAsync(
            ProductionLine line,
            OEEMetrics metrics,
            List<ProductionPost> posts)
        {
            await Task.Delay(50);

            var newAlerts = new List<Alert>();

            // ALERTE 1: OEE Critique
            var minOEEPercent = line.CalculateMinOEEPercentage();
            if (metrics.OEE < minOEEPercent * 0.8)  // Seuil critique à 80% du minimum
            {
                var alert = new Alert
                {
                    AlertId = _nextAlertId++,
                    LineId = line.LineId,
                    AlertType = "OEE_CRITICAL",
                    Severity = metrics.OEE < minOEEPercent ? "Critical" : "Warning",
                    Message = $"OEE {metrics.OEE:F1}% below minimum {minOEEPercent:F1}%",
                    RecommendedAction = "Check production line status immediately",
                    CreatedDate = DateTime.UtcNow
                };
                newAlerts.Add(alert);
            }

            // ALERTE 2: Stock faible par poste
            foreach (var post in posts)
            {
                var stockPercentage = (post.CurrentStock / (double)post.MaxCapacity) * 100;

                if (stockPercentage < 20)
                {
                    var severity = stockPercentage < 10 ? "Critical" : "Warning";
                    var alert = new Alert
                    {
                        AlertId = _nextAlertId++,
                        LineId = line.LineId,
                        PostId = post.PostId,
                        AlertType = "STOCK_LOW",
                        Severity = severity,
                        Message = $"Post '{post.PostName}' stock at {stockPercentage:F0}% ({post.CurrentStock}/{post.MaxCapacity})",
                        RecommendedAction = severity == "Critical"
                            ? "Immediately replenish material or start upstream post"
                            : "Monitor stock levels closely",
                        CreatedDate = DateTime.UtcNow
                    };
                    newAlerts.Add(alert);
                }
            }

            // ALERTE 3: Cadence inférieure à la cible
            var performanceThreshold = line.MinPerformance;
            if (metrics.Performance < performanceThreshold)
            {
                var alert = new Alert
                {
                    AlertId = _nextAlertId++,
                    LineId = line.LineId,
                    AlertType = "LOW_CADENCE",
                    Severity = metrics.Performance < performanceThreshold * 0.8 ? "Critical" : "Warning",
                    Message = $"Performance {metrics.Performance:F1}% below target {performanceThreshold:F1}%",
                    RecommendedAction = "Investigate speed bottlenecks or equipment issues",
                    CreatedDate = DateTime.UtcNow
                };
                newAlerts.Add(alert);
            }

            // ALERTE 4: Disponibilité basse (arrêts/maintenance)
            var availabilityThreshold = line.MinAvailability;
            if (metrics.Availability < availabilityThreshold)
            {
                var alert = new Alert
                {
                    AlertId = _nextAlertId++,
                    LineId = line.LineId,
                    AlertType = "LOW_AVAILABILITY",
                    Severity = "Warning",
                    Message = $"Availability {metrics.Availability:F1}% below target {availabilityThreshold:F1}%",
                    RecommendedAction = "Check for equipment failures or maintenance issues",
                    CreatedDate = DateTime.UtcNow
                };
                newAlerts.Add(alert);
            }

            // ALERTE 5: Qualité basse (défauts)
            var qualityThreshold = line.MinQuality;
            if (metrics.Quality < qualityThreshold)
            {
                var alert = new Alert
                {
                    AlertId = _nextAlertId++,
                    LineId = line.LineId,
                    AlertType = "QUALITY_LOW",
                    Severity = "Warning",
                    Message = $"Quality {metrics.Quality:F1}% below target {qualityThreshold:F1}%",
                    RecommendedAction = "Review quality control measures and adjust parameters",
                    CreatedDate = DateTime.UtcNow
                };
                newAlerts.Add(alert);
            }

            // Sauvegarder les alertes
            _alerts.AddRange(newAlerts);

            return newAlerts;
        }

        /// <summary>
        /// Récupère les alertes actives (non acquittées) d'une ligne
        /// </summary>
        public async Task<List<Alert>> GetActiveAlertsAsync(int lineId)
        {
            await Task.Delay(50);
            return _alerts
                .Where(a => a.LineId == lineId && !a.IsAcknowledged)
                .OrderByDescending(a => a.CreatedDate)
                .ToList();
        }
    }
}
