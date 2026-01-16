namespace DigitalisationERP.Core.Entities.Robotics;

/// <summary>
/// Robot Task/Command for material feeding and handling
/// Queue of tasks for robot execution
/// </summary>
public class RobotTask : BaseEntity
{
    /// <summary>
    /// Task ID
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Robot Equipment Number
    /// </summary>
    public string RobotId { get; set; } = string.Empty;

    /// <summary>
    /// Task Type
    /// </summary>
    public RobotTaskType TaskType { get; set; }

    /// <summary>
    /// Material Number to handle
    /// </summary>
    public string? MaterialNumber { get; set; }

    /// <summary>
    /// Quantity to move/feed
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Source location (pickup point)
    /// </summary>
    public string SourceLocation { get; set; } = string.Empty;

    /// <summary>
    /// Destination location (drop-off point)
    /// </summary>
    public string DestinationLocation { get; set; } = string.Empty;

    /// <summary>
    /// Production Order this task is related to
    /// </summary>
    public string? ProductionOrderNumber { get; set; }

    /// <summary>
    /// Task priority (1=highest)
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Scheduled execution time
    /// </summary>
    public DateTime ScheduledTime { get; set; }

    /// <summary>
    /// Actual start time
    /// </summary>
    public DateTime? ActualStartTime { get; set; }

    /// <summary>
    /// Actual completion time
    /// </summary>
    public DateTime? ActualCompletionTime { get; set; }

    /// <summary>
    /// Task status
    /// </summary>
    public RobotTaskStatus Status { get; set; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of retry attempts
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Robot response/acknowledgment
    /// </summary>
    public string? RobotResponse { get; set; }
}

public enum RobotTaskType
{
    FeedRawMaterial = 1,
    PickAndPlace = 2,
    MaterialTransfer = 3,
    QualityInspection = 4,
    Packaging = 5,
    Return = 6
}

public enum RobotTaskStatus
{
    Queued = 1,
    Scheduled = 2,
    SentToRobot = 3,
    Acknowledged = 4,
    InProgress = 5,
    Completed = 6,
    Failed = 7,
    Cancelled = 8,
    Retrying = 9
}
