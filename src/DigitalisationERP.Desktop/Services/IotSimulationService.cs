using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DigitalisationERP.Desktop.Models;

namespace DigitalisationERP.Desktop.Services
{
    /// <summary>
    /// Service de simulation IoT pour tester les modules sans matériel réel
    /// Simule : capteurs, robots AGV/AMR, signaux lumineux
    /// </summary>
    public class IotSimulationService
    {
        private readonly Random _random = new Random();
        private readonly Timer _sensorTimer;
        private readonly Timer _robotTimer;
        private bool _isRunning;

        // Collections observables pour le binding UI
        public ObservableCollection<IotSensorReading> SensorReadings { get; }
        public ObservableCollection<RobotState> Robots { get; }
        public ObservableCollection<IotLogEvent> LogEvents { get; }

        // Événements pour notifier l'UI
        public event EventHandler<IotLogEvent>? LogAdded;
        public event EventHandler<IotSensorReading>? SensorAlert;
        public event EventHandler<IotSensorReading>? SensorReadingUpdated;
        public event EventHandler<RobotState>? RobotStatusChanged;

        public IotSimulationService()
        {
            SensorReadings = new ObservableCollection<IotSensorReading>();
            Robots = new ObservableCollection<RobotState>();
            LogEvents = new ObservableCollection<IotLogEvent>();

            // Timers pour mise à jour automatique
            _sensorTimer = new Timer(UpdateSensors, null, Timeout.Infinite, Timeout.Infinite);
            _robotTimer = new Timer(UpdateRobots, null, Timeout.Infinite, Timeout.Infinite);

            InitializeSimulation();
        }

        /// <summary>
        /// Initialise les capteurs et robots virtuels
        /// </summary>
        private void InitializeSimulation()
        {
            // Capteurs pour chaque poste de production
            for (int i = 1; i <= 5; i++)
            {
                string postCode = $"POST-{i:D2}";

                // Capteur de température
                SensorReadings.Add(new IotSensorReading
                {
                    SensorId = $"TEMP-{i:D2}",
                    PostCode = postCode,
                    Type = SensorType.Temperature,
                    Value = 20 + _random.Next(0, 30),
                    Unit = "°C",
                    State = SensorState.OK,
                    Timestamp = DateTime.Now
                });

                // Capteur de vibration
                SensorReadings.Add(new IotSensorReading
                {
                    SensorId = $"VIB-{i:D2}",
                    PostCode = postCode,
                    Type = SensorType.Vibration,
                    Value = _random.Next(0, 50),
                    Unit = "Hz",
                    State = SensorState.OK,
                    Timestamp = DateTime.Now
                });
            }

            // Robot d'approvisionnement
            Robots.Add(new RobotState
            {
                RobotId = "SUPPLY-BOT-01",
                Type = RobotType.SupplyChain,
                Status = RobotStatus.IDLE,
                CurrentLocation = "WAREHOUSE",
                TargetLocation = "",
                BatteryLevel = 95,
                CurrentTask = "En attente de commande",
                LastUpdate = DateTime.Now
            });

            // Robot de maintenance
            Robots.Add(new RobotState
            {
                RobotId = "MAINT-BOT-01",
                Type = RobotType.Maintenance,
                Status = RobotStatus.IDLE,
                CurrentLocation = "MAINTENANCE-ZONE",
                TargetLocation = "",
                BatteryLevel = 82,
                CurrentTask = "En attente d'alerte",
                LastUpdate = DateTime.Now
            });

            AddLog("SYSTEM", "INFO", "Simulation IoT initialisée avec succès");
        }

        /// <summary>
        /// Démarre la simulation (mise à jour automatique)
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;

            _isRunning = true;
            _sensorTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(3)); // Mise à jour capteurs toutes les 3s
            _robotTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(1));  // Mise à jour robots toutes les 1s

            AddLog("SYSTEM", "INFO", "Simulation démarrée");
        }

        /// <summary>
        /// Arrête la simulation
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _sensorTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _robotTimer.Change(Timeout.Infinite, Timeout.Infinite);

            AddLog("SYSTEM", "INFO", "Simulation arrêtée");
        }

        /// <summary>
        /// Mise à jour automatique des valeurs des capteurs
        /// </summary>
        private void UpdateSensors(object? state)
        {
            if (!_isRunning) return;

            var dispatcher = App.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted) return;

            foreach (var sensor in SensorReadings)
            {
                // Variation aléatoire de la valeur
                double variation = (_random.NextDouble() - 0.5) * 5;
                var newValue = Math.Max(0, sensor.Value + variation);
                var newTimestamp = DateTime.Now;

                // Déterminer l'état selon le type
                var newState = sensor.Type switch
                {
                    SensorType.Temperature => newValue > 80 ? SensorState.CRITICAL :
                                             newValue > 60 ? SensorState.WARNING : SensorState.OK,
                    SensorType.Vibration => newValue > 70 ? SensorState.CRITICAL :
                                           newValue > 50 ? SensorState.WARNING : SensorState.OK,
                    _ => SensorState.OK
                };

                dispatcher.Invoke(() =>
                {
                    sensor.Value = newValue;
                    sensor.Timestamp = newTimestamp;
                    sensor.State = newState;

                    // Continuous sensor stream
                    SensorReadingUpdated?.Invoke(this, sensor);

                    // Alerte si état critique
                    if (sensor.State == SensorState.CRITICAL)
                    {
                        AddLog(sensor.SensorId, "ERROR", $"⚠️ Valeur critique détectée: {sensor.Value:F1}{sensor.Unit}");
                        SensorAlert?.Invoke(this, sensor);
                    }
                });
            }
        }

        /// <summary>
        /// Mise à jour automatique des robots (déplacement, batterie)
        /// </summary>
        private void UpdateRobots(object? state)
        {
            if (!_isRunning) return;

            var dispatcher = App.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted) return;

            foreach (var robot in Robots)
            {
                dispatcher.Invoke(() =>
                {
                    // Décharge lente de la batterie
                    if (robot.Status != RobotStatus.CHARGING && robot.BatteryLevel > 0)
                    {
                        robot.BatteryLevel = Math.Max(0, robot.BatteryLevel - (robot.Status == RobotStatus.MOVING ? 2 : 1));
                    }

                    // Alerte batterie faible
                    if (robot.BatteryLevel < 20 && robot.Status != RobotStatus.CHARGING)
                    {
                        AddLog(robot.RobotId, "WARNING", $"🔋 Batterie faible: {robot.BatteryLevel}%");
                    }

                    robot.LastUpdate = DateTime.Now;
                });
            }
        }

        /// <summary>
        /// Commande : Robot d'approvisionnement vers un poste
        /// </summary>
        public async Task SendSupplyRobotToPost(string postCode, string materialType)
        {
            var robot = Robots.FirstOrDefault(r => r.Type == RobotType.SupplyChain);
            if (robot == null) return;

            AddLog(robot.RobotId, "INFO", $"📦 Commande reçue: Livrer {materialType} au {postCode}");

            // Phase 1: Déplacement vers le stock
            robot.Status = RobotStatus.MOVING;
            robot.TargetLocation = "WAREHOUSE";
            robot.CurrentTask = $"Déplacement vers l'entrepôt";
            RobotStatusChanged?.Invoke(this, robot);
            await Task.Delay(2000); // Simulation 2 secondes

            // Phase 2: Chargement matériel
            robot.Status = RobotStatus.WORKING;
            robot.CurrentLocation = "WAREHOUSE";
            robot.CurrentTask = $"Chargement de {materialType}";
            AddLog(robot.RobotId, "INFO", $"⚙️ Chargement en cours...");
            await Task.Delay(3000); // Simulation 3 secondes

            // Phase 3: Déplacement vers le poste
            robot.Status = RobotStatus.MOVING;
            robot.TargetLocation = postCode;
            robot.CurrentTask = $"Livraison vers {postCode}";
            AddLog(robot.RobotId, "INFO", $"🚚 En route vers {postCode}");
            await Task.Delay(4000); // Simulation 4 secondes

            // Phase 4: Livraison
            robot.Status = RobotStatus.WORKING;
            robot.CurrentLocation = postCode;
            robot.CurrentTask = $"Livraison en cours";
            await Task.Delay(2000);

            // Phase 5: Retour en attente
            robot.Status = RobotStatus.IDLE;
            robot.CurrentLocation = postCode;
            robot.TargetLocation = "";
            robot.CurrentTask = "Mission terminée";
            AddLog(robot.RobotId, "INFO", $"✅ Livraison terminée au {postCode}");
            RobotStatusChanged?.Invoke(this, robot);
        }

        /// <summary>
        /// Commande : Robot de maintenance vers un poste en panne
        /// </summary>
        public async Task SendMaintenanceRobotToPost(string postCode, string issue)
        {
            var robot = Robots.FirstOrDefault(r => r.Type == RobotType.Maintenance);
            if (robot == null) return;

            AddLog(robot.RobotId, "WARNING", $"🔧 Alerte reçue: {issue} au {postCode}");

            // Phase 1: Préparation outillage
            robot.Status = RobotStatus.WORKING;
            robot.CurrentTask = $"Préparation outillage pour {issue}";
            RobotStatusChanged?.Invoke(this, robot);
            await Task.Delay(2000);

            // Phase 2: Déplacement vers le poste
            robot.Status = RobotStatus.MOVING;
            robot.TargetLocation = postCode;
            robot.CurrentTask = $"Déplacement vers {postCode}";
            AddLog(robot.RobotId, "INFO", $"🚀 En route vers {postCode}");
            await Task.Delay(5000); // Simulation 5 secondes

            // Phase 3: Arrivée sur site
            robot.Status = RobotStatus.WORKING;
            robot.CurrentLocation = postCode;
            robot.CurrentTask = $"Assistance maintenance prête";
            AddLog(robot.RobotId, "INFO", $"🛠️ Robot arrivé sur {postCode}. Outillage disponible.");
            RobotStatusChanged?.Invoke(this, robot);
            await Task.Delay(3000);

            // Phase 4: Retour en attente
            robot.Status = RobotStatus.IDLE;
            robot.TargetLocation = "";
            robot.CurrentTask = "En attente d'alerte";
            AddLog(robot.RobotId, "INFO", $"✅ Assistance terminée");
            RobotStatusChanged?.Invoke(this, robot);
        }

        /// <summary>
        /// Ajoute un événement au log console
        /// </summary>
        private void AddLog(string source, string level, string message)
        {
            var logEvent = new IotLogEvent
            {
                Timestamp = DateTime.Now,
                Source = source,
                Level = level,
                Message = message
            };

            // Ajouter en début de liste (dernier log en haut)
            App.Current?.Dispatcher.Invoke(() =>
            {
                LogEvents.Insert(0, logEvent);

                // Limiter à 100 logs
                while (LogEvents.Count > 100)
                {
                    LogEvents.RemoveAt(LogEvents.Count - 1);
                }
            });

            LogAdded?.Invoke(this, logEvent);
        }

        /// <summary>
        /// Simule une panne sur un poste (pour test)
        /// </summary>
        public void SimulatePostFailure(string postCode)
        {
            var sensor = SensorReadings.FirstOrDefault(s => s.PostCode == postCode && s.Type == SensorType.Temperature);
            if (sensor != null)
            {
                sensor.Value = 95; // Force température critique
                sensor.State = SensorState.CRITICAL;
                AddLog(sensor.SensorId, "ERROR", $"🚨 PANNE SIMULÉE au {postCode}");
            }
        }

        public void Dispose()
        {
            Stop();
            _sensorTimer?.Dispose();
            _robotTimer?.Dispose();
        }
    }
}
