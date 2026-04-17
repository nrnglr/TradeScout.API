using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeScout.API.Data;
using TradeScout.API.DTOs;
using TradeScout.API.Services;

namespace TradeScout.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DiscountCodeController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DiscountCodeController> _logger;

    public DiscountCodeController(
        ApplicationDbContext context,
        ILogger<DiscountCodeController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// İndirim kodunu doğrula ve indirimli fiyatı hesapla
    /// </summary>
    [HttpPost("validate")]
    [AllowAnonymous]
    public async Task<ActionResult<DiscountCodeValidationDto>> ValidateDiscountCode([FromBody] ValidateDiscountCodeDto request)
    {
        try
        {
            // Debug: Request body'yi logla
            _logger.LogInformation("🔍 VALIDATE REQUEST RECEIVED");
            _logger.LogInformation("🔍 Request object null mu? {IsNull}", request == null);
            
            if (request == null)
            {
                _logger.LogError("❌ Request body null!");
                return BadRequest(new { message = "Request body boş olamaz." });
            }
            
            _logger.LogInformation("🔍 Code: '{Code}' (Length: {Length})", request.Code ?? "null", request.Code?.Length ?? 0);
            _logger.LogInformation("🔍 PackageCode: '{PackageCode}'", request.PackageCode ?? "null");
            
            // Validation
            if (string.IsNullOrWhiteSpace(request.Code))
            {
                _logger.LogWarning("⚠️ İndirim kodu boş");
                return BadRequest(new { message = "İndirim kodu gereklidir." });
            }
            
            if (string.IsNullOrWhiteSpace(request.PackageCode))
            {
                _logger.LogWarning("⚠️ Paket kodu boş");
                return BadRequest(new { message = "Paket kodu gereklidir." });
            }
            
            _logger.LogInformation("🎫 İndirim kodu doğrulama: {Code} | Paket: {Package}", 
                request.Code, request.PackageCode);

            // İndirim kodunu bul
            var discountCode = await _context.DiscountCodes
                .FirstOrDefaultAsync(dc => dc.Code.ToUpper() == request.Code.ToUpper());

            if (discountCode == null)
            {
                return Ok(new DiscountCodeValidationDto
                {
                    IsValid = false,
                    Message = "Geçersiz indirim kodu. Lütfen kontrol edip tekrar deneyin."
                });
            }

            // Kod aktif mi kontrol et
            if (!discountCode.IsActive)
            {
                return Ok(new DiscountCodeValidationDto
                {
                    IsValid = false,
                    Message = "Bu kod artık aktif değil."
                });
            }

            // Kullanım limiti dolmuş mu kontrol et
            if (discountCode.CurrentUses >= discountCode.MaxUses)
            {
                return Ok(new DiscountCodeValidationDto
                {
                    IsValid = false,
                    Message = "Bu kodun kullanım limiti dolmuştur."
                });
            }

            // Son kullanma tarihi geçmiş mi kontrol et
            if (discountCode.ExpiresAt.HasValue && discountCode.ExpiresAt.Value < DateTime.UtcNow)
            {
                return Ok(new DiscountCodeValidationDto
                {
                    IsValid = false,
                    Message = "Bu kodun kullanım süresi dolmuştur."
                });
            }

            // Paketi bul ve fiyatını al
            var morparaService = HttpContext.RequestServices.GetRequiredService<IMorparaPaymentService>();
            var packages = morparaService.GetAvailablePackages();
            var package = packages.FirstOrDefault(p => 
                p.ProductCode == request.PackageCode || 
                p.Alias.Equals(request.PackageCode, StringComparison.OrdinalIgnoreCase));

            if (package == null)
            {
                return BadRequest(new { message = "Geçersiz paket kodu." });
            }

            // İndirimli fiyatı USD cinsinden hesapla
            var originalPrice = package.PriceTry;
            var discountAmount = originalPrice * discountCode.DiscountPercentage / 100m;
            var discountedPrice = originalPrice - discountAmount;

            _logger.LogInformation("✅ Geçerli indirim kodu | %{Discount} indirim | {Original} USD → {Discounted} USD",
                discountCode.DiscountPercentage, originalPrice, discountedPrice);

            return Ok(new DiscountCodeValidationDto
            {
                IsValid = true,
                Message = $"✅ İndirim kodu başarıyla uygulandı! %{discountCode.DiscountPercentage} indirim kazandınız.",
                DiscountPercentage = discountCode.DiscountPercentage,
                OriginalPrice = originalPrice,
                DiscountedPrice = discountedPrice,
                DiscountAmount = discountAmount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "İndirim kodu doğrulama hatası");
            return StatusCode(500, new { message = "Bir hata oluştu. Lütfen tekrar deneyin." });
        }
    }

    /// <summary>
    /// Admin: Tüm indirim kodlarını listele
    /// </summary>
    [HttpGet("admin/list")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ListDiscountCodes([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            var skip = (page - 1) * pageSize;
            
            var codes = await _context.DiscountCodes
                .OrderByDescending(dc => dc.CreatedAt)
                .Skip(skip)
                .Take(pageSize)
                .Select(dc => new
                {
                    dc.Id,
                    dc.Code,
                    dc.DiscountPercentage,
                    dc.MaxUses,
                    dc.CurrentUses,
                    RemainingUses = dc.MaxUses - dc.CurrentUses,
                    dc.IsActive,
                    dc.CreatedAt,
                    dc.ExpiresAt,
                    dc.Description
                })
                .ToListAsync();

            var totalCount = await _context.DiscountCodes.CountAsync();

            return Ok(new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                codes
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "İndirim kodları listeleme hatası");
            return StatusCode(500, new { message = "Bir hata oluştu." });
        }
    }
}