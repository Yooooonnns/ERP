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
    public class SchedulingController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SchedulingController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/scheduling/schedules
        [HttpGet("schedules")]
        public async Task<ActionResult<IEnumerable<object>>> GetProductionSchedules(
            [FromQuery] ScheduleStatusEnum? status = null,
            [FromQuery] long? productionOrderId = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var query = _context.ProductionSchedules.AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(s => s.Status == status.Value);
            }

            if (productionOrderId.HasValue)
            {
                query = query.Where(s => s.ProductionOrderId == productionOrderId.Value);
            }

            if (startDate.HasValue)
            {
                query = query.Where(s => s.PlannedStartDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(s => s.PlannedEndDate <= endDate.Value);
            }

            var schedules = await query
                .Include(s => s.AssignedProductionPost)
                .Include(s => s.PredecessorSchedule)
                .OrderBy(s => s.SequenceNumber)
                .ThenBy(s => s.PlannedStartDate)
                .Select(s => new
                {
                    s.Id,
                    s.ScheduleNumber,
                    ScheduleStatus = s.Status,
                    s.PlannedStartDate,
                    s.PlannedEndDate,
                    s.ActualStartDate,
                    s.ActualEndDate,
                    s.PlannedDurationMinutes,
                    s.ActualDurationMinutes,
                    s.Priority,
                    s.SequenceNumber,
                    s.SetupTimeMinutes,
                    AssignedPost = s.AssignedProductionPost != null ? new
                    {
                        s.AssignedProductionPost.Id,
                        s.AssignedProductionPost.Name,
                        s.AssignedProductionPost.Code
                    } : null,
                    PredecessorScheduleNumber = s.PredecessorSchedule != null ? s.PredecessorSchedule.ScheduleNumber : null,
                    Progress = s.ActualStartDate != null && s.PlannedEndDate > DateTime.UtcNow && s.ActualDurationMinutes.HasValue
                        ? (DateTime.UtcNow - s.ActualStartDate.Value).TotalMinutes / (double)s.PlannedDurationMinutes * 100
                        : 0,
                    IsDelayed = s.ActualStartDate != null && DateTime.UtcNow > s.PlannedEndDate && s.ActualEndDate == null,
                    DelayMinutes = s.ActualStartDate != null && DateTime.UtcNow > s.PlannedEndDate && s.ActualEndDate == null
                        ? (DateTime.UtcNow - s.PlannedEndDate).TotalMinutes
                        : 0
                })
                .ToListAsync();

            return Ok(schedules);
        }

        // GET: api/scheduling/schedules/{id}
        [HttpGet("schedules/{id}")]
        public async Task<ActionResult<object>> GetProductionSchedule(long id)
        {
            var schedule = await _context.ProductionSchedules
                .Include(s => s.AssignedProductionPost)
                .Include(s => s.PredecessorSchedule)
                .Include(s => s.DependentSchedules)
                .Where(s => s.Id == id)
                .Select(s => new
                {
                    s.Id,
                    s.ScheduleNumber,
                    ScheduleStatus = s.Status,
                    s.PlannedStartDate,
                    s.PlannedEndDate,
                    s.ActualStartDate,
                    s.ActualEndDate,
                    s.PlannedDurationMinutes,
                    s.ActualDurationMinutes,
                    s.Priority,
                    s.SequenceNumber,
                    s.SetupTimeMinutes,
                    s.SetupRequirements,
                    s.MaterialConstraints,
                    s.ToolConstraints,
                    s.SkillConstraints,
                    s.AssignedOperatorIds,
                    s.Notes,
                    AssignedPost = s.AssignedProductionPost != null ? new
                    {
                        s.AssignedProductionPost.Id,
                        s.AssignedProductionPost.Name,
                        s.AssignedProductionPost.Code
                    } : null,
                    PredecessorSchedule = s.PredecessorSchedule != null ? new
                    {
                        s.PredecessorSchedule.Id,
                        s.PredecessorSchedule.ScheduleNumber,
                        ScheduleStatus = s.PredecessorSchedule.Status
                    } : null,
                    DependentSchedules = s.DependentSchedules.Select(d => new
                    {
                        d.Id,
                        d.ScheduleNumber,
                        ScheduleStatus = d.Status
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (schedule == null)
            {
                return NotFound(new { message = "Production schedule not found" });
            }

            return Ok(schedule);
        }

        // POST: api/scheduling/schedules
        [HttpPost("schedules")]
        public async Task<ActionResult<ProductionSchedule>> CreateProductionSchedule(ProductionSchedule schedule)
        {
            // Validate predecessor if specified
            if (schedule.PredecessorScheduleId.HasValue)
            {
                var predecessor = await _context.ProductionSchedules.FindAsync(schedule.PredecessorScheduleId.Value);
                if (predecessor == null)
                {
                    return BadRequest(new { message = "Predecessor schedule not found" });
                }

                // Ensure start date is after predecessor's end date
                if (schedule.PlannedStartDate < predecessor.PlannedEndDate)
                {
                    schedule.PlannedStartDate = predecessor.PlannedEndDate;
                }
            }

            // Generate schedule number
            var maxNumber = await _context.ProductionSchedules
                .OrderByDescending(s => s.Id)
                .Select(s => s.ScheduleNumber)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (!string.IsNullOrEmpty(maxNumber) && maxNumber.StartsWith("SCH"))
            {
                int.TryParse(maxNumber.Substring(3), out nextNumber);
                nextNumber++;
            }
            schedule.ScheduleNumber = $"SCH{nextNumber:D6}";

            schedule.Status = ScheduleStatusEnum.Planned;
            schedule.PlannedEndDate = schedule.PlannedStartDate.AddMinutes(schedule.PlannedDurationMinutes + (schedule.SetupTimeMinutes ?? 0));
            schedule.CreatedAt = DateTime.UtcNow;

            _context.ProductionSchedules.Add(schedule);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProductionSchedule), new { id = schedule.Id }, schedule);
        }

        // PUT: api/scheduling/schedules/{id}
        [HttpPut("schedules/{id}")]
        public async Task<IActionResult> UpdateProductionSchedule(long id, ProductionSchedule schedule)
        {
            if (id != schedule.Id)
            {
                return BadRequest(new { message = "ID mismatch" });
            }

            var existingSchedule = await _context.ProductionSchedules.FindAsync(id);
            if (existingSchedule == null)
            {
                return NotFound(new { message = "Production schedule not found" });
            }

            existingSchedule.Status = schedule.Status;
            existingSchedule.PlannedStartDate = schedule.PlannedStartDate;
            existingSchedule.PlannedDurationMinutes = schedule.PlannedDurationMinutes;
            existingSchedule.PlannedEndDate = schedule.PlannedStartDate.AddMinutes(schedule.PlannedDurationMinutes + (schedule.SetupTimeMinutes ?? 0));
            existingSchedule.Priority = schedule.Priority;
            existingSchedule.SequenceNumber = schedule.SequenceNumber;
            existingSchedule.AssignedProductionPostId = schedule.AssignedProductionPostId;
            existingSchedule.SetupTimeMinutes = schedule.SetupTimeMinutes;
            existingSchedule.SetupRequirements = schedule.SetupRequirements;
            existingSchedule.MaterialConstraints = schedule.MaterialConstraints;
            existingSchedule.ToolConstraints = schedule.ToolConstraints;
            existingSchedule.SkillConstraints = schedule.SkillConstraints;
            existingSchedule.AssignedOperatorIds = schedule.AssignedOperatorIds;
            existingSchedule.Notes = schedule.Notes;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // PUT: api/scheduling/schedules/{id}/start
        [HttpPut("schedules/{id}/start")]
        public async Task<IActionResult> StartSchedule(long id)
        {
            var schedule = await _context.ProductionSchedules.FindAsync(id);
            if (schedule == null)
            {
                return NotFound(new { message = "Production schedule not found" });
            }

            // Check if predecessor is completed
            if (schedule.PredecessorScheduleId.HasValue)
            {
                var predecessor = await _context.ProductionSchedules.FindAsync(schedule.PredecessorScheduleId.Value);
                if (predecessor == null || predecessor.Status != ScheduleStatusEnum.Completed)
                {
                    return BadRequest(new { message = "Cannot start schedule. Predecessor schedule must be completed first." });
                }
            }

            schedule.ActualStartDate = DateTime.UtcNow;
            schedule.Status = ScheduleStatusEnum.InProgress;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Schedule started", actualStartDate = schedule.ActualStartDate });
        }

        // PUT: api/scheduling/schedules/{id}/complete
        [HttpPut("schedules/{id}/complete")]
        public async Task<IActionResult> CompleteSchedule(long id)
        {
            var schedule = await _context.ProductionSchedules.FindAsync(id);
            if (schedule == null)
            {
                return NotFound(new { message = "Production schedule not found" });
            }

            if (schedule.ActualStartDate == null)
            {
                return BadRequest(new { message = "Cannot complete schedule that has not been started" });
            }

            schedule.ActualEndDate = DateTime.UtcNow;
            schedule.ActualDurationMinutes = (int)(schedule.ActualEndDate.Value - schedule.ActualStartDate.Value).TotalMinutes;
            schedule.Status = ScheduleStatusEnum.Completed;

            await _context.SaveChangesAsync();

            return Ok(new { 
                message = "Schedule completed", 
                actualEndDate = schedule.ActualEndDate,
                actualDurationMinutes = schedule.ActualDurationMinutes
            });
        }

        // GET: api/scheduling/gantt
        [HttpGet("gantt")]
        public async Task<ActionResult<object>> GetGanttData(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var start = startDate ?? DateTime.UtcNow.Date;
            var end = endDate ?? DateTime.UtcNow.Date.AddDays(30);

            var schedules = await _context.ProductionSchedules
                .Include(s => s.AssignedProductionPost)
                .Where(s => s.PlannedStartDate <= end && s.PlannedEndDate >= start)
                .OrderBy(s => s.SequenceNumber)
                .Select(s => new
                {
                    s.Id,
                    s.ScheduleNumber,
                    ScheduleStatus = s.Status,
                    s.PlannedStartDate,
                    s.PlannedEndDate,
                    s.ActualStartDate,
                    s.ActualEndDate,
                    s.Priority,
                    s.SequenceNumber,
                    PostName = s.AssignedProductionPost != null ? s.AssignedProductionPost.Name : "Unassigned",
                    PostCode = s.AssignedProductionPost != null ? s.AssignedProductionPost.Code : "",
                    s.PredecessorScheduleId,
                    Dependencies = _context.ProductionSchedules
                        .Where(d => d.PredecessorScheduleId == s.Id)
                        .Select(d => d.Id)
                        .ToList()
                })
                .ToListAsync();

            // Group by post for timeline visualization
            var timeline = schedules
                .GroupBy(s => new { PostCode = s.PostCode, PostName = s.PostName })
                .Select(g => new
                {
                    g.Key.PostCode,
                    g.Key.PostName,
                    Schedules = g.Select(s => new
                    {
                        s.Id,
                        s.ScheduleNumber,
                        s.ScheduleStatus,
                        s.PlannedStartDate,
                        s.PlannedEndDate,
                        s.ActualStartDate,
                        s.ActualEndDate,
                        s.Priority,
                        s.PredecessorScheduleId,
                        s.Dependencies
                    }).ToList()
                })
                .ToList();

            return Ok(new
            {
                startDate = start,
                endDate = end,
                schedules = schedules,
                timeline = timeline
            });
        }

        // POST: api/scheduling/optimize
        [HttpPost("optimize")]
        public async Task<ActionResult<object>> OptimizeSchedules([FromBody] OptimizeRequest request)
        {
            var schedules = await _context.ProductionSchedules
                .Include(s => s.AssignedProductionPost)
                .Where(s => request.ScheduleIds.Contains(s.Id))
                .ToListAsync();

            if (schedules.Count == 0)
            {
                return BadRequest(new { message = "No schedules found to optimize" });
            }

            // Simple optimization: Sort by priority and dependencies
            var optimized = new List<ProductionSchedule>();
            var processed = new HashSet<long>();
            var currentDate = request.StartDate ?? DateTime.UtcNow;

            // First, handle schedules without dependencies
            var noDependencies = schedules.Where(s => !s.PredecessorScheduleId.HasValue)
                .OrderByDescending(s => s.Priority)
                .ThenBy(s => s.SequenceNumber);

            foreach (var schedule in noDependencies)
            {
                schedule.PlannedStartDate = currentDate;
                schedule.PlannedEndDate = currentDate.AddMinutes(schedule.PlannedDurationMinutes + (schedule.SetupTimeMinutes ?? 0));
                schedule.Status = ScheduleStatusEnum.Ready;
                
                optimized.Add(schedule);
                processed.Add(schedule.Id);
                
                currentDate = schedule.PlannedEndDate.AddMinutes(10); // 10 min buffer
            }

            // Then handle dependent schedules
            while (processed.Count < schedules.Count)
            {
                var nextBatch = schedules
                    .Where(s => !processed.Contains(s.Id) && 
                               (!s.PredecessorScheduleId.HasValue || processed.Contains(s.PredecessorScheduleId.Value)))
                    .OrderByDescending(s => s.Priority)
                    .ToList();

                if (nextBatch.Count == 0) break; // Circular dependency or orphaned schedules

                foreach (var schedule in nextBatch)
                {
                    if (schedule.PredecessorScheduleId.HasValue)
                    {
                        var predecessor = optimized.First(s => s.Id == schedule.PredecessorScheduleId.Value);
                        currentDate = predecessor.PlannedEndDate.AddMinutes(10);
                    }

                    schedule.PlannedStartDate = currentDate;
                    schedule.PlannedEndDate = currentDate.AddMinutes(schedule.PlannedDurationMinutes + (schedule.SetupTimeMinutes ?? 0));
                    schedule.Status = ScheduleStatusEnum.Ready;
                    
                    optimized.Add(schedule);
                    processed.Add(schedule.Id);
                    
                    currentDate = schedule.PlannedEndDate.AddMinutes(10);
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"Optimized {optimized.Count} schedules",
                totalDuration = (decimal)(currentDate - (request.StartDate ?? DateTime.UtcNow)).TotalMinutes,
                schedules = optimized.Select(s => new
                {
                    s.Id,
                    s.ScheduleNumber,
                    s.PlannedStartDate,
                    s.PlannedEndDate,
                    s.Priority,
                    s.SequenceNumber
                })
            });
        }

        // GET: api/scheduling/statistics
        [HttpGet("statistics")]
        public async Task<ActionResult<object>> GetSchedulingStatistics()
        {
            var totalSchedules = await _context.ProductionSchedules.CountAsync();
            var completedSchedules = await _context.ProductionSchedules.CountAsync(s => s.Status == ScheduleStatusEnum.Completed);
            var inProgressSchedules = await _context.ProductionSchedules.CountAsync(s => s.Status == ScheduleStatusEnum.InProgress);
            var delayedSchedules = await _context.ProductionSchedules.CountAsync(s => 
                s.ActualStartDate != null && s.ActualEndDate == null && DateTime.UtcNow > s.PlannedEndDate);

            var avgScheduleEfficiency = await _context.ProductionSchedules
                .Where(s => s.Status == ScheduleStatusEnum.Completed && s.ActualDurationMinutes.HasValue && s.ActualDurationMinutes.Value > 0)
                .Select(s => (double)s.PlannedDurationMinutes / (s.ActualDurationMinutes ?? 1) * 100)
                .DefaultIfEmpty(100)
                .AverageAsync();

            var upcomingSchedules = await _context.ProductionSchedules
                .Where(s => s.PlannedStartDate <= DateTime.UtcNow.AddDays(7) && 
                           s.Status != ScheduleStatusEnum.Completed)
                .CountAsync();

            return Ok(new
            {
                totalSchedules,
                completedSchedules,
                inProgressSchedules,
                delayedSchedules,
                avgScheduleEfficiency = Math.Round(avgScheduleEfficiency, 1),
                upcomingSchedules,
                generatedAt = DateTime.UtcNow
            });
        }
    }

    public class OptimizeRequest
    {
        public List<long> ScheduleIds { get; set; } = new List<long>();
        public DateTime? StartDate { get; set; }
    }
}
