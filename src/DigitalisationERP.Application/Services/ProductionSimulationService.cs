using DigitalisationERP.Core;
using DigitalisationERP.Core.Entities;
using DigitalisationERP.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DigitalisationERP.Application.Services
{
    /// <summary>
    /// Service pour simuler les données de production en temps réel
    /// Génère des plans de production, mises à jour de statut, et métriques OEE
    /// </summary>
    public class ProductionSimulationService
    {
        private readonly Random _random = new Random();
        private readonly Dictionary<int, ProductionLineState> _lineStates = new();
        private readonly Dictionary<int, PostProductionData> _postData = new();

        public ProductionSimulationService()
        {
            InitializeProductionLines();
        }

        /// <summary>
        /// Génère une mise à jour de production pour un poste
        /// Simule la progression de la production en temps réel
        /// </summary>
        public ProductionUpdate GenerateProductionUpdate(int postId, int lineId)
        {
            var lineState = GetOrCreateLineState(lineId);
            var postData = GetOrCreatePostData(postId);

            // Simule la production (70-90% efficiency)
            double efficiency = 70 + _random.NextDouble() * 20;
            int itemsProduced = GenerateItemsProduced(postData);
            int defects = Math.Max(0, (int)(itemsProduced * (1 - efficiency / 100)));

            // Mises à jour de statut du poste
            var status = GeneratePostStatus();

            // Calcul du TaktTime réel vs théorique
            double taktTimeTheoretical = 60; // 1 item par minute en général
            double taktTimeActual = taktTimeTheoretical / (efficiency / 100);

            // Mise à jour OEE
            var oeeMetrics = CalculateOEE(efficiency, itemsProduced, defects, taktTimeActual);

            return new ProductionUpdate
            {
                PostId = postId,
                LineId = lineId,
                Timestamp = DateTime.Now,
                ItemsProduced = itemsProduced,
                DefectCount = defects,
                EfficiencyPercent = Math.Round(efficiency, 2),
                PostStatus = status,
                TaktTimeSecond = Math.Round(taktTimeActual, 2),
                TaktTimeTheoreticalSecond = taktTimeTheoretical,
                OEEMetrics = oeeMetrics,
                CycleTime = Math.Round(taktTimeActual, 2),
                DowntimeSeconds = GenerateDowntime()
            };
        }

        /// <summary>
        /// Génère un plan de production complet pour une ligne
        /// </summary>
        public ProductionPlanSimulation GenerateProductionPlan(int lineId, int durationMinutes = 480)
        {
            var lineState = GetOrCreateLineState(lineId);

            var plan = new ProductionPlanSimulation
            {
                LineId = lineId,
                PlannedStartTime = DateTime.Now,
                PlannedEndTime = DateTime.Now.AddMinutes(durationMinutes),
                TargetQuantity = _random.Next(500, 1500),
                EstimatedTaktTime = 60,
                ShiftType = GenerateShiftType(),
                Operators = _random.Next(3, 8),
                MaterialsAllocated = GenerateMaterials()
            };

            return plan;
        }

        /// <summary>
        /// Génère un incident/arrêt de production
        /// </summary>
        public ProductionIncident? GenerateIncident(int postId, int lineId, double probability = 0.05)
        {
            if (_random.NextDouble() > probability)
                return null;

            var incidentTypes = new[]
            {
                ("Equipment Jam", 5, 30),
                ("Material Supply Issue", 10, 45),
                ("Quality Check Failed", 5, 20),
                ("Sensor Malfunction", 15, 60),
                ("Operator Error", 3, 15),
                ("Electrical Problem", 20, 90),
                ("Safety Stop", 2, 25)
            };

            var (type, minDowntime, maxDowntime) = incidentTypes[_random.Next(incidentTypes.Length)];

            return new ProductionIncident
            {
                PostId = postId,
                LineId = lineId,
                IncidentType = type,
                Timestamp = DateTime.Now,
                EstimatedDowntimeMinutes = _random.Next(minDowntime, maxDowntime),
                Severity = type switch
                {
                    "Equipment Jam" => "High",
                    "Material Supply Issue" => "Medium",
                    "Quality Check Failed" => "Medium",
                    "Sensor Malfunction" => "High",
                    "Operator Error" => "Low",
                    "Electrical Problem" => "Critical",
                    "Safety Stop" => "Critical",
                    _ => "Unknown"
                },
                RequiresIntervention = _random.NextDouble() < 0.6,
                Status = "Active"
            };
        }

        /// <summary>
        /// Génère une mise à jour OEE en temps réel
        /// </summary>
        public OEEUpdate GenerateOEEUpdate(int postId, int lineId, double availability, int produced, int defects)
        {
            double quality = produced > 0 ? ((produced - defects) / (double)produced) * 100 : 100;
            double performance = 70 + _random.NextDouble() * 25;

            return new OEEUpdate
            {
                PostId = postId,
                LineId = lineId,
                Timestamp = DateTime.Now,
                Availability = Math.Round(availability, 2),
                Performance = Math.Round(performance, 2),
                Quality = Math.Round(quality, 2),
                OEEPercent = Math.Round((availability / 100) * (performance / 100) * (quality / 100) * 100, 2),
                TrendDirection = _random.NextDouble() < 0.5 ? "Up" : "Down"
            };
        }

        /// <summary>
        /// Génère les données pour tous les postes d'une ligne
        /// </summary>
        public List<ProductionUpdate> GenerateLineProductionUpdates(int lineId, List<int> postIds)
        {
            return postIds.Select(postId => GenerateProductionUpdate(postId, lineId)).ToList();
        }

        /// <summary>
        /// Génère un rapport de production horaire
        /// </summary>
        public HourlyProductionReport GenerateHourlyReport(int lineId, List<int> postIds)
        {
            var updates = GenerateLineProductionUpdates(lineId, postIds);

            return new HourlyProductionReport
            {
                LineId = lineId,
                ReportTime = DateTime.Now,
                TotalItemsProduced = updates.Sum(u => u.ItemsProduced),
                TotalDefects = updates.Sum(u => u.DefectCount),
                AverageEfficiency = updates.Average(u => u.EfficiencyPercent),
                AverageTaktTime = updates.Average(u => u.TaktTimeSecond),
                TotalDowntimeMinutes = updates.Sum(u => u.DowntimeSeconds) / 60.0,
                PostReports = updates.Select(u => new PostProductionReport
                {
                    PostId = u.PostId,
                    ItemsProduced = u.ItemsProduced,
                    DefectCount = u.DefectCount,
                    Efficiency = u.EfficiencyPercent,
                    Status = u.PostStatus
                }).ToList(),
                QualityRate = updates.Count > 0 
                    ? (1 - (updates.Sum(u => u.DefectCount) / (double)updates.Sum(u => u.ItemsProduced))) * 100 
                    : 100,
                LineStatus = GenerateLineStatus(updates)
            };
        }

        /// <summary>
        /// Génère un événement temps réel pour streaming
        /// </summary>
        public RealtimeProductionEvent GenerateRealtimeEvent(int lineId)
        {
            var eventTypes = new[]
            {
                "Production_Update",
                "Incident_Detected",
                "Quality_Issue",
                "Target_Reached",
                "Shift_Change",
                "Material_Low",
                "Maintenance_Alert"
            };

            return new RealtimeProductionEvent
            {
                LineId = lineId,
                EventType = eventTypes[_random.Next(eventTypes.Length)],
                Timestamp = DateTime.Now,
                Severity = _random.NextDouble() < 0.7 ? "Info" : (_random.NextDouble() < 0.9 ? "Warning" : "Critical"),
                Message = GenerateEventMessage(),
                Data = new Dictionary<string, object>
                {
                    { "lineId", lineId },
                    { "timestamp", DateTime.Now },
                    { "value", _random.NextDouble() * 100 }
                }
            };
        }

        // ==================== PRIVATE METHODS ====================

        private int GenerateItemsProduced(PostProductionData postData)
        {
            // Produit 1-5 items par appel (peut être appelé fréquemment)
            int items = _random.Next(1, 6);
            postData.TotalProduced += items;
            return items;
        }

        private string GeneratePostStatus()
        {
            var statuses = new[] { "Running", "Idle", "Setup", "Quality Check", "Cleaning" };
            return statuses[_random.Next(statuses.Length)];
        }

        private int GenerateDowntime()
        {
            // 5% de chance d'avoir du downtime (0-120 secondes)
            if (_random.NextDouble() < 0.05)
                return _random.Next(0, 121);
            return 0;
        }

        private OEEMetrics CalculateOEE(double efficiency, int produced, int defects, double taktTime)
        {
            double availability = 80 + _random.NextDouble() * 15;
            double quality = produced > 0 ? ((produced - defects) / (double)produced) * 100 : 100;
            double performance = efficiency;

            return new OEEMetrics
            {
                Availability = Math.Round(availability, 2),
                Performance = Math.Round(performance, 2),
                Quality = Math.Round(quality, 2),
                OEEPercent = Math.Round((availability / 100) * (performance / 100) * (quality / 100) * 100, 2),
                CalculatedTime = (int)(DateTime.Now - DateTime.UtcNow).TotalSeconds,
            };
        }

        private string GenerateShiftType()
        {
            var hour = DateTime.Now.Hour;
            if (hour >= 6 && hour < 14) return "Morning";
            if (hour >= 14 && hour < 22) return "Afternoon";
            return "Night";
        }

        private List<string> GenerateMaterials()
        {
            var materials = new[] { "Raw Material A", "Raw Material B", "Component C", "Paint", "Packaging" };
            int count = _random.Next(2, 5);
            return materials.Take(count).ToList();
        }

        private string GenerateEventMessage()
        {
            var messages = new[]
            {
                "Production target reached 50%",
                "Equipment efficiency optimal",
                "Quality check passed",
                "Material supply status normal",
                "Operator shift change in progress",
                "Preventive maintenance scheduled",
                "Production rate above baseline",
                "System performance nominal"
            };
            return messages[_random.Next(messages.Length)];
        }

        private string GenerateLineStatus(List<ProductionUpdate> updates)
        {
            var avgEfficiency = updates.Average(u => u.EfficiencyPercent);
            if (avgEfficiency > 85) return "Excellent";
            if (avgEfficiency > 75) return "Good";
            if (avgEfficiency > 65) return "Fair";
            return "Poor";
        }

        private void InitializeProductionLines()
        {
            for (int i = 1; i <= 7; i++)
            {
                _lineStates[i] = new ProductionLineState
                {
                    LineId = i,
                    IsRunning = true,
                    CurrentShift = GenerateShiftType()
                };
            }
        }

        private ProductionLineState GetOrCreateLineState(int lineId)
        {
            if (!_lineStates.ContainsKey(lineId))
            {
                _lineStates[lineId] = new ProductionLineState
                {
                    LineId = lineId,
                    IsRunning = true,
                    CurrentShift = GenerateShiftType()
                };
            }
            return _lineStates[lineId];
        }

        private PostProductionData GetOrCreatePostData(int postId)
        {
            if (!_postData.ContainsKey(postId))
            {
                _postData[postId] = new PostProductionData { PostId = postId, TotalProduced = 0 };
            }
            return _postData[postId];
        }

        // ==================== INTERNAL CLASSES ====================

        private class ProductionLineState
        {
            public int LineId { get; set; }
            public bool IsRunning { get; set; }
            public string CurrentShift { get; set; } = string.Empty;
        }

        private class PostProductionData
        {
            public int PostId { get; set; }
            public int TotalProduced { get; set; }
        }
    }

    // ==================== DTOs & MODELS ====================

    /// <summary>
    /// Mise à jour de production en temps réel d'un poste
    /// </summary>
    public class ProductionUpdate
    {
        public int PostId { get; set; }
        public int LineId { get; set; }
        public DateTime Timestamp { get; set; }
        public int ItemsProduced { get; set; }
        public int DefectCount { get; set; }
        public double EfficiencyPercent { get; set; }
        public string PostStatus { get; set; } = string.Empty;
        public double TaktTimeSecond { get; set; }
        public double TaktTimeTheoreticalSecond { get; set; }
        public double CycleTime { get; set; }
        public int DowntimeSeconds { get; set; }
        public OEEMetrics OEEMetrics { get; set; } = new();
    }

    /// <summary>
    /// Plan de production simulé
    /// </summary>
    public class ProductionPlanSimulation
    {
        public int LineId { get; set; }
        public DateTime PlannedStartTime { get; set; }
        public DateTime PlannedEndTime { get; set; }
        public int TargetQuantity { get; set; }
        public double EstimatedTaktTime { get; set; }
        public string ShiftType { get; set; } = string.Empty;
        public int Operators { get; set; }
        public List<string> MaterialsAllocated { get; set; } = new();
    }

    /// <summary>
    /// Incident/arrêt de production
    /// </summary>
    public class ProductionIncident
    {
        public int PostId { get; set; }
        public int LineId { get; set; }
        public string IncidentType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int EstimatedDowntimeMinutes { get; set; }
        public string Severity { get; set; } = string.Empty;
        public bool RequiresIntervention { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// Mise à jour OEE en temps réel
    /// </summary>
    public class OEEUpdate
    {
        public int PostId { get; set; }
        public int LineId { get; set; }
        public DateTime Timestamp { get; set; }
        public double Availability { get; set; }
        public double Performance { get; set; }
        public double Quality { get; set; }
        public double OEEPercent { get; set; }
        public string TrendDirection { get; set; } = string.Empty;
    }

    /// <summary>
    /// Rapport de production horaire
    /// </summary>
    public class HourlyProductionReport
    {
        public int LineId { get; set; }
        public DateTime ReportTime { get; set; }
        public int TotalItemsProduced { get; set; }
        public int TotalDefects { get; set; }
        public double AverageEfficiency { get; set; }
        public double AverageTaktTime { get; set; }
        public double TotalDowntimeMinutes { get; set; }
        public double QualityRate { get; set; }
        public string LineStatus { get; set; } = string.Empty;
        public List<PostProductionReport> PostReports { get; set; } = new();
    }

    /// <summary>
    /// Rapport de production par poste
    /// </summary>
    public class PostProductionReport
    {
        public int PostId { get; set; }
        public int ItemsProduced { get; set; }
        public int DefectCount { get; set; }
        public double Efficiency { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// Événement temps réel pour streaming (WebSocket/SignalR)
    /// </summary>
    public class RealtimeProductionEvent
    {
        public int LineId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Severity { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, object> Data { get; set; } = new();
    }
}
