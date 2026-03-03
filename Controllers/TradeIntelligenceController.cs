using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public TradeIntelligenceController(
        ITradeIntelligenceService tradeIntelligenceService,
        IPdfExportService pdfExportService,
        ILogger<TradeIntelligenceController> logger)
    {
        _tradeIntelligenceService = tradeIntelligenceService;
        _pdfExportService = pdfExportService;
        _logger = logger;
    }

    /// <summary>
    /// Ticari istihbarat raporu oluştur
    /// </summary>
    /// <param name="request">Rapor parametreleri</param>
    /// <returns>Detaylı ticari istihbarat raporu</returns>
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
    /// Ticari istihbarat raporunu PDF olarak indir
    /// </summary>
    /// <param name="request">Rapor parametreleri</param>
    /// <returns>PDF dosyası</returns>
    [HttpPost("download-pdf")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DownloadPdf([FromBody] TradeIntelligenceRequestDto request)
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

            _logger.LogInformation("📄 PDF Report request: HS={HsCode}, Product={Product}, Target={Target}",
                request.HsCode, request.ProductName, request.TargetCountry);

            // Önce raporu oluştur
            var report = await _tradeIntelligenceService.GenerateReportAsync(request);

            if (!report.Success || string.IsNullOrEmpty(report.ReportContent))
            {
                return BadRequest(new { message = report.ErrorMessage ?? "Rapor oluşturulamadı." });
            }

            // PDF oluştur
            var pdfBytes = _pdfExportService.GenerateAnalysisPdf(
                report.ReportContent, 
                request.ProductName, 
                request.TargetCountry);

            // Dosya adı
            var safeProductName = SanitizeFileName(request.ProductName);
            var safeCountryName = SanitizeFileName(request.TargetCountry);
            var fileName = $"Pazar_Analizi_{safeProductName}_{safeCountryName}_{DateTime.Now:yyyyMMdd}.pdf";

            _logger.LogInformation("✅ PDF generated: {FileName}, Size: {Size} bytes", fileName, pdfBytes.Length);

            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF generation error");
            return StatusCode(500, new { message = "PDF oluşturulurken bir hata oluştu." });
        }
    }

    /// <summary>
    /// Mevcut rapor içeriğinden PDF oluştur (rapor zaten oluşturulmuşsa)
    /// </summary>
    [HttpPost("convert-to-pdf")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult ConvertToPdf([FromBody] ConvertToPdfRequest request)
    {
        try
        {
            if (request == null)
            {
                _logger.LogWarning("❌ Request body is null");
                return BadRequest(new { message = "Request body boş olamaz." });
            }

            // ReportContent veya MarkdownContent'i al
            var content = request.GetContent();
            
            _logger.LogInformation("📄 ConvertToPdf request received: ProductName={ProductName}, TargetCountry={TargetCountry}, ContentLength={ContentLength}",
                request.ProductName ?? "null",
                request.TargetCountry ?? "null",
                content.Length);

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("❌ ReportContent/MarkdownContent is empty");
                return BadRequest(new { message = "Rapor içeriği gereklidir. (ReportContent veya MarkdownContent)" });
            }

            var pdfBytes = _pdfExportService.GenerateAnalysisPdf(
                content,
                request.ProductName ?? "Pazar Analizi",
                request.TargetCountry ?? "");

            var fileName = $"Pazar_Analizi_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF conversion error");
            return StatusCode(500, new { message = "PDF oluşturulurken bir hata oluştu." });
        }
    }

    private string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.Replace(" ", "_").Take(50).Aggregate("", (s, c) => s + c);
    }

    /// <summary>
    /// Örnek HS Kodları listesi
    /// </summary>
    [HttpGet("sample-hs-codes")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<HsCodeSample>), StatusCodes.Status200OK)]
    public ActionResult<List<HsCodeSample>> GetSampleHsCodes()
    {
        var samples = new List<HsCodeSample>
        {
            new() { Code = "87116000", Description = "Elektrikli Bisiklet", Category = "Taşıt Araçları" },
            new() { Code = "84713000", Description = "Dizüstü Bilgisayar", Category = "Elektronik" },
            new() { Code = "62034200", Description = "Erkek Pamuklu Pantolon", Category = "Tekstil" },
            new() { Code = "94036000", Description = "Ahşap Mobilya", Category = "Mobilya" },
            new() { Code = "08051000", Description = "Portakal", Category = "Tarım Ürünleri" },
            new() { Code = "72083900", Description = "Sıcak Haddelenmiş Çelik", Category = "Metal" },
            new() { Code = "39201000", Description = "Polietilen Film", Category = "Plastik" },
            new() { Code = "85176200", Description = "Telekomünikasyon Cihazları", Category = "Elektronik" },
            new() { Code = "69089000", Description = "Seramik Karo", Category = "İnşaat Malzemesi" },
            new() { Code = "84818019", Description = "Vanalar ve Valfler", Category = "Makine" },
            new() { Code = "27101990", Description = "Petrol Ürünleri", Category = "Enerji" },
            new() { Code = "30049099", Description = "İlaç Ürünleri", Category = "İlaç" },
            new() { Code = "73269090", Description = "Demir/Çelik Yapılar", Category = "Metal" },
            new() { Code = "40111000", Description = "Otomobil Lastikleri", Category = "Kauçuk" },
            new() { Code = "61091000", Description = "Pamuklu Tişört", Category = "Tekstil" }
        };

        return Ok(samples);
    }

    /// <summary>
    /// Popüler hedef ülkeler listesi
    /// </summary>
    [HttpGet("popular-countries")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<CountryInfo>), StatusCodes.Status200OK)]
    public ActionResult<List<CountryInfo>> GetPopularCountries()
    {
        var countries = new List<CountryInfo>
        {
            // Avrupa
            new() { Name = "Almanya", Code = "DE", Region = "Avrupa", TurkeyFTA = true },
            new() { Name = "İngiltere", Code = "GB", Region = "Avrupa", TurkeyFTA = true },
            new() { Name = "Fransa", Code = "FR", Region = "Avrupa", TurkeyFTA = true },
            new() { Name = "İtalya", Code = "IT", Region = "Avrupa", TurkeyFTA = true },
            new() { Name = "Hollanda", Code = "NL", Region = "Avrupa", TurkeyFTA = true },
            new() { Name = "İspanya", Code = "ES", Region = "Avrupa", TurkeyFTA = true },
            new() { Name = "Polonya", Code = "PL", Region = "Avrupa", TurkeyFTA = true },
            new() { Name = "Bulgaristan", Code = "BG", Region = "Avrupa", TurkeyFTA = true },
            new() { Name = "Romanya", Code = "RO", Region = "Avrupa", TurkeyFTA = true },
            
            // Ortadoğu
            new() { Name = "Suudi Arabistan", Code = "SA", Region = "Ortadoğu", TurkeyFTA = false },
            new() { Name = "Birleşik Arap Emirlikleri", Code = "AE", Region = "Ortadoğu", TurkeyFTA = false },
            new() { Name = "Irak", Code = "IQ", Region = "Ortadoğu", TurkeyFTA = false },
            new() { Name = "İsrail", Code = "IL", Region = "Ortadoğu", TurkeyFTA = true },
            
            // Kuzey Afrika
            new() { Name = "Mısır", Code = "EG", Region = "Afrika", TurkeyFTA = true },
            new() { Name = "Libya", Code = "LY", Region = "Afrika", TurkeyFTA = false },
            new() { Name = "Fas", Code = "MA", Region = "Afrika", TurkeyFTA = true },
            
            // Amerika
            new() { Name = "Amerika Birleşik Devletleri", Code = "US", Region = "Amerika", TurkeyFTA = false },
            new() { Name = "Brezilya", Code = "BR", Region = "Amerika", TurkeyFTA = false },
            
            // Asya
            new() { Name = "Çin", Code = "CN", Region = "Asya", TurkeyFTA = false },
            new() { Name = "Japonya", Code = "JP", Region = "Asya", TurkeyFTA = false },
            new() { Name = "Güney Kore", Code = "KR", Region = "Asya", TurkeyFTA = true },
            new() { Name = "Hindistan", Code = "IN", Region = "Asya", TurkeyFTA = false },
            
            // BDT
            new() { Name = "Rusya", Code = "RU", Region = "BDT", TurkeyFTA = false },
            new() { Name = "Ukrayna", Code = "UA", Region = "BDT", TurkeyFTA = true },
            new() { Name = "Azerbaycan", Code = "AZ", Region = "BDT", TurkeyFTA = true },
            new() { Name = "Kazakistan", Code = "KZ", Region = "BDT", TurkeyFTA = false }
        };

        return Ok(countries);
    }

    /// <summary>
    /// Kullanıcının pazar analizi geçmişini getir
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(List<MarketAnalysisHistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MarketAnalysisHistoryDto>>> GetAnalysisHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "Kullanıcı kimliği doğrulanamadı." });
            }

            var analyses = await _tradeIntelligenceService.GetUserAnalysisHistoryAsync(userId.Value, page, pageSize);
            
            var result = analyses.Select(a => new MarketAnalysisHistoryDto
            {
                Id = a.Id,
                HsCode = a.HsCode,
                ProductName = a.ProductName,
                TargetCountry = a.TargetCountry,
                OriginCountry = a.OriginCountry,
                IsSuccessful = a.IsSuccessful,
                PdfDownloaded = a.PdfDownloaded,
                CreatedAt = a.CreatedAt,
                ViewCount = a.ViewCount,
                IsFavorite = a.IsFavorite,
                Notes = a.Notes
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting analysis history");
            return StatusCode(500, new { message = "Geçmiş yüklenirken bir hata oluştu." });
        }
    }

    /// <summary>
    /// Belirli bir analizi getir
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(MarketAnalysisDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MarketAnalysisDetailDto>> GetAnalysisById(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var analysis = await _tradeIntelligenceService.GetAnalysisByIdAsync(id, userId);
            
            if (analysis == null)
            {
                return NotFound(new { message = "Analiz bulunamadı." });
            }

            var result = new MarketAnalysisDetailDto
            {
                Id = analysis.Id,
                HsCode = analysis.HsCode,
                ProductName = analysis.ProductName,
                TargetCountry = analysis.TargetCountry,
                OriginCountry = analysis.OriginCountry,
                ReportContent = analysis.ReportContent,
                IsSuccessful = analysis.IsSuccessful,
                PdfDownloaded = analysis.PdfDownloaded,
                CreatedAt = analysis.CreatedAt,
                ViewCount = analysis.ViewCount,
                IsFavorite = analysis.IsFavorite,
                Notes = analysis.Notes
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting analysis by id: {Id}", id);
            return StatusCode(500, new { message = "Analiz yüklenirken bir hata oluştu." });
        }
    }

    /// <summary>
    /// Analizi favorilere ekle/çıkar
    /// </summary>
    [HttpPost("{id}/toggle-favorite")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleFavorite(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "Kullanıcı kimliği doğrulanamadı." });
            }

            var isFavorite = await _tradeIntelligenceService.ToggleFavoriteAsync(id, userId.Value);
            return Ok(new { isFavorite });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling favorite for id: {Id}", id);
            return StatusCode(500, new { message = "Favori güncellenirken bir hata oluştu." });
        }
    }

    /// <summary>
    /// Analize not ekle
    /// </summary>
    [HttpPost("{id}/add-note")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddNote(int id, [FromBody] AddNoteRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "Kullanıcı kimliği doğrulanamadı." });
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
            _logger.LogError(ex, "Error adding note for id: {Id}", id);
            return StatusCode(500, new { message = "Not eklenirken bir hata oluştu." });
        }
    }

    #region Helper Methods

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("userId")?.Value 
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        if (int.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return null;
    }

    private string? GetClientIpAddress()
    {
        // Check for forwarded IP (behind proxy/load balancer)
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',').FirstOrDefault()?.Trim();
        }

        // Direct connection IP
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    #endregion
}

// DTOs for history endpoints
public class MarketAnalysisHistoryDto
{
    public int Id { get; set; }
    public string HsCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string TargetCountry { get; set; } = string.Empty;
    public string OriginCountry { get; set; } = string.Empty;
    public bool IsSuccessful { get; set; }
    public bool PdfDownloaded { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ViewCount { get; set; }
    public bool IsFavorite { get; set; }
    public string? Notes { get; set; }
}

public class MarketAnalysisDetailDto : MarketAnalysisHistoryDto
{
    public string? ReportContent { get; set; }
}

public class AddNoteRequest
{
    public string Note { get; set; } = string.Empty;
}

public class HsCodeSample
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class CountryInfo
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public bool TurkeyFTA { get; set; } // Turkey Free Trade Agreement
}
