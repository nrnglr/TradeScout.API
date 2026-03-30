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
    /// <summary>
    /// Frontend bu endpoint'i çağırır → paymentUrl alır → kullanıcıyı yönlendirir.
    /// Hem abonelik hem extra kredi için aynı endpoint kullanılır.
    /// </summary>
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
    /// <summary>
    /// Paratika ödeme sonucunu buraya POST eder (RETURNURL).
    /// Form-data olarak gelir. Kullanıcıyı frontend'e yönlendir.
    /// </summary>
    [HttpPost("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback([FromForm] IFormCollection form)
    {
        _logger.LogInformation("🔔 Paratika Callback alındı | Keys={Keys}",
            string.Join(", ", form.Keys));

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
            Amount            = form["amount"],
            Currency          = form["currency"],
            InstallmentCount  = form["installmentCount"],
            CardToken         = form["cardToken"],      // ← Recurring için kritik
            CardBrand         = form["cardBrand"],
            CardPanMasked     = form["cardPanMasked"],
            PaymentSystem     = form["paymentSystem"],
            Random            = form["random"],
            SdSha512          = form["sdSha512"],
            CustomData        = form["customData"],
        };

        var result = await _paymentService.ProcessCallbackAsync(callback);

        // Paratika callback'ten sonra kullanıcıyı frontend'e yönlendir
        // (Paratika sayfadan dönerken redirect bekler)
        var frontendUrl = result.Success
            ? $"https://fgstrade.com/payment/success?orderId={callback.MerchantPaymentId}"
            : $"https://fgstrade.com/payment/failed?orderId={callback.MerchantPaymentId}&error={Uri.EscapeDataString(result.ErrorMessage ?? "Ödeme başarısız")}";

        return Redirect(frontendUrl);

        // ÖNEMLİ NOT: Eğer Paratika JSON bekliyor ve redirect istemiyorsa
        // aşağıdaki satırı kullan:
        // return Ok(new { status = result.Success ? "SUCCESS" : "FAILED" });
    }

    // ─── POST /api/payment/paratika/verify ───────────────────────────────────
    /// <summary>
    /// Frontend ödeme sayfasından döndükten sonra bunu çağırır.
    /// Tarayıcı kapanması/kesinti güvenlik ağı.
    /// </summary>
    [HttpPost("verify")]
    [Authorize]
    public async Task<IActionResult> Verify([FromBody] ParatikaVerifyRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.MerchantPaymentId))
            return BadRequest(new { error = "merchantPaymentId gereklidir" });

        var result = await _paymentService.VerifyAndProcessPaymentAsync(body.MerchantPaymentId);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new
        {
            success            = true,
            isAlreadyProcessed = result.IsAlreadyProcessed,
            creditsAdded       = result.CreditsAdded,
            packageName        = result.PackageName,
            userId             = result.UserId
        });
    }

    // ─── POST /api/payment/paratika/recurring-notification ───────────────────
    /// <summary>
    /// Paratika her otomatik çekim yaptığında buraya bildirim gönderir.
    /// Paratika panelinde Notification URL olarak kayıtlı olmalı.
    /// </summary>
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

        // Paratika notification'a 200 OK beklediğini söylüyor
        return Ok(new { received = true });
    }

    // ─── DELETE /api/payment/paratika/subscription ───────────────────────────
    /// <summary>
    /// Kullanıcı kendi profilinden aboneliğini iptal eder.
    /// </summary>
    [HttpDelete("subscription")]
    [Authorize]
    public async Task<IActionResult> CancelSubscription([FromBody] SubscriptionCancelRequest? body)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub") ?? "";

        if (!int.TryParse(userIdStr, out int userId))
            return Unauthorized(new { error = "Kimlik doğrulanamadı" });

        var reason = body?.Reason ?? "Kullanıcı tarafından iptal edildi";
        var success = await _paymentService.CancelSubscriptionAsync(userId, reason);

        if (!success)
            return BadRequest(new { error = "İptal edilecek aktif abonelik bulunamadı veya iptal işlemi başarısız" });

        return Ok(new { success = true, message = "Aboneliğiniz iptal edildi. Mevcut dönem sonuna kadar erişiminiz devam eder." });
    }

    // ─── GET /api/payment/paratika/subscription ──────────────────────────────
    /// <summary>
    /// Kullanıcının aktif abonelik bilgisini getirir.
    /// </summary>
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