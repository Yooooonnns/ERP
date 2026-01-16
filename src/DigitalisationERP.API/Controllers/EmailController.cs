using DigitalisationERP.Application.DTOs.Email;
using DigitalisationERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalisationERP.API.Controllers;

/// <summary>
/// Email operations controller
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmailController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailController> _logger;

    public EmailController(IEmailService emailService, ILogger<EmailController> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Send a custom email
    /// </summary>
    [HttpPost("send")]
    [ProducesResponseType(typeof(long), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendEmail([FromBody] SendEmailRequest request)
    {
        try
        {
            var emailId = await _emailService.SendEmailAsync(request);
            return Ok(new { emailId, message = "Email queued successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email");
            return BadRequest(new { message = "Failed to send email", error = ex.Message });
        }
    }

    /// <summary>
    /// Send email using template
    /// </summary>
    [HttpPost("send-template")]
    [ProducesResponseType(typeof(long), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendTemplateEmail([FromBody] SendTemplateEmailRequest request)
    {
        try
        {
            var emailId = await _emailService.SendTemplateEmailAsync(request);
            return Ok(new { emailId, message = "Email queued successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send template email");
            return BadRequest(new { message = "Failed to send template email", error = ex.Message });
        }
    }

    /// <summary>
    /// Send production report to recipients
    /// </summary>
    [HttpPost("production-report")]
    [ProducesResponseType(typeof(long), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendProductionReport([FromBody] SendProductionReportRequest request)
    {
        try
        {
            var emailId = await _emailService.SendProductionReportAsync(request);
            return Ok(new { emailId, message = $"Production report sent to {request.Recipients.Count} recipients" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send production report");
            return BadRequest(new { message = "Failed to send production report", error = ex.Message });
        }
    }

    /// <summary>
    /// Send stock alert to warehouse team
    /// </summary>
    [HttpPost("stock-alert")]
    [ProducesResponseType(typeof(long), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendStockAlert([FromBody] SendStockAlertRequest request)
    {
        try
        {
            var emailId = await _emailService.SendStockAlertAsync(request);
            return Ok(new { emailId, message = "Stock alert sent successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send stock alert");
            return BadRequest(new { message = "Failed to send stock alert", error = ex.Message });
        }
    }

    /// <summary>
    /// Send maintenance alert to technicians
    /// </summary>
    [HttpPost("maintenance-alert")]
    [ProducesResponseType(typeof(long), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendMaintenanceAlert([FromBody] SendMaintenanceAlertRequest request)
    {
        try
        {
            var emailId = await _emailService.SendMaintenanceAlertAsync(request);
            return Ok(new { emailId, message = "Maintenance alert sent successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send maintenance alert");
            return BadRequest(new { message = "Failed to send maintenance alert", error = ex.Message });
        }
    }

    /// <summary>
    /// Get email status by ID
    /// </summary>
    [HttpGet("{emailId}/status")]
    [ProducesResponseType(typeof(EmailQueueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEmailStatus(long emailId)
    {
        try
        {
            var status = await _emailService.GetEmailStatusAsync(emailId);
            if (status == null)
                return NotFound(new { message = "Email not found" });

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get email status");
            return BadRequest(new { message = "Failed to get email status", error = ex.Message });
        }
    }

    /// <summary>
    /// Get pending emails in queue
    /// </summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(List<EmailQueueDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingEmails()
    {
        try
        {
            var emails = await _emailService.GetPendingEmailsAsync();
            return Ok(new { count = emails.Count, emails });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending emails");
            return BadRequest(new { message = "Failed to get pending emails", error = ex.Message });
        }
    }

    /// <summary>
    /// Process email queue manually
    /// </summary>
    [HttpPost("process-queue")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ProcessEmailQueue()
    {
        try
        {
            var result = await _emailService.ProcessEmailQueueAsync();
            return Ok(new { success = result, message = "Email queue processed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process email queue");
            return BadRequest(new { message = "Failed to process email queue", error = ex.Message });
        }
    }

    /// <summary>
    /// Cancel a queued email
    /// </summary>
    [HttpPost("{emailId}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelEmail(long emailId)
    {
        try
        {
            var result = await _emailService.CancelEmailAsync(emailId);
            if (!result)
                return NotFound(new { message = "Email not found or already sent" });

            return Ok(new { message = "Email cancelled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel email");
            return BadRequest(new { message = "Failed to cancel email", error = ex.Message });
        }
    }

    /// <summary>
    /// Retry a failed email
    /// </summary>
    [HttpPost("{emailId}/retry")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetryEmail(long emailId)
    {
        try
        {
            var result = await _emailService.RetryEmailAsync(emailId);
            if (!result)
                return NotFound(new { message = "Email not found" });

            return Ok(new { message = "Email retry initiated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retry email");
            return BadRequest(new { message = "Failed to retry email", error = ex.Message });
        }
    }

    /// <summary>
    /// Verify email token (public endpoint)
    /// </summary>
    [HttpGet("verify")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token)
    {
        try
        {
            var result = await _emailService.VerifyEmailTokenAsync(token);
            if (!result)
                return BadRequest(new { message = "Invalid or expired verification token" });

            return Ok(new { message = "Email verified successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify email");
            return BadRequest(new { message = "Failed to verify email", error = ex.Message });
        }
    }
}
