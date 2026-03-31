using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TradeScout.API.Services;

namespace TradeScout.API.Controllers;

[ApiController]
[Route("api/payment/paratika")]
public class ParatikaPaymentController : ControllerBase
{
    private readonly IParatikaPaymentService            _paymentService;
    private readonly ILogger<ParatikaPaymentController> _logger;

    private static readonly string FrontendBaseUrl =
        Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "https://fgstrade.com";

    public ParatikaPaymentController(
        IParatikaPaymentService paymentService,
        ILogger<ParatikaPaymentController> logger)
    {
        _paymentService = paymentService;
        _logger         = logger;
    }

    // ─── GET /api/payment/paratika/packages ──────────────────────────────────
    [HttpGet("packages")]
    [AllowAnonymous]
    public IActionResult GetPackages() => Ok(_paymentService.GetAvailablePackages());

    // ─── POST /api/payment/paratika/initialize ───────────────────────────────
    [HttpPost("initialize")]
    [Authorize]
    public async Task<IActionResult> Initialize([FromBody] ParatikaInitRequest body)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub") ?? "";

        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { error = "Kimlik doğrulanamadı" });

        var result = await _paymentService.InitializePaymentAsync(new ParatikaPaymentRequestDto
        {
            ProductCode  = body.ProductCode ?? "",
            UserId       = userId,
            Installment  = body.Installment > 0 ? body.Installment : 1,
            DiscountCode = body.DiscountCode,
        });

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(new
        {
            paymentUrl        = result.PaymentUrl,
            sessionToken      = result.SessionToken,
            merchantPaymentId = result.MerchantPaymentId,
        });
    }

    // ─── POST /api/payment/paratika/callback ─────────────────────────────────
   [HttpPost("callback")]
[AllowAnonymous]
public async Task<IActionResult> Callback([FromForm] IFormCollection form)
{
    // Loglara düşen 'sdSha512' veya 'SD_SHA512' karmaşasını çözmek için:
    var incomingHash = form["sdSha512"].ToString();
    if (string.IsNullOrEmpty(incomingHash)) incomingHash = form["SD_SHA512"].ToString();

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
            Amount            = form["amount"],
            Currency          = form["currency"],
            InstallmentCount  = form["installmentCount"],
            CardToken         = form["cardToken"],
            CardBrand         = form["cardBrand"],
            CardPanMasked     = form["cardPanMasked"],
            PaymentSystem     = form["paymentSystem"],
            Random            = form["random"],
            SdSha512          = form["sdSha512"],
            CustomData        = form["customData"],
        };

       var result = await _paymentService.ProcessCallbackAsync(callback);

    // EĞER HASH HATASI VARSA BİLE FRONTEND'E YÖNLENDİR Kİ KULLANICI EKRANDA KILITLI KALMASIN
    string frontendUrl;
    if (result.Success)
    {
        frontendUrl = $"{FrontendBaseUrl}/payment/success?orderId={callback.MerchantPaymentId}";
    }
    else
    {
        _logger.LogError("Ödeme başarısız veya Hash hatalı: {Msg}", result.ErrorMessage);
        var errorCode = Uri.EscapeDataString(result.BankErrorCode ?? "HASH_ERROR");
        frontendUrl = $"{FrontendBaseUrl}/payment/failed?orderId={callback.MerchantPaymentId}&errorCode={errorCode}";
    }

    // Paratika POST Callback attığı için HTTP 302 Redirect bazen tarayıcıda sorun yaratabilir.
    // HTML bazlı bir yönlendirme döndürmek en garanti yoldur:
    return Content($"<html><head><script>window.location.href='{frontendUrl}';</script></head><body>Yönlendiriliyorsunuz...</body></html>", "text/html");
}

    // ─── POST /api/payment/paratika/verify ───────────────────────────────────
    [HttpPost("verify")]
    [Authorize]
    public async Task<IActionResult> Verify([FromBody] ParatikaVerifyRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.MerchantPaymentId))
            return BadRequest(new { error = "merchantPaymentId gereklidir" });

        var result = await _paymentService.VerifyAndProcessPaymentAsync(body.MerchantPaymentId);

        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage });

        return Ok(new
        {
            success            = true,
            orderId            = body.MerchantPaymentId,
            isAlreadyProcessed = result.IsAlreadyProcessed,
            creditsAdded       = result.CreditsAdded,
            packageName        = result.PackageName,
            membershipEnd      = result.MembershipEnd,
            userId             = result.UserId
        });
    }

    // ─── POST /api/payment/paratika/recurring-notification ───────────────────
    [HttpPost("recurring-notification")]
    [AllowAnonymous]
    public async Task<IActionResult> RecurringNotification([FromForm] IFormCollection form)
    {
        _logger.LogInformation("🔄 Recurring Notification alındı | PlanCode={Plan} | Code={Code}",
            form["planCode"].ToString(), form["responseCode"].ToString());

        var notification = new ParatikaRecurringNotificationDto
        {
            PlanCode          = form["planCode"],
            MerchantPaymentId = form["merchantPaymentId"],
            PgTranId          = form["pgTranId"],
            ResponseCode      = form["responseCode"],
            ResponseMsg       = form["responseMsg"],
            Amount            = form["amount"],
            Currency          = form["currency"],
            CardToken         = form["cardToken"],
            CardPanMasked     = form["cardPanMasked"],
            ErrorCode         = form["errorCode"],
            Error             = form["error"],
        };

        await _paymentService.ProcessRecurringNotificationAsync(notification);
        return Ok(new { received = true });
    }

    // ─── DELETE /api/payment/paratika/subscription ───────────────────────────
    [HttpDelete("subscription")]
    [Authorize]
    public async Task<IActionResult> CancelSubscription([FromBody] SubscriptionCancelRequest? body)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub") ?? "";

        if (!int.TryParse(userIdStr, out int userId))
            return Unauthorized(new { error = "Kimlik doğrulanamadı" });

        var reason  = body?.Reason ?? "Kullanıcı tarafından iptal edildi";
        var success = await _paymentService.CancelSubscriptionAsync(userId, reason);

        if (!success)
            return BadRequest(new { error = "İptal edilecek aktif abonelik bulunamadı veya iptal işlemi başarısız" });

        return Ok(new { success = true, message = "Aboneliğiniz iptal edildi. Mevcut dönem sonuna kadar erişiminiz devam eder." });
    }

    // ─── GET /api/payment/paratika/subscription ──────────────────────────────
    [HttpGet("subscription")]
    [Authorize]
    public async Task<IActionResult> GetSubscription()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub") ?? "";

        if (!int.TryParse(userIdStr, out int userId))
            return Unauthorized();

        var sub = await _paymentService.GetActiveSubscriptionAsync(userId);
        if (sub == null)
            return Ok(new { hasActiveSubscription = false });

        return Ok(new
        {
            hasActiveSubscription = true,
            planCode        = sub.PlanCode,
            packageName     = sub.PackageName,
            period          = sub.Period,
            amount          = sub.Amount,
            status          = sub.Status,
            startDate       = sub.StartDate,
            nextBillingDate = sub.NextBillingDate,
            cardLastFour    = sub.CardLastFour,
            cardBrand       = sub.CardBrand,
        });
    }

    // ─── GET /api/payment/paratika/query/{merchantPaymentId} ─────────────────
    [HttpGet("query/{merchantPaymentId}")]
    [Authorize]
    public async Task<IActionResult> Query(string merchantPaymentId)
    {
        var result = await _paymentService.QueryTransactionAsync(merchantPaymentId);
        if (result == null) return NotFound(new { error = "İşlem sorgulanamadı" });
        return Ok(result);
    }
}

// ─── Request DTO'ları ─────────────────────────────────────────────────────────
public class ParatikaInitRequest
{
    public string? ProductCode  { get; set; }
    public int     Installment  { get; set; } = 1;
    public string? DiscountCode { get; set; }
}

public class ParatikaVerifyRequest
{
    public string? MerchantPaymentId { get; set; }
}

public class SubscriptionCancelRequest
{
    public string? Reason { get; set; }
}
