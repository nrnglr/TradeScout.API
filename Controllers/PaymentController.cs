using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TradeScout.API.Models.Payment;
using TradeScout.API.Services;
using TradeScout.API.DTOs;
using TradeScout.API.Models; 
using TradeScout.API.Data;   
using Microsoft.EntityFrameworkCore;
namespace TradeScout.API.Controllers;

/// <summary>
/// Payment Controller - Paratika Ödeme Altyapısı
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class PaymentController : ControllerBase
{
    private readonly IParatikaPaymentService _paratikaPaymentService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PaymentController> _logger;
    private readonly IEmailService _email;

    public PaymentController(
        IParatikaPaymentService paratikaPaymentService,
        ApplicationDbContext context,
        ILogger<PaymentController> logger,
        IEmailService email)
    {
        _paratikaPaymentService = paratikaPaymentService;
        _context = context;
        _logger = logger;
        _email = email;
    }

    /// <summary>
    /// Mevcut paketleri listele
    /// </summary>
    [HttpGet("packages")]
    [AllowAnonymous]
    public ActionResult<List<FgsTradePackage>> GetPackages()
    {
        var packages = _paratikaPaymentService.GetAvailablePackages();
        return Ok(packages);
    }

    /// <summary>
    /// Standart paket ödemesi başlat - Paratika Hosted Page URL al
    /// </summary>
    [HttpPost("initialize")]
    [Authorize]
    public async Task<IActionResult> InitializePayment([FromBody] ParatikaInitRequest request)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? User.FindFirst("sub")?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized(new { message = "Kullanıcı kimliği doğrulanamadı" });
            }

            _logger.LogInformation("💳 Paratika ödeme başlatma isteği - UserId: {UserId}, ProductCode: {ProductCode}",
                userIdClaim, request.ProductCode);

            var result = await _paratikaPaymentService.InitializePaymentAsync(new ParatikaPaymentRequestDto
            {
                ProductCode = request.ProductCode ?? "",
                UserId = userIdClaim,
                Installment = request.Installment > 0 ? request.Installment : 1,
                DiscountCode = request.DiscountCode
            });

            if (!result.Success)
            {
                return BadRequest(new { 
                    message = result.ErrorMessage,
                    errorCode = result.ErrorCode
                });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ödeme başlatma hatası");
            return StatusCode(500, new { message = "Ödeme başlatılırken bir hata oluştu" });
        }
    }

    /// <summary>
    /// FortexGlobe Eğitim Programı Ödeme ve Kayıt Başlatma
    /// </summary>
    [HttpPost("initialize-training")]
    [AllowAnonymous]
    public async Task<IActionResult> InitializeTrainingPayment([FromBody] PaymentRequestDto request)
    {
        try
        {
            _logger.LogInformation("🎓 Eğitim ödeme başlatma isteği - Email: {Email}, Slot: {Slot}", request.Email, request.SelectedSlot);

            // 1. Veritabanına "Pending" durumunda eğitim kaydını atıyoruz
            var registration = new TrainingRegistration
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                Phone = request.Phone,
                City = request.City,
                SelectedSlot = request.SelectedSlot,
                PaymentMethod = "card",
                PaymentStatus = "Pending",
                Amount = 6600, // Gerçek fiyat veya test tutarı
                RegistrationCode = $"TRG-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}"
            };

            _context.TrainingRegistrations.Add(registration);
            await _context.SaveChangesAsync();

            // 2. Paratika İstek Modeli Oluşturma
            var paratikaRequest = new ParatikaPaymentRequestDto
            {
                ProductCode = "FORTEX_CERT_01", 
                UserId = request.Email,
                Installment = 6, 
                DiscountCode = null,
                MerchantPaymentId = registration.RegistrationCode
            };

            var result = await _paratikaPaymentService.InitializePaymentAsync(paratikaRequest);

            if (result.Success)
            {
                return Ok(new { paymentUrl = result.PaymentUrl, registrationCode = registration.RegistrationCode });
            }

            registration.PaymentStatus = "Failed";
            await _context.SaveChangesAsync();
            return BadRequest(new { message = result.ErrorMessage ?? "Ödeme oturumu oluşturulamadı" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Eğitim ödeme başlatma hatası");
            return StatusCode(500, new { message = "Eğitim kaydı oluşturulurken bir hata oluştu" });
        }
    }

    /// <summary>
    /// Paratika callback endpoint - Ödeme sonucu buraya gelir
    /// </summary>
    [HttpPost("callback")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> PaymentCallback([FromForm] IFormCollection form)
    {
        var callback = new ParatikaCallbackDto
        {
            MerchantPaymentId = form["merchantPaymentId"],
            ApiMerchantId     = form["apiMerchantId"],
            SessionToken      = form["sessionToken"],
            CustomerId        = form["customerId"],
            PgTranId          = form["pgTranId"],
            PgTranDate        = form["pgTranDate"],
            PgTranRefId       = form["pgTranRefId"],
            PgTranApprCode    = form["pgTranApprCode"],
            ResponseCode      = form["responseCode"],
            ResponseMsg       = form["responseMsg"],
            ErrorCode         = form["errorCode"],
            ErrorMsg          = form["errorMsg"],
            PgTranErrorCode   = form["pgTranErrorCode"],
            Amount            = !string.IsNullOrEmpty(form["amount"]) ? form["amount"].ToString() : form["AMOUNT"].ToString(),
            Currency          = form["currency"],
            InstallmentCount  = form["installmentCount"],
            CardToken         = form["cardToken"],
            CardBrand         = form["cardBrand"],
            CardPanMasked     = form["cardPanMasked"],
            PaymentSystem     = form["paymentSystem"],
            Random            = form["random"],
            SdSha512          = !string.IsNullOrEmpty(form["sdSha512"]) ? form["sdSha512"].ToString() : form["SD_SHA512"].ToString(),
            CustomData        = form["customData"],
        };

        var result = await _paratikaPaymentService.ProcessCallbackAsync(callback);
        var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "https://fgstrade.com";

        // Eğitim kaydıysa durumu güncelle ve mailleri at
        if (callback.MerchantPaymentId?.StartsWith("TRG-") == true)
            await TrainingPaymentHandler.CompleteAsync(_context, _email, _logger, callback.MerchantPaymentId, result.Success);

        if (result.Success)
        {
            _logger.LogInformation("✅ Paratika Callback başarılı - SUCCESS | OrderId: {OrderId}", callback.MerchantPaymentId);
            return Redirect($"{frontendUrl}/payment/success?orderId={callback.MerchantPaymentId}&gateway=paratika");
        }

        _logger.LogWarning("❌ Paratika Callback başarısız - FAILED | OrderId: {OrderId}", callback.MerchantPaymentId);
        return Redirect($"{frontendUrl}/payment/failed?orderId={callback.MerchantPaymentId}&errorCode={result.BankErrorCode}&gateway=paratika");
    }

   /// <summary>
    /// Ödeme doğrulama (Güvenlik ağı ve Eğitim doğrulaması)
    /// </summary>
    [HttpGet("verify/{orderId}")]
    [HttpPost("verify/{orderId}")]
    [AllowAnonymous]
    public async Task<ActionResult> VerifyPayment(string orderId)
    {
        try
        {
            _logger.LogInformation("🔍 Ödeme doğrulama isteği | OrderId: {OrderId}", orderId);

            // 🎓 Eğer gelen istek bir eğitim kaydiyse (TRG-), TrainingRegistrations tablosundan kontrol et
            if (!string.IsNullOrEmpty(orderId) && orderId.StartsWith("TRG-"))
            {
                var trainingReg = await _context.TrainingRegistrations
                    .FirstOrDefaultAsync(r => r.RegistrationCode == orderId);

                if (trainingReg != null)
                {
                    // Kayıt SADECE Paratika callback'i (hash doğrulamalı) ile Completed olur.
                    // Bu uç sadece durumu okur; kimse buradan "ödendi" işaretleyemez.
                    if (trainingReg.PaymentStatus == "Completed")
                        return Ok(new { success = true, message = "Eğitim ödemesi doğrulandı", orderId });

                    return BadRequest(new { success = false, message = "Ödeme henüz onaylanmadı", orderId });
                }

                _logger.LogWarning("⚠️ Doğrulama sırasında eğitim kaydı bulunamadı | OrderId: {OrderId}", orderId);
                return BadRequest(new { success = false, message = "Ödeme kaydı bulunamadı" });
            }

            // Standart paketler için Paratika servis doğrulaması
            var result = await _paratikaPaymentService.VerifyAndProcessPaymentAsync(orderId);

            if (result.Success)
            {
                return Ok(new { success = true, message = "Ödeme doğrulandı", orderId, creditsAdded = result.CreditsAdded });
            }

            return BadRequest(new { success = false, message = result.ErrorMessage ?? "Ödeme doğrulanamadı" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ödeme doğrulama hatası | OrderId: {OrderId}", orderId);
            return StatusCode(500, new { success = false, message = "Doğrulama hatası" });
        }
    }
}