using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradeScout.API.Data;
using TradeScout.API.Models;
using TradeScout.API.Models.Payment;

namespace TradeScout.API.Services;

// ─── Interface ────────────────────────────────────────────────────────────────
public interface IMorparaPaymentService
{
    Task<MorparaPaymentResponseDto> InitializePaymentAsync(MorparaPaymentRequestDto request);
    Task<MorparaCallbackResult> ProcessCallbackAsync(MorparaCallbackDto callback);
    Task<MorparaCheckPaymentResponseDto?> CheckPaymentAsync(string conversationId);
    Task<PaymentVerificationResult> VerifyAndProcessPaymentAsync(string conversationId);
    List<FgsTradePackage> GetAvailablePackages();
}

// ─── DTO'lar ──────────────────────────────────────────────────────────────────
public class MorparaPaymentRequestDto
{
    public string ProductCode { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public int Installment { get; set; } = 1;
    public string? DiscountCode { get; set; }
}

public class MorparaPaymentResponseDto
{
    public bool Success { get; set; }
    public string? PaymentUrl { get; set; }
    public string? ConversationId { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
}

public class MorparaCallbackDto
{
    public string? ConversationId { get; set; }
    public string? OrderId { get; set; }
    public string? PaymentId { get; set; }
    public string? ResponseCode { get; set; }
    public string? ResponseDesc { get; set; }
    public string? MerchantId { get; set; }
}

public class MorparaCallbackResult
{
    public bool Success { get; set; }
    public string? ConversationId { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
}

public class MorparaCheckPaymentResponseDto
{
    public string? ResponseCode { get; set; }
    public string? ResponseDescription { get; set; }
    public string? ConversationId { get; set; }
    public string? OrderId { get; set; }
    public string? PaymentId { get; set; }
    public string? TransactionStatus { get; set; }
    public string? TransactionType { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public int InstallmentCount { get; set; }
}

// ─── Service ──────────────────────────────────────────────────────────────────
public class MorparaPaymentService : IMorparaPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MorparaPaymentService> _logger;
    private readonly ApplicationDbContext _dbContext;

    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _merchantId;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _callbackUrl;

    private readonly List<FgsTradePackage> _packages = new()
    {
        new() { ProductCode="1274715", Alias="starter_monthly",  Name="Starter",         NameTr="Başlangıç",         PriceUsd=15m,  PriceTry=1m,   Credits=10,  DurationDays=30,  MaxInstallment=1,  IsYearly=false, IsCredit=false },
        new() { ProductCode="1274739", Alias="pro_monthly",      Name="Pro",              NameTr="Profesyonel",       PriceUsd=39m,  PriceTry=1m,  Credits=40,  DurationDays=30,  MaxInstallment=1,  IsYearly=false, IsCredit=false },
        new() { ProductCode="1274779", Alias="business_monthly", Name="Business",         NameTr="İş",                PriceUsd=79m,  PriceTry=1m,  Credits=100, DurationDays=30,  MaxInstallment=1,  IsYearly=false, IsCredit=false },
        new() { ProductCode="1274716", Alias="starter_yearly",   Name="Starter Yıllık",  NameTr="Başlangıç Yıllık",  PriceUsd=99m,  PriceTry=4257m,  Credits=10,  DurationDays=365, MaxInstallment=12, IsYearly=true,  IsCredit=false },
        new() { ProductCode="1274740", Alias="pro_yearly",       Name="Pro Yıllık",       NameTr="Profesyonel Yıllık",PriceUsd=299m, PriceTry=12857m, Credits=40,  DurationDays=365, MaxInstallment=12, IsYearly=true,  IsCredit=false },
        new() { ProductCode="1274780", Alias="business_yearly",  Name="Business Yıllık",  NameTr="İş Yıllık",         PriceUsd=599m, PriceTry=25757m, Credits=100, DurationDays=365, MaxInstallment=12, IsYearly=true,  IsCredit=false },
        new() { ProductCode="1274710", Alias="credit_10",        Name="10 Kredi",         NameTr="10 Ekstra Kredi",   PriceUsd=10m,  PriceTry=430m,   Credits=10,  DurationDays=0,   MaxInstallment=1,  IsYearly=false, IsCredit=true  },
        new() { ProductCode="1274725", Alias="credit_25",        Name="25 Kredi",         NameTr="25 Ekstra Kredi",   PriceUsd=20m,  PriceTry=860m,   Credits=25,  DurationDays=0,   MaxInstallment=1,  IsYearly=false, IsCredit=true  },
        new() { ProductCode="1274750", Alias="credit_50",        Name="50 Kredi",         NameTr="50 Ekstra Kredi",   PriceUsd=35m,  PriceTry=1505m,  Credits=50,  DurationDays=0,   MaxInstallment=1,  IsYearly=false, IsCredit=true  },
        new() { ProductCode="1247100", Alias="credit_100",       Name="100 Kredi",        NameTr="100 Ekstra Kredi",  PriceUsd=60m,  PriceTry=2580m,  Credits=100, DurationDays=0,   MaxInstallment=1,  IsYearly=false, IsCredit=true  },
    };

    public MorparaPaymentService(
        HttpClient httpClient,
        ILogger<MorparaPaymentService> logger,
        ApplicationDbContext dbContext)
    {
        _httpClient = httpClient;
        _logger = logger;
        _dbContext = dbContext;

        _clientId = Env("MORPARA_CLIENT_ID", "");
        _clientSecret = Env("MORPARA_CLIENT_SECRET", "");
        _merchantId = Env("MORPARA_MERCHANT_ID", "");
        _apiKey = Env("MORPARA_API_KEY", "");
        _baseUrl = Env("MORPARA_BASE_URL", "https://sale-gateway.morpara.com");
        _callbackUrl = Env("MORPARA_CALLBACK_URL", "https://api.fgstrade.com/api/payment/morpara/callback");
    }

    public List<FgsTradePackage> GetAvailablePackages() => _packages;

    // ═════════════════════════════════════════════════════════════════════════
    // ADIM 1: ÖDEME BAŞLATMA
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<MorparaPaymentResponseDto> InitializePaymentAsync(MorparaPaymentRequestDto request)
    {
        try
        {
            _logger.LogInformation("💳 MORPARA ÖDEME BAŞLATILIYOR | UserId={Uid} | Product={Prod}",
                request.UserId, request.ProductCode);

            var package = FindPackage(request.ProductCode);
            if (package == null)
                return Fail("Geçersiz paket kodu", "INVALID_PRODUCT");

            var conversationId = BuildConversationId(request.UserId);

            decimal finalPrice = package.PriceTry;
            decimal discountPercent = 0;
            if (!string.IsNullOrEmpty(request.DiscountCode))
            {
                var dc = await _dbContext.DiscountCodes
                    .FirstOrDefaultAsync(d => d.Code == request.DiscountCode && d.IsActive);
                if (dc != null)
                {
                    discountPercent = dc.DiscountPercentage;
                    finalPrice = Math.Round(package.PriceTry * (1 - discountPercent / 100), 2);
                }
            }

            var amountStr = finalPrice.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            var frontendBase = Env("FRONTEND_URL", "https://fgstrade.com");
            var returnUrl = $"{frontendBase}/payment/success?cid={conversationId}";
            var failUrl = $"{frontendBase}/payment/failed?cid={conversationId}";
            var installmentInt = request.Installment <= 1 ? 0 : request.Installment;
            var installmentStr = installmentInt.ToString();

            // Sign: Mor Para'nın belirlediği kesin sıra
            // ConversationId;MerchantId;ReturnUrl;FailUrl;PaymentMethod;Language;
            // PaymentInstrumentType;TransactionType;VftFlag;InstallmentCount;Amount;CurrencyCode;PFSubMerchantId;ApiKey
            var signValues = new List<string>
            {
                conversationId,
                _merchantId,
                returnUrl,
                failUrl,
                "HOSTEDPAYMENT",
                "tr",
                "CARD",
                "SALE",
                "False",         // VftFlag — büyük F zorunlu
                installmentStr,
                amountStr,
                "949",
                _merchantId,     // PFSubMerchantId
                _apiKey          // d2VydHl1YXNkZmdoamts (maildeki değer)
            };
            var sign = CalculateDynamicSign(signValues);

            // Payload — apiKey body'e de eklendi
            var payload = new
            {
                merchantId = _merchantId,
                returnUrl = returnUrl,
                failUrl = failUrl,
                callbackUrl = _callbackUrl,
                paymentMethod = "HOSTEDPAYMENT",
                paymentInstrumentType = "CARD",
                language = "tr",
                conversationId = conversationId,
                apiKey = _apiKey,
                sign = sign,
                transactionDetails = new
                {
                    transactionType = "SALE",
                    installmentCount = installmentInt,
                    amount = amountStr,
                    currencyCode = "949",
                    vftFlag = false
                },
                extraParameter = new
                {
                    pFSubMerchantId = _merchantId
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("📤 MORPARA istek | ConvId={Id} | Body={Body}", conversationId, json);

            var requestMessage = new HttpRequestMessage(HttpMethod.Post,
                $"{_baseUrl}/v1/HostedPayment/HostedPaymentRedirect")
            { Content = content };
            AddMorparaHeaders(requestMessage);

            var response = await _httpClient.SendAsync(requestMessage);
            var rawBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("📥 MORPARA yanıt | Status={S} | Body={B}",
                (int)response.StatusCode, rawBody.Length > 1000 ? rawBody[..1000] : rawBody);

            if (!response.IsSuccessStatusCode)
                return Fail($"Mor Para API hatası: {(int)response.StatusCode} | {rawBody}", "API_ERROR");

            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            var paymentUrl = GetStr(root, "returnUrl");
            var respCode = GetStr(root, "responseCode") ?? GetStr(root, "code");

            if (string.IsNullOrEmpty(paymentUrl))
            {
                var errMsg = GetStr(root, "responseDescription") ?? GetStr(root, "message") ?? "Bilinmeyen hata";
                _logger.LogWarning("❌ MORPARA başarısız | Code={Code} | Msg={Msg}", respCode, errMsg);
                return Fail(errMsg, respCode ?? "UNKNOWN");
            }

            await SavePendingPaymentAsync(conversationId, request.UserId, package,
                finalPrice, request.DiscountCode, discountPercent);

            _logger.LogInformation("✅ MORPARA başarılı | ConvId={Id} | Url={Url}", conversationId, paymentUrl);

            return new MorparaPaymentResponseDto
            {
                Success = true,
                PaymentUrl = paymentUrl,
                ConversationId = conversationId,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ MORPARA InitializePayment hatası | UserId={Uid}", request.UserId);
            return Fail("Sistem hatası: " + ex.Message, "SYSTEM_ERROR");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // ADIM 2a: CALLBACK İŞLEME
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<MorparaCallbackResult> ProcessCallbackAsync(MorparaCallbackDto callback)
    {
        try
        {
            _logger.LogInformation("📩 MORPARA CALLBACK | ConvId={Id} | Code={Code}",
                callback.ConversationId, callback.ResponseCode);

            if (string.IsNullOrEmpty(callback.ConversationId))
                return new MorparaCallbackResult { Success = false, ErrorMessage = "ConversationId eksik" };

            var checkResult = await CheckPaymentAsync(callback.ConversationId);

            if (checkResult == null)
                return new MorparaCallbackResult
                {
                    Success = false,
                    ConversationId = callback.ConversationId,
                    ErrorMessage = "Ödeme durumu doğrulanamadı"
                };

            bool isApproved = checkResult.ResponseCode == "B0000"
                           && checkResult.ResponseDescription == "Approved";

            if (!isApproved)
            {
                await SaveFailedPaymentAsync(callback.ConversationId,
                    $"{checkResult.ResponseCode}: {checkResult.ResponseDescription}");
                return new MorparaCallbackResult
                {
                    Success = false,
                    ConversationId = callback.ConversationId,
                    ErrorCode = checkResult.ResponseCode,
                    ErrorMessage = checkResult.ResponseDescription
                };
            }

            await ActivateMembershipAsync(callback.ConversationId, checkResult);
            return new MorparaCallbackResult { Success = true, ConversationId = callback.ConversationId };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ ProcessCallback hatası | ConvId={Id}", callback.ConversationId);
            return new MorparaCallbackResult { Success = false, ErrorMessage = "Sistem hatası: " + ex.Message };
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // ADIM 2b: CHECKPAYMENT
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<MorparaCheckPaymentResponseDto?> CheckPaymentAsync(string conversationId)
    {
        try
{
    _logger.LogInformation("🔍 CheckPayment | ConvId={Id}", conversationId);

    // DİKKAT: decodedApiKey satırını tamamen kaldırdık!
    // Doğrudan _apiKey kullanıyoruz. Sıralama: merchantId, conversationId, apiKey
    var sign = CalculateDynamicSign(new List<string> { _merchantId, conversationId, _apiKey });

    var payload = new 
    { 
        merchantId = _merchantId, 
        conversationId = conversationId, 
        apiKey = _apiKey,
        sign = sign 
    };

    var json = JsonSerializer.Serialize(payload);
    var content = new StringContent(json, Encoding.UTF8, "application/json");

    var requestMessage = new HttpRequestMessage(HttpMethod.Post,
        $"{_baseUrl}/v1/Payment/CheckPayment")
    { Content = content };

    AddMorparaHeaders(requestMessage);

    // ... (Kodun geri kalanı aynı kalacak: httpClient.SendAsync vs.)
            AddMorparaHeaders(requestMessage);

            var response = await _httpClient.SendAsync(requestMessage);
            var rawBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("📥 CheckPayment yanıt | Status={S} | Body={B}",
                (int)response.StatusCode, rawBody.Length > 800 ? rawBody[..800] : rawBody);

            if (!response.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            var result = new MorparaCheckPaymentResponseDto
            {
                ResponseCode = GetStr(root, "responseCode"),
                ResponseDescription = GetStr(root, "responseDescription"),
                ConversationId = GetStr(root, "conversationId"),
                OrderId = GetStr(root, "orderId"),
                PaymentId = GetStr(root, "paymentId"),
                TransactionStatus = GetStr(root, "transactionStatus"),
                TransactionType = GetStr(root, "transactionType"),
            };

            if (root.TryGetProperty("paymentInfo", out var payInfo))
            {
                if (payInfo.TryGetProperty("amount", out var amtEl))
                    result.Amount = amtEl.ValueKind == JsonValueKind.Number ? amtEl.GetDecimal() : 0;
                if (payInfo.TryGetProperty("currency", out var curEl))
                    result.Currency = curEl.GetString();
                if (payInfo.TryGetProperty("installmentCount", out var instEl))
                    result.InstallmentCount = instEl.ValueKind == JsonValueKind.Number ? instEl.GetInt32() : 1;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ CheckPayment hatası | ConvId={Id}", conversationId);
            return null;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // FRONTEND TETİKLEMELİ DOĞRULAMA
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<PaymentVerificationResult> VerifyAndProcessPaymentAsync(string conversationId)
    {
        try
        {
            var existing = await _dbContext.PaymentHistories
                .Where(p => p.OrderId == conversationId && p.Status == "SUCCESS")
                .FirstOrDefaultAsync();

            if (existing != null)
                return new PaymentVerificationResult
                {
                    Success = true,
                    IsAlreadyProcessed = true,
                    CreditsAdded = existing.CreditsAdded,
                    PackageName = existing.PackageName,
                    UserId = existing.UserId
                };

            var checkResult = await CheckPaymentAsync(conversationId);
            if (checkResult == null)
                return new PaymentVerificationResult { Success = false, ErrorMessage = "Ödeme sorgulanamadı" };

            bool isApproved = checkResult.ResponseCode == "B0000"
                           && checkResult.ResponseDescription == "Approved";

            if (!isApproved)
                return new PaymentVerificationResult
                {
                    Success = false,
                    ErrorMessage = $"Ödeme başarısız: {checkResult.ResponseDescription}"
                };

            await ActivateMembershipAsync(conversationId, checkResult);

            var payment = await _dbContext.PaymentHistories
                .Where(p => p.OrderId == conversationId && p.Status == "SUCCESS")
                .FirstOrDefaultAsync();

            if (payment == null)
                return new PaymentVerificationResult { Success = false, ErrorMessage = "Kayıt bulunamadı" };

            var user = await _dbContext.Users.FindAsync(payment.UserId);
            return new PaymentVerificationResult
            {
                Success = true,
                IsAlreadyProcessed = false,
                CreditsAdded = payment.CreditsAdded,
                PackageName = payment.PackageName ?? "Bilinmeyen",
                UserId = payment.UserId,
                MembershipEnd = user?.MembershipEnd
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ VerifyAndProcess hatası | ConvId={Id}", conversationId);
            return new PaymentVerificationResult { Success = false, ErrorMessage = "Sistem hatası: " + ex.Message };
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // AKTİVASYON
    // ═════════════════════════════════════════════════════════════════════════

    private async Task ActivateMembershipAsync(string conversationId, MorparaCheckPaymentResponseDto checkResult)
    {
        try
        {
            var (userId, productCode) = ExtractFromConversationId(conversationId);

            var pending = await _dbContext.PaymentHistories
                .Where(p => p.OrderId == conversationId && p.Status == "PENDING")
                .FirstOrDefaultAsync();

            if (pending != null && pending.UserId > 0) userId = pending.UserId;

            if (!string.IsNullOrEmpty(pending?.PackageName))
            {
                var matched = _packages.FirstOrDefault(p =>
                    p.Name == pending.PackageName || p.NameTr == pending.PackageName);
                if (matched != null) productCode = matched.ProductCode;
            }

            var package = FindPackage(productCode ?? "");
            if (package == null || userId <= 0) return;

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return;

            user.Credits += package.Credits;

            if (!package.IsCredit && package.DurationDays > 0)
            {
                var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
                var baseDate = user.MembershipEnd.HasValue && user.MembershipEnd.Value > now
                    ? user.MembershipEnd.Value : now;
                user.MembershipEnd = DateTime.SpecifyKind(baseDate.AddDays(package.DurationDays), DateTimeKind.Utc);
                user.PackageType = package.Name;
                user.MembershipStart = now;
                user.MaxResultsPerSearch = Math.Max(user.MaxResultsPerSearch, 200);
            }

            if (pending != null)
            {
                pending.Status = "SUCCESS";
                pending.PaymentDate = DateTime.UtcNow;
                pending.TransactionId = checkResult.PaymentId ?? checkResult.OrderId ?? "";
            }
            else
            {
                _dbContext.PaymentHistories.Add(new PaymentHistory
                {
                    UserId = userId,
                    OrderId = conversationId,
                    TransactionId = checkResult.PaymentId ?? checkResult.OrderId ?? "",
                    ProductCode = package.ProductCode,
                    PackageName = package.Name,
                    Amount = checkResult.Amount,
                    FinalAmount = checkResult.Amount,
                    Currency = "TRY",
                    Status = "SUCCESS",
                    CreditsAdded = package.Credits,
                    PaymentDate = DateTime.UtcNow,
                });
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("🎉 AKTİVASYON TAMAMLANDI | UserId={Uid} | Credits={Cred}",
                userId, package.Credits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ ActivateMembership hatası | ConvId={Id}", conversationId);
            throw;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // YARDIMCI METODLAR
    // ═════════════════════════════════════════════════════════════════════════

    private static string CalculateDynamicSign(List<string> values)
    {
        var concatenated = string.Join(";", values);
        if (string.IsNullOrWhiteSpace(concatenated)) return string.Empty;
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(concatenated));
        return Convert.ToBase64String(hashBytes).ToUpperInvariant();
    }

    private static string GenerateClientSecretHash(string clientSecret, string timestamp)
    {
        var decodedSecret = Encoding.UTF8.GetString(Convert.FromBase64String(clientSecret));
        var combined = decodedSecret + timestamp;
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
        var hexHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(hexHash));
    }

    private void AddMorparaHeaders(HttpRequestMessage request)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var encodedSecret = GenerateClientSecretHash(_clientSecret, timestamp);

        request.Headers.Add("x-ClientID", _clientId);
        request.Headers.Add("x-ClientSecret", encodedSecret);
        request.Headers.Add("x-GrantType", "client_credentials");
        request.Headers.Add("x-Scope", "pf_write, pf_read");
        request.Headers.Add("x-Timestamp", timestamp);
        request.Headers.Add("Accept", "application/json");
        if (!request.Headers.Contains("User-Agent"))
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
    }

    private static string BuildConversationId(string userId)
    {
        var datePart = DateTime.UtcNow.ToString("yyyyMMddHHmm");
        var userPart = userId.Length > 5 ? userId[^5..] : userId.PadLeft(5, '0');
        var result = $"FGS{datePart}{userPart}";
        return result.Length > 20 ? result[..20] : result.PadRight(20, '0');
    }

    private static (int userId, string? productCode) ExtractFromConversationId(string? convId)
    {
        if (string.IsNullOrEmpty(convId) || !convId.StartsWith("FGS") || convId.Length < 18)
            return (0, null);
        var userPart = convId.Substring(15, Math.Min(5, convId.Length - 15)).TrimStart('0');
        int.TryParse(userPart, out int userId);
        return (userId, null);
    }

    private async Task SavePendingPaymentAsync(string conversationId, string userId,
        FgsTradePackage package, decimal amount, string? discountCode, decimal discountPercent)
    {
        try
        {
            if (!int.TryParse(userId, out int uid)) return;
            _dbContext.PaymentHistories.Add(new PaymentHistory
            {
                UserId = uid,
                OrderId = conversationId,
                TransactionId = "",
                ProductCode = package.ProductCode,
                PackageName = package.Name,
                Amount = amount,
                FinalAmount = amount,
                Currency = "TRY",
                Status = "PENDING",
                CreditsAdded = package.Credits,
                PaymentDate = DateTime.UtcNow,
                DiscountCode = discountCode,
            });
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Pending kayıt hatası | ConvId={Id}", conversationId);
        }
    }

    private async Task SaveFailedPaymentAsync(string conversationId, string errorMessage)
    {
        try
        {
            var pending = await _dbContext.PaymentHistories
                .Where(p => p.OrderId == conversationId && p.Status == "PENDING")
                .FirstOrDefaultAsync();

            if (pending != null)
            {
                pending.Status = "FAILED";
                pending.ErrorMessage = errorMessage;
                pending.PaymentDate = DateTime.UtcNow;
            }
            else
            {
                var (userId, _) = ExtractFromConversationId(conversationId);
                if (userId > 0)
                    _dbContext.PaymentHistories.Add(new PaymentHistory
                    {
                        UserId = userId,
                        OrderId = conversationId,
                        TransactionId = "",
                        Amount = 0,
                        Currency = "TRY",
                        Status = "FAILED",
                        PaymentDate = DateTime.UtcNow,
                        ErrorMessage = errorMessage,
                    });
            }
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveFailedPayment hatası | ConvId={Id}", conversationId);
        }
    }

    private FgsTradePackage? FindPackage(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        return _packages.FirstOrDefault(p =>
            p.ProductCode == code ||
            p.Alias.Equals(code, StringComparison.OrdinalIgnoreCase) ||
            p.Name.Equals(code, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetStr(JsonElement el, params string[] keys)
    {
        foreach (var k in keys)
            if (el.TryGetProperty(k, out var v))
                return v.ValueKind == JsonValueKind.String ? v.GetString()
                     : v.ValueKind == JsonValueKind.Number ? v.GetDecimal().ToString() : null;
        return null;
    }

    private static string Env(string key, string fallback)
        => (Environment.GetEnvironmentVariable(key) ?? fallback).Trim();

    private static MorparaPaymentResponseDto Fail(string msg, string code)
        => new() { Success = false, ErrorMessage = msg, ErrorCode = code };
}