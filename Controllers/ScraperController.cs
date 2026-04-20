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
        _logger.LogWarning("🔴 [NORMAL SCRAPE] ENDPOINT ÇAĞRILDI"); // Debug log
        
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

            // Kredi kontrolü (her arama 1 kredi)
            var requiredCredits = 1;
            if (user.Credits < requiredCredits)
            {
                return StatusCode(402, new 
                { 
                    message = $"Yetersiz kredi. Gerekli: {requiredCredits}, Mevcut: {user.Credits}",
                    requiredCredits,
                    availableCredits = user.Credits
                });
            }

            // MaxResults limitini kontrol et
            if (user.Role != "Admin" && request.MaxResults > user.MaxResultsPerSearch)
            {
                return BadRequest(new
                {
                    message = $"Maksimum {user.MaxResultsPerSearch} firma talebinde bulunabilirsiniz.",
                    maxAllowed = user.MaxResultsPerSearch,
                    requested = request.MaxResults
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

                // Kullanıcı kredisini düş (her arama 1 kredi, kaç firma bulundğundan bağımsız)
                var creditsBefore = user.Credits;
                var actualCreditsUsed = 1;
                user.Credits -= actualCreditsUsed;

                // Job'ı güncelle
                job.Status = "Completed";
                job.TotalResults = businesses.Count;
                job.CreditsUsed = actualCreditsUsed;
                job.CompletedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("� KREDİ DÜŞÜŞÜ [Normal] | UserId={UserId} | Önceki={Before} | Düşen={Used} | Kalan={After}", 
                    userId, creditsBefore, actualCreditsUsed, user.Credits);

                // Response mesajını oluştur - az firma bulunursa kullanıcıya bilgi ver
                string responseMessage;
                if (businesses.Count == 0)
                {
                    responseMessage = "Bu bölgede işletme bulunamadı.";
                }
                else if (businesses.Count < request.MaxResults)
                {
                    responseMessage = $"{businesses.Count} işletme bulundu (hedef: {request.MaxResults}). Bu bölgede daha fazla işletme bulunamadı.";
                }
                else
                {
                    responseMessage = $"Başarıyla {businesses.Count} işletme bulundu ve kaydedildi.";
                }

                // Response oluştur
                var response = new ScrapeResponseDto
                {
                    JobId = job.Id,
                    Status = "Completed",
                    Message = responseMessage,
                    TotalResults = businesses.Count,
                    CreditsUsed = actualCreditsUsed,
                    RemainingCredits = user.Credits,
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

            // Dosya adı için güvenli karakterler kullan (özel karakterleri temizle)
            var safeCategory = SanitizeFileName(job.Category ?? "data");
            var safeCity = SanitizeFileName(job.City ?? "");
            var safeCountry = SanitizeFileName(job.Country ?? "");
            
            // Dosya adı formatı: TradeScout_Kategori_Şehir_Ülke_Tarih.xlsx
            var locationPart = string.IsNullOrEmpty(safeCountry) 
                ? safeCity 
                : $"{safeCity}_{safeCountry}";
            
            var fileName = $"TradeScout_{safeCategory}_{locationPart}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

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
        _logger.LogWarning("🟡 [PARALLEL SCRAPE] ENDPOINT ÇAĞRILDI"); // Debug log
        
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
            // Her arama için 1 kredi (firma sayısına değil)
            var requiredCredits = 1;
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
            if (user.Role != "Admin" && request.MaxResults > user.MaxResultsPerSearch)
            {
                return BadRequest(new
                {
                    message = $"Maksimum {user.MaxResultsPerSearch} firma talebinde bulunabilirsiniz.",
                    maxAllowed = user.MaxResultsPerSearch,
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
                // Her arama için sadece 1 kredi düş (kaç firma bulunduğundan bağımsız)
                var creditsBefore = user.Credits;
                var actualCreditsUsed = 1;
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

                _logger.LogInformation("� KREDİ DÜŞÜŞÜ [Parallel] | UserId={UserId} | Önceki={Before} | Düşen={Used} | Kalan={After}", 
                    userId, creditsBefore, actualCreditsUsed, user.Credits);

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
    /// Uses batched processing (15 per batch) to prevent 504 Gateway Timeout
    /// Smart credit deduction: only charges for successfully enriched records
    /// </summary>
    [HttpPost("scrape-gemini")]
    [ProducesResponseType(typeof(ScrapeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
    public async Task<ActionResult<ScrapeResponseDto>> ScrapeBusinessesWithGemini([FromBody] ScrapeRequestDto request)
    {
        _logger.LogWarning("🟢 [GEMINI SCRAPE] ENDPOINT ÇAĞRILDI"); // Debug log
        
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

            // ⚠️ SMART CREDIT CHECK: Check minimum credits (at least 1)
            // Actual deduction will be based on successful results at the END
            if (user.Role != "Admin" && user.Credits < 1)
            {
                return StatusCode(402, new 
                { 
                    message = $"Yetersiz kredi. En az 1 kredi gerekli, Mevcut: {user.Credits}",
                    requiredCredits = 1,
                    availableCredits = user.Credits
                });
            }

            var category = request.Category ?? "business";
            var city = request.City ?? "";

            // Validate MaxResults for non-admin users
            if (user.Role != "Admin" && request.MaxResults > user.MaxResultsPerSearch)
            {
                return BadRequest(new
                {
                    message = $"Maksimum {user.MaxResultsPerSearch} firma talebinde bulunabilirsiniz.",
                    maxAllowed = user.MaxResultsPerSearch,
                    requested = request.MaxResults
                });
            }

            _logger.LogInformation("🤖 GEMINI AI Scraping başlatılıyor: UserId={UserId}, Category={Category}, City={City}, MaxResults={MaxResults}", 
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
                // STEP 1: GEMINI AI search to find businesses
                // NOTE: Using CancellationToken.None to prevent client disconnect from cancelling the operation
                _logger.LogInformation("📍 STEP 1: Gemini AI ile firma arama başlatılıyor...");
                var businesses = await _geminiSearchService.SearchBusinessesAsync(
                    category,
                    city,
                    request.Country,
                    request.MaxResults,
                    CancellationToken.None // Don't use HttpContext.RequestAborted - let operation complete
                );

                _logger.LogInformation("✅ STEP 1 tamamlandı: {Count} firma bulundu", businesses.Count);

                // STEP 2: BATCHED ENRICHMENT (60 per batch to prevent 504 timeout)
                _logger.LogInformation("📧 STEP 2: Batched enrichment başlatılıyor ({Count} firma, 60'lık batch)...", businesses.Count);
                
                var (enrichedBusinesses, successfulCount) = await _geminiSearchService.EnrichBusinessesAsync(
                    businesses,
                    batchSize: 60,
                    CancellationToken.None // Don't use HttpContext.RequestAborted - let operation complete
                );

                _logger.LogInformation("✅ STEP 2 tamamlandı: {SuccessCount}/{TotalCount} firma zenginleştirildi", 
                    successfulCount, enrichedBusinesses.Count);

                // Save to database
                foreach (var businessDto in enrichedBusinesses)
                {
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

                    _context.Businesses.Add(business);
                }

                // ✅ SMART CREDIT DEDUCTION (at the END, only for successful enrichments)
                // Her arama sadece 1 kredi harcar (kaç firma bulunursa bulunsun)
                // ✅ SMART CREDIT DEDUCTION (at the END, only for successful enrichments)
                var actualCreditsUsed = 0;
                if (user.Role != "Admin")
                {
                    var creditsBefore = user.Credits;
                    
                    // 🌟 KRİTİK GÜNCELLEME: Sadece firma bulunduysa kredi düş
                    if (enrichedBusinesses.Count > 0)
                    {
                        actualCreditsUsed = 1;
                        
                        // Kullanıcının kredisi yetmiyorsa (ek güvenlik)
                        actualCreditsUsed = Math.Min(actualCreditsUsed, user.Credits);
                        
                        user.Credits -= actualCreditsUsed;
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Firma bulunamadığı için kredi düşülmedi. UserId: {UserId}", userId);
                    }
                    
                    _logger.LogInformation("💳 KREDİ DURUMU | UserId={UserId} | Önceki={Before} | Düşen={Used} | Kalan={After}", 
                        userId, creditsBefore, actualCreditsUsed, user.Credits);
                    
                    _logger.LogInformation("📊 İşlem detayı: {TotalCount} firma bulundu, {SuccessCount} zenginleştirildi", 
                        enrichedBusinesses.Count, successfulCount);
                }
                // Update job
                job.Status = enrichedBusinesses.Count > 0 ? "Completed" : "NoResults";
                job.TotalResults = enrichedBusinesses.Count;
                job.CreditsUsed = actualCreditsUsed;
                job.CompletedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("🎉 GEMINI AI Scraping tamamlandı! Toplam: {Total}, Zenginleştirildi: {Enriched}, Kredi: {Credits}", 
                    enrichedBusinesses.Count, successfulCount, actualCreditsUsed);

                // Response
                // Generate appropriate message based on results
                string message;
                if (enrichedBusinesses.Count == 0)
                {
                    message = $"⚠️ Bu bölgede '{category}' sektöründe işletme bulunamadı. Farklı bir arama deneyin.";
                }
                else if (enrichedBusinesses.Count < request.MaxResults)
                {
                    message = $"🤖 Bu bölgede {enrichedBusinesses.Count} işletme bulundu (hedef: {request.MaxResults}). {successfulCount} tanesi zenginleştirildi.";
                }
                else
                {
                    message = $"🤖 Gemini AI ile {enrichedBusinesses.Count} işletme bulundu, {successfulCount} tanesi başarıyla zenginleştirildi!";
                }

                var response = new ScrapeResponseDto
                {
                    JobId = job.Id,
                    Status = enrichedBusinesses.Count > 0 ? "Completed" : "NoResults",
                    Message = message,
                    TotalResults = enrichedBusinesses.Count,
                    CreditsUsed = actualCreditsUsed,
                    Businesses = enrichedBusinesses,
                    RemainingCredits = user.Credits,
                    DownloadUrl = enrichedBusinesses.Count > 0 ? $"/api/scraper/download/{job.Id}" : null
                };

                return Ok(response);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("⚠️ GEMINI AI Scraping timeout (işlem çok uzun sürdü)");
                
                job.Status = "Timeout";
                job.ErrorMessage = "İşlem zaman aşımına uğradı";
                job.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return StatusCode(408, new { message = "İşlem zaman aşımına uğradı. Daha az firma ile tekrar deneyin." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ GEMINI AI Scraping hatası");

                // Mark job as failed (NO credit deduction on error!)
                job.Status = "Failed";
                job.ErrorMessage = TruncateString(ex.Message, 1000);
                job.CreditsUsed = 0; // No credits used on failure
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

    /// <summary>
    /// Helper method to sanitize file names (remove invalid characters)
    /// </summary>
    private static string SanitizeFileName(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "unknown";

        // Türkçe karakterleri dönüştür
        var result = input
            .Replace("ı", "i")
            .Replace("İ", "I")
            .Replace("ğ", "g")
            .Replace("Ğ", "G")
            .Replace("ü", "u")
            .Replace("Ü", "U")
            .Replace("ş", "s")
            .Replace("Ş", "S")
            .Replace("ö", "o")
            .Replace("Ö", "O")
            .Replace("ç", "c")
            .Replace("Ç", "C");

        // Geçersiz dosya karakterlerini kaldır
        var invalidChars = System.IO.Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
            result = result.Replace(c.ToString(), "");
        }

        // Boşlukları alt çizgi yap
        result = result.Replace(" ", "_");

        // Birden fazla alt çizgiyi teke indir
        while (result.Contains("__"))
        {
            result = result.Replace("__", "_");
        }

        // Maksimum 30 karakter — Windows yol uzunluğu sınırı için
        if (result.Length > 30)
            result = result.Substring(0, 30);

        return result.Trim('_');
    }

    /// <summary>
    /// Kullanıcının geçmiş aramalarını getir
    /// </summary>
    [HttpGet("my-jobs")]
    [ProducesResponseType(typeof(List<UserJobDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<UserJobDto>>> GetMyJobs([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            // Kullanıcı ID'sini JWT token'dan al
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Geçersiz kullanıcı token'ı" });
            }

            // Kullanıcının BAŞARILI job'larını getir (en yeni en üstte, Failed olanlar hariç)
            var jobs = await _context.ScrapingJobs
                .Where(j => j.UserId == userId && j.Status == "Completed")
                .OrderByDescending(j => j.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(j => new UserJobDto
                {
                    JobId = j.Id,
                    Category = j.Category,
                    City = j.City,
                    Country = j.Country ?? "",
                    Language = j.Language,
                    Status = j.Status,
                    TotalResults = j.TotalResults,
                    CreditsUsed = j.CreditsUsed,
                    CreatedAt = j.CreatedAt,
                    CompletedAt = j.CompletedAt
                })
                .ToListAsync();

            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Kullanıcı job'ları getirme hatası");
            return StatusCode(500, new { message = "Bir hata oluştu" });
        }
    }

    /// <summary>
    /// Kullanıcının toplam arama sayısını getir
    /// </summary>
    [HttpGet("my-jobs/count")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> GetMyJobsCount()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Geçersiz kullanıcı token'ı" });
            }

            var totalCount = await _context.ScrapingJobs
                .Where(j => j.UserId == userId)
                .CountAsync();

            var completedCount = await _context.ScrapingJobs
                .Where(j => j.UserId == userId && j.Status == "Completed")
                .CountAsync();

            return Ok(new { total = totalCount, completed = completedCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Job sayısı getirme hatası");
            return StatusCode(500, new { message = "Bir hata oluştu" });
        }
    }
}

/// <summary>
/// DTO for user's job history
/// </summary>
public class UserJobDto
{
    public int JobId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalResults { get; set; }
    public int CreditsUsed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}