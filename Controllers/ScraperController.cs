using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TradeScout.API.Data;
using TradeScout.API.DTOs;
using TradeScout.API.Models;
using TradeScout.API.Services;

namespace TradeScout.API.Controllers;

/// <summary>
/// Scraper controller for Google Maps data collection
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize] // JWT ile korumalı
public class ScraperController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IGoogleMapsScraperService _scraperService;
    private readonly IExcelExportService _excelService;
    private readonly ILogger<ScraperController> _logger;

    public ScraperController(
        ApplicationDbContext context,
        IGoogleMapsScraperService scraperService,
        IExcelExportService excelService,
        ILogger<ScraperController> logger)
    {
        _context = context;
        _scraperService = scraperService;
        _excelService = excelService;
        _logger = logger;
    }

    /// <summary>
    /// Start scraping businesses from Google Maps
    /// </summary>
    [HttpPost("scrape")]
    [ProducesResponseType(typeof(ScrapeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
    public async Task<ActionResult<ScrapeResponseDto>> ScrapeBusinesses([FromBody] ScrapeRequestDto request)
    {
        try
        {
            // Validate request - either SearchQuery or (Category + City) is required
            if (!request.IsValid())
            {
                return BadRequest(new 
                { 
                    message = "Lütfen 'searchQuery' parametresi veya 'category' + 'city' parametrelerini gönderin",
                    example1 = new { searchQuery = "restaurants in Istanbul", maxResults = 20 },
                    example2 = new { category = "mobilya", city = "Gaziantep", country = "Türkiye", maxResults = 20 }
                });
            }

            // Kullanıcı ID'sini JWT token'dan al
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Geçersiz kullanıcı token'ı" });
            }

            // Kullanıcıyı bul
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return Unauthorized(new { message = "Kullanıcı bulunamadı" });
            }

            // Kredi kontrolü (her işletme 1 kredi)
            var requiredCredits = request.MaxResults;
            if (user.Credits < requiredCredits)
            {
                return StatusCode(402, new 
                { 
                    message = $"Yetersiz kredi. Gerekli: {requiredCredits}, Mevcut: {user.Credits}",
                    requiredCredits,
                    availableCredits = user.Credits
                });
            }

            // Get final search query
            var searchQuery = request.GetSearchQuery();
            var category = request.Category ?? "business";
            var city = request.City ?? "";

            _logger.LogInformation("🚀 Scraping isteği alındı: UserId={UserId}, SearchQuery={SearchQuery}", 
                userId, searchQuery);

            // Scraping job'ı oluştur
            var job = new ScrapingJob
            {
                UserId = userId,
                Category = category,
                City = city,
                Country = request.Country ?? "Turkey",
                Language = request.Language,
                Status = "InProgress",
                StartedAt = DateTime.UtcNow
            };

            _context.ScrapingJobs.Add(job);
            await _context.SaveChangesAsync();

            try
            {
                // Google Maps'ten veri çek - Use the complete search query
                var businesses = await _scraperService.ScrapeBusinessesAsync(
                    searchQuery,  // Complete search query (e.g., "restaurants in Istanbul" or "mobilya Gaziantep Turkey")
                    city,
                    request.Country,
                    request.Language,
                    request.MaxResults,
                    HttpContext.RequestAborted
                );

                _logger.LogInformation("✅ Scraping tamamlandı: {Count} işletme bulundu", businesses.Count);

                // Veritabanına kaydet
                foreach (var businessDto in businesses)
                {
                    var business = new Business
                    {
                        UserId = userId,
                        ScrapingJobId = job.Id, // ✅ Job ID'yi kaydet
                        BusinessName = businessDto.BusinessName,
                        Address = businessDto.Address,
                        Phone = businessDto.Phone,
                        Website = businessDto.Website,
                        Rating = businessDto.Rating,
                        ReviewCount = businessDto.ReviewCount,
                        WorkingHours = businessDto.WorkingHours,
                        Category = businessDto.Category,
                        City = businessDto.City,
                        Country = businessDto.Country,
                        Language = request.Language,
                        GoogleMapsUrl = businessDto.GoogleMapsUrl
                    };

                    _context.Businesses.Add(business);
                }

                // Kullanıcı kredisini düş
                var actualCreditsUsed = businesses.Count;
                user.Credits -= actualCreditsUsed;

                // Job'ı güncelle
                job.Status = "Completed";
                job.TotalResults = businesses.Count;
                job.CreditsUsed = actualCreditsUsed;
                job.CompletedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("💾 Veriler kaydedildi. Kullanılan kredi: {Credits}", actualCreditsUsed);

                // Response oluştur
                var response = new ScrapeResponseDto
                {
                    JobId = job.Id,
                    Status = "Completed",
                    Message = $"Başarıyla {businesses.Count} işletme bulundu ve kaydedildi.",
                    TotalResults = businesses.Count,
                    CreditsUsed = actualCreditsUsed,
                    Businesses = businesses,
                    DownloadUrl = $"/api/scraper/download/{job.Id}"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Scraping hatası");

                // Job'ı hata olarak işaretle
                job.Status = "Failed";
                job.ErrorMessage = ex.Message;
                job.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return StatusCode(500, new { message = "Veri çekme işlemi başarısız oldu", error = ex.Message });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Beklenmeyen hata");
            return StatusCode(500, new { message = "Bir hata oluştu", error = ex.Message });
        }
    }

    /// <summary>
    /// Download scraped data as Excel
    /// </summary>
    [HttpGet("download/{jobId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DownloadExcel(int jobId)
    {
        try
        {
            // Kullanıcı ID'sini JWT token'dan al
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Geçersiz kullanıcı token'ı" });
            }

            // Job'ı bul
            var job = await _context.ScrapingJobs
                .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId);

            if (job == null)
            {
                return NotFound(new { message = "Scraping job bulunamadı" });
            }

            // İlgili işletmeleri al - Job ID bazlı daha güvenilir
            var businesses = await _context.Businesses
                .Where(b => b.UserId == userId && b.ScrapingJobId == jobId)
                .OrderBy(b => b.Id)
                .ToListAsync();

            if (!businesses.Any())
            {
                return NotFound(new { message = "Veri bulunamadı" });
            }

            // DTO'ya dönüştür
            var businessDtos = businesses.Select(b => new BusinessDto
            {
                BusinessName = b.BusinessName,
                Address = b.Address,
                Phone = b.Phone,
                Website = b.Website,
                Rating = b.Rating,
                ReviewCount = b.ReviewCount,
                WorkingHours = b.WorkingHours,
                Category = b.Category,
                City = b.City,
                Country = b.Country,
                GoogleMapsUrl = b.GoogleMapsUrl
            }).ToList();

            // Excel oluştur
            var excelBytes = _excelService.ExportToExcel(businessDtos, job.Category, job.City);

            var fileName = $"TradeScout_{job.Category}_{job.City}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            return File(excelBytes, 
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Excel indirme hatası");
            return StatusCode(500, new { message = "Excel dosyası oluşturulamadı", error = ex.Message });
        }
    }

    /// <summary>
    /// Get scraping job history for the authenticated user
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ScrapingJob>>> GetHistory()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Geçersiz kullanıcı token'ı" });
            }

            var jobs = await _context.ScrapingJobs
                .Where(j => j.UserId == userId)
                .OrderByDescending(j => j.CreatedAt)
                .Take(50)
                .ToListAsync();

            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Geçmiş getirme hatası");
            return StatusCode(500, new { message = "Geçmiş alınamadı", error = ex.Message });
        }
    }

    /// <summary>
    /// Get user's current credit balance
    /// </summary>
    [HttpGet("credits")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> GetCredits()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Geçersiz kullanıcı token'ı" });
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "Kullanıcı bulunamadı" });
            }

            return Ok(new 
            { 
                credits = user.Credits,
                packageType = user.PackageType,
                fullName = user.FullName,
                email = user.Email
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Kredi getirme hatası");
            return StatusCode(500, new { message = "Kredi bilgisi alınamadı", error = ex.Message });
        }
    }
}
