using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigitalisationERP.Core.Entities;
using DigitalisationERP.Infrastructure.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DigitalisationERP.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class MaintenanceController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MaintenanceController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/maintenance/schedules
        [HttpGet("schedules")]
        public async Task<ActionResult<IEnumerable<object>>> GetMaintenanceSchedules(
            [FromQuery] MaintenanceStatusEnum? status = null,
            [FromQuery] long? productionPostId = null,
            [FromQuery] bool overdueOnly = false)
        {
            var query = _context.MaintenanceSchedules.AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(m => m.Status == status.Value);
            }

            if (productionPostId.HasValue)
            {
                query = query.Where(m => m.ProductionPostId == productionPostId.Value);
            }

            if (overdueOnly)
            {
                query = query.Where(m => m.ScheduledDate < DateTime.UtcNow && m.Status != MaintenanceStatusEnum.Completed);
            }

            var schedules = await query
                .Include(m => m.ProductionPost)
                .OrderBy(m => m.ScheduledDate)
                .Select(m => new
                {
                    m.Id,
                    m.MaintenanceType,
                    m.Priority,
                    m.Status,
                    m.Title,
                    m.Description,
                    m.ScheduledDate,
                    m.CompletedDate,
                    m.EstimatedDurationMinutes,
                    m.ActualDurationMinutes,
                    m.IsRecurring,
                    m.RecurrenceIntervalDays,
                    m.TriggerUsageHours,
                    m.CurrentUsageHours,
                    m.TriggerCycleCount,
                    m.CurrentCycleCount,
                    m.HealthScore,
                    m.EstimatedCost,
                    m.ActualCost,
                    PostName = m.ProductionPost.Name,
                    PostCode = m.ProductionPost.Code,
                    IsOverdue = m.ScheduledDate < DateTime.UtcNow && m.Status != MaintenanceStatusEnum.Completed,
                    DaysUntilDue = m.ScheduledDate.HasValue ? (m.ScheduledDate.Value - DateTime.UtcNow).Days : 0,
                    UsageProgress = m.TriggerUsageHours.HasValue && m.TriggerUsageHours.Value > 0 && m.CurrentUsageHours.HasValue ? (double)m.CurrentUsageHours.Value / m.TriggerUsageHours.Value * 100 : 0,
                    CycleProgress = m.TriggerCycleCount.HasValue && m.TriggerCycleCount.Value > 0 && m.CurrentCycleCount.HasValue ? (double)m.CurrentCycleCount.Value / m.TriggerCycleCount.Value * 100 : 0
                })
                .ToListAsync();

            return Ok(schedules);
        }

        // GET: api/maintenance/schedules/{id}
        [HttpGet("schedules/{id}")]
        public async Task<ActionResult<object>> GetMaintenanceSchedule(long id)
        {
            var schedule = await _context.MaintenanceSchedules
                .Include(m => m.ProductionPost)
                .Where(m => m.Id == id)
                .Select(m => new
                {
                    m.Id,
                    m.MaintenanceType,
                    m.Priority,
                    m.Status,
                    m.Title,
                    m.Description,
                    m.ScheduledDate,
                    m.CompletedDate,
                    m.EstimatedDurationMinutes,
                    m.ActualDurationMinutes,
                    m.IsRecurring,
                    m.RecurrenceIntervalDays,
                    m.TriggerUsageHours,
                    m.CurrentUsageHours,
                    m.TriggerCycleCount,
                    m.CurrentCycleCount,
                    m.HealthScore,
                    m.RequiredParts,
                    m.EstimatedCost,
                    m.ActualCost,
                    ProductionPost = new
                    {
                        m.ProductionPost.Id,
                        m.ProductionPost.Name,
                        m.ProductionPost.Code
                    }
                })
                .FirstOrDefaultAsync();

            if (schedule == null)
            {
                return NotFound(new { message = "Maintenance schedule not found" });
            }

            return Ok(schedule);
        }

        // POST: api/maintenance/schedules
        [HttpPost("schedules")]
        public async Task<ActionResult<MaintenanceSchedule>> CreateMaintenanceSchedule(MaintenanceSchedule schedule)
        {
            var post = await _context.ProductionPosts.FindAsync(schedule.ProductionPostId);
            if (post == null)
            {
                return BadRequest(new { message = "Production post not found" });
            }

            schedule.Status = MaintenanceStatusEnum.Scheduled;
            schedule.CreatedAt = DateTime.UtcNow;

            _context.MaintenanceSchedules.Add(schedule);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMaintenanceSchedule), new { id = schedule.Id }, schedule);
        }

        // PUT: api/maintenance/schedules/{id}
        [HttpPut("schedules/{id}")]
        public async Task<IActionResult> UpdateMaintenanceSchedule(long id, MaintenanceSchedule schedule)
        {
            if (id != schedule.Id)
            {
                return BadRequest(new { message = "ID mismatch" });
            }

            var existingSchedule = await _context.MaintenanceSchedules.FindAsync(id);
            if (existingSchedule == null)
            {
                return NotFound(new { message = "Maintenance schedule not found" });
            }

            existingSchedule.MaintenanceType = schedule.MaintenanceType;
            existingSchedule.Priority = schedule.Priority;
            existingSchedule.Status = schedule.Status;
            existingSchedule.Title = schedule.Title;
            existingSchedule.Description = schedule.Description;
            existingSchedule.ScheduledDate = schedule.ScheduledDate;
            existingSchedule.EstimatedDurationMinutes = schedule.EstimatedDurationMinutes;
            existingSchedule.IsRecurring = schedule.IsRecurring;
            existingSchedule.RecurrenceIntervalDays = schedule.RecurrenceIntervalDays;
            existingSchedule.TriggerUsageHours = schedule.TriggerUsageHours;
            existingSchedule.TriggerCycleCount = schedule.TriggerCycleCount;
            existingSchedule.RequiredParts = schedule.RequiredParts;
            existingSchedule.EstimatedCost = schedule.EstimatedCost;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // PUT: api/maintenance/schedules/{id}/complete
        [HttpPut("schedules/{id}/complete")]
        public async Task<IActionResult> CompleteMaintenanceSchedule(long id, [FromBody] MaintenanceHistory history)
        {
            var schedule = await _context.MaintenanceSchedules.FindAsync(id);
            if (schedule == null)
            {
                return NotFound(new { message = "Maintenance schedule not found" });
            }

            schedule.Status = MaintenanceStatusEnum.Completed;
            schedule.CompletedDate = DateTime.UtcNow;
            schedule.ActualDurationMinutes = history.DurationMinutes;
            schedule.ActualCost = history.Cost;

            // Create maintenance history record
            history.MaintenanceScheduleId = (int)id;
            history.ExecutionDate = DateTime.UtcNow;
            history.CreatedAt = DateTime.UtcNow;

            _context.MaintenanceHistories.Add(history);

            // If recurring, create next schedule
            if (schedule.IsRecurring && schedule.RecurrenceIntervalDays > 0)
            {
                var nextSchedule = new MaintenanceSchedule
                {
                    ProductionPostId = schedule.ProductionPostId,
                    MaintenanceType = schedule.MaintenanceType,
                    Priority = schedule.Priority,
                    Status = MaintenanceStatusEnum.Scheduled,
                    Title = schedule.Title,
                    Description = schedule.Description,
                    ScheduledDate = schedule.ScheduledDate.HasValue ? schedule.ScheduledDate.Value.AddDays(schedule.RecurrenceIntervalDays ?? 0) : DateTime.UtcNow.AddDays(schedule.RecurrenceIntervalDays ?? 0),
                    EstimatedDurationMinutes = schedule.EstimatedDurationMinutes,
                    IsRecurring = true,
                    RecurrenceIntervalDays = schedule.RecurrenceIntervalDays,
                    TriggerUsageHours = schedule.TriggerUsageHours,
                    CurrentUsageHours = 0,
                    TriggerCycleCount = schedule.TriggerCycleCount,
                    CurrentCycleCount = 0,
                    HealthScore = 100,
                    RequiredParts = schedule.RequiredParts,
                    EstimatedCost = schedule.EstimatedCost,
                    CreatedAt = DateTime.UtcNow
                };

                _context.MaintenanceSchedules.Add(nextSchedule);
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Maintenance completed successfully", scheduleId = schedule.Id });
        }

        // POST: api/maintenance/history
        [HttpPost("history")]
        public async Task<ActionResult<MaintenanceHistory>> CreateMaintenanceHistory(MaintenanceHistory history)
        {
            var schedule = await _context.MaintenanceSchedules.FindAsync(history.MaintenanceScheduleId);
            if (schedule == null)
            {
                return BadRequest(new { message = "Maintenance schedule not found" });
            }

            history.ExecutionDate = DateTime.UtcNow;
            history.CreatedAt = DateTime.UtcNow;

            _context.MaintenanceHistories.Add(history);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMaintenanceSchedule), new { id = history.MaintenanceScheduleId }, history);
        }

        // GET: api/maintenance/predictive
        [HttpGet("predictive")]
        public async Task<ActionResult<IEnumerable<object>>> GetPredictiveMaintenance()
        {
            var schedules = await _context.MaintenanceSchedules
                .Include(m => m.ProductionPost)
                .Where(m => m.Status == MaintenanceStatusEnum.Scheduled || m.Status == MaintenanceStatusEnum.InProgress)
                .Where(m => 
                    (m.TriggerUsageHours > 0 && m.CurrentUsageHours >= m.TriggerUsageHours * 0.8) ||
                    (m.TriggerCycleCount > 0 && m.CurrentCycleCount >= m.TriggerCycleCount * 0.8) ||
                    (m.HealthScore <= 75))
                .Select(m => new
                {
                    m.Id,
                    m.Title,
                    m.MaintenanceType,
                    m.Priority,
                    PostName = m.ProductionPost.Name,
                    PostCode = m.ProductionPost.Code,
                    m.HealthScore,
                    UsageProgress = m.TriggerUsageHours.HasValue && m.TriggerUsageHours.Value > 0 && m.CurrentUsageHours.HasValue ? (double)m.CurrentUsageHours.Value / m.TriggerUsageHours.Value * 100 : 0,
                    CycleProgress = m.TriggerCycleCount.HasValue && m.TriggerCycleCount.Value > 0 && m.CurrentCycleCount.HasValue ? (double)m.CurrentCycleCount.Value / m.TriggerCycleCount.Value * 100 : 0,
                    PredictedFailureDate = m.TriggerUsageHours.HasValue && m.TriggerUsageHours.Value > 0 && m.CurrentUsageHours.HasValue && m.CurrentUsageHours.Value > 0 
                        ? DateTime.UtcNow.AddDays((m.TriggerUsageHours.Value - m.CurrentUsageHours.Value) * 0.1) // Simple prediction
                        : (DateTime?)null,
                    RecommendedAction = m.HealthScore.HasValue && m.HealthScore.Value <= 50 ? "Immediate attention required" :
                                       m.HealthScore <= 75 ? "Schedule maintenance soon" :
                                       "Monitor closely",
                    m.EstimatedCost
                })
                .OrderBy(m => m.HealthScore)
                .ToListAsync();

            return Ok(schedules);
        }

        // GET: api/maintenance/health-scores
        [HttpGet("health-scores")]
        public async Task<ActionResult<IEnumerable<object>>> GetHealthScores()
        {
            var posts = await _context.ProductionPosts
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Code,
                    HealthScore = _context.MaintenanceSchedules
                        .Where(m => m.ProductionPostId == p.Id)
                        .OrderByDescending(m => m.CreatedAt)
                        .Select(m => m.HealthScore)
                        .FirstOrDefault(),
                    ActiveMaintenanceSchedules = _context.MaintenanceSchedules
                        .Count(m => m.ProductionPostId == p.Id && 
                               (m.Status == MaintenanceStatusEnum.Scheduled || m.Status == MaintenanceStatusEnum.InProgress)),
                    OverdueMaintenanceCount = _context.MaintenanceSchedules
                        .Count(m => m.ProductionPostId == p.Id && 
                               m.ScheduledDate.HasValue && m.ScheduledDate.Value < DateTime.UtcNow && 
                               m.Status != MaintenanceStatusEnum.Completed),
                    LastMaintenanceDate = _context.MaintenanceSchedules
                        .Where(m => m.ProductionPostId == p.Id && m.LastMaintenanceDate.HasValue)
                        .OrderByDescending(m => m.LastMaintenanceDate)
                        .Select(m => m.LastMaintenanceDate)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(posts);
        }

        // PUT: api/maintenance/schedules/{id}/update-usage
        [HttpPut("schedules/{id}/update-usage")]
        public async Task<IActionResult> UpdateUsageMetrics(long id, [FromBody] UsageUpdateDto dto)
        {
            var schedule = await _context.MaintenanceSchedules.FindAsync(id);
            if (schedule == null)
            {
                return NotFound(new { message = "Maintenance schedule not found" });
            }

            schedule.CurrentUsageHours = dto.UsageHours;
            schedule.CurrentCycleCount = dto.CycleCount;
            
            // Simple health score calculation
            double usageRatio = schedule.TriggerUsageHours.HasValue && schedule.TriggerUsageHours.Value > 0 
                ? (double)dto.UsageHours / schedule.TriggerUsageHours.Value 
                : 0;
            double cycleRatio = schedule.TriggerCycleCount.HasValue && schedule.TriggerCycleCount.Value > 0 
                ? (double)dto.CycleCount / schedule.TriggerCycleCount.Value 
                : 0;
            
            double maxRatio = Math.Max(usageRatio, cycleRatio);
            schedule.HealthScore = Math.Max(0, 100 - (maxRatio * 100));

            // Check if maintenance should be triggered
            if ((schedule.TriggerUsageHours.HasValue && schedule.CurrentUsageHours >= schedule.TriggerUsageHours.Value) || 
                (schedule.TriggerCycleCount.HasValue && schedule.CurrentCycleCount >= schedule.TriggerCycleCount.Value))
            {
                schedule.Status = MaintenanceStatusEnum.Overdue;
                schedule.Priority = MaintenancePriorityEnum.High;
            }

            await _context.SaveChangesAsync();

            return Ok(new { 
                message = "Usage metrics updated", 
                healthScore = schedule.HealthScore,
                status = schedule.Status
            });
        }

        // GET: api/maintenance/statistics
        [HttpGet("statistics")]
        public async Task<ActionResult<object>> GetMaintenanceStatistics()
        {
            var totalSchedules = await _context.MaintenanceSchedules.CountAsync();
            var completedSchedules = await _context.MaintenanceSchedules.CountAsync(m => m.Status == MaintenanceStatusEnum.Completed);
            var overdueSchedules = await _context.MaintenanceSchedules.CountAsync(m => 
                m.ScheduledDate < DateTime.UtcNow && m.Status != MaintenanceStatusEnum.Completed);
            
            var avgHealthScore = await _context.MaintenanceSchedules
                .Where(m => m.Status != MaintenanceStatusEnum.Completed && m.HealthScore.HasValue)
                .Select(m => m.HealthScore ?? 100)
                .DefaultIfEmpty(100)
                .AverageAsync();

            var totalCost = await _context.MaintenanceHistories.SumAsync(h => h.Cost);
            
            var recentMaintenance = await _context.MaintenanceHistories
                .Where(h => h.ExecutionDate >= DateTime.UtcNow.AddDays(-30))
                .CountAsync();

            return Ok(new
            {
                totalSchedules,
                completedSchedules,
                overdueSchedules,
                avgHealthScore = Math.Round(avgHealthScore, 1),
                totalCost,
                recentMaintenance,
                generatedAt = DateTime.UtcNow
            });
        }
    }

    public class UsageUpdateDto
    {
        public int UsageHours { get; set; }
        public int CycleCount { get; set; }
    }
}
