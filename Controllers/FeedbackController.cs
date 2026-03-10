using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TradeScout.API.Data;
using TradeScout.API.DTOs;
using TradeScout.API.Models;
using TradeScout.API.Services;

namespace TradeScout.API.Controllers;

/// <summary>
/// Feedback controller for handling user feedback and complaints
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class FeedbackController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<FeedbackController> _logger;

    public FeedbackController(
        ApplicationDbContext context,
        IEmailService emailService,
        ILogger<FeedbackController> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Submit feedback or complaint
    /// </summary>
    /// <param name="feedbackDto">Feedback data</param>
    /// <returns>Feedback response with ID</returns>
    [HttpPost]
    [ProducesResponseType(typeof(FeedbackResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FeedbackResponseDto>> SubmitFeedback([FromBody] FeedbackDto feedbackDto)
    {
        try
        {
            // Check if model is valid
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Create feedback entity
            var feedback = new Feedback
            {
                FullName = feedbackDto.FullName,
                Email = feedbackDto.Email.ToLower(),
                Phone = feedbackDto.Phone,
                Subject = feedbackDto.Subject,
                Message = feedbackDto.Message,
                FeedbackType = feedbackDto.FeedbackType ?? "other",
                IsRead = false,
                Status = "pending",
                CreatedAt = DateTime.UtcNow
            };

            // Save to database
            _context.Feedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            // Send emails asynchronously (don't wait for response)
            _ = Task.Run(async () =>
            {
                var emailSent = await _emailService.SendFeedbackEmailAsync(
                    feedbackDto.FullName,
                    feedbackDto.Email,
                    feedbackDto.Phone,
                    feedbackDto.Subject,
                    feedbackDto.Message,
                    feedbackDto.FeedbackType
                );

                if (emailSent)
                {
                    _logger.LogInformation("Feedback email başarıyla gönderildi: {FeedbackId}", feedback.Id);
                }
                else
                {
                    _logger.LogWarning("Feedback email gönderilemedi: {FeedbackId}", feedback.Id);
                }
            });

            // Return response
            var response = new FeedbackResponseDto
            {
                Id = feedback.Id,
                FullName = feedback.FullName,
                Email = feedback.Email,
                Subject = feedback.Subject,
                Message = feedback.Message,
                FeedbackType = feedback.FeedbackType,
                IsRead = feedback.IsRead,
                Status = feedback.Status,
                CreatedAt = feedback.CreatedAt
            };

            _logger.LogInformation("Yeni feedback kaydedildi: {FeedbackId} - {Email}", feedback.Id, feedback.Email);

            return CreatedAtAction(nameof(GetFeedbackById), new { id = feedback.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Feedback gönderme hatası: {ErrorMessage}", ex.Message);
            return StatusCode(500, new { 
                message = "Feedback gönderimi sırasında bir hata oluştu.",
                error = ex.Message,
                details = ex.InnerException?.Message
            });
        }
    }

    /// <summary>
    /// Get feedback by ID (Admin only)
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(FeedbackResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeedbackResponseDto>> GetFeedbackById(int id)
    {
        try
        {
            var feedback = await _context.Feedbacks.FindAsync(id);

            if (feedback == null)
            {
                return NotFound(new { message = "Feedback bulunamadı." });
            }

            // Mark as read
            if (!feedback.IsRead)
            {
                feedback.IsRead = true;
                await _context.SaveChangesAsync();
            }

            var response = MapToResponse(feedback);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Feedback alma hatası: {FeedbackId}", id);
            return StatusCode(500, new { message = "Feedback alınamadı." });
        }
    }

    /// <summary>
    /// Get all feedbacks with pagination (Admin only)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<FeedbackResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<FeedbackResponseDto>>> GetAllFeedbacks(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        [FromQuery] string? feedbackType = null)
    {
        try
        {
            var query = _context.Feedbacks.AsQueryable();

            // Filter by status
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(f => f.Status == status);
            }

            // Filter by feedback type
            if (!string.IsNullOrEmpty(feedbackType))
            {
                query = query.Where(f => f.FeedbackType == feedbackType);
            }

            // Order by created date descending
            var totalCount = await query.CountAsync();
            var feedbacks = await query
                .OrderByDescending(f => f.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var responses = feedbacks.Select(MapToResponse).ToList();

            return Ok(new
            {
                data = responses,
                pagination = new
                {
                    page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Feedback listesi alma hatası");
            return StatusCode(500, new { message = "Feedback listesi alınamadı." });
        }
    }

    /// <summary>
    /// Reply to feedback (Admin only)
    /// </summary>
    [HttpPost("{id}/reply")]
    [ProducesResponseType(typeof(FeedbackResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeedbackResponseDto>> ReplyToFeedback(
        int id,
        [FromBody] ReplyDto replyDto)
    {
        try
        {
            var feedback = await _context.Feedbacks.FindAsync(id);

            if (feedback == null)
            {
                return NotFound(new { message = "Feedback bulunamadı." });
            }

            // Update feedback
            feedback.Reply = replyDto.Reply;
            feedback.Status = replyDto.Status ?? "resolved";
            feedback.RepliedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Send reply email to user
            var replyEmailBody = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; }}
                        .container {{ max-width: 600px; margin: 20px auto; background-color: white; padding: 20px; border-radius: 8px; }}
                        .header {{ background-color: #2c3e50; color: white; padding: 15px; border-radius: 5px; }}
                        .content {{ margin: 20px 0; }}
                        .footer {{ margin-top: 20px; padding-top: 15px; border-top: 1px solid #ddd; color: #888; font-size: 12px; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h2>Feedback'inize Cevap Verildi</h2>
                        </div>
                        <div class='content'>
                            <p>Merhaba {feedback.FullName},</p>
                            <p><strong>Başlık:</strong> {feedback.Subject}</p>
                            <p><strong>Cevap:</strong></p>
                            <p>{replyDto.Reply.Replace("\n", "<br>")}</p>
                        </div>
                        <div class='footer'>
                            <p>FGSTrade Destek Ekibi</p>
                        </div>
                    </div>
                </body>
                </html>";

            _ = Task.Run(async () =>
            {
                await _emailService.SendEmailAsync(feedback.Email, "Feedback'inize Cevap Verildi - FGSTrade", replyEmailBody);
            });

            var response = MapToResponse(feedback);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Feedback cevap verme hatası: {FeedbackId}", id);
            return StatusCode(500, new { message = "Cevap verilemedi." });
        }
    }

    /// <summary>
    /// Delete feedback (Admin only)
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFeedback(int id)
    {
        try
        {
            var feedback = await _context.Feedbacks.FindAsync(id);

            if (feedback == null)
            {
                return NotFound(new { message = "Feedback bulunamadı." });
            }

            _context.Feedbacks.Remove(feedback);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Feedback silindi: {FeedbackId}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Feedback silme hatası: {FeedbackId}", id);
            return StatusCode(500, new { message = "Feedback silinemedi." });
        }
    }

    /// <summary>
    /// Test SMTP email configuration
    /// </summary>
    /// <param name="email">Email address to send test email</param>
    /// <returns>Test result</returns>
    [HttpGet("test-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> TestEmail([FromQuery] string email = "info@fgstrade.com")
    {
        try
        {
            _logger.LogInformation("📧 SMTP test email gönderiliyor: {Email}", email);

            var testHtml = $@"
                <html>
                <body style='font-family: Arial, sans-serif; padding: 20px;'>
                    <h2 style='color: #2563eb;'>🎉 FGS Trade - SMTP Test Başarılı!</h2>
                    <p>Bu email SMTP ayarlarınızın doğru çalıştığını göstermektedir.</p>
                    <hr style='border: 1px solid #e5e7eb;' />
                    <p><strong>Test Zamanı:</strong> {DateTime.Now:dd/MM/yyyy HH:mm:ss}</p>
                    <p><strong>Sunucu:</strong> mail.kurumsaleposta.com:465</p>
                    <p style='color: #16a34a;'>✅ Email servisi aktif ve çalışıyor!</p>
                </body>
                </html>";

            var result = await _emailService.SendEmailAsync(
                email,
                "🧪 FGS Trade - SMTP Test Email",
                testHtml
            );

            if (result)
            {
                _logger.LogInformation("✅ Test email başarıyla gönderildi: {Email}", email);
                return Ok(new { 
                    success = true, 
                    message = $"Test email başarıyla gönderildi: {email}",
                    timestamp = DateTime.Now
                });
            }
            else
            {
                _logger.LogWarning("❌ Test email gönderilemedi: {Email}", email);
                return StatusCode(500, new { 
                    success = false, 
                    message = "Email gönderilemedi. SMTP ayarlarını kontrol edin.",
                    timestamp = DateTime.Now
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ SMTP test hatası: {Error}", ex.Message);
            return StatusCode(500, new { 
                success = false, 
                message = "SMTP test hatası",
                error = ex.Message,
                innerError = ex.InnerException?.Message,
                timestamp = DateTime.Now
            });
        }
    }

    private FeedbackResponseDto MapToResponse(Feedback feedback)
    {
        return new FeedbackResponseDto
        {
            Id = feedback.Id,
            FullName = feedback.FullName,
            Email = feedback.Email,
            Subject = feedback.Subject,
            Message = feedback.Message,
            FeedbackType = feedback.FeedbackType,
            IsRead = feedback.IsRead,
            Status = feedback.Status,
            CreatedAt = feedback.CreatedAt,
            RepliedAt = feedback.RepliedAt,
            Reply = feedback.Reply
        };
    }
}

/// <summary>
/// DTO for feedback reply
/// </summary>
public class ReplyDto
{
    [Required(ErrorMessage = "Cevap zorunludur")]
    [MaxLength(2000, ErrorMessage = "Cevap en fazla 2000 karakter olabilir")]
    public string Reply { get; set; } = string.Empty;

    [MaxLength(50, ErrorMessage = "Status en fazla 50 karakter olabilir")]
    public string? Status { get; set; }
}