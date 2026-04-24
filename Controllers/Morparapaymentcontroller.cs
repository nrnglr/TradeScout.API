using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TradeScout.API.Services;

namespace TradeScout.API.Controllers;

[ApiController]
[Route("api/payment/morpara")]
public class MorparaPaymentController : ControllerBase
{
    private readonly IMorparaPaymentService            _paymentService;
    private readonly ILogger<MorparaPaymentController> _logger;

    private static readonly string FrontendBaseUrl =
        Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "https://fgstrade.com";

    public MorparaPaymentController(
        IMorparaPaymentService paymentService,
        ILogger<MorparaPaymentController> logger)
    {
        _paymentService = paymentService;
        _logger         = logger;
    }

    // ─── GET /api/payment/morpara/packages ────────────────────────────────────
    [HttpGet("packages")]
    [AllowAnonymous]
    public IActionResult GetPackages() => Ok(_paymentService.GetAvailablePackages());

    // ─── POST /api/payment/morpara/initialize ─────────────────────────────────
    [HttpPost("initialize")]
    [Authorize]
    public async Task<IActionResult> Initialize([FromBody] MorparaInitRequest body)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub") ?? "";

        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { error = "Kimlik doğrulanamadı" });

        var result = await _paymentService.InitializePaymentAsync(new MorparaPaymentRequestDto
        {
            ProductCode  = body.ProductCode ?? "",
            UserId       = userId,
            Installment  = body.Installment > 0 ? body.Installment : 1,
            DiscountCode = body.DiscountCode,
            Currency     = body.Currency ?? "TRY",
        });

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(new
        {
            paymentUrl     = result.PaymentUrl,
            conversationId = result.ConversationId,
        });
    }

    // ─── POST /api/payment/morpara/callback ───────────────────────────────────
    [HttpPost("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback([FromForm] IFormCollection form)
    {
        var rawConvId   = form["conversationId"].ToString();
        var cleanConvId = rawConvId.Contains('?') ? rawConvId.Split('?')[0] : rawConvId;

        var callback = new MorparaCallbackDto
        {
            ConversationId = cleanConvId,
            OrderId        = form["orderId"].ToString(),
            PaymentId      = form["paymentId"].ToString(),
            ResponseCode   = form["responseCode"].ToString(),
            ResponseDesc   = form["responseDescription"].ToString(),
            MerchantId     = form["merchantId"].ToString(),
        };

        _logger.LogInformation("📩 MORPARA CALLBACK POST | ConvId={Id} | OrderId={OrderId} | Code={Code}",
            callback.ConversationId, callback.OrderId, callback.ResponseCode);

        var result = await _paymentService.ProcessCallbackAsync(callback);

        string frontendUrl;
        if (result.Success)
        {
            frontendUrl = $"{FrontendBaseUrl}/payment/success?cid={callback.ConversationId}";
        }
        else
        {
            _logger.LogError("❌ Mor Para callback başarısız | ConvId={Id} | Msg={Msg}",
                callback.ConversationId, result.ErrorMessage);
            var errorCode = Uri.EscapeDataString(result.ErrorCode ?? "PAYMENT_FAILED");
            frontendUrl = $"{FrontendBaseUrl}/payment/failed?cid={callback.ConversationId}&errorCode={errorCode}";
        }

        return Content(
            $"<html><head><script>window.location.href='{frontendUrl}';</script></head><body>Yönlendiriliyorsunuz...</body></html>",
            "text/html");
    }

    // ─── GET /api/payment/morpara/callback ────────────────────────────────────
    // Mor Para returnUrl'e GET ile orderId query param gönderiyor
    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> CallbackGet([FromQuery] string? orderId, [FromQuery] string? conversationId)
    {
        _logger.LogInformation("📩 MORPARA CALLBACK GET | OrderId={OrderId} | ConvId={ConvId}",
            orderId, conversationId);

        // Mor Para'nın orderId'si ile CheckPayment yapıyoruz
        var queryId = orderId ?? conversationId ?? "";

        var callback = new MorparaCallbackDto
        {
            ConversationId = queryId,
            OrderId        = orderId ?? "",
        };

        var result = await _paymentService.ProcessCallbackAsync(callback);

        string frontendUrl;
        if (result.Success)
        {
            frontendUrl = $"{FrontendBaseUrl}/payment/success?cid={queryId}";
        }
        else
        {
            _logger.LogError("❌ Mor Para GET callback başarısız | OrderId={Id} | Msg={Msg}",
                queryId, result.ErrorMessage);
            var errorCode = Uri.EscapeDataString(result.ErrorCode ?? "PAYMENT_FAILED");
            frontendUrl = $"{FrontendBaseUrl}/payment/failed?cid={queryId}&errorCode={errorCode}";
        }

        return Redirect(frontendUrl);
    }

    // ─── POST /api/payment/morpara/verify ─────────────────────────────────────
    [HttpPost("verify")]
    [AllowAnonymous]
    public async Task<IActionResult> Verify([FromBody] MorparaVerifyRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.ConversationId))
            return BadRequest(new { error = "conversationId gereklidir" });

        // Gelen conversationId'yi de temizle
        var cleanConvId = body.ConversationId.Contains('?')
            ? body.ConversationId.Split('?')[0]
            : body.ConversationId;

        var result = await _paymentService.VerifyAndProcessPaymentAsync(cleanConvId);

        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage });

        return Ok(new
        {
            success            = true,
            orderId            = cleanConvId,
            isAlreadyProcessed = result.IsAlreadyProcessed,
            creditsAdded       = result.CreditsAdded,
            packageName        = result.PackageName,
            membershipEnd      = result.MembershipEnd,
            userId             = result.UserId
        });
    }

    // ─── POST /api/payment/morpara/checkpayment ───────────────────────────────
    [HttpPost("checkpayment")]
    [Authorize]
    public async Task<IActionResult> CheckPayment([FromBody] MorparaCheckPaymentRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.ConversationId))
            return BadRequest(new { error = "conversationId gereklidir" });

        var result = await _paymentService.CheckPaymentAsync(body.ConversationId);
        if (result == null)
            return NotFound(new { error = "Ödeme sorgulanamadı" });

        return Ok(result);
    }
}

// ─── Request DTO'ları ─────────────────────────────────────────────────────────
public class MorparaInitRequest
{
    public string? ProductCode  { get; set; }
    public int     Installment  { get; set; } = 1;
    public string? DiscountCode { get; set; }
    public string  Currency     { get; set; } = "TRY";
}

public class MorparaVerifyRequest
{
    public string? ConversationId { get; set; }
}

public class MorparaCheckPaymentRequest
{
    public string? ConversationId { get; set; }
}