using DigitalisationERP.Core.Entities;
using DigitalisationERP.Core.Entities.IoT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DigitalisationERP.Application.Services
{
    /// <summary>
    /// Service pour calculer dynamiquement la santé (health score) de chaque poste de production
    /// Formule: HealthScore = (DaysOK/14 × 40%) + (CompletionRate × 35%) + (SensorHealth × 25%)
    /// </summary>
    public class MaintenanceHealthScoreCalculationService
    {
        private readonly List<MaintenanceSchedule> _maintenanceHistory = new();
        private readonly List<SensorReading> _sensorReadings = new();

        // Paramètres de calcul
        private const double DaysOkWeight = 0.40;          // 40% poids pour délai depuis maintenance
        private const double CompletionRateWeight = 0.35;  // 35% poids pour taux complétion
        private const double SensorHealthWeight = 0.25;    // 25% poids pour santé senseurs
        private const int OptimalDaysBetweenMaintenance = 14;

        /// <summary>
        /// Calcule le health score d'un poste de production
        /// </summary>
        public double CalculateHealthScore(ProductionPost post)
        {
            if (post == null) return 0;

            // 1. Facteur 1 : Jours depuis dernière maintenance (40%)
            double daysOkScore = CalculateDaysOkScore((long)post.Id);

            // 2. Facteur 2 : Taux de complétion des maintenances prévues (35%)
            double completionRateScore = CalculateCompletionRateScore((long)post.Id);

            // 3. Facteur 3 : Santé des senseurs (25%)
            double sensorHealthScore = CalculateSensorHealthScore((long)post.Id);

            // Formule pondérée
            double healthScore = (daysOkScore * DaysOkWeight) +
                                (completionRateScore * CompletionRateWeight) +
                                (sensorHealthScore * SensorHealthWeight);

            // Borner entre 0 et 100
            return Math.Min(Math.Max(healthScore, 0), 100);
        }

        /// <summary>
        /// Calcule le score basé sur les jours depuis dernière maintenance (0-100)
        /// Plus la maintenance est récente, plus le score est élevé
        /// </summary>
        private double CalculateDaysOkScore(long postId)
        {
            var lastMaintenance = _maintenanceHistory
                .Where(m => m.ProductionPostId == postId && m.Status == MaintenanceStatusEnum.Completed)
                .OrderByDescending(m => m.CompletedDate)
                .FirstOrDefault();

            if (lastMaintenance == null)
                return 0; // Jamais maintenu = santé très mauvaise

            int daysSinceMaintenance = (int)(DateTime.Now - (lastMaintenance.CompletedDate ?? DateTime.Now)).TotalDays;

            // Score décroît linéairement
            // À 0 jours: 100%, à 14 jours: 50%, à 28 jours: 0%
            if (daysSinceMaintenance <= 0)
                return 100;
            else if (daysSinceMaintenance >= OptimalDaysBetweenMaintenance * 2)
                return 0;
            else
                return 100 * (1 - (daysSinceMaintenance / (double)(OptimalDaysBetweenMaintenance * 2)));
        }

        /// <summary>
        /// Calcule le score basé sur le taux de complétion des maintenances (0-100)
        /// Pénalise les tâches en retard
        /// </summary>
        private double CalculateCompletionRateScore(long postId)
        {
            var scheduledTasks = _maintenanceHistory
                .Where(m => m.ProductionPostId == postId)
                .ToList();

            if (scheduledTasks.Count == 0)
                return 50; // Pas de données = score neutre

            var completedTasks = scheduledTasks.Count(m => m.Status == MaintenanceStatusEnum.Completed);
            var overdueTasks = scheduledTasks.Count(m => m.Status == MaintenanceStatusEnum.Overdue);

            // Pénalité pour retard: chaque tâche en retard = -10 points
            double penalty = overdueTasks * 10;
            double baseScore = (completedTasks / (double)scheduledTasks.Count) * 100;

            return Math.Max(baseScore - penalty, 0);
        }

        /// <summary>
        /// Calcule le score basé sur la santé des senseurs du poste (0-100)
        /// Prend en compte les lectures critiques des derniers jours
        /// </summary>
        private double CalculateSensorHealthScore(long postId)
        {
            var recentReadings = _sensorReadings
                .Where(r => r.ProductionPostId == postId && 
                           r.Timestamp >= DateTime.Now.AddDays(-7))
                .ToList();

            if (recentReadings.Count == 0)
                return 75; // Pas de données senseurs = score neutre

            // Compte les alertes par niveau
            var criticalAlerts = recentReadings.Count(r => r.AlertLevel == AlertLevel.Critical);
            var warningAlerts = recentReadings.Count(r => r.AlertLevel == AlertLevel.Warning);
            var infoAlerts = recentReadings.Count(r => r.AlertLevel == AlertLevel.Info);

            // Formule de pénalité
            double penalty = (criticalAlerts * 15) + (warningAlerts * 5) + (infoAlerts * 1);
            double baseScore = 100 - penalty;

            return Math.Max(baseScore, 0);
        }

        /// <summary>
        /// Calcule les health scores pour une ligne entière de production
        /// </summary>
        public Dictionary<long, double> CalculateLineHealthScores(int lineId, List<ProductionPost> posts)
        {
            var result = new Dictionary<long, double>();

            var linePosts = posts.Where(p => p.ProductionLineId == lineId).ToList();
            foreach (var post in linePosts)
            {
                result[post.Id] = CalculateHealthScore(post);
            }

            return result;
        }

        /// <summary>
        /// Détermine la couleur du statut en fonction du health score
        /// </summary>
        public string GetHealthStatusColor(double healthScore)
        {
            if (healthScore >= 85)
                return "Green";      // 🟢 Bon fonctionnement
            else if (healthScore >= 70)
                return "Yellow";     // 🟡 Attention requise
            else if (healthScore >= 50)
                return "Orange";     // 🟠 Maintenance prévue bientôt
            else
                return "Red";        // 🔴 Maintenance critique
        }

        /// <summary>
        /// Détermine le statut textuel et l'icône
        /// </summary>
        public (string Icon, string Status, string Description) GetHealthStatus(double healthScore)
        {
            return healthScore switch
            {
                >= 85 => ("🟢", "Good", "Equipment operating normally"),
                >= 70 => ("🟡", "Warning", "Maintenance attention needed"),
                >= 50 => ("🟠", "Scheduled", "Maintenance planned soon"),
                _ => ("🔴", "Critical", "Immediate maintenance required")
            };
        }

        /// <summary>
        /// Ajoute une lecture de maintenance à l'historique
        /// </summary>
        public void AddMaintenanceRecord(MaintenanceSchedule schedule)
        {
            _maintenanceHistory.Add(schedule);
        }

        /// <summary>
        /// Ajoute une lecture de senseur
        /// </summary>
        public void AddSensorReading(SensorReading reading)
        {
            _sensorReadings.Add(reading);
        }

        /// <summary>
        /// Récupère l'historique de santé d'un poste (pour graphiques)
        /// </summary>
        public List<(DateTime Date, double HealthScore)> GetHealthHistory(int postId, int daysBack = 30)
        {
            var history = new List<(DateTime, double)>();

            for (int i = daysBack; i >= 0; i--)
            {
                var date = DateTime.Now.AddDays(-i);
                // Simulation: on pourrait récupérer les données historiques
                history.Add((date, 75 + (i % 20))); // Mock data
            }

            return history;
        }

        /// <summary>
        /// Calcule les KPI clés pour une ligne de production
        /// </summary>
        public MaintenanceKPIs CalculateKPIs(int lineId, List<ProductionPost> posts, List<MaintenanceSchedule> schedules)
        {
            var linePosts = posts.Where(p => p.ProductionLineId == lineId).ToList();
            var lineSchedules = schedules.Where(s => linePosts.Any(p => p.Id == s.ProductionPostId)).ToList();

            var completedSchedules = lineSchedules.Where(s => s.Status == MaintenanceStatusEnum.Completed).ToList();
            var overdueSchedules = lineSchedules.Where(s => s.Status == MaintenanceStatusEnum.Overdue).ToList();
            var inProgressSchedules = lineSchedules.Where(s => s.Status == MaintenanceStatusEnum.InProgress).ToList();

            // MTBF = Mean Time Between Failures (jours moyens entre pannes)
            double mtbf = completedSchedules.Count > 0
                ? (DateTime.Now - (completedSchedules.First().CompletedDate ?? DateTime.Now)).TotalDays / completedSchedules.Count
                : 0;

            // MTTR = Mean Time To Repair (heures moyennes de réparation)
            double mttr = 0;
            if (completedSchedules.Count > 0)
            {
                var totalDuration = completedSchedules.Sum(s => s.ActualDurationMinutes ?? 0);
                mttr = totalDuration / (completedSchedules.Count * 60.0); // convertir en heures
            }

            // Availability = temps opérationnel / temps total
            double availability = linePosts.Any() 
                ? linePosts.Average(p => CalculateHealthScore(p)) 
                : 0;

            // Taux de surcharge
            double overdueRate = lineSchedules.Count > 0
                ? (overdueSchedules.Count / (double)lineSchedules.Count) * 100
                : 0;

            return new MaintenanceKPIs
            {
                MTBF = mtbf,
                MTTR = mttr,
                Availability = availability,
                OverdueTasksRate = overdueRate,
                TotalScheduledTasks = lineSchedules.Count,
                CompletedTasks = completedSchedules.Count,
                OverdueTasks = overdueSchedules.Count,
                InProgressTasks = inProgressSchedules.Count,
                AverageHealthScore = availability
            };
        }
    }

    /// <summary>
    /// Classe pour regrouper les KPI de maintenance
    /// </summary>
    public class MaintenanceKPIs
    {
        /// <summary>Mean Time Between Failures (jours)</summary>
        public double MTBF { get; set; }

        /// <summary>Mean Time To Repair (heures)</summary>
        public double MTTR { get; set; }

        /// <summary>Disponibilité équipement (0-100%)</summary>
        public double Availability { get; set; }

        /// <summary>Taux de tâches en retard (0-100%)</summary>
        public double OverdueTasksRate { get; set; }

        /// <summary>Nombre total de tâches planifiées</summary>
        public int TotalScheduledTasks { get; set; }

        /// <summary>Nombre de tâches complétées</summary>
        public int CompletedTasks { get; set; }

        /// <summary>Nombre de tâches en retard</summary>
        public int OverdueTasks { get; set; }

        /// <summary>Nombre de tâches en cours</summary>
        public int InProgressTasks { get; set; }

        /// <summary>Score de santé moyen (0-100)</summary>
        public double AverageHealthScore { get; set; }

        public override string ToString()
        {
            return $"MTBF: {MTBF:F1}d | MTTR: {MTTR:F1}h | Availability: {Availability:F1}% | Overdue: {OverdueTasksRate:F1}%";
        }
    }
}
