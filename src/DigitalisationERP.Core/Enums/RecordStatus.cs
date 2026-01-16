namespace DigitalisationERP.Core.Enums;

/// <summary>
/// Record status following SAP conventions
/// </summary>
public enum RecordStatus
{
    /// <summary>
    /// Active record
    /// </summary>
    Active = 1,

    /// <summary>
    /// Blocked/Inactive record
    /// </summary>
    Blocked = 2,

    /// <summary>
    /// Marked for deletion
    /// </summary>
    MarkedForDeletion = 3,

    /// <summary>
    /// Draft/In Progress
    /// </summary>
    Draft = 4
}
