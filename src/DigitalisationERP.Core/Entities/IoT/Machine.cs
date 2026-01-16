namespace DigitalisationERP.Core.Entities.IoT;

/// <summary>
/// Machine/Equipment Master (Similar to SAP PM Equipment Master)
/// Represents physical machines, sensors, and robots in the facility
/// </summary>
public class Machine : BaseEntity
{
    /// <summary>
    /// Equipment Number (EQUNR in SAP)
    /// </summary>
    public string EquipmentNumber { get; set; } = string.Empty;

    /// <summary>
    /// Machine Name/Description
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Machine Type
    /// </summary>
    public MachineType MachineType { get; set; }

    /// <summary>
    /// Work Center (production line)
    /// </summary>
    public string WorkCenter { get; set; } = string.Empty;

    /// <summary>
    /// Manufacturer
    /// </summary>
    public string? Manufacturer { get; set; }

    /// <summary>
    /// Model Number
    /// </summary>
    public string? ModelNumber { get; set; }

    /// <summary>
    /// Serial Number
    /// </summary>
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Installation Date
    /// </summary>
    public DateTime? InstallationDate { get; set; }

    /// <summary>
    /// Current operational status
    /// </summary>
    public MachineStatus Status { get; set; }

    /// <summary>
    /// Is IoT-enabled (has sensors)?
    /// </summary>
    public bool IoTEnabled { get; set; }

    /// <summary>
    /// MQTT Topic for sensor data
    /// </summary>
    public string? MqttTopic { get; set; }

    /// <summary>
    /// Last maintenance date
    /// </summary>
    public DateTime? LastMaintenanceDate { get; set; }

    /// <summary>
    /// Next scheduled maintenance date
    /// </summary>
    public DateTime? NextMaintenanceDate { get; set; }

    /// <summary>
    /// Total operating hours
    /// </summary>
    public decimal TotalOperatingHours { get; set; }

    /// <summary>
    /// Robot-specific: Can this robot feed raw materials?
    /// </summary>
    public bool CanFeedRawMaterials { get; set; }
}

public enum MachineType
{
    ProductionMachine = 1,
    Robot = 2,
    ConveyorBelt = 3,
    QualityControlStation = 4,
    PackagingMachine = 5,
    Sensor = 6,
    Other = 99
}

public enum MachineStatus
{
    Operational = 1,
    Idle = 2,
    InMaintenance = 3,
    Breakdown = 4,
    Offline = 5
}
