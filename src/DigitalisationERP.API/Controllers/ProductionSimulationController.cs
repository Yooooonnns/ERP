using DigitalisationERP.Application.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DigitalisationERP.API.Controllers
{
    /// <summary>
    /// Contrôleur pour la simulation de production en temps réel
    /// Génère des mises à jour de production, incidents, et métriques OEE
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ProductionSimulationController : ControllerBase
    {
        private readonly ProductionSimulationService _simulationService;

        public ProductionSimulationController(ProductionSimulationService simulationService)
        {
            _simulationService = simulationService;
        }

        /// <summary>
        /// Génère une mise à jour de production pour un poste
        /// </summary>
        [HttpGet("update")]
        public IActionResult GetProductionUpdate([FromQuery] int postId, [FromQuery] int lineId = 1)
        {
            try
            {
                if (postId <= 0 || lineId <= 0)
                    return BadRequest(new { error = "postId et lineId doivent être positifs" });

                var update = _simulationService.GenerateProductionUpdate(postId, lineId);

                return Ok(new { data = MapToDto(update) });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Génère les mises à jour pour tous les postes d'une ligne
        /// </summary>
        [HttpGet("line-updates")]
        public IActionResult GetLineProductionUpdates([FromQuery] int lineId, [FromQuery] string postIds = "1,2,3,4,5,6,7")
        {
            try
            {
                if (lineId <= 0)
                    return BadRequest(new { error = "lineId doit être positif" });

                var postIdList = postIds.Split(',').Select(p => int.Parse(p.Trim())).ToList();
                var updates = _simulationService.GenerateLineProductionUpdates(lineId, postIdList);

                return Ok(new
                {
                    data = updates.Select(u => MapToDto(u)).ToList(),
                    count = updates.Count,
                    timestamp = DateTime.Now,
                    averageEfficiency = updates.Average(u => u.EfficiencyPercent),
                    totalProduced = updates.Sum(u => u.ItemsProduced)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Génère un plan de production complet
        /// </summary>
        [HttpGet("plan")]
        public IActionResult GetProductionPlan([FromQuery] int lineId, [FromQuery] int durationMinutes = 480)
        {
            try
            {
                if (lineId <= 0)
                    return BadRequest(new { error = "lineId doit être positif" });

                var plan = _simulationService.GenerateProductionPlan(lineId, durationMinutes);

                return Ok(new
                {
                    data = new
                    {
                        lineId = plan.LineId,
                        plannedStartTime = plan.PlannedStartTime,
                        plannedEndTime = plan.PlannedEndTime,
                        targetQuantity = plan.TargetQuantity,
                        estimatedTaktTime = plan.EstimatedTaktTime,
                        shiftType = plan.ShiftType,
                        operators = plan.Operators,
                        materialsAllocated = plan.MaterialsAllocated
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Génère un incident de production (arrêt, problème)
        /// </summary>
        [HttpGet("incident")]
        public IActionResult GetProductionIncident([FromQuery] int postId, [FromQuery] int lineId = 1)
        {
            try
            {
                if (postId <= 0 || lineId <= 0)
                    return BadRequest(new { error = "postId et lineId doivent être positifs" });

                var incident = _simulationService.GenerateIncident(postId, lineId);

                if (incident == null)
                    return Ok(new { data = (object?)null, message = "Pas d'incident générée ce cycle" });

                return Ok(new
                {
                    data = new
                    {
                        postId = incident.PostId,
                        lineId = incident.LineId,
                        incidentType = incident.IncidentType,
                        timestamp = incident.Timestamp,
                        estimatedDowntimeMinutes = incident.EstimatedDowntimeMinutes,
                        severity = incident.Severity,
                        requiresIntervention = incident.RequiresIntervention,
                        status = incident.Status.ToString()
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Génère une mise à jour OEE
        /// </summary>
        [HttpGet("oee")]
        public IActionResult GetOEEUpdate(
            [FromQuery] int postId,
            [FromQuery] int lineId = 1,
            [FromQuery] int produced = 50,
            [FromQuery] int defects = 5)
        {
            try
            {
                if (postId <= 0 || lineId <= 0)
                    return BadRequest(new { error = "postId et lineId doivent être positifs" });

                double availability = 70 + new Random().NextDouble() * 25;
                var update = _simulationService.GenerateOEEUpdate(postId, lineId, availability, produced, defects);

                return Ok(new
                {
                    data = new
                    {
                        postId = update.PostId,
                        lineId = update.LineId,
                        timestamp = update.Timestamp,
                        availability = update.Availability,
                        performance = update.Performance,
                        quality = update.Quality,
                        oeePercent = update.OEEPercent,
                        trendDirection = update.TrendDirection
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Génère un rapport de production horaire
        /// </summary>
        [HttpGet("hourly-report")]
        public IActionResult GetHourlyReport([FromQuery] int lineId, [FromQuery] string postIds = "1,2,3,4,5,6,7")
        {
            try
            {
                if (lineId <= 0)
                    return BadRequest(new { error = "lineId doit être positif" });

                var postIdList = postIds.Split(',').Select(p => int.Parse(p.Trim())).ToList();
                var report = _simulationService.GenerateHourlyReport(lineId, postIdList);

                return Ok(new
                {
                    data = new
                    {
                        lineId = report.LineId,
                        reportTime = report.ReportTime,
                        totalItemsProduced = report.TotalItemsProduced,
                        totalDefects = report.TotalDefects,
                        averageEfficiency = Math.Round(report.AverageEfficiency, 2),
                        averageTaktTime = Math.Round(report.AverageTaktTime, 2),
                        totalDowntimeMinutes = Math.Round(report.TotalDowntimeMinutes, 2),
                        qualityRate = Math.Round(report.QualityRate, 2),
                        lineStatus = report.LineStatus,
                        postReports = report.PostReports.Select(pr => new
                        {
                            postId = pr.PostId,
                            itemsProduced = pr.ItemsProduced,
                            defectCount = pr.DefectCount,
                            efficiency = pr.Efficiency,
                            status = pr.Status
                        }).ToList()
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Génère un événement temps réel
        /// </summary>
        [HttpGet("realtime-event")]
        public IActionResult GetRealtimeEvent([FromQuery] int lineId = 1)
        {
            try
            {
                if (lineId <= 0)
                    return BadRequest(new { error = "lineId doit être positif" });

                var evt = _simulationService.GenerateRealtimeEvent(lineId);

                return Ok(new
                {
                    data = new
                    {
                        lineId = evt.LineId,
                        eventType = evt.EventType,
                        timestamp = evt.Timestamp,
                        severity = evt.Severity,
                        message = evt.Message,
                        data = evt.Data
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Simulation complète d'une journée de production
        /// </summary>
        [HttpPost("full-day-simulation")]
        public IActionResult RunFullDaySimulation([FromBody] FullDaySimulationRequest request)
        {
            try
            {
                if (request == null || request.LineId <= 0)
                    return BadRequest(new { error = "lineId invalide" });

                var lineId = request.LineId;

                var postIds = request.PostIds ?? Enumerable.Range(1, 7).ToList();

                // Génère 12 heures de simulation (un appel par heure)
                var hourlyReports = new List<object>();
                for (int hour = 0; hour < 12; hour++)
                {
                    var report = _simulationService.GenerateHourlyReport(lineId, postIds);
                    hourlyReports.Add(new
                    {
                        hour = hour,
                        report = report
                    });
                }

                return Ok(new
                {
                    data = new
                    {
                        lineId = request.LineId,
                        simulationDate = DateTime.Now,
                        hoursSimulated = 12,
                        hourlyReports = hourlyReports,
                        summary = new
                        {
                            totalProduced = hourlyReports.Sum(r => ((dynamic)r).report.TotalItemsProduced),
                            totalDefects = hourlyReports.Sum(r => ((dynamic)r).report.TotalDefects),
                            averageEfficiency = hourlyReports.Average(r => ((dynamic)r).report.AverageEfficiency),
                            averageLineStatus = "See hourly data"
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Streaming continu de mises à jour production
        /// </summary>
        [HttpGet("stream")]
        public IActionResult GetProductionStream([FromQuery] int lineId, [FromQuery] int intervalSeconds = 1)
        {
            try
            {
                if (lineId <= 0)
                    return BadRequest(new { error = "lineId doit être positif" });

                var updates = new List<object>();
                for (int i = 0; i < 10; i++)
                {
                    var update = _simulationService.GenerateRealtimeEvent(lineId);
                    updates.Add(new
                    {
                        index = i + 1,
                        timestamp = update.Timestamp,
                        eventType = update.EventType,
                        severity = update.Severity,
                        message = update.Message
                    });
                }

                return Ok(new
                {
                    data = updates,
                    count = updates.Count,
                    streamInterval = $"{intervalSeconds}s",
                    lineId = lineId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ==================== HELPER METHODS ====================

        private object MapToDto(ProductionUpdate update)
        {
            return new
            {
                postId = update.PostId,
                lineId = update.LineId,
                timestamp = update.Timestamp,
                itemsProduced = update.ItemsProduced,
                defectCount = update.DefectCount,
                efficiencyPercent = update.EfficiencyPercent,
                postStatus = update.PostStatus,
                taktTimeSecond = update.TaktTimeSecond,
                taktTimeTheoreticalSecond = update.TaktTimeTheoreticalSecond,
                cycleTime = update.CycleTime,
                downtimeSeconds = update.DowntimeSeconds,
                oeeMetrics = update.OEEMetrics != null ? new
                {
                    availability = update.OEEMetrics.Availability,
                    performance = update.OEEMetrics.Performance,
                    quality = update.OEEMetrics.Quality,
                    oeePercent = update.OEEMetrics.OEEPercent
                } : null
            };
        }
    }

    /// <summary>
    /// Request DTO pour simulation journée complète
    /// </summary>
    public class FullDaySimulationRequest
    {
        public int LineId { get; set; }
        public List<int> PostIds { get; set; } = new();
        public int HoursToSimulate { get; set; } = 12;
    }
}
