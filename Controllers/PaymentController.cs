using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TradeScout.API.Models.Payment;
using TradeScout.API.Services;
using TradeScout.API.DTOs;
using TradeScout.API.Models; 
using TradeScout.API.Data;   

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

        // Eğer gelen ödeme bir eğitim kaydiyse veritabanındaki statüyü güncelle ve mailleri at
        if (callback.MerchantPaymentId?.StartsWith("TRG-") == true)
        {
            var trainingReg = _context.TrainingRegistrations.FirstOrDefault(r => r.RegistrationCode == callback.MerchantPaymentId);

            if (trainingReg != null && trainingReg.PaymentStatus != "Completed")
            {
                trainingReg.PaymentStatus = result.Success ? "Completed" : "Failed";
                trainingReg.PaidAt = result.Success ? DateTime.UtcNow : null;
                await _context.SaveChangesAsync();

                if (result.Success)
                    await SendTrainingEmailsAsync(trainingReg);
            }
        }

        if (result.Success)
        {
            _logger.LogInformation("✅ Paratika Callback başarılı - SUCCESS | OrderId: {OrderId}", callback.MerchantPaymentId);
            return Redirect($"{frontendUrl}/payment/success?orderId={callback.MerchantPaymentId}&gateway=paratika");
        }

        _logger.LogWarning("❌ Paratika Callback başarısız - FAILED | OrderId: {OrderId}", callback.MerchantPaymentId);
        return Redirect($"{frontendUrl}/payment/failed?orderId={callback.MerchantPaymentId}&errorCode={result.BankErrorCode}&gateway=paratika");
    }

    /// <summary>
    /// Ödeme doğrulama (Güvenlik ağı)
    /// </summary>
    [HttpGet("verify/{orderId}")]
    [HttpPost("verify/{orderId}")]
    [AllowAnonymous]
    public async Task<ActionResult> VerifyPayment(string orderId)
    {
        try
        {
            _logger.LogInformation("🔍 Paratika ödeme doğrulama isteği | OrderId: {OrderId}", orderId);
            var result = await _paratikaPaymentService.VerifyAndProcessPaymentAsync(orderId);

            if (result.Success)
            {
                return Ok(new { success = true, message = "Ödeme doğrulandı", orderId, creditsAdded = result.CreditsAdded });
            }

            return BadRequest(new { success = false, message = result.ErrorMessage ?? "Ödeme doğrulanamadı" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ödeme doğrulama hatası");
            return StatusCode(500, new { success = false, message = "Doğrulama hatası" });
        }
    }

    /// <summary>
    /// Ödeme alındıktan sonra: (1) info@fgstrade.com'a kayıt bildirimi, (2) müşteriye onay maili.
    /// </summary>
    private async Task SendTrainingEmailsAsync(TrainingRegistration reg)
    {
        static string e(string? text) => System.Net.WebUtility.HtmlEncode(text ?? "");

        try
        {
            var adminBody = $@"
                <h3>Yeni Eğitim Kaydı (Ödeme Alındı)</h3>
                <p><b>Kayıt Kodu:</b> {e(reg.RegistrationCode)}<br>
                <b>Ad Soyad:</b> {e(reg.FirstName)} {e(reg.LastName)}<br>
                <b>E-posta:</b> {e(reg.Email)}<br>
                <b>Telefon:</b> {e(reg.Phone)}<br>
                <b>Şehir:</b> {e(reg.City)}<br>
                <b>Meslek/Firma:</b> {e(reg.Occupation ?? "-")}<br>
                <b>Dönem:</b> {e(reg.SelectedSlot)}<br>
                <b>Tutar:</b> {reg.Amount:N2} TL<br>
                <b>Ödeme Tarihi (UTC):</b> {reg.PaidAt:dd.MM.yyyy HH:mm}</p>";

            await _email.SendEmailAsync(
                "info@fgstrade.com",
                $"[FGS Trade] Yeni eğitim kaydı: {reg.FirstName} {reg.LastName}",
                adminBody);

            var userBody = $@"
                <h3>Ödemeniz alındı, kaydınız tamamlandı ✅</h3>
                <p>Merhaba {e(reg.FirstName)},</p>
                <p>Dış Ticaret &amp; E-Ticaret Sertifika Programı için ödemeniz başarıyla alındı.</p>
                <p><b>Kayıt kodunuz:</b> {e(reg.RegistrationCode)}<br>
                <b>Seçtiğiniz dönem:</b> {e(reg.SelectedSlot)}</p>
                <p>Sorularınız için bu e-postayı yanıtlayabilirsiniz.</p>
                <p><b>FGS TRADE Ekibi</b></p>";

            await _email.SendEmailAsync(
                reg.Email,
                "Eğitim kaydınız tamamlandı - FGS Trade",
                userBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Eğitim mailleri gönderilirken hata | {Code}", reg.RegistrationCode);
        }
    }
}