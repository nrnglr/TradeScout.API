using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradeScout.API.Data;
using TradeScout.API.DTOs;
using TradeScout.API.Services;

namespace TradeScout.API.Controllers;

/// <summary>
/// Trade Intelligence Controller - Ticari İstihbarat Raporları
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class TradeIntelligenceController : ControllerBase
{
    private readonly ITradeIntelligenceService _tradeIntelligenceService;
    private readonly IPdfExportService _pdfExportService;
    private readonly ILogger<TradeIntelligenceController> _logger;
    private readonly ApplicationDbContext _context;

    public TradeIntelligenceController(
        ITradeIntelligenceService tradeIntelligenceService,
        IPdfExportService pdfExportService,
        ILogger<TradeIntelligenceController> logger,
        ApplicationDbContext context)
    {
        _tradeIntelligenceService = tradeIntelligenceService;
        _pdfExportService = pdfExportService;
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Ticari istihbarat raporu oluştur
    /// </summary>
    [HttpPost("generate-report")]
    [ProducesResponseType(typeof(TradeIntelligenceReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TradeIntelligenceReportDto>> GenerateReport([FromBody] TradeIntelligenceRequestDto request)
    {
        try
        {
            // Validation
            if (string.IsNullOrWhiteSpace(request.HsCode))
            {
                return BadRequest(new { message = "GTIP/HS Code gereklidir." });
            }

            if (string.IsNullOrWhiteSpace(request.ProductName))
            {
                return BadRequest(new { message = "Ürün ismi gereklidir." });
            }

            if (string.IsNullOrWhiteSpace(request.TargetCountry))
            {
                return BadRequest(new { message = "Hedef ülke gereklidir." });
            }

            // Default origin country
            if (string.IsNullOrWhiteSpace(request.OriginCountry))
            {
                request.OriginCountry = "Türkiye";
            }

            // Kullanıcı bilgilerini al
            var userId = GetCurrentUserId();
            var ipAddress = GetClientIpAddress();
            var userAgent = Request.Headers["User-Agent"].ToString();

            // Kredi kontrolü (her pazar analizi 5 kredi) - Admin kullanıcılar hariç
            var requiredCredits = 5;
            if (userId.HasValue)
            {
                var user = await _context.Users.FindAsync(userId.Value);
                if (user == null)
                    return Unauthorized(new { message = "Kullanıcı bulunamadı." });

                // Admin kontrolü - Admin kullanıcılar kredi harcamaz
                if (user.Role == "Admin")
                {
                    _logger.LogInformation("👑 Admin kullanıcı - kredi kontrolü bypass edildi | UserId={UserId}", userId);
                }
                // Free Trial kontrolü - İlk 30 gün ücretsiz
                else if (_tradeIntelligenceService.IsInFreeTrial(userId.Value))
                {
                    _logger.LogInformation("🎁 Free Trial kullanıcı - kredi kontrolü bypass edildi | UserId={UserId}", userId);
                }
                // Normal kullanıcılar için kredi kontrolü
                else
                {
                    if (user.Credits < requiredCredits)
                    {
                        return StatusCode(402, new
                        {
                            message = $"Yetersiz kredi. Pazar analizi için {requiredCredits} kredi gereklidir. Mevcut: {user.Credits}",
                            requiredCredits,
                            availableCredits = user.Credits
                        });
                    }

                    // Krediyi düş
                    user.Credits -= requiredCredits;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("💳 Pazar analizi kredisi düşüldü | UserId={UserId} | Kullanılan={Used} | Kalan={Left}",
                        userId, requiredCredits, user.Credits);
                }
            }

            _logger.LogInformation("📊 Trade Intelligence Report request: HS={HsCode}, Product={Product}, Target={Target}, Origin={Origin}, UserId={UserId}",
                request.HsCode, request.ProductName, request.TargetCountry, request.OriginCountry, userId);

            var report = await _tradeIntelligenceService.GenerateReportAsync(request, userId, ipAddress, userAgent);

            if (!report.Success)
            {
                return BadRequest(new { message = report.ErrorMessage });
            }

            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Trade Intelligence Report error");
            return StatusCode(500, new { message = "Rapor oluşturulurken bir hata oluştu." });
        }
    }

    /// <summary>
    /// Kullanıcının analiz geçmişini getir
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAnalysisHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { message = "Kullanıcı girişi gereklidir." });
            }

            var analyses = await _tradeIntelligenceService.GetUserAnalysisHistoryAsync(userId.Value, page, pageSize);
            return Ok(analyses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analiz geçmişi getirme hatası");
            return StatusCode(500, new { message = "Geçmiş yüklenirken bir hata oluştu." });
        }
    }

    /// <summary>
    /// Belirli bir analizi getir
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAnalysisById(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var analysis = await _tradeIntelligenceService.GetAnalysisByIdAsync(id, userId);

            if (analysis == null)
            {
                return NotFound(new { message = "Analiz bulunamadı." });
            }

            return Ok(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analiz getirme hatası: {Id}", id);
            return StatusCode(500, new { message = "Analiz yüklenirken bir hata oluştu." });
        }
    }

    /// <summary>
    /// Analizi favorilere ekle/çıkar
    /// </summary>
    [HttpPost("{id}/favorite")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleFavorite(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { message = "Kullanıcı girişi gereklidir." });
            }

            var isFavorite = await _tradeIntelligenceService.ToggleFavoriteAsync(id, userId.Value);
            return Ok(new { isFavorite });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Favori toggle hatası: {Id}", id);
            return StatusCode(500, new { message = "İşlem sırasında bir hata oluştu." });
        }
    }

    /// <summary>
    /// Analize not ekle
    /// </summary>
    [HttpPost("{id}/note")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddNote(int id, [FromBody] AddNoteRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { message = "Kullanıcı girişi gereklidir." });
            }

            var success = await _tradeIntelligenceService.AddNoteAsync(id, userId.Value, request.Note);
            if (!success)
            {
                return NotFound(new { message = "Analiz bulunamadı." });
            }

            return Ok(new { message = "Not eklendi." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Not ekleme hatası: {Id}", id);
            return StatusCode(500, new { message = "İşlem sırasında bir hata oluştu." });
        }
    }

    /// <summary>
    /// Markdown içeriğini PDF'e dönüştür
    /// </summary>
    [HttpPost("convert-to-pdf")]
    [AllowAnonymous] // PDF dönüştürme için auth gerekli değil
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult ConvertToPdf([FromBody] ConvertToPdfRequest request)
    {
        try
        {
            var content = request.GetContent();
            
            if (string.IsNullOrWhiteSpace(content))
            {
                return BadRequest(new { message = "İçerik gereklidir. ReportContent veya MarkdownContent alanlarından biri dolu olmalıdır." });
            }

            _logger.LogInformation("📄 PDF dönüştürme: {Product} - {Country}", 
                request.ProductName, request.TargetCountry);

            var pdfBytes = _pdfExportService.GenerateAnalysisPdf(
                content,
                request.ProductName ?? "Ürün",
                request.TargetCountry ?? "Ülke"
            );

            var fileName = $"Pazar_Analizi_{SanitizeFileName(request.ProductName)}_{SanitizeFileName(request.TargetCountry)}_{DateTime.Now:yyyyMMdd}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF dönüştürme hatası");
            return StatusCode(500, new { message = "PDF oluşturulurken bir hata oluştu." });
        }
    }

    /// <summary>
    /// Dosya adı için güvenli string oluştur
    /// </summary>
    private static string SanitizeFileName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return "Bilinmeyen";
        
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }

    // Helper methods
    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (int.TryParse(userIdClaim, out int userId))
        {
            return userId;
        }
        return null;
    }

    private string GetClientIpAddress()
    {
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

/// <summary>
/// Not ekleme request DTO
/// </summary>
public class AddNoteRequest
{
    public string Note { get; set; } = string.Empty;
}

/// <summary>
/// ChartData dahil PDF dönüştürme için genişletilmiş request
/// (ConvertToPdfRequest'i override eder — ChartData alanı eklendi)
/// </summary>
public class ConvertToPdfRequest
{
    public string? ReportContent { get; set; }
    public string? MarkdownContent { get; set; }
    public string? HsCode { get; set; }
    public string? ProductName { get; set; }
    public string? TargetCountry { get; set; }
    public string? OriginCountry { get; set; }
    public string? GetContent() =>
        ReportContent ?? MarkdownContent;
}