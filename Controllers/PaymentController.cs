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
            // Gelen tüm verileri logla
            _logger.LogInformation("🔔 === TOSLA CALLBACK START ===");
            _logger.LogInformation("📋 OrderId: {OrderId}", callback.OrderId ?? "(null)");
            _logger.LogInformation("📋 TransactionId: {TxId}", callback.TransactionId ?? "(null)");
            _logger.LogInformation("📋 Code: {Code}", callback.Code);
            _logger.LogInformation("📋 Message: {Msg}", callback.Message ?? "(null)");
            _logger.LogInformation("📋 BankResponseCode: {BankCode}", callback.BankResponseCode ?? "(null)");
            _logger.LogInformation("📋 BankResponseMessage: {BankMsg}", callback.BankResponseMessage ?? "(null)");
            _logger.LogInformation("📋 Amount: {Amount}", callback.Amount);
            _logger.LogInformation("📋 Echo: {Echo}", callback.Echo ?? "(null)");
            _logger.LogInformation("📋 ExtraParameters: {Extra}", callback.ExtraParameters ?? "(null)");
            _logger.LogInformation("📋 AuthCode: {Auth}", callback.AuthCode ?? "(null)");
            _logger.LogInformation("📋 MdStatus: {MdStatus}", callback.MdStatus ?? "(null)");
            _logger.LogInformation("📋 RequestStatus: {ReqStatus}", callback.RequestStatus);
            _logger.LogInformation("🔔 === TOSLA CALLBACK END ===");

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

    /// <summary>
    /// Ödeme doğrulama - Frontend'den tetiklenir, Tosla'dan ödeme durumunu sorgular
    /// </summary>
    [HttpPost("verify/{orderId}")]
    [AllowAnonymous] // Kullanıcı ödeme sonrası logout olabilir
    [ProducesResponseType(typeof(PaymentVerificationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentVerificationResponseDto>> VerifyPayment(string orderId)
    {
        try
        {
            _logger.LogInformation("🔍 Ödeme doğrulama isteği alındı | OrderId: {OrderId}", orderId);

            // Tosla'dan ödeme durumunu sorgula
            var result = await _toslaPaymentService.VerifyAndProcessPaymentAsync(orderId);

            if (result.Success)
            {
                _logger.LogInformation("✅ Ödeme doğrulandı ve işlendi | OrderId: {OrderId} | Credits: {Credits}", 
                    orderId, result.CreditsAdded);
                
                return Ok(new PaymentVerificationResponseDto
                {
                    Success = true,
                    Message = "Ödeme başarıyla doğrulandı ve krediler yüklendi",
                    OrderId = orderId,
                    CreditsAdded = result.CreditsAdded,
                    PackageName = result.PackageName,
                    IsAlreadyProcessed = result.IsAlreadyProcessed
                });
            }

            _logger.LogWarning("❌ Ödeme doğrulanamadı | OrderId: {OrderId} | Error: {Error}", 
                orderId, result.ErrorMessage);
            
            return BadRequest(new PaymentVerificationResponseDto
            {
                Success = false,
                Message = result.ErrorMessage ?? "Ödeme doğrulanamadı",
                OrderId = orderId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ödeme doğrulama hatası | OrderId: {OrderId}", orderId);
            return StatusCode(500, new PaymentVerificationResponseDto
            {
                Success = false,
                Message = "Ödeme doğrulanırken bir hata oluştu",
                OrderId = orderId
            });
        }
    }
}