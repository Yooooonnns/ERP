using System;
using System.ComponentModel;

namespace DigitalisationERP.Desktop.Models
{
    /// <summary>
    /// Énumération des types de capteurs IoT
    /// </summary>
    public enum SensorType
    {
        Temperature,
        Vibration,
        Pressure,
        Humidity,
        Speed,
        Current,
        Power
    }

    /// <summary>
    /// État d'un capteur IoT
    /// </summary>
    public enum SensorState
    {
        OK,          // Valeur normale
        WARNING,     // Valeur en zone d'alerte
        CRITICAL,    // Valeur critique
        OFFLINE      // Capteur hors ligne
    }

    /// <summary>
    /// Lecture d'un capteur IoT (Digital Twin)
    /// </summary>
    public class IotSensorReading : INotifyPropertyChanged
    {
        private string _sensorId = "";
        private string _postCode = "";
        private SensorType _type;
        private double _value;
        private SensorState _state;
        private DateTime _timestamp;
        private string _unit = "";
        private bool _isAcknowledged;

        public string SensorId
        {
            get => _sensorId;
            set { _sensorId = value; OnPropertyChanged(nameof(SensorId)); }
        }

        public string PostCode
        {
            get => _postCode;
            set { _postCode = value; OnPropertyChanged(nameof(PostCode)); }
        }

        public SensorType Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(nameof(Type)); }
        }

        public double Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(nameof(Value)); }
        }

        public SensorState State
        {
            get => _state;
            set { _state = value; OnPropertyChanged(nameof(State)); }
        }

        public DateTime Timestamp
        {
            get => _timestamp;
            set { _timestamp = value; OnPropertyChanged(nameof(Timestamp)); }
        }

        public string Unit
        {
            get => _unit;
            set { _unit = value; OnPropertyChanged(nameof(Unit)); }
        }

        /// <summary>
        /// Indique si l'alerte critique a été acquittée par un opérateur
        /// </summary>
        public bool IsAcknowledged
        {
            get => _isAcknowledged;
            set { _isAcknowledged = value; OnPropertyChanged(nameof(IsAcknowledged)); OnPropertyChanged(nameof(DisplayText)); }
        }

        public string DisplayText => $"[{PostCode}] {Type}: {Value:F1}{Unit} - {State}{(State == SensorState.CRITICAL && !IsAcknowledged ? " ⚠️ NON ACQUITTÉ" : "")}";

        public string StateColor => State switch
        {
            SensorState.OK => "#10b981",
            SensorState.WARNING => "#f59e0b",
            SensorState.CRITICAL => "#ef4444",
            SensorState.OFFLINE => "#6b7280",
            _ => "#6b7280"
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Type de robot
    /// </summary>
    public enum RobotType
    {
        SupplyChain,    // Robot d'approvisionnement matières premières
        Maintenance     // Robot assistance maintenance
    }

    /// <summary>
    /// État d'un robot
    /// </summary>
    public enum RobotStatus
    {
        IDLE,           // En attente
        MOVING,         // En déplacement
        WORKING,        // En cours d'action
        CHARGING,       // En charge
        ERROR           // Erreur
    }

    /// <summary>
    /// État d'un robot AGV/AMR (Digital Twin)
    /// </summary>
    public class RobotState : INotifyPropertyChanged
    {
        private string _robotId = "";
        private RobotType _type;
        private RobotStatus _status;
        private string _currentLocation = "";
        private string _targetLocation = "";
        private int _batteryLevel;
        private DateTime _lastUpdate;
        private string _currentTask = "";

        public string RobotId
        {
            get => _robotId;
            set { _robotId = value; OnPropertyChanged(nameof(RobotId)); }
        }

        public RobotType Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(nameof(Type)); }
        }

        public RobotStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        public string CurrentLocation
        {
            get => _currentLocation;
            set { _currentLocation = value; OnPropertyChanged(nameof(CurrentLocation)); }
        }

        public string TargetLocation
        {
            get => _targetLocation;
            set { _targetLocation = value; OnPropertyChanged(nameof(TargetLocation)); }
        }

        public int BatteryLevel
        {
            get => _batteryLevel;
            set { _batteryLevel = value; OnPropertyChanged(nameof(BatteryLevel)); }
        }

        public DateTime LastUpdate
        {
            get => _lastUpdate;
            set { _lastUpdate = value; OnPropertyChanged(nameof(LastUpdate)); }
        }

        public string CurrentTask
        {
            get => _currentTask;
            set { _currentTask = value; OnPropertyChanged(nameof(CurrentTask)); }
        }

        public string DisplayText => $"[{RobotId}] {Type} - {Status} ({BatteryLevel}%)";

        public string StatusColor => Status switch
        {
            RobotStatus.IDLE => "#10b981",
            RobotStatus.MOVING => "#f59e0b",
            RobotStatus.WORKING => "#3b82f6",
            RobotStatus.CHARGING => "#8b5cf6",
            RobotStatus.ERROR => "#ef4444",
            _ => "#6b7280"
        };

        public string StatusIcon => Status switch
        {
            RobotStatus.IDLE => "fas fa-stop-circle",
            RobotStatus.MOVING => "fas fa-truck-moving",
            RobotStatus.WORKING => "fas fa-cog fa-spin",
            RobotStatus.CHARGING => "fas fa-battery-half",
            RobotStatus.ERROR => "fas fa-exclamation-triangle",
            _ => "fas fa-robot"
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Événement IoT loggé dans la console
    /// </summary>
    public class IotLogEvent
    {
        public DateTime Timestamp { get; set; }
        public string Source { get; set; } = "";
        public string Level { get; set; } = "INFO"; // INFO, WARNING, ERROR
        public string Message { get; set; } = "";

        public string DisplayText => $"[{Timestamp:HH:mm:ss}] [{Level}] {Source}: {Message}";

        public string LevelColor => Level switch
        {
            "INFO" => "#10b981",
            "WARNING" => "#f59e0b",
            "ERROR" => "#ef4444",
            _ => "#6b7280"
        };
    }
}
