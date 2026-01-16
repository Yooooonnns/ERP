using DigitalisationERP.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DigitalisationERP.Application.Services
{
    /// <summary>
    /// Service pour la planification et gestion des maintenances prévues
    /// Crée, modifie, valide et assigne les tâches de maintenance
    /// </summary>
    public class PlannedMaintenanceService
    {
        private readonly List<PlannedMaintenanceTask> _plannedTasks = new();
        private readonly List<MaintenanceSchedule> _schedules;

        public PlannedMaintenanceService(List<MaintenanceSchedule> schedules)
        {
            _schedules = schedules ?? new List<MaintenanceSchedule>();
        }

        /// <summary>
        /// Crée une nouvelle tâche de maintenance planifiée
        /// </summary>
        public Task<(bool Success, string Message, PlannedMaintenanceTask? Task)> CreatePlannedMaintenanceAsync(
            int postId,
            string postCode,
            string title,
            string description,
            DateTime scheduledStartDate,
            DateTime scheduledEndDate,
            string maintenanceType, // "Preventive", "Corrective", "Predictive"
            string priority,         // "Low", "Normal", "High", "Critical"
            int estimatedDurationMinutes,
            string assignedTechnicianId)
        {
            try
            {
                // Validation 1: Les dates doivent être valides
                if (scheduledEndDate <= scheduledStartDate)
                    return Task.FromResult<(bool Success, string Message, PlannedMaintenanceTask? Task)>((false, "End date must be after start date", null));

                // Validation 2: Pas de chevauchement avec tâche existante
                var conflictingTasks = _plannedTasks
                    .Where(t => t.PostId == postId &&
                               t.Status != "Cancelled" &&
                               t.ScheduledStartDate < scheduledEndDate &&
                               t.ScheduledEndDate > scheduledStartDate)
                    .ToList();

                if (conflictingTasks.Any())
                    return Task.FromResult<(bool Success, string Message, PlannedMaintenanceTask? Task)>((false, $"Conflict with existing maintenance: {conflictingTasks.First().Title}", null));

                // Validation 3: Vérifie les disponibilités des techniciens
                var technicianAvailability = CheckTechnicianAvailability(assignedTechnicianId, scheduledStartDate, scheduledEndDate);
                if (!technicianAvailability)
                    return Task.FromResult<(bool Success, string Message, PlannedMaintenanceTask? Task)>((false, "Technician not available for this period", null));

                // Crée la tâche
                var task = new PlannedMaintenanceTask
                {
                    Id = Guid.NewGuid().ToString(),
                    PostId = postId,
                    PostCode = postCode,
                    Title = title,
                    Description = description,
                    ScheduledStartDate = scheduledStartDate,
                    ScheduledEndDate = scheduledEndDate,
                    MaintenanceType = maintenanceType,
                    Priority = priority,
                    EstimatedDurationMinutes = estimatedDurationMinutes,
                    ActualDurationMinutes = null,
                    AssignedTechnicianId = assignedTechnicianId,
                    Status = "Scheduled", // Scheduled → InProgress → Completed/Cancelled
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    Notes = new List<string>(),
                    Materials = new List<string>(),
                    CalculatedMTTR = CalculateMTTR(estimatedDurationMinutes)
                };

                _plannedTasks.Add(task);

                // Crée aussi une entrée MaintenanceSchedule pour la persistance
                var schedule = new MaintenanceSchedule
                {
                    MaintenanceCode = $"PM-{postCode}-{DateTime.Now:yyyyMMddHHmmss}",
                    ProductionPostId = postId,
                    Title = title,
                    Description = description,
                    MaintenanceType = Enum.TryParse<MaintenanceTypeEnum>(maintenanceType, true, out var mt) ? mt : MaintenanceTypeEnum.Preventive,
                    Priority = Enum.TryParse<MaintenancePriorityEnum>(priority, true, out var pr) ? pr : MaintenancePriorityEnum.Normal,
                    Status = Enum.TryParse<MaintenanceStatusEnum>("Scheduled", true, out var st) ? st : MaintenanceStatusEnum.Scheduled,
                    ScheduledDate = scheduledStartDate,
                    EstimatedDurationMinutes = estimatedDurationMinutes,
                    CreatedDate = DateTime.Now
                };

                _schedules.Add(schedule);

                return Task.FromResult<(bool Success, string Message, PlannedMaintenanceTask? Task)>((true, $"Maintenance scheduled for {scheduledStartDate:dd/MM/yyyy HH:mm}", task));
            }
            catch (Exception ex)
            {
                return Task.FromResult<(bool Success, string Message, PlannedMaintenanceTask? Task)>((false, $"Error: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Met à jour une tâche de maintenance planifiée
        /// </summary>
        public Task<(bool Success, string Message)> UpdatePlannedMaintenanceAsync(
            string taskId,
            DateTime? newStartDate = null,
            DateTime? newEndDate = null,
            string? newStatus = null,
            string? notes = null)
        {
            try
            {
                var task = _plannedTasks.FirstOrDefault(t => t.Id == taskId);
                if (task == null)
                    return Task.FromResult((false, "Task not found"));

                // Ne peut pas modifier une tâche complétée ou annulée
                if (task.Status == "Completed" || task.Status == "Cancelled")
                    return Task.FromResult((false, $"Cannot modify {task.Status} task"));

                if (newStartDate.HasValue && newEndDate.HasValue)
                {
                    if (newEndDate <= newStartDate)
                        return Task.FromResult((false, "End date must be after start date"));

                    task.ScheduledStartDate = newStartDate.Value;
                    task.ScheduledEndDate = newEndDate.Value;
                }

                if (!string.IsNullOrEmpty(newStatus))
                {
                    task.Status = newStatus;

                    if (newStatus == "Completed")
                    {
                        task.CompletedDate = DateTime.Now;
                        task.ActualDurationMinutes = (int)(DateTime.Now - task.ScheduledStartDate).TotalMinutes;
                    }
                }

                if (!string.IsNullOrEmpty(notes))
                {
                    task.Notes.Add($"[{DateTime.Now:HH:mm}] {notes}");
                }

                task.UpdatedAt = DateTime.Now;

                return Task.FromResult((true, "Task updated successfully"));
            }
            catch (Exception ex)
            {
                return Task.FromResult((false, $"Error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Valide si une maintenance peut être effectuée sur un poste
        /// Retourne des avertissements si des conditions ne sont pas optimales
        /// </summary>
        public ValidationResult ValidateMaintenanceRequest(
            int postId,
            DateTime startDate,
            DateTime endDate,
            List<ProductionPost> allPosts)
        {
            var result = new ValidationResult { IsValid = true, Warnings = new List<string>() };

            // Vérifie si le poste existe
            var post = allPosts.FirstOrDefault(p => p.Id == postId);
            if (post == null)
            {
                result.IsValid = false;
                result.Warnings.Add("Post not found");
                return result;
            }

            // Avertissement 1: Poste actuellement en production
            if (post.Status == PostStatus.Active)
            {
                result.Warnings.Add("⚠️ Post is currently active - production will be interrupted");
            }

            // Avertissement 2: Maintenance très longue
            int durationHours = (int)(endDate - startDate).TotalHours;
            if (durationHours > 8)
            {
                result.Warnings.Add($"⚠️ Long maintenance duration ({durationHours}h) - may affect production schedule");
            }

            // Avertissement 3: Planifiée pendant une période de forte production
            if (IsHighProductionPeriod(startDate))
            {
                result.Warnings.Add("⚠️ Scheduled during peak production hours");
            }

            // Avertissement 4: Maintenance en arrière-plan déjà planifiée
            var conflictCount = _plannedTasks
                .Count(t => t.PostId == postId &&
                           t.Status == "Scheduled" &&
                           t.ScheduledStartDate < endDate &&
                           t.ScheduledEndDate > startDate);

            if (conflictCount > 0)
            {
                result.Warnings.Add($"⚠️ {conflictCount} other maintenance task(s) already scheduled during this period");
            }

            return result;
        }

        /// <summary>
        /// Obtient le calendrier de maintenance pour un poste
        /// </summary>
        public List<PlannedMaintenanceTask> GetMaintenanceCalendar(int postId, int monthsAhead = 3)
        {
            return _plannedTasks
                .Where(t => t.PostId == postId &&
                           t.ScheduledStartDate <= DateTime.Now.AddMonths(monthsAhead))
                .OrderBy(t => t.ScheduledStartDate)
                .ToList();
        }

        /// <summary>
        /// Obtient toutes les tâches planifiées pour une ligne
        /// </summary>
        public List<PlannedMaintenanceTask> GetLineMaintenanceSchedule(int lineId, List<ProductionPost> posts)
        {
            var postIds = posts
                .Where(p => p.ProductionLineId == lineId)
                .Select(p => p.Id)
                .ToList();

            return _plannedTasks
                .Where(t => postIds.Contains(t.PostId))
                .OrderBy(t => t.ScheduledStartDate)
                .ToList();
        }

        /// <summary>
        /// Obtient les tâches urgentes (à court terme ou en retard)
        /// </summary>
        public List<PlannedMaintenanceTask> GetUrgentTasks()
        {
            return _plannedTasks
                .Where(t => (t.Status == "Scheduled" && t.ScheduledStartDate <= DateTime.Now.AddDays(3)) ||
                           (t.Status == "Scheduled" && t.ScheduledStartDate < DateTime.Now) ||
                           t.Status == "InProgress")
                .OrderBy(t => t.ScheduledStartDate)
                .ToList();
        }

        /// <summary>
        /// Assigne un technicien à une tâche
        /// </summary>
        public Task<(bool Success, string Message)> AssignTechnicianAsync(
            string taskId,
            string technicianId,
            string technicianName)
        {
            var task = _plannedTasks.FirstOrDefault(t => t.Id == taskId);
            if (task == null)
                return Task.FromResult((false, "Task not found"));

            task.AssignedTechnicianId = technicianId;
            task.AssignedTechnicianName = technicianName;

            return Task.FromResult((true, $"Assigned to {technicianName}"));
        }

        /// <summary>
        /// Ajoute des matériaux requis à une tâche
        /// </summary>
        public void AddMaterialToTask(string taskId, string material)
        {
            var task = _plannedTasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null && !task.Materials.Contains(material))
            {
                task.Materials.Add(material);
            }
        }

        /// <summary>
        /// Calcule le MTTR (Mean Time To Repair) pour une tâche
        /// </summary>
        private double CalculateMTTR(int estimatedDurationMinutes)
        {
            // Ajoute 20% de marge (temps d'accès, diagnostic, etc.)
            return estimatedDurationMinutes * 1.2 / 60.0; // convertir en heures
        }

        /// <summary>
        /// Vérifie la disponibilité d'un technicien
        /// </summary>
        private bool CheckTechnicianAvailability(string technicianId, DateTime startDate, DateTime endDate)
        {
            // Logique mock: supposer toujours disponible
            // En production: vérifier contre calendrier des techniciens
            return true;
        }

        /// <summary>
        /// Détermine si c'est une période de forte production
        /// </summary>
        private bool IsHighProductionPeriod(DateTime date)
        {
            // Logique mock: fort production entre 6h et 14h, et 14h à 22h
            int hour = date.Hour;
            return (hour >= 6 && hour < 22);
        }

        /// <summary>
        /// Récupère les statistiques de maintenance
        /// </summary>
        public MaintenanceStatistics GetMaintenanceStatistics()
        {
            return new MaintenanceStatistics
            {
                TotalScheduledTasks = _plannedTasks.Count(t => t.Status == "Scheduled"),
                TotalInProgressTasks = _plannedTasks.Count(t => t.Status == "InProgress"),
                TotalCompletedTasks = _plannedTasks.Count(t => t.Status == "Completed"),
                TotalCancelledTasks = _plannedTasks.Count(t => t.Status == "Cancelled"),
                CompletionRate = _plannedTasks.Count > 0
                    ? (_plannedTasks.Count(t => t.Status == "Completed") / (double)_plannedTasks.Count) * 100
                    : 0,
                AverageTaskDuration = _plannedTasks
                    .Where(t => t.ActualDurationMinutes.HasValue)
                    .Average(t => t.ActualDurationMinutes) ?? 0
            };
        }
    }

    /// <summary>
    /// Représente une tâche de maintenance planifiée
    /// </summary>
    public class PlannedMaintenanceTask
    {
        public string Id { get; set; } = string.Empty;
        public int PostId { get; set; }
        public string PostCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime ScheduledStartDate { get; set; }
        public DateTime ScheduledEndDate { get; set; }
        public string MaintenanceType { get; set; } = string.Empty; // Preventive, Corrective, Predictive
        public string Priority { get; set; } = string.Empty; // Low, Normal, High, Critical
        public int EstimatedDurationMinutes { get; set; }
        public int? ActualDurationMinutes { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string AssignedTechnicianId { get; set; } = string.Empty;
        public string AssignedTechnicianName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Scheduled, InProgress, Completed, Cancelled
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<string> Notes { get; set; } = new();
        public List<string> Materials { get; set; } = new();
        public double CalculatedMTTR { get; set; } // Mean Time To Repair (heures)
    }

    /// <summary>
    /// Résultat de validation d'une demande de maintenance
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Statistiques de maintenance
    /// </summary>
    public class MaintenanceStatistics
    {
        public int TotalScheduledTasks { get; set; }
        public int TotalInProgressTasks { get; set; }
        public int TotalCompletedTasks { get; set; }
        public int TotalCancelledTasks { get; set; }
        public double CompletionRate { get; set; }
        public double AverageTaskDuration { get; set; }
    }
}
