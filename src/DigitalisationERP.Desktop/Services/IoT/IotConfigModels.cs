using System.Collections.Generic;

namespace DigitalisationERP.Desktop.Services.IoT
{
    /// <summary>
    /// Configuration racine pour le système IoT
    /// </summary>
    public class IotConfiguration
    {
        public string Provider { get; set; } = "simulation";
        public MqttConfiguration Mqtt { get; set; } = new();
        public BluetoothConfiguration Bluetooth { get; set; } = new();
        public List<SensorConfiguration> Sensors { get; set; } = new();
        public List<RobotConfiguration> Robots { get; set; } = new();
        public LoggingConfiguration Logging { get; set; } = new();
        public SimulationConfiguration Simulation { get; set; } = new();
    }

    /// <summary>
    /// Configuration for Bluetooth devices exposed as a serial (COM) port on Windows.
    /// This is commonly how HC-05/HC-06 or similar modules appear once paired.
    /// </summary>
    public class BluetoothConfiguration
    {
        public bool Enabled { get; set; } = false;
        public string ComPort { get; set; } = "COM3";
        public int BaudRate { get; set; } = 9600;

        /// <summary>
        /// When enabled, the production simulator will wait for real Bluetooth sensor triggers
        /// (post 1 and last post) before allowing pieces to progress.
        /// Default is false to avoid blocking the UI/flow when Bluetooth is only used for outbound messaging.
        /// </summary>
        public bool GateProductionOnSensorTriggers { get; set; } = false;

        /// <summary>
        /// Max time to wait for a required sensor trigger before falling back to simulation.
        /// Set to 0 to wait indefinitely.
        /// </summary>
        public int ProductionGateTimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// Payload sent when an OF starts.
        /// Some microcontroller firmwares use strict string comparisons, so spacing can matter.
        /// </summary>
        public string OfStartPayload { get; set; } = "{\"cmd\" : \"start\"}";

        /// <summary>
        /// Payload sent when an OF ends (quantity completed).
        /// </summary>
        public string OfEndPayload { get; set; } = "{\"cmd\":\"end\"}";

        /// <summary>
        /// New line appended after each message.
        /// Most microcontroller receivers parse line-delimited commands.
        /// </summary>
        public string NewLine { get; set; } = "\n";

        /// <summary>
        /// Optional default robot id to target.
        /// </summary>
        public string RobotId { get; set; } = "AGV-001";
    }

    /// <summary>
    /// Configuration du broker MQTT
    /// </summary>
    public class MqttConfiguration
    {
        public string BrokerAddress { get; set; } = "localhost";
        public int BrokerPort { get; set; } = 1883;
        public string ClientId { get; set; } = "DigitalisationERP_Desktop";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }

    /// <summary>
    /// Configuration d'un capteur
    /// </summary>
    public class SensorConfiguration
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public SensorThresholds Thresholds { get; set; } = new();
        public string MqttTopic { get; set; } = string.Empty;
    }

    /// <summary>
    /// Seuils d'alerte pour un capteur
    /// </summary>
    public class SensorThresholds
    {
        public double Warning { get; set; }
        public double Critical { get; set; }
    }

    /// <summary>
    /// Configuration d'un robot
    /// </summary>
    public class RobotConfiguration
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string BaseLocation { get; set; } = string.Empty;
        public List<string> AllowedLocations { get; set; } = new();
        public string MqttCommandTopic { get; set; } = string.Empty;
        public string MqttStateTopic { get; set; } = string.Empty;
    }

    /// <summary>
    /// Configuration du système de logs
    /// </summary>
    public class LoggingConfiguration
    {
        public bool EnableFileLogging { get; set; } = true;
        public string LogDirectory { get; set; } = "logs";
        public string LogLevel { get; set; } = "INFO";
        public int ArchiveAfterDays { get; set; } = 30;
    }

    /// <summary>
    /// Configuration de la simulation
    /// </summary>
    public class SimulationConfiguration
    {
        public bool EnableRandomAlerts { get; set; } = true;
        public int SensorUpdateIntervalMs { get; set; } = 3000;
        public double AlertProbability { get; set; } = 0.15;
    }
}
