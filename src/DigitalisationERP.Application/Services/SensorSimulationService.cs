using DigitalisationERP.Core.Entities.IoT;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DigitalisationERP.Application.Services
{
    /// <summary>
    /// Service pour simuler des données de senseurs en temps réel
    /// Génère des lectures réalistes avec anomalies aléatoires pour tester le système
    /// </summary>
    public class SensorSimulationService
    {
        private readonly Random _random = new Random();
        private readonly Dictionary<int, SensorHistory> _sensorHistories = new();
        private readonly Dictionary<string, SensorThresholds> _sensorThresholds;

        public SensorSimulationService()
        {
            _sensorThresholds = InitializeSensorThresholds();
            InitializeSensorHistories();
        }

        /// <summary>
        /// Génère une nouvelle lecture de senseur simulée
        /// </summary>
        public SensorReading GenerateSensorReading(int postId, string equipmentNumber, SensorType sensorType)
        {
            var thresholds = _sensorThresholds[sensorType.ToString()];
            var history = GetOrCreateHistory(postId);

            // Obtient la dernière valeur enregistrée pour ce senseur
            var lastValue = history.LastValues.ContainsKey(sensorType) 
                ? history.LastValues[sensorType] 
                : thresholds.Normal;

            // Simule des fluctuations réalistes
            double value = SimulateValue(lastValue, thresholds);

            // Enregistre la valeur pour la prochaine itération
            if (!history.LastValues.ContainsKey(sensorType))
                history.LastValues[sensorType] = value;
            else
                history.LastValues[sensorType] = value;

            // Vérifie si la valeur dépasse les seuils
            bool isNormal = value >= thresholds.Min && value <= thresholds.Max;
            AlertLevel? alertLevel = DetermineAlertLevel(value, thresholds);

            return new SensorReading
            {
                ProductionPostId = postId,
                EquipmentNumber = equipmentNumber,
                SensorName = GetSensorName(sensorType),
                SensorType = sensorType,
                Timestamp = DateTime.Now,
                Value = Math.Round(value, 2),
                Unit = thresholds.Unit,
                ThresholdMin = thresholds.Min,
                ThresholdMax = thresholds.Max,
                IsNormal = isNormal,
                AlertLevel = alertLevel,
                Status = GetStatusDescription(sensorType, value, thresholds),
                ProductionOrderNumber = $"ORD-{DateTime.Now:yyyyMMdd}"
            };
        }

        /// <summary>
        /// Génère un ensemble de lectures pour tous les senseurs d'un poste
        /// </summary>
        public List<SensorReading> GeneratePostSensorReadings(int postId, string equipmentNumber)
        {
            var readings = new List<SensorReading>();

            var sensorTypes = new[]
            {
                SensorType.MotorTemperature,
                SensorType.BearingTemperature,
                SensorType.Pressure,
                SensorType.Vibration,
                SensorType.MotorSpeed,
                SensorType.PowerConsumption,
                SensorType.OilLevel,
                SensorType.CoolantLevel
            };

            foreach (var sensorType in sensorTypes)
            {
                readings.Add(GenerateSensorReading(postId, equipmentNumber, sensorType));
            }

            return readings;
        }

        /// <summary>
        /// Génère des lectures avec anomalies pour tester les alertes
        /// </summary>
        public SensorReading GenerateAnomalySensorReading(int postId, string equipmentNumber, SensorType sensorType, string anomalyType = "overheat")
        {
            var thresholds = _sensorThresholds[sensorType.ToString()];
            double value;

            // Crée des anomalies réalistes
            switch (anomalyType.ToLower())
            {
                case "overheat":
                    value = thresholds.Max + (10 + _random.NextDouble() * 40);
                    break;

                case "undercool":
                    value = thresholds.Min - (5 + _random.NextDouble() * 20);
                    break;

                case "vibration_spike":
                    value = thresholds.Max * 1.5 + _random.NextDouble() * thresholds.Max;
                    break;

                case "pressure_drop":
                    value = thresholds.Min * 0.7 - _random.NextDouble() * 5;
                    break;

                case "low_level":
                    value = thresholds.Min + (5 + _random.NextDouble() * 10);
                    break;

                default:
                    value = thresholds.Max + _random.NextDouble() * 20;
                    break;
            }

            return new SensorReading
            {
                ProductionPostId = postId,
                EquipmentNumber = equipmentNumber,
                SensorName = GetSensorName(sensorType),
                SensorType = sensorType,
                Timestamp = DateTime.Now,
                Value = Math.Round(value, 2),
                Unit = thresholds.Unit,
                ThresholdMin = thresholds.Min,
                ThresholdMax = thresholds.Max,
                IsNormal = false,
                AlertLevel = AlertLevel.Emergency,
                Status = $"ANOMALY: {anomalyType} detected - {value:F1} {thresholds.Unit}",
                ProductionOrderNumber = $"ORD-{DateTime.Now:yyyyMMdd}"
            };
        }

        /// <summary>
        /// Obtient l'historique des valeurs pour trend analysis
        /// </summary>
        public List<SensorReading> GetSensorHistory(int postId, SensorType sensorType, int minutesBack = 60)
        {
            var history = new List<SensorReading>();
            var thresholds = _sensorThresholds[sensorType.ToString()];

            for (int i = minutesBack; i >= 0; i--)
            {
                var timestamp = DateTime.Now.AddMinutes(-i);
                var value = SimulateValue(thresholds.Normal, thresholds);

                history.Add(new SensorReading
                {
                    ProductionPostId = postId,
                    EquipmentNumber = $"EQ-{postId:D3}",
                    SensorName = GetSensorName(sensorType),
                    SensorType = sensorType,
                    Timestamp = timestamp,
                    Value = value,
                    Unit = thresholds.Unit,
                    ThresholdMin = thresholds.Min,
                    ThresholdMax = thresholds.Max,
                    IsNormal = value >= thresholds.Min && value <= thresholds.Max,
                    AlertLevel = DetermineAlertLevel(value, thresholds)
                });
            }

            return history;
        }

        /// <summary>
        /// Simule une valeur réaliste avec petit bruit gaussien
        /// </summary>
        private double SimulateValue(double baseValue, SensorThresholds thresholds)
        {
            // Box-Muller transform pour Gaussian noise
            double u1 = _random.NextDouble();
            double u2 = _random.NextDouble();
            double z0 = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);

            // Ajoute petit bruit (2% de la plage)
            double range = thresholds.Max - thresholds.Min;
            double noise = z0 * (range * 0.02);
            double newValue = baseValue + noise;

            // Retour vers la normale avec probabilité
            if (_random.NextDouble() < 0.1)
            {
                newValue = thresholds.Normal;
            }

            // Anomalie aléatoire (1% de chance)
            if (_random.NextDouble() < 0.01)
            {
                newValue = thresholds.Max * (1 + _random.NextDouble());
            }

            // Borne entre min et max (avec marge)
            newValue = Math.Max(thresholds.Min - 10, Math.Min(thresholds.Max + 10, newValue));

            return newValue;
        }

        /// <summary>
        /// Détermine le niveau d'alerte basé sur la valeur et les seuils
        /// </summary>
        private AlertLevel? DetermineAlertLevel(double value, SensorThresholds thresholds)
        {
            if (value >= thresholds.Min && value <= thresholds.Max)
                return null;

            if (value > thresholds.Max)
            {
                double excess = value - thresholds.Max;
                double range = thresholds.Max - thresholds.Min;

                if (excess > range * 0.3)
                    return AlertLevel.Emergency;
                else if (excess > range * 0.2)
                    return AlertLevel.Critical;
                else if (excess > range * 0.1)
                    return AlertLevel.Warning;
            }
            else if (value < thresholds.Min)
            {
                double deficit = thresholds.Min - value;
                double range = thresholds.Max - thresholds.Min;

                if (deficit > range * 0.3)
                    return AlertLevel.Emergency;
                else if (deficit > range * 0.2)
                    return AlertLevel.Critical;
                else if (deficit > range * 0.1)
                    return AlertLevel.Warning;
            }

            return AlertLevel.Info;
        }

        /// <summary>
        /// Obtient une description lisible du statut
        /// </summary>
        private string GetStatusDescription(SensorType sensorType, double value, SensorThresholds thresholds)
        {
            if (value < thresholds.Min)
                return $"Low {GetSensorName(sensorType)} ({value:F1} {thresholds.Unit})";
            else if (value > thresholds.Max)
                return $"High {GetSensorName(sensorType)} ({value:F1} {thresholds.Unit})";
            else
                return $"Normal {GetSensorName(sensorType)} ({value:F1} {thresholds.Unit})";
        }

        /// <summary>
        /// Obtient le nom lisible du type de senseur
        /// </summary>
        private string GetSensorName(SensorType sensorType)
        {
            return sensorType switch
            {
                SensorType.MotorTemperature => "Motor Temperature",
                SensorType.BearingTemperature => "Bearing Temperature",
                SensorType.Pressure => "System Pressure",
                SensorType.Vibration => "Vibration Level",
                SensorType.MotorSpeed => "Motor Speed",
                SensorType.PowerConsumption => "Power Consumption",
                SensorType.OilLevel => "Oil Level",
                SensorType.CoolantLevel => "Coolant Level",
                SensorType.BeltTension => "Belt Tension",
                SensorType.FlowRate => "Flow Rate",
                SensorType.Humidity => "Humidity",
                _ => "Unknown Sensor"
            };
        }

        /// <summary>
        /// Initialise les seuils pour chaque type de senseur
        /// </summary>
        private Dictionary<string, SensorThresholds> InitializeSensorThresholds()
        {
            return new Dictionary<string, SensorThresholds>
            {
                { SensorType.MotorTemperature.ToString(), new SensorThresholds { Min = 40, Max = 85, Normal = 67.5, Unit = "°C" } },
                { SensorType.BearingTemperature.ToString(), new SensorThresholds { Min = 30, Max = 75, Normal = 52.5, Unit = "°C" } },
                { SensorType.Pressure.ToString(), new SensorThresholds { Min = 2, Max = 7, Normal = 5, Unit = "Bar" } },
                { SensorType.Vibration.ToString(), new SensorThresholds { Min = 0.5, Max = 8, Normal = 3.5, Unit = "mm/s" } },
                { SensorType.MotorSpeed.ToString(), new SensorThresholds { Min = 1400, Max = 1550, Normal = 1475, Unit = "RPM" } },
                { SensorType.PowerConsumption.ToString(), new SensorThresholds { Min = 1, Max = 5, Normal = 3, Unit = "kW" } },
                { SensorType.OilLevel.ToString(), new SensorThresholds { Min = 50, Max = 100, Normal = 87.5, Unit = "%" } },
                { SensorType.CoolantLevel.ToString(), new SensorThresholds { Min = 40, Max = 100, Normal = 85, Unit = "%" } },
                { SensorType.BeltTension.ToString(), new SensorThresholds { Min = 5, Max = 20, Normal = 12.5, Unit = "mm" } },
                { SensorType.FlowRate.ToString(), new SensorThresholds { Min = 30, Max = 120, Normal = 75, Unit = "L/min" } },
                { SensorType.Humidity.ToString(), new SensorThresholds { Min = 20, Max = 80, Normal = 50, Unit = "%" } }
            };
        }

        /// <summary>
        /// Initialise les historiques de senseurs
        /// </summary>
        private void InitializeSensorHistories()
        {
            for (int i = 1; i <= 7; i++)
            {
                _sensorHistories[i] = new SensorHistory { LastValues = new Dictionary<SensorType, double>() };
            }
        }

        /// <summary>
        /// Obtient ou crée un historique pour un poste
        /// </summary>
        private SensorHistory GetOrCreateHistory(int postId)
        {
            if (!_sensorHistories.ContainsKey(postId))
            {
                _sensorHistories[postId] = new SensorHistory { LastValues = new Dictionary<SensorType, double>() };
            }
            return _sensorHistories[postId];
        }

        /// <summary>
        /// Structure pour stocker les seuils de senseur
        /// </summary>
        private class SensorThresholds
        {
            public double Min { get; set; }
            public double Max { get; set; }
            public double Normal { get; set; }
            public string Unit { get; set; } = string.Empty;
        }

        /// <summary>
        /// Historique des valeurs de senseur
        /// </summary>
        private class SensorHistory
        {
            public Dictionary<SensorType, double> LastValues { get; set; } = new();
        }
    }
}
