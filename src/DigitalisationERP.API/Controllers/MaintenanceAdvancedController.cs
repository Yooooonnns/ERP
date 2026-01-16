using Microsoft.AspNetCore.Mvc;
using DigitalisationERP.Application.Services;
using DigitalisationERP.Core.Entities;
using DigitalisationERP.Core.Entities.IoT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DigitalisationERP.API.Controllers
{
    /// <summary>
    /// API endpoints pour la maintenance avancée avec health scores, alertes et KPIs
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class MaintenanceAdvancedController : ControllerBase
    {
        private readonly MaintenanceHealthScoreCalculationService _healthScoreService;
        private readonly MaintenanceAlertManager _alertManager;
        private readonly PlannedMaintenanceService _plannedMaintenanceService;
        private readonly ILogger<MaintenanceAdvancedController> _logger;

        public MaintenanceAdvancedController(
            MaintenanceHealthScoreCalculationService healthScoreService,
            MaintenanceAlertManager alertManager,
            PlannedMaintenanceService plannedMaintenanceService,
            ILogger<MaintenanceAdvancedController> logger)
        {
            _healthScoreService = healthScoreService ?? throw new ArgumentNullException(nameof(healthScoreService));
            _alertManager = alertManager ?? throw new ArgumentNullException(nameof(alertManager));
            _plannedMaintenanceService = plannedMaintenanceService ?? throw new ArgumentNullException(nameof(plannedMaintenanceService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Récupère les health scores pour toutes les postes d'une ligne
        /// GET /api/maintenanceadvanced/health-scores?lineId=1
        /// </summary>
        [HttpGet("health-scores")]
        [ProducesResponseType(typeof(HealthScoresResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetHealthScores([FromQuery] int lineId)
        {
            try
            {
                _logger.LogInformation($"Fetching health scores for line {lineId}");

                // En production: récupérer from database
                var mockPosts = GetMockPostsForLine(lineId);
                var healthScores = new Dictionary<long, double>();

                foreach (var post in mockPosts)
                {
                    healthScores[post.Id] = _healthScoreService.CalculateHealthScore(post);
                }

                var response = new HealthScoresResponseDto
                {
                    LineId = lineId,
                    Timestamp = DateTime.UtcNow,
                    HealthScores = healthScores.Select(kvp => new HealthScoreItemDto
                    {
                        PostId = (int)kvp.Key,
                        HealthScore = kvp.Value,
                        Status = kvp.Value >= 85 ? "Good" : kvp.Value >= 70 ? "Warning" : kvp.Value >= 50 ? "Scheduled" : "Critical",
                        Icon = kvp.Value >= 85 ? "🟢" : kvp.Value >= 70 ? "🟡" : kvp.Value >= 50 ? "🟠" : "🔴"
                    }).ToList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching health scores");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Récupère le health score détaillé pour un poste spécifique
        /// GET /api/maintenanceadvanced/health-scores/{postId}
        /// </summary>
        [HttpGet("health-scores/{postId}")]
        [ProducesResponseType(typeof(HealthScoreDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetHealthScoreDetail([FromRoute] int postId)
        {
            try
            {
                var post = GetMockPost(postId);
                if (post == null)
                    return NotFound(new { error = "Post not found" });

                var healthScore = _healthScoreService.CalculateHealthScore(post);
                var (icon, status, description) = _healthScoreService.GetHealthStatus(healthScore);
                var history = _healthScoreService.GetHealthHistory(postId, 30);

                var response = new HealthScoreDetailDto
                {
                    PostId = postId,
                    PostCode = post.PostCode,
                    PostName = post.PostName,
                    CurrentHealthScore = healthScore,
                    Status = status,
                    Icon = icon,
                    Description = description,
                    LastMaintenanceDate = DateTime.Now.AddDays(-10),
                    NextScheduledMaintenance = DateTime.Now.AddDays(14),
                    History = history.Select(h => new HealthScoreHistoryItemDto
                    {
                        Date = h.Date,
                        Score = h.HealthScore
                    }).ToList(),
                    Timestamp = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching health score detail");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Récupère les alertes actives pour une ligne
        /// GET /api/maintenanceadvanced/alerts?lineId=1&severity=Critical
        /// </summary>
        [HttpGet("alerts")]
        [ProducesResponseType(typeof(AlertsResponseDto), StatusCodes.Status200OK)]
        public IActionResult GetAlerts(
            [FromQuery] int lineId,
            [FromQuery] string? severity = null)
        {
            try
            {
                _logger.LogInformation($"Fetching alerts for line {lineId}");

                var mockPosts = GetMockPostsForLine(lineId);
                var mockSchedules = GetMockSchedules();
                var mockSensorReadings = GetMockSensorReadings();

                var allAlerts = _alertManager.GenerateAlertsForLine(lineId, mockPosts, mockSchedules, mockSensorReadings);

                // Filtre par sévérité si spécifiée
                if (!string.IsNullOrEmpty(severity))
                {
                    allAlerts = allAlerts.Where(a => a.Severity == severity).ToList();
                }

                var response = new AlertsResponseDto
                {
                    LineId = lineId,
                    TotalAlerts = allAlerts.Count,
                    AlertCounts = _alertManager.GetAlertCounts(allAlerts),
                    Alerts = allAlerts.OrderByDescending(a => a.CreatedAt).Select(a => new AlertItemDto
                    {
                        AlertId = a.AlertId,
                        PostCode = a.PostCode,
                        PostName = a.PostName,
                        Title = a.Title,
                        Description = a.Description,
                        Severity = a.Severity,
                        Icon = a.Icon,
                        CreatedAt = a.CreatedAt,
                        DueDate = a.DueDate,
                        Status = a.Status,
                        RequiredAction = a.RequiredAction
                    }).ToList(),
                    Timestamp = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching alerts");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Acquitte une alerte (mark as acknowledged)
        /// PATCH /api/maintenanceadvanced/alerts/{alertId}/acknowledge
        /// </summary>
        [HttpPatch("alerts/{alertId}/acknowledge")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult AcknowledgeAlert([FromRoute] string alertId)
        {
            try
            {
                _alertManager.AcknowledgeAlert(alertId);
                _logger.LogInformation($"Alert {alertId} acknowledged");

                return Ok(new { message = "Alert acknowledged" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error acknowledging alert");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Récupère les KPIs de maintenance pour une ligne
        /// GET /api/maintenanceadvanced/kpis?lineId=1
        /// </summary>
        [HttpGet("kpis")]
        [ProducesResponseType(typeof(KPIsResponseDto), StatusCodes.Status200OK)]
        public IActionResult GetKPIs([FromQuery] int lineId)
        {
            try
            {
                _logger.LogInformation($"Fetching KPIs for line {lineId}");

                var mockPosts = GetMockPostsForLine(lineId);
                var mockSchedules = GetMockSchedules();

                var kpis = _healthScoreService.CalculateKPIs(lineId, mockPosts, mockSchedules);

                var response = new KPIsResponseDto
                {
                    LineId = lineId,
                    MTBF = kpis.MTBF,
                    MTTR = kpis.MTTR,
                    Availability = kpis.Availability,
                    OverdueTasksRate = kpis.OverdueTasksRate,
                    TotalScheduledTasks = kpis.TotalScheduledTasks,
                    CompletedTasks = kpis.CompletedTasks,
                    OverdueTasks = kpis.OverdueTasks,
                    InProgressTasks = kpis.InProgressTasks,
                    AverageHealthScore = kpis.AverageHealthScore,
                    Timestamp = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching KPIs");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Crée une nouvelle tâche de maintenance planifiée
        /// POST /api/maintenanceadvanced/planned-maintenance
        /// </summary>
        [HttpPost("planned-maintenance")]
        [ProducesResponseType(typeof(PlannedMaintenanceResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreatePlannedMaintenance([FromBody] CreatePlannedMaintenanceRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var (success, message, task) = await _plannedMaintenanceService.CreatePlannedMaintenanceAsync(
                    request.PostId,
                    request.PostCode,
                    request.Title,
                    request.Description,
                    request.ScheduledStartDate,
                    request.ScheduledEndDate,
                    request.MaintenanceType,
                    request.Priority,
                    request.EstimatedDurationMinutes,
                    request.AssignedTechnicianId);

                if (!success)
                    return BadRequest(new { error = message });

                if (task == null)
                    return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Planned maintenance task was null" });

                var response = new PlannedMaintenanceResponseDto
                {
                    Id = task.Id,
                    PostCode = task.PostCode,
                    Title = task.Title,
                    ScheduledStartDate = task.ScheduledStartDate,
                    ScheduledEndDate = task.ScheduledEndDate,
                    Status = task.Status,
                    Message = message
                };

                return CreatedAtAction(nameof(GetKPIs), new { id = task.Id }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating planned maintenance");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Récupère les tâches planifiées urgentes (court terme ou en retard)
        /// GET /api/maintenanceadvanced/urgent-tasks
        /// </summary>
        [HttpGet("urgent-tasks")]
        [ProducesResponseType(typeof(UrgentTasksResponseDto), StatusCodes.Status200OK)]
        public IActionResult GetUrgentTasks()
        {
            try
            {
                var urgentTasks = _plannedMaintenanceService.GetUrgentTasks();

                var response = new UrgentTasksResponseDto
                {
                    TotalUrgentTasks = urgentTasks.Count,
                    Tasks = urgentTasks.Select(t => new UrgentTaskItemDto
                    {
                        PostCode = t.PostCode,
                        Title = t.Title,
                        ScheduledStartDate = t.ScheduledStartDate,
                        DaysUntilDue = (int)(t.ScheduledStartDate - DateTime.Now).TotalDays,
                        Status = t.Status,
                        Priority = t.Priority
                    }).ToList(),
                    Timestamp = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching urgent tasks");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Recalcule les health scores pour une ligne (après nouvelle données)
        /// POST /api/maintenanceadvanced/recalculate-health-scores
        /// </summary>
        [HttpPost("recalculate-health-scores")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult RecalculateHealthScores([FromBody] RecalculateHealthScoresRequestDto request)
        {
            try
            {
                _logger.LogInformation($"Recalculating health scores for line {request.LineId}");

                var mockPosts = GetMockPostsForLine(request.LineId);
                var healthScores = _healthScoreService.CalculateLineHealthScores(request.LineId, mockPosts);

                return Ok(new
                {
                    message = "Health scores recalculated",
                    lineId = request.LineId,
                    postsUpdated = healthScores.Count,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recalculating health scores");
                return BadRequest(new { error = ex.Message });
            }
        }

        #region Helper Methods

        private List<ProductionPost> GetMockPostsForLine(int lineId)
        {
            return new List<ProductionPost>
            {
                new ProductionPost { Id = 1, PostCode = "POST-A01", PostName = "Cutting Station", ProductionLineId = 1 },
                new ProductionPost { Id = 2, PostCode = "POST-A02", PostName = "Assembly", ProductionLineId = 1 },
                new ProductionPost { Id = 3, PostCode = "POST-A03", PostName = "QC", ProductionLineId = 1 },
                new ProductionPost { Id = 5, PostCode = "POST-B01", PostName = "Material", ProductionLineId = 2 },
            };
        }

        private ProductionPost? GetMockPost(long postId)
        {
            return GetMockPostsForLine(0).FirstOrDefault(p => p.Id == postId);
        }

        private List<MaintenanceSchedule> GetMockSchedules()
        {
            return new List<MaintenanceSchedule>
            {
                new MaintenanceSchedule { ProductionPostId = 1, Status = MaintenanceStatusEnum.Completed, CompletedDate = DateTime.Now.AddDays(-10) },
                new MaintenanceSchedule { ProductionPostId = 2, Status = MaintenanceStatusEnum.Scheduled, ScheduledDate = DateTime.Now.AddDays(5) }
            };
        }

        private List<SensorReading> GetMockSensorReadings()
        {
            return new List<SensorReading>();
        }

        #endregion
    }

    #region DTOs

    public class HealthScoresResponseDto
    {
        public int LineId { get; set; }
        public List<HealthScoreItemDto> HealthScores { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class HealthScoreItemDto
    {
        public int PostId { get; set; }
        public double HealthScore { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }

    public class HealthScoreDetailDto
    {
        public int PostId { get; set; }
        public string PostCode { get; set; } = string.Empty;
        public string PostName { get; set; } = string.Empty;
        public double CurrentHealthScore { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime? LastMaintenanceDate { get; set; }
        public DateTime? NextScheduledMaintenance { get; set; }
        public List<HealthScoreHistoryItemDto> History { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class HealthScoreHistoryItemDto
    {
        public DateTime Date { get; set; }
        public double Score { get; set; }
    }

    public class AlertsResponseDto
    {
        public int LineId { get; set; }
        public int TotalAlerts { get; set; }
        public AlertCountByType AlertCounts { get; set; } = new();
        public List<AlertItemDto> Alerts { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class AlertItemDto
    {
        public string AlertId { get; set; } = string.Empty;
        public string PostCode { get; set; } = string.Empty;
        public string PostName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string RequiredAction { get; set; } = string.Empty;
    }

    public class KPIsResponseDto
    {
        public int LineId { get; set; }
        public double MTBF { get; set; }
        public double MTTR { get; set; }
        public double Availability { get; set; }
        public double OverdueTasksRate { get; set; }
        public int TotalScheduledTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int OverdueTasks { get; set; }
        public int InProgressTasks { get; set; }
        public double AverageHealthScore { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class CreatePlannedMaintenanceRequestDto
    {
        public int PostId { get; set; }
        public string PostCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime ScheduledStartDate { get; set; }
        public DateTime ScheduledEndDate { get; set; }
        public string MaintenanceType { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public int EstimatedDurationMinutes { get; set; }
        public string AssignedTechnicianId { get; set; } = string.Empty;
    }

    public class PlannedMaintenanceResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string PostCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime ScheduledStartDate { get; set; }
        public DateTime ScheduledEndDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class UrgentTasksResponseDto
    {
        public int TotalUrgentTasks { get; set; }
        public List<UrgentTaskItemDto> Tasks { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class UrgentTaskItemDto
    {
        public string PostCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime ScheduledStartDate { get; set; }
        public int DaysUntilDue { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
    }

    public class RecalculateHealthScoresRequestDto
    {
        public int LineId { get; set; }
    }

    #endregion
}
