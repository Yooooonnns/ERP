namespace DigitalisationERP.Domain.Identity.Enums;

/// <summary>
/// User types following SAP standards:
/// - S: System/Service user for automated processes
/// - B: Business user for interactive daily operations
/// - P: Partner user for external supplier/partner access
/// - C: Customer user for customer portal access
/// </summary>
public enum UserTypeEnum
{
    /// <summary>System or Service user - automated processes, APIs, scheduled jobs</summary>
    S = 1,

    /// <summary>Business user - interactive system access by employees</summary>
    B = 2,

    /// <summary>Partner user - supplier, partner, or third-party access</summary>
    P = 3,

    /// <summary>Customer user - customer portal or self-service access</summary>
    C = 4
}
