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
    private readonly IParallelGoogleMapsScraperService _parallelScraperService;
    private readonly IGeminiSearchService _geminiSearchService;
    private readonly IExcelExportService _excelService;
    private readonly ILogger<ScraperController> _logger;

    public ScraperController(
        ApplicationDbContext context,
        IGoogleMapsScraperService scraperService,
        IParallelGoogleMapsScraperService parallelScraperService,
        IGeminiSearchService geminiSearchService,
        IExcelExportService excelService,
        ILogger<ScraperController> logger)
    {
        _context = context;
        _scraperService = scraperService;
        _parallelScraperService = parallelScraperService;
        _geminiSearchService = geminiSearchService;
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
                    // 🔧 FIX: Truncate long values to fit database constraints
                    var business = new Business
                    {
                        UserId = userId,
                        ScrapingJobId = job.Id, // ✅ Job ID'yi kaydet
                        BusinessName = TruncateString(businessDto.BusinessName, 300),
                        Address = TruncateString(businessDto.Address, 500),
                        Phone = TruncateString(businessDto.Phone, 50),
                        Mobile = TruncateString(businessDto.Mobile, 50),
                        Email = TruncateString(businessDto.Email, 255),
                        Website = TruncateString(businessDto.Website, 500),
                        SocialMedia = TruncateString(businessDto.SocialMedia, 500),
                        Comments = TruncateString(businessDto.Comments, 2000),
                        Rating = businessDto.Rating,
                        ReviewCount = businessDto.ReviewCount,
                        WorkingHours = TruncateString(businessDto.WorkingHours, 1000),
                        Category = TruncateString(businessDto.Category, 200),
                        City = TruncateString(businessDto.City, 100),
                        Country = TruncateString(businessDto.Country, 100),
                        Language = request.Language
                    };

                    _logger.LogDebug("📝 Business saved to DB: Name={Name}, Email={Email}, Mobile={Mobile}, SocialMedia={SocialMedia}", 
                        business.BusinessName, business.Email, business.Mobile, business.SocialMedia);

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
                job.ErrorMessage = TruncateString(ex.Message, 1000); // ✅ Truncate error message
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
                Mobile = b.Mobile,
                Email = b.Email,
                Website = b.Website,
                SocialMedia = b.SocialMedia,
                Comments = b.Comments,
                Rating = b.Rating,
                ReviewCount = b.ReviewCount,
                WorkingHours = b.WorkingHours,
                Category = b.Category,
                City = b.City,
                Country = b.Country
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

    /// <summary>
    /// Start PARALLEL scraping with multiple proxies for high-speed results
    /// </summary>
    [HttpPost("scrape-parallel")]
    [ProducesResponseType(typeof(ScrapeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
    public async Task<ActionResult<ScrapeResponseDto>> ScrapeBusinessesParallel([FromBody] ScrapeRequestDto request)
    {
        try
        {
            // Validate request
            if (!request.IsValid())
            {
                return BadRequest(new 
                { 
                    message = "Lütfen 'category' + 'city' parametrelerini gönderin",
                    example = new { category = "mobilya", city = "Istanbul", country = "Turkey", maxResults = 50 }
                });
            }

            // Get user ID from JWT
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Geçersiz kullanıcı token'ı" });
            }

            // Find user
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return Unauthorized(new { message = "Kullanıcı bulunamadı" });
            }

            // Credit check (Admin users bypass this)
            var requiredCredits = request.MaxResults;
            if (user.Role != "Admin" && user.Credits < requiredCredits)
            {
                return StatusCode(402, new 
                { 
                    message = $"Yetersiz kredi. Gerekli: {requiredCredits}, Mevcut: {user.Credits}",
                    requiredCredits,
                    availableCredits = user.Credits
                });
            }

            var category = request.Category ?? "business";
            var city = request.City ?? "";

            // Validate MaxResults for non-admin users
            if (user.Role != "Admin" && request.MaxResults > 10)
            {
                return BadRequest(new
                {
                    message = "Maksimum 10 firma talebinde bulunabilirsiniz. Admin için limit yoktur.",
                    maxAllowed = 10,
                    requested = request.MaxResults
                });
            }

            _logger.LogInformation("🚀 PARALLEL Scraping isteği alındı: UserId={UserId}, Category={Category}, City={City}, MaxResults={MaxResults}", 
                userId, category, city, request.MaxResults);

            // Create scraping job
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
                // PARALLEL scraping with multiple proxies
                var businesses = await _parallelScraperService.ScrapeBusinessesParallelAsync(
                    category,
                    city,
                    request.Country,
                    request.Language,
                    request.MaxResults,
                    HttpContext.RequestAborted
                );

                _logger.LogInformation("✅ PARALLEL Scraping tamamlandı: {Count} işletme bulundu", businesses.Count);

                // Save to database
                foreach (var businessDto in businesses)
                {
                    // 🔧 FIX: Truncate long values to fit database constraints
                    var business = new Business
                    {
                        UserId = userId,
                        ScrapingJobId = job.Id,
                        BusinessName = TruncateString(businessDto.BusinessName, 300),
                        Address = TruncateString(businessDto.Address, 500),
                        Phone = TruncateString(businessDto.Phone, 50),
                        Mobile = TruncateString(businessDto.Mobile, 50),
                        Email = TruncateString(businessDto.Email, 255),
                        Website = TruncateString(businessDto.Website, 500),
                        SocialMedia = TruncateString(businessDto.SocialMedia, 500),
                        Comments = TruncateString(businessDto.Comments, 2000),
                        Rating = businessDto.Rating,
                        ReviewCount = businessDto.ReviewCount,
                        WorkingHours = TruncateString(businessDto.WorkingHours, 1000),
                        Category = TruncateString(businessDto.Category, 200),
                        City = TruncateString(businessDto.City, 100),
                        Country = TruncateString(businessDto.Country, 100),
                        Language = request.Language,
                    };

                    _logger.LogDebug("📝 Business saved to DB: Name={Name}, Email={Email}, Mobile={Mobile}, SocialMedia={SocialMedia}", 
                        business.BusinessName, business.Email, business.Mobile, business.SocialMedia);

                    _context.Businesses.Add(business);
                }

                // Deduct credits (Admin users don't lose credits)
                var actualCreditsUsed = businesses.Count;
                if (user.Role != "Admin")
                {
                    user.Credits -= actualCreditsUsed;
                }

                // Update job
                job.Status = "Completed";
                job.TotalResults = businesses.Count;
                job.CreditsUsed = user.Role == "Admin" ? 0 : actualCreditsUsed;
                job.CompletedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("💾 Veriler kaydedildi. Kullanılan kredi: {Credits}", actualCreditsUsed);

                // Response
                var response = new ScrapeResponseDto
                {
                    JobId = job.Id,
                    Status = "Completed",
                    Message = $"Başarıyla {businesses.Count} işletme bulundu ve kaydedildi. (PARALLEL MODE)",
                    TotalResults = businesses.Count,
                    CreditsUsed = actualCreditsUsed,
                    Businesses = businesses,
                    DownloadUrl = $"/api/scraper/download/{job.Id}"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ PARALLEL Scraping hatası");

                // Mark job as failed
                job.Status = "Failed";
                job.ErrorMessage = TruncateString(ex.Message, 1000); // ✅ Truncate error message
                job.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return StatusCode(500, new { message = "Parallel scraping başarısız oldu", error = ex.Message });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Beklenmeyen hata");
            return StatusCode(500, new { message = "Bir hata oluştu", error = ex.Message });
        }
    }

    /// <summary>
    /// ULTRA-FAST scraping with Gemini AI + Google Search Tool
    /// </summary>
    [HttpPost("scrape-gemini")]
    [ProducesResponseType(typeof(ScrapeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
    public async Task<ActionResult<ScrapeResponseDto>> ScrapeBusinessesWithGemini([FromBody] ScrapeRequestDto request)
    {
        try
        {
            // Validate request
            if (!request.IsValid())
            {
                return BadRequest(new 
                { 
                    message = "Lütfen 'category' + 'city' parametrelerini gönderin",
                    example = new { category = "restaurant", city = "Istanbul", country = "Turkey", maxResults = 50 }
                });
            }

            // Get user ID from JWT
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Geçersiz kullanıcı token'ı" });
            }

            // Find user
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return Unauthorized(new { message = "Kullanıcı bulunamadı" });
            }

            // Credit check (Admin users bypass this)
            var requiredCredits = request.MaxResults;
            if (user.Role != "Admin" && user.Credits < requiredCredits)
            {
                return StatusCode(402, new 
                { 
                    message = $"Yetersiz kredi. Gerekli: {requiredCredits}, Mevcut: {user.Credits}",
                    requiredCredits,
                    availableCredits = user.Credits
                });
            }

            var category = request.Category ?? "business";
            var city = request.City ?? "";

            // Validate MaxResults for non-admin users
            if (user.Role != "Admin" && request.MaxResults > 10)
            {
                return BadRequest(new
                {
                    message = "Maksimum 10 firma talebinde bulunabilirsiniz. Admin için limit yoktur.",
                    maxAllowed = 10,
                    requested = request.MaxResults
                });
            }

            _logger.LogInformation("🤖 GEMINI AI Scraping isteği alındı: UserId={UserId}, Category={Category}, City={City}, MaxResults={MaxResults}", 
                userId, category, city, request.MaxResults);

            // Create scraping job
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
                // GEMINI AI scraping with Google Search
                var businesses = await _geminiSearchService.SearchBusinessesAsync(
                    category,
                    city,
                    request.Country,
                    request.MaxResults,
                    HttpContext.RequestAborted
                );

                _logger.LogInformation("✅ GEMINI AI Scraping tamamlandı: {Count} işletme bulundu", businesses.Count);

                // Save to database
                foreach (var businessDto in businesses)
                {
                    // 🔧 FIX: Truncate long values to fit database constraints
                    var business = new Business
                    {
                        UserId = userId,
                        ScrapingJobId = job.Id,
                        BusinessName = TruncateString(businessDto.BusinessName, 300),
                        Address = TruncateString(businessDto.Address, 500),
                        Phone = TruncateString(businessDto.Phone, 50),
                        Mobile = TruncateString(businessDto.Mobile, 50),
                        Email = TruncateString(businessDto.Email, 255),
                        Website = TruncateString(businessDto.Website, 500),
                        SocialMedia = TruncateString(businessDto.SocialMedia, 500),
                        Comments = TruncateString(businessDto.Comments, 2000),
                        Rating = businessDto.Rating,
                        ReviewCount = businessDto.ReviewCount,
                        WorkingHours = TruncateString(businessDto.WorkingHours, 1000),
                        Category = TruncateString(businessDto.Category, 200),
                        City = TruncateString(businessDto.City, 100),
                        Country = TruncateString(businessDto.Country, 100),
                        Language = request.Language,
                    };

                    _logger.LogDebug("📝 Business saved to DB: Name={Name}, Email={Email}, Mobile={Mobile}, SocialMedia={SocialMedia}", 
                        business.BusinessName, business.Email, business.Mobile, business.SocialMedia);

                    _context.Businesses.Add(business);
                }

                // Deduct credits (Admin users don't lose credits)
                var actualCreditsUsed = businesses.Count;
                if (user.Role != "Admin")
                {
                    user.Credits -= actualCreditsUsed;
                }

                // Update job
                job.Status = "Completed";
                job.TotalResults = businesses.Count;
                job.CreditsUsed = user.Role == "Admin" ? 0 : actualCreditsUsed;
                job.CompletedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("💾 Veriler kaydedildi. Kullanılan kredi: {Credits}", actualCreditsUsed);

                // Response
                var response = new ScrapeResponseDto
                {
                    JobId = job.Id,
                    Status = "Completed",
                    Message = $"🤖 Gemini AI ile {businesses.Count} işletme bulundu ve kaydedildi!",
                    TotalResults = businesses.Count,
                    CreditsUsed = actualCreditsUsed,
                    Businesses = businesses,
                    DownloadUrl = $"/api/scraper/download/{job.Id}"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ GEMINI AI Scraping hatası");

                // Mark job as failed
                job.Status = "Failed";
                job.ErrorMessage = TruncateString(ex.Message, 1000); // ✅ Truncate error message
                job.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return StatusCode(500, new { message = "Gemini AI scraping başarısız oldu", error = ex.Message });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Beklenmeyen hata");
            return StatusCode(500, new { message = "Bir hata oluştu", error = ex.Message });
        }
    }

    /// <summary>
    /// Helper method to truncate strings to fit database constraints
    /// </summary>
    private static string? TruncateString(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }
}
