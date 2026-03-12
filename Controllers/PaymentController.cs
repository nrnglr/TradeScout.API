using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TradeScout.API.Models.Payment;
using TradeScout.API.Services;

namespace TradeScout.API.Controllers;

/// <summary>
/// Payment Controller - Ödeme işlemleri
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class PaymentController : ControllerBase
{
    private readonly IToslaPaymentService _toslaPaymentService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IToslaPaymentService toslaPaymentService,
        ILogger<PaymentController> logger)
    {
        _toslaPaymentService = toslaPaymentService;
        _logger = logger;
    }

    /// <summary>
    /// Mevcut paketleri listele
    /// </summary>
    [HttpGet("packages")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<FgsTradePackage>), StatusCodes.Status200OK)]
    public ActionResult<List<FgsTradePackage>> GetPackages()
    {
        var packages = _toslaPaymentService.GetAvailablePackages();
        return Ok(packages);
    }

    /// <summary>
    /// Ödeme başlat - Tosla ödeme URL'i al
    /// </summary>
    [HttpPost("initialize")]
    [Authorize]
    [ProducesResponseType(typeof(ToslaPaymentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ToslaPaymentResponseDto>> InitializePayment([FromBody] ToslaPaymentRequestDto request)
    {
        try
        {
            // Kullanıcı ID'sini JWT'den al
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? User.FindFirst("sub")?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized(new { message = "Kullanıcı kimliği doğrulanamadı" });
            }

            // Request'e kullanıcı ID'sini ekle
            request.UserId = userIdClaim;

            _logger.LogInformation("💳 Ödeme başlatma isteği - UserId: {UserId}, ProductCode: {ProductCode}",
                request.UserId, request.ProductCode);

            // Ödeme başlat
            var result = await _toslaPaymentService.InitializePaymentAsync(request);

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
    /// Tosla callback endpoint - Ödeme sonucu buraya gelir
    /// </summary>
    [HttpPost("callback")]
    [AllowAnonymous] // Tosla'dan gelecek, auth yok
    [Consumes("application/x-www-form-urlencoded", "application/json", "multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PaymentCallback([FromForm] ToslaCallbackDto callback)
    {
        try
        {
            _logger.LogInformation("🔔 Tosla callback alındı - OrderId: {OrderId} | TransactionId: {TxId} | Code: {Code} | BankCode: {BankCode} | Amount: {Amount}",
                callback.OrderId, callback.TransactionId, callback.Code, callback.BankResponseCode, callback.Amount);

            var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "https://fgstrade.com";

            var success = await _toslaPaymentService.ProcessCallbackAsync(callback);

            if (success)
            {
                _logger.LogInformation("✅ Callback işlendi - SUCCESS | OrderId: {OrderId}", callback.OrderId);
                
                // Tosla'ya OK yanıtı döndür (önemli!)
                // Eğer redirect gerekiyorsa, önce OK döndürüp sonra JavaScript ile yönlendir
                // Ama genelde Tosla kendi 3D Secure sayfasından kullanıcıyı yönlendirir
                
                // Ödeme başarılı → kullanıcıyı success sayfasına yönlendir
                return Redirect($"{frontendUrl}/payment/success?orderId={callback.OrderId}");
            }

            _logger.LogWarning("❌ Callback işlendi - FAILED | OrderId: {OrderId} | Code: {Code}", 
                callback.OrderId, callback.BankResponseCode);
            
            // Ödeme başarısız → failed sayfasına yönlendir
            return Redirect($"{frontendUrl}/payment/failed?orderId={callback.OrderId}&errorCode={callback.BankResponseCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Callback işleme hatası | OrderId: {OrderId}", callback?.OrderId);
            var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "https://fgstrade.com";
            return Redirect($"{frontendUrl}/payment/failed?error=system");
        }
    }

    /// <summary>
    /// Ödeme başarılı sayfası - Kullanıcı ödeme sonrası buraya yönlendirilir
    /// </summary>
    [HttpGet("success")]
    [AllowAnonymous]
    public IActionResult PaymentSuccess([FromQuery] string? orderId, [FromQuery] string? transactionId)
    {
        _logger.LogInformation("✅ Ödeme başarılı sayfası - OrderId: {OrderId}", orderId);
        
        // Frontend'e yönlendir
        var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "https://fgstrade.com";
        return Redirect($"{frontendUrl}/payment/success?orderId={orderId}&transactionId={transactionId}");
    }

    /// <summary>
    /// Ödeme başarısız sayfası
    /// </summary>
    [HttpGet("failed")]
    [AllowAnonymous]
    public IActionResult PaymentFailed([FromQuery] string? orderId, [FromQuery] string? error)
    {
        _logger.LogWarning("❌ Ödeme başarısız - OrderId: {OrderId}, Error: {Error}", orderId, error);
        
        // Frontend'e yönlendir
        var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "https://fgstrade.com";
        return Redirect($"{frontendUrl}/payment/failed?orderId={orderId}&error={error}");
    }
}