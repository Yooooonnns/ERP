using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DigitalisationERP.Desktop.Models;

namespace DigitalisationERP.Desktop.Services.IoT
{
    /// <summary>
    /// Implémentation de simulation pour les tests sans matériel réel
    /// Réutilise la logique existante de IotSimulationService
    /// </summary>
    public class SimulationIotProvider : IIotProvider
    {
        private readonly IotSimulationService _simulationService;
        private bool _isConnected;

        public string ProviderName => "Digital Twin Simulation";
        public bool IsConnected => _isConnected;

        public event EventHandler<SensorReadingEventArgs>? SensorReadingReceived;
        public event EventHandler<RobotStateEventArgs>? RobotStateChanged;
        public event EventHandler<IotLogEventArgs>? LogEventAdded;
        public event EventHandler<AlertEventArgs>? CriticalAlertRaised;

        public SimulationIotProvider(IotSimulationService simulationService)
        {
            _simulationService = simulationService ?? throw new ArgumentNullException(nameof(simulationService));

            // Abonnement aux événements de la simulation
            _simulationService.SensorAlert += OnSimulationSensorAlert;
            _simulationService.SensorReadingUpdated += OnSimulationSensorReadingUpdated;
            _simulationService.RobotStatusChanged += OnSimulationRobotStatusChanged;
            _simulationService.LogAdded += OnSimulationLogAdded;
        }

        public Task<bool> ConnectAsync()
        {
            // La simulation est toujours "connectée" car elle tourne en mémoire
            _isConnected = true;
            _simulationService.Start();
            
            LogEventAdded?.Invoke(this, new IotLogEventArgs
            {
                LogEvent = new IotLogEvent
                {
                    Timestamp = DateTime.Now,
                    Level = "INFO",
                    Source = "SimulationProvider",
                    Message = "Simulation IoT démarrée avec succès"
                }
            });

            return Task.FromResult(true);
        }

        public Task DisconnectAsync()
        {
            _isConnected = false;
            _simulationService.Stop();
            
            LogEventAdded?.Invoke(this, new IotLogEventArgs
            {
                LogEvent = new IotLogEvent
                {
                    Timestamp = DateTime.Now,
                    Level = "INFO",
                    Source = "SimulationProvider",
                    Message = "Simulation IoT arrêtée"
                }
            });

            return Task.CompletedTask;
        }

        public Task<IotSensorReading?> ReadSensorAsync(string sensorId)
        {
            var sensor = _simulationService.SensorReadings
                .FirstOrDefault(s => s.SensorId == sensorId);
            
            return Task.FromResult<IotSensorReading?>(sensor);
        }

        public async Task<bool> SendRobotCommandAsync(string robotId, RobotCommand command, string? targetLocation = null)
        {
            var robot = _simulationService.Robots.FirstOrDefault(r => r.RobotId == robotId);
            if (robot == null) return false;

            switch (command)
            {
                case RobotCommand.GoToLocation when targetLocation != null:
                    robot.Status = RobotStatus.MOVING;
                    robot.TargetLocation = targetLocation;
                    robot.CurrentTask = $"En déplacement vers {targetLocation}";
                    RobotStateChanged?.Invoke(this, new RobotStateEventArgs { State = robot });
                    
                    // Simuler l'arrivée après 5 secondes
                    await Task.Delay(5000);
                    robot.Status = RobotStatus.IDLE;
                    robot.CurrentLocation = targetLocation;
                    robot.TargetLocation = "";
                    robot.CurrentTask = "Arrivé à destination";
                    RobotStateChanged?.Invoke(this, new RobotStateEventArgs { State = robot });
                    return true;

                case RobotCommand.ReturnToBase:
                    robot.Status = RobotStatus.MOVING;
                    robot.TargetLocation = "BASE";
                    robot.CurrentTask = "Retour à la base";
                    RobotStateChanged?.Invoke(this, new RobotStateEventArgs { State = robot });
                    
                    await Task.Delay(5000);
                    robot.Status = RobotStatus.IDLE;
                    robot.CurrentLocation = "BASE";
                    robot.TargetLocation = "";
                    robot.CurrentTask = "À la base";
                    RobotStateChanged?.Invoke(this, new RobotStateEventArgs { State = robot });
                    return true;

                case RobotCommand.Stop:
                    robot.Status = RobotStatus.IDLE;
                    robot.TargetLocation = "";
                    robot.CurrentTask = "Arrêt d'urgence";
                    RobotStateChanged?.Invoke(this, new RobotStateEventArgs { State = robot });
                    return true;

                default:
                    return false;
            }
        }

        public Task<RobotState?> GetRobotStateAsync(string robotId)
        {
            var robot = _simulationService.Robots
                .FirstOrDefault(r => r.RobotId == robotId);
            
            return Task.FromResult<RobotState?>(robot);
        }

        public Task<IotSensorReading[]> GetAllSensorsAsync()
        {
            return Task.FromResult(_simulationService.SensorReadings.ToArray());
        }

        public Task<RobotState[]> GetAllRobotsAsync()
        {
            return Task.FromResult(_simulationService.Robots.ToArray());
        }

        public Task<bool> SetSensorThresholdAsync(string sensorId, double warningThreshold, double criticalThreshold)
        {
            // Dans la simulation, les seuils sont fixes mais on pourrait les rendre configurables
            LogEventAdded?.Invoke(this, new IotLogEventArgs
            {
                LogEvent = new IotLogEvent
                {
                    Timestamp = DateTime.Now,
                    Level = "INFO",
                    Source = "SimulationProvider",
                    Message = $"Configuration seuils pour {sensorId}: Warning={warningThreshold}, Critical={criticalThreshold}"
                }
            });

            return Task.FromResult(true);
        }

        private void OnSimulationSensorAlert(object? sender, IotSensorReading sensor)
        {
            // Lever l'événement d'alerte critique via le HAL
            CriticalAlertRaised?.Invoke(this, new AlertEventArgs
            {
                SensorId = sensor.SensorId,
                Message = $"{sensor.PostCode}: {sensor.DisplayText}",
                Value = sensor.Value,
                Threshold = sensor.Type == SensorType.Temperature ? 80.0 : 70.0
            });
        }

        private void OnSimulationSensorReadingUpdated(object? sender, IotSensorReading sensor)
        {
            SensorReadingReceived?.Invoke(this, new SensorReadingEventArgs { Reading = sensor });
        }

        private void OnSimulationRobotStatusChanged(object? sender, RobotState robot)
        {
            RobotStateChanged?.Invoke(this, new RobotStateEventArgs { State = robot });
        }

        private void OnSimulationLogAdded(object? sender, IotLogEvent logEvent)
        {
            LogEventAdded?.Invoke(this, new IotLogEventArgs { LogEvent = logEvent });
        }
    }
}
