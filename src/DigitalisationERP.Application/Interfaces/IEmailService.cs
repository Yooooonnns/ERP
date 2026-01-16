using DigitalisationERP.Application.DTOs.Email;

namespace DigitalisationERP.Application.Interfaces;

public interface IEmailService
{
    // Basic email sending
    Task<long> SendEmailAsync(SendEmailRequest request);
    Task<long> SendTemplateEmailAsync(SendTemplateEmailRequest request);
    
    // Authentication emails
    Task<long> SendVerificationEmailAsync(SendVerificationEmailRequest request);
    Task<long> SendPasswordResetEmailAsync(SendPasswordResetEmailRequest request);
    Task<long> SendPasswordChangedNotificationAsync(string email, string username);
    Task<long> SendWelcomeEmailAsync(string email, string username, string fullName);
    
    // Account approval/rejection emails
    Task SendApprovalEmailAsync(SendApprovalEmailRequest request);
    Task SendRejectionEmailAsync(SendRejectionEmailRequest request);
    
    // Business emails
    Task<long> SendProductionReportAsync(SendProductionReportRequest request);
    Task<long> SendStockAlertAsync(SendStockAlertRequest request);
    Task<long> SendMaintenanceAlertAsync(SendMaintenanceAlertRequest request);
    
    // Email queue management
    Task<List<EmailQueueDto>> GetPendingEmailsAsync();
    Task<EmailQueueDto?> GetEmailStatusAsync(long emailId);
    Task<bool> ProcessEmailQueueAsync();
    Task<bool> CancelEmailAsync(long emailId);
    Task<bool> RetryEmailAsync(long emailId);
    
    // Email verification
    Task<string> GenerateEmailVerificationTokenAsync(long userId, string email);
    Task<bool> VerifyEmailTokenAsync(string token);
    
    // Email templates
    string GetEmailTemplate(string templateName, Dictionary<string, object> data);
}
