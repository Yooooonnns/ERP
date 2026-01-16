namespace DigitalisationERP.Core.Entities.IoT;

/// <summary>
/// IoT Sensor Data (Time-series data from manufacturing sensors)
/// Should be stored in TimescaleDB for optimal performance
/// </summary>
public class SensorReading : BaseEntity
{
    /// <summary>
    /// Production Post ID (Foreign Key)
    /// </summary>
    public int ProductionPostId { get; set; }

    /// <summary>
    /// Equipment/Machine Number
    /// </summary>
    public string EquipmentNumber { get; set; } = string.Empty;

    /// <summary>
    /// Sensor Type/Name
    /// </summary>
    public string SensorName { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of the reading
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Sensor value
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// Unit of measurement (°C, RPM, Bar, %, etc.)
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Sensor type category
    /// </summary>
    public SensorType SensorType { get; set; }

    /// <summary>
    /// Is this reading within normal range?
    /// </summary>
    public bool IsNormal { get; set; }

    /// <summary>
    /// Threshold minimum value
    /// </summary>
    public double? ThresholdMin { get; set; }

    /// <summary>
    /// Threshold maximum value
    /// </summary>
    public double? ThresholdMax { get; set; }

    /// <summary>
    /// Alert level if out of range
    /// </summary>
    public AlertLevel? AlertLevel { get; set; }

    /// <summary>
    /// Reference to production order if reading was during production
    /// </summary>
    public string? ProductionOrderNumber { get; set; }

    /// <summary>
    /// Status description (e.g., "Normal", "High Temperature", "Vibration Spike")
    /// </summary>
    public string Status { get; set; } = "Normal";
}

/// <summary>
/// Sensor types used in production maintenance monitoring
/// </summary>
public enum SensorType
{
    /// <summary>Motor temperature monitoring (°C)</summary>
    MotorTemperature = 1,

    /// <summary>Bearing temperature monitoring (°C)</summary>
    BearingTemperature = 2,

    /// <summary>System pressure monitoring (Bar/PSI)</summary>
    Pressure = 3,

    /// <summary>Vibration analysis (mm/s)</summary>
    Vibration = 4,

    /// <summary>Motor speed/RPM (RPM)</summary>
    MotorSpeed = 5,

    /// <summary>Power consumption (kW)</summary>
    PowerConsumption = 6,

    /// <summary>Oil level indicator (%)</summary>
    OilLevel = 7,

    /// <summary>Coolant level indicator (%)</summary>
    CoolantLevel = 8,

    /// <summary>Belt tension (N or %)</summary>
    BeltTension = 9,

    /// <summary>Output flow rate (L/min)</summary>
    FlowRate = 10,

    /// <summary>Humidity level (%) - for control environments</summary>
    Humidity = 11,

    /// <summary>Other custom sensor types</summary>
    Other = 99
}

/// <summary>
/// Alert severity levels triggered by sensor thresholds
/// </summary>
public enum AlertLevel
{
    /// <summary>Informational - no action required</summary>
    Info = 1,

    /// <summary>Warning - maintenance attention needed within 48h</summary>
    Warning = 2,

    /// <summary>Critical - maintenance required within 24h</summary>
    Critical = 3,

    /// <summary>Emergency - immediate action required</summary>
    Emergency = 4
}
