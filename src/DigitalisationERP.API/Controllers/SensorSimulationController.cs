using DigitalisationERP.Application.Services;
using DigitalisationERP.Core.Entities.IoT;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DigitalisationERP.API.Controllers
{
    /// <summary>
    /// Contrôleur pour la simulation de données de senseurs
    /// Permet de générer des lectures réalistes sans matériel IoT
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SensorSimulationController : ControllerBase
    {
        private readonly SensorSimulationService _simulationService;

        public SensorSimulationController(SensorSimulationService simulationService)
        {
            _simulationService = simulationService;
        }

        /// <summary>
        /// Génère une lecture simple pour un senseur spécifique
        /// </summary>
        [HttpGet("reading")]
        public IActionResult GetSensorReading([FromQuery] int postId, [FromQuery] string sensorType = "MotorTemperature")
        {
            try
            {
                if (postId <= 0)
                    return BadRequest(new { error = "postId doit être positif" });

                if (!Enum.TryParse<SensorType>(sensorType, out var type))
                    return BadRequest(new { error = $"Type de senseur invalide: {sensorType}" });

                var reading = _simulationService.GenerateSensorReading(
                    postId,
                    $"EQ-{postId:D3}",
                    type
                );

                return Ok(new { data = MapToDto(reading) });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Génère des lectures pour tous les senseurs d'un poste
        /// </summary>
        [HttpGet("post-readings")]
        public IActionResult GetPostSensorReadings([FromQuery] int postId)
        {
            try
            {
                if (postId <= 0)
                    return BadRequest(new { error = "postId doit être positif" });

                var readings = _simulationService.GeneratePostSensorReadings(
                    postId,
                    $"EQ-{postId:D3}"
                );

                return Ok(new 
                { 
                    data = readings.Select(r => MapToDto(r)).ToList(),
                    count = readings.Count,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Génère une anomalie pour tester les alertes
        /// Types: overheat, undercool, vibration_spike, pressure_drop, low_level
        /// </summary>
        [HttpGet("anomaly")]
        public IActionResult GetAnomalySensorReading(
            [FromQuery] int postId,
            [FromQuery] string sensorType = "MotorTemperature",
            [FromQuery] string anomalyType = "overheat")
        {
            try
            {
                if (postId <= 0)
                    return BadRequest(new { error = "postId doit être positif" });

                if (!Enum.TryParse<SensorType>(sensorType, out var type))
                    return BadRequest(new { error = $"Type de senseur invalide: {sensorType}" });

                var validAnomalies = new[] { "overheat", "undercool", "vibration_spike", "pressure_drop", "low_level" };
                if (!validAnomalies.Contains(anomalyType.ToLower()))
                    return BadRequest(new { error = $"Type d'anomalie invalide. Utilisez: {string.Join(", ", validAnomalies)}" });

                var reading = _simulationService.GenerateAnomalySensorReading(
                    postId,
                    $"EQ-{postId:D3}",
                    type,
                    anomalyType
                );

                return Ok(new { data = MapToDto(reading) });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Obtient l'historique des valeurs pour trend analysis
        /// </summary>
        [HttpGet("history")]
        public IActionResult GetSensorHistory(
            [FromQuery] int postId,
            [FromQuery] string sensorType = "MotorTemperature",
            [FromQuery] int minutesBack = 60)
        {
            try
            {
                if (postId <= 0)
                    return BadRequest(new { error = "postId doit être positif" });

                if (!Enum.TryParse<SensorType>(sensorType, out var type))
                    return BadRequest(new { error = $"Type de senseur invalide: {sensorType}" });

                if (minutesBack < 1 || minutesBack > 1440)
                    return BadRequest(new { error = "minutesBack doit être entre 1 et 1440" });

                var readings = _simulationService.GetSensorHistory(postId, type, minutesBack);

                return Ok(new
                {
                    data = readings.Select(r => MapToDto(r)).ToList(),
                    sensorType = sensorType,
                    count = readings.Count,
                    period = $"Dernières {minutesBack} minutes"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Simule des anomalies multiples sur une chaîne de production complète
        /// </summary>
        [HttpPost("full-simulation")]
        public IActionResult RunFullSimulation([FromBody] FullSimulationRequestDto request)
        {
            try
            {
                if (request?.LinePostIds == null || !request.LinePostIds.Any())
                    return BadRequest(new { error = "Au moins un postId est requis" });

                if (request.LinePostIds.Any(id => id <= 0))
                    return BadRequest(new { error = "Tous les postIds doivent être positifs" });

                var result = new FullSimulationResultDto
                {
                    SimulationDate = DateTime.Now,
                    PostReadings = new List<PostReadingDto>(),
                    AnomalousReadings = new List<SensorReadingDto>()
                };

                // Génère des lectures normales pour chaque poste
                foreach (var postId in request.LinePostIds)
                {
                    var readings = _simulationService.GeneratePostSensorReadings(postId, $"EQ-{postId:D3}");
                    
                    result.PostReadings.Add(new PostReadingDto
                    {
                        PostId = postId,
                        EquipmentNumber = $"EQ-{postId:D3}",
                        SensorReadings = readings.Select(r => MapToDto(r)).ToList(),
                        AverageHealth = readings.Count(r => r.IsNormal) / (double)readings.Count * 100
                    });

                    // 30% de chance de générer une anomalie pour un senseur
                    var random = new Random();
                    if (random.NextDouble() < 0.3)
                    {
                        var sensorTypes = new[] { SensorType.MotorTemperature, SensorType.Vibration, SensorType.OilLevel };
                        var randomSensor = sensorTypes[random.Next(sensorTypes.Length)];
                        var anomalyTypes = new[] { "overheat", "vibration_spike", "low_level" };
                        var anomaly = _simulationService.GenerateAnomalySensorReading(
                            postId,
                            $"EQ-{postId:D3}",
                            randomSensor,
                            anomalyTypes[random.Next(anomalyTypes.Length)]
                        );
                        result.AnomalousReadings.Add(MapToDto(anomaly));
                    }
                }

                result.TotalReadings = result.PostReadings.Sum(p => p.SensorReadings.Count);
                result.AnomaliesDetected = result.AnomalousReadings.Count;

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Obtient les types de senseurs disponibles
        /// </summary>
        [HttpGet("sensor-types")]
        public IActionResult GetAvailableSensorTypes()
        {
            var types = Enum.GetNames(typeof(SensorType))
                .Where(n => n != "Other")
                .Select(n => new { name = n, value = (int)Enum.Parse(typeof(SensorType), n) })
                .ToList();

            return Ok(new { sensorTypes = types, count = types.Count });
        }

        /// <summary>
        /// Obtient les types d'anomalies disponibles
        /// </summary>
        [HttpGet("anomaly-types")]
        public IActionResult GetAvailableAnomalyTypes()
        {
            var anomalies = new[]
            {
                new { name = "overheat", description = "Température moteur dépassant les seuils" },
                new { name = "undercool", description = "Température anormalement basse" },
                new { name = "vibration_spike", description = "Pics de vibration détectés" },
                new { name = "pressure_drop", description = "Chute de pression système" },
                new { name = "low_level", description = "Niveau bas de fluide" }
            };

            return Ok(new { anomalyTypes = anomalies });
        }

        /// <summary>
        /// Mappe une entité SensorReading vers un DTO
        /// </summary>
        private SensorReadingDto MapToDto(SensorReading reading)
        {
            return new SensorReadingDto
            {
                Id = (int)reading.Id,
                ProductionPostId = (int)reading.ProductionPostId,
                EquipmentNumber = reading.EquipmentNumber ?? string.Empty,
                SensorName = reading.SensorName ?? string.Empty,
                SensorType = reading.SensorType.ToString(),
                Value = reading.Value,
                Unit = reading.Unit ?? string.Empty,
                Timestamp = reading.Timestamp,
                ThresholdMin = reading.ThresholdMin,
                ThresholdMax = reading.ThresholdMax,
                IsNormal = reading.IsNormal,
                AlertLevel = reading.AlertLevel?.ToString() ?? string.Empty,
                Status = reading.Status ?? string.Empty,
                ProductionOrderNumber = reading.ProductionOrderNumber ?? string.Empty
            };
        }
    }

    /// <summary>
    /// DTO pour une lecture de senseur
    /// </summary>
    public class SensorReadingDto
    {
        public int Id { get; set; }
        public int ProductionPostId { get; set; }
        public string EquipmentNumber { get; set; } = string.Empty;
        public string SensorName { get; set; } = string.Empty;
        public string SensorType { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double? ThresholdMin { get; set; }
        public double? ThresholdMax { get; set; }
        public bool IsNormal { get; set; }
        public string AlertLevel { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ProductionOrderNumber { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO pour la requête de simulation complète
    /// </summary>
    public class FullSimulationRequestDto
    {
        public List<int> LinePostIds { get; set; } = new();
        public string LineIdentifier { get; set; } = "Line-1";
        public bool IncludeAnomalies { get; set; } = true;
    }

    /// <summary>
    /// DTO pour le résultat de simulation complète
    /// </summary>
    public class FullSimulationResultDto
    {
        public DateTime SimulationDate { get; set; }
        public List<PostReadingDto> PostReadings { get; set; } = new();
        public List<SensorReadingDto> AnomalousReadings { get; set; } = new();
        public int TotalReadings { get; set; }
        public int AnomaliesDetected { get; set; }
    }

    /// <summary>
    /// DTO pour les lectures d'un poste
    /// </summary>
    public class PostReadingDto
    {
        public int PostId { get; set; }
        public string EquipmentNumber { get; set; } = string.Empty;
        public List<SensorReadingDto> SensorReadings { get; set; } = new();
        public double AverageHealth { get; set; }
    }
}
