namespace DigitalisationERP.Core.Abstractions;

/// <summary>
/// Service interface for audit logging.
/// </summary>
public interface IAuditLogService
{
    Task LogAsync(string action, string tableName, string? recordId, string? oldValues, string? newValues, long? userId, string? ipAddress, string? sessionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<dynamic>> GetAuditLogsAsync(string? tableName = null, string? recordId = null, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
}
