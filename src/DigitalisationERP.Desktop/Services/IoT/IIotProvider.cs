using System;
using System.Threading.Tasks;
using DigitalisationERP.Desktop.Models;

namespace DigitalisationERP.Desktop.Services.IoT
{
    /// <summary>
    /// Interface abstraite pour les fournisseurs IoT (Simulation ou Matériel réel)
    /// Permet le plug-and-play entre simulation et capteurs physiques
    /// </summary>
    public interface IIotProvider
    {
        /// <summary>
        /// Nom du fournisseur (ex: "Simulation", "MQTT", "OPC UA")
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Indique si le provider est connecté
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Événement déclenché lors de la réception d'une lecture de capteur
        /// </summary>
        event EventHandler<SensorReadingEventArgs>? SensorReadingReceived;

        /// <summary>
        /// Événement déclenché lors d'un changement d'état de robot
        /// </summary>
        event EventHandler<RobotStateEventArgs>? RobotStateChanged;

        /// <summary>
        /// Événement déclenché pour les logs système
        /// </summary>
        event EventHandler<IotLogEventArgs>? LogEventAdded;

        /// <summary>
        /// Événement déclenché en cas d'alerte critique
        /// </summary>
        event EventHandler<AlertEventArgs>? CriticalAlertRaised;

        /// <summary>
        /// Se connecte au système IoT (simulation ou matériel)
        /// </summary>
        Task<bool> ConnectAsync();

        /// <summary>
        /// Se déconnecte du système IoT
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Lit les données d'un capteur spécifique
        /// </summary>
        Task<IotSensorReading?> ReadSensorAsync(string sensorId);

        /// <summary>
        /// Envoie une commande à un robot
        /// </summary>
        Task<bool> SendRobotCommandAsync(string robotId, RobotCommand command, string? targetLocation = null);

        /// <summary>
        /// Obtient l'état actuel d'un robot
        /// </summary>
        Task<RobotState?> GetRobotStateAsync(string robotId);

        /// <summary>
        /// Obtient tous les capteurs disponibles
        /// </summary>
        Task<IotSensorReading[]> GetAllSensorsAsync();

        /// <summary>
        /// Obtient tous les robots disponibles
        /// </summary>
        Task<RobotState[]> GetAllRobotsAsync();

        /// <summary>
        /// Configure un seuil d'alerte pour un capteur
        /// </summary>
        Task<bool> SetSensorThresholdAsync(string sensorId, double warningThreshold, double criticalThreshold);
    }

    /// <summary>
    /// Commandes disponibles pour les robots
    /// </summary>
    public enum RobotCommand
    {
        Stop,
        GoToLocation,
        ReturnToBase,
        StartTask,
        Pause
    }

    /// <summary>
    /// Arguments pour l'événement de lecture de capteur
    /// </summary>
    public class SensorReadingEventArgs : EventArgs
    {
        public IotSensorReading Reading { get; set; } = null!;
    }

    /// <summary>
    /// Arguments pour l'événement d'état de robot
    /// </summary>
    public class RobotStateEventArgs : EventArgs
    {
        public RobotState State { get; set; } = null!;
    }

    /// <summary>
    /// Arguments pour l'événement de log IoT
    /// </summary>
    public class IotLogEventArgs : EventArgs
    {
        public IotLogEvent LogEvent { get; set; } = null!;
    }

    /// <summary>
    /// Arguments pour l'événement d'alerte critique
    /// </summary>
    public class AlertEventArgs : EventArgs
    {
        public string SensorId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public double Value { get; set; }
        public double Threshold { get; set; }
    }
}
