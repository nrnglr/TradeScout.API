using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TradeScout.API.Data;
using TradeScout.API.Models;
using TradeScout.API.Models.Payment;

namespace TradeScout.API.Services;

public interface IToslaPaymentService
{
    Task<ToslaPaymentResponseDto> InitializePaymentAsync(ToslaPaymentRequestDto request);
    Task<bool> ProcessCallbackAsync(ToslaCallbackDto callback);
    List<FgsTradePackage> GetAvailablePackages();
    Task<ToslaInquiryResponseDto?> InquiryPaymentAsync(string orderId);
}

/// <summary>
/// FGSTrade Paket Yapısı:
///
/// AYLIK (taksit yok):
///   1274715  Starter Monthly   → $15/ay
///   1274739  Pro Monthly       → $39/ay
///   1274779  Business Monthly  → $79/ay
///
/// YILLIK (9 veya 12 taksit seçeneği var):
///   1274716  Starter Annual    → $99/yıl   (aylık $15 yerine %45 indirim)
///   1274740  Pro Annual        → $299/yıl  (aylık $39 yerine %36 indirim)
///   1274780  Business Annual   → $599/yıl  (aylık $79 yerine %37 indirim)
///
/// EXTRA KREDİ (tek seferlik, taksit yok):
///   1274710  10 Kredi  Extra
///   1274725  25 Kredi  Extra
///   1274750  50 Kredi  Extra
///   1247100  100 Kredi Extra
/// </summary>
public class ToslaPaymentService : IToslaPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ToslaPaymentService> _logger;
    private readonly ApplicationDbContext _dbContext;

    private readonly long   _clientId;
    private readonly string _apiUser;
    private readonly string _apiPass;
    private readonly string _baseUrl;
    private readonly string _callbackUrl;

    // ─── Paket listesi ────────────────────────────────────────────────────────
    private readonly List<FgsTradePackage> _packages = new()
    {
        // ── Aylık paketler (taksit yok, MaxInstallment=1) ────────────────────
        new()
        {
            ProductCode    = "1274715",
            Alias          = "starter_monthly",
            Name           = "Starter",
            NameTr         = "Başlangıç",
            PriceUsd       = 15m,
            PriceTry       = 1m,        // ⚠️ TEST FİYATI: 1 TL — canlıya geçince 525m yap
            Credits        = 0,
            DurationDays   = 30,
            MaxInstallment = 1,
            IsYearly       = false,
            IsCredit       = false,
            Description    = "Starter Aylık Üyelik - TEST 1TL"
        },
        new()
        {
            ProductCode    = "1274739",
            Alias          = "pro_monthly",
            Name           = "Pro",
            NameTr         = "Profesyonel",
            PriceUsd       = 39m,
            PriceTry       = 1365m,
            Credits        = 0,
            DurationDays   = 30,
            MaxInstallment = 1,
            IsYearly       = false,
            IsCredit       = false,
            Description    = "Pro Aylık Üyelik - $39/ay"
        },
        new()
        {
            ProductCode    = "1274779",
            Alias          = "business_monthly",
            Name           = "Business",
            NameTr         = "İş",
            PriceUsd       = 79m,
            PriceTry       = 2765m,
            Credits        = 0,
            DurationDays   = 30,
            MaxInstallment = 1,
            IsYearly       = false,
            IsCredit       = false,
            Description    = "Business Aylık Üyelik - $79/ay"
        },

        // ── Yıllık paketler (9 veya 12 taksit seçeneği var) ─────────────────
        new()
        {
            ProductCode    = "1274716",
            Alias          = "starter_yearly",
            Name           = "Starter Yıllık",
            NameTr         = "Başlangıç Yıllık",
            PriceUsd       = 99m,
            PriceTry       = 200m,      // ⚠️ TEST FİYATI: 2 TL — canlıya geçince 3465m yap
            Credits        = 0,
            DurationDays   = 365,
            MaxInstallment = 2,         // TEST: 2 taksit (her biri 1 TL)
            IsYearly       = true,
            IsCredit       = false,
            Description    = "Starter Yıllık Üyelik - TEST 2TL (2×1TL taksit)"
        },
        new()
        {
            ProductCode    = "1274740",
            Alias          = "pro_yearly",
            Name           = "Pro Yıllık",
            NameTr         = "Profesyonel Yıllık",
            PriceUsd       = 299m,
            PriceTry       = 10465m,
            Credits        = 0,
            DurationDays   = 365,
            MaxInstallment = 12,
            IsYearly       = true,
            IsCredit       = false,
            Description    = "Pro Yıllık Üyelik - $299/yıl (%36 indirim)"
        },
        new()
        {
            ProductCode    = "1274780",
            Alias          = "business_yearly",
            Name           = "Business Yıllık",
            NameTr         = "İş Yıllık",
            PriceUsd       = 599m,
            PriceTry       = 20965m,
            Credits        = 0,
            DurationDays   = 365,
            MaxInstallment = 12,
            IsYearly       = true,
            IsCredit       = false,
            Description    = "Business Yıllık Üyelik - $599/yıl (%37 indirim)"
        },

        // ── Extra Kredi paketleri (tek seferlik, taksit yok) ─────────────────
        new()
        {
            ProductCode    = "1274710",
            Alias          = "credit_10",
            Name           = "10 Kredi",
            NameTr         = "10 Ekstra Kredi",
            PriceUsd       = 10m,
            PriceTry       = 1m,        // ⚠️ TEST İÇİN 1 TL - CANLIDA 350m YAPILACAK!
            Credits        = 10,
            DurationDays   = 0,         // Süresiz
            MaxInstallment = 1,
            IsYearly       = false,
            IsCredit       = true,
            Description    = "10 Ekstra Arama Kredisi"
        },
        new()
        {
            ProductCode    = "1274725",
            Alias          = "credit_25",
            Name           = "25 Kredi",
            NameTr         = "25 Ekstra Kredi",
            PriceUsd       = 20m,
            PriceTry       = 700m,
            Credits        = 25,
            DurationDays   = 0,
            MaxInstallment = 1,
            IsYearly       = false,
            IsCredit       = true,
            Description    = "25 Ekstra Arama Kredisi"
        },
        new()
        {
            ProductCode    = "1274750",
            Alias          = "credit_50",
            Name           = "50 Kredi",
            NameTr         = "50 Ekstra Kredi",
            PriceUsd       = 35m,
            PriceTry       = 1225m,
            Credits        = 50,
            DurationDays   = 0,
            MaxInstallment = 1,
            IsYearly       = false,
            IsCredit       = true,
            Description    = "50 Ekstra Arama Kredisi"
        },
        new()
        {
            ProductCode    = "1247100",
            Alias          = "credit_100",
            Name           = "100 Kredi",
            NameTr         = "100 Ekstra Kredi",
            PriceUsd       = 60m,
            PriceTry       = 2100m,
            Credits        = 100,
            DurationDays   = 0,
            MaxInstallment = 1,
            IsYearly       = false,
            IsCredit       = true,
            Description    = "100 Ekstra Arama Kredisi"
        },
    };

    public ToslaPaymentService(
        HttpClient httpClient,
        ILogger<ToslaPaymentService> logger,
        IConfiguration configuration,
        ApplicationDbContext dbContext)
    {
        _httpClient = httpClient;
        _logger     = logger;
        _dbContext  = dbContext;

        var clientIdStr = (Environment.GetEnvironmentVariable("TOSLA_CLIENT_ID")
            ?? configuration["ToslaSettings:ClientId"] ?? "").Trim();
        _clientId = long.TryParse(clientIdStr, out var cid) ? cid : 0;

        _apiUser = (Environment.GetEnvironmentVariable("TOSLA_API_USER")
            ?? configuration["ToslaSettings:ApiUser"] ?? "").Trim();

        _apiPass = (Environment.GetEnvironmentVariable("TOSLA_API_PASS")
            ?? configuration["ToslaSettings:ApiPass"] ?? "").Trim();

        _baseUrl = (Environment.GetEnvironmentVariable("TOSLA_BASE_URL")
            ?? configuration["ToslaSettings:BaseUrl"]
            ?? "https://entegrasyon.tosla.com/api/Payment").Trim().TrimEnd('/');

        _callbackUrl = (Environment.GetEnvironmentVariable("TOSLA_CALLBACK_URL")
            ?? configuration["ToslaSettings:CallbackUrl"]
            ?? "https://api.fgstrade.com/api/payment/callback").Trim();

        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        if (_clientId == 0 || string.IsNullOrEmpty(_apiUser) || string.IsNullOrEmpty(_apiPass))
            _logger.LogError("!!! TOSLA CREDENTIALS EKSİK !!! ClientId={Cid} ApiUser={User}", _clientId, _apiUser);
        else
            _logger.LogInformation("ToslaPaymentService hazır | ClientId={Cid} | BaseUrl={Url}", _clientId, _baseUrl);
    }

    // ─────────────────────────────────────────────────────────────────────────
    public async Task<ToslaPaymentResponseDto> InitializePaymentAsync(ToslaPaymentRequestDto request)
    {
        try
        {
            var package = FindPackage(request.ProductCode);
            if (package is null)
            {
                _logger.LogWarning("Paket bulunamadı: '{Code}'", request.ProductCode);
                return Fail("Geçersiz paket kodu.", "INVALID_PRODUCT");
            }

            // Taksit sayısı belirle
            // - Aylık paket ve kredi → taksit YOK (1)
            // - Yıllık paket → frontend'den gelen installment (max 12)
            int installment = 1;
            if (package.IsYearly && !package.IsCredit)
            {
                installment = Math.Clamp(request.Installment, 1, package.MaxInstallment);
            }

            // Hash parametreleri (GMT+3, tek seferde üret)
            var rnd      = Random.Shared.Next(100000, 999999).ToString();
            var timeSpan = DateTime.UtcNow.AddHours(3).ToString("yyyyMMddHHmmss");
            
            // Tosla Hash Formülü: apiPass + clientId + apiUser + rnd + timeSpan
            // NOT: callbackUrl hash'e DAHİL DEĞİL - sadece body'de gönderilir
            var hashInput = _apiPass + _clientId + _apiUser + rnd + timeSpan;
            var hash = ComputeHash(hashInput);

            _logger.LogInformation(
                "HASH DEBUG | Rnd={Rnd} | TimeSpan={Ts} | ApiPass={Pass} | ClientId={Cid} | ApiUser={User}",
                rnd, timeSpan, _apiPass, _clientId, _apiUser);
            _logger.LogInformation("HASH INPUT: '{Input}'", hashInput);
            _logger.LogInformation("HASH OUTPUT: '{Hash}'", hash);

            // OrderId max 20 karakter
            var ts      = DateTime.UtcNow.AddHours(3).ToString("yyMMddHHmm");
            var uid     = (request.UserId.Length > 7 ? request.UserId[..7] : request.UserId).PadLeft(7, '0');
            var orderId = $"FGS{ts}{uid}";
            if (orderId.Length > 20) orderId = orderId[..20];

            // Tutar: TL kuruşa çevir
            var amountKurus = (long)(package.PriceTry * 100);

            var body = new
            {
                clientId         = _clientId,
                apiUser          = _apiUser,
                rnd              = rnd,
                timeSpan         = timeSpan,
                hash             = hash,
                orderId          = orderId,
                callbackUrl      = _callbackUrl,
                amount           = amountKurus,
                currency         = 949,
                installmentCount = installment,
                description      = $"FGSTrade - {package.NameTr}",
                echo             = $"{request.UserId}|{package.ProductCode}",
                extraParameters  = JsonSerializer.Serialize(new
                {
                    userId      = request.UserId,
                    productCode = package.ProductCode,
                    credits     = package.Credits,
                    isYearly    = package.IsYearly,
                    isCredit    = package.IsCredit,
                    durationDays= package.DurationDays
                })
            };

            var json    = JsonSerializer.Serialize(body);
            _logger.LogInformation("REQUEST BODY: {Json}", json);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url     = $"{_baseUrl}/threeDPayment";

            _logger.LogInformation("POST → {Url} | Paket={Pkg} | Taksit={Inst} | Tutar={Amount} kuruş",
                url, package.Name, installment, amountKurus);

            var response = await _httpClient.PostAsync(url, content);
            var raw      = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Tosla yanıt: HTTP {Status} | {Body}",
                (int)response.StatusCode, raw.Length > 500 ? raw[..500] : raw);

            if (!response.IsSuccessStatusCode)
                return Fail("Ödeme sistemi ile iletişim kurulamadı", ((int)response.StatusCode).ToString());

            using var doc = JsonDocument.Parse(raw);
            var root      = doc.RootElement;
            var code      = GetInt(root, "Code", "code");

            if (code == 0)
            {
                var sessionId     = GetStr(root, "ThreeDSessionId", "threeDSessionId");
                var transactionId = GetStr(root, "TransactionId",   "transactionId");

                if (!string.IsNullOrEmpty(sessionId))
                {
                    var paymentUrl = $"{_baseUrl}/threeDSecure/{sessionId}";
                    _logger.LogInformation("✅ Ödeme URL oluşturuldu | {Url}", paymentUrl);
                    return new ToslaPaymentResponseDto
                    {
                        Success         = true,
                        PaymentUrl      = paymentUrl,
                        TransactionId   = transactionId ?? orderId,
                        ThreeDSessionId = sessionId
                    };
                }
            }

            var msg = GetStr(root, "Message", "message") ?? "Bilinmeyen hata";
            _logger.LogError("Tosla hata | Code={Code} | Msg={Msg}", code, msg);
            return Fail(msg, code.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ödeme başlatma hatası");
            return Fail("Sistem hatası oluştu.", "SYSTEM_ERROR");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    public async Task<bool> ProcessCallbackAsync(ToslaCallbackDto callback)
    {
        try
        {
            _logger.LogInformation("🔔 CALLBACK ALINDI | OrderId={Oid} | Code={Code} | BankCode={Bank} | BankMsg={Msg} | Amount={Amt} | Echo={Echo}",
                callback.OrderId, callback.Code, callback.BankResponseCode, callback.BankResponseMessage, 
                callback.Amount, callback.Echo);

            if (callback.Code == 0 && callback.BankResponseCode == "00")
            {
                _logger.LogInformation("✅ Ödeme BAŞARILI | Aktivasyon başlatılıyor...");
                await ActivateMembershipAsync(callback);
                _logger.LogInformation("✅ Aktivasyon tamamlandı | OrderId={Oid}", callback.OrderId);
                return true;
            }

            _logger.LogWarning("❌ Ödeme BAŞARISIZ | Code={Code} | BankCode={Bank} | BankMsg={Msg}",
                callback.Code, callback.BankResponseCode, callback.BankResponseMessage);
            await SaveFailedPaymentAsync(callback);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ CALLBACK HATASI | OrderId={Oid} | Message={Msg}", 
                callback.OrderId, ex.Message);
            return false;
        }
    }

    public async Task<ToslaInquiryResponseDto?> InquiryPaymentAsync(string orderId)
    {
        try
        {
            var rnd      = Random.Shared.Next(100000, 999999).ToString();
            var timeSpan = DateTime.UtcNow.AddHours(3).ToString("yyyyMMddHHmmss");
            var hash     = ComputeHash(_apiPass + _clientId + _apiUser + rnd + timeSpan);
            var body     = JsonSerializer.Serialize(new { clientId = _clientId, apiUser = _apiUser, rnd, timeSpan, hash, orderId });
            var resp     = await _httpClient.PostAsync($"{_baseUrl}/inquiry", new StringContent(body, Encoding.UTF8, "application/json"));
            var raw      = await resp.Content.ReadAsStringAsync();
            return resp.IsSuccessStatusCode
                ? JsonSerializer.Deserialize<ToslaInquiryResponseDto>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                : null;
        }
        catch (Exception ex) { _logger.LogError(ex, "Sorgulama hatası"); return null; }
    }

    public List<FgsTradePackage> GetAvailablePackages() => _packages;

    // ─────────────────────────────────────────────────────────────────────────
    // Başarılı ödeme → üyelik veya kredi aktifleştir
    // ─────────────────────────────────────────────────────────────────────────
    private async Task ActivateMembershipAsync(ToslaCallbackDto callback)
    {
        _logger.LogInformation("🎯 ActivateMembershipAsync başladı | OrderId={Oid}", callback.OrderId);
        
        try
        {
            // Echo: "UserId|ProductCode"
            var parts       = (callback.Echo ?? "").Split('|');
            var userIdStr   = parts.Length >= 1 ? parts[0] : "";
            var productCode = parts.Length >= 2 ? parts[1] : "";

            _logger.LogInformation("📋 Echo parse | Parts={Count} | UserId={Uid} | ProductCode={Pc}", 
                parts.Length, userIdStr, productCode);

            // ExtraParameters fallback
            if (!string.IsNullOrEmpty(callback.ExtraParameters))
            {
                _logger.LogInformation("📦 ExtraParameters mevcut: {Extra}", callback.ExtraParameters);
                try
                {
                    using var doc = JsonDocument.Parse(callback.ExtraParameters);
                    var root = doc.RootElement;
                    if (string.IsNullOrEmpty(userIdStr)   && root.TryGetProperty("userId",      out var u)) userIdStr   = u.GetString() ?? "";
                    if (string.IsNullOrEmpty(productCode) && root.TryGetProperty("productCode", out var p)) productCode = p.GetString() ?? "";
                    _logger.LogInformation("📦 ExtraParameters parse | UserId={Uid} | ProductCode={Pc}", userIdStr, productCode);
                }
                catch (Exception exParse) 
                { 
                    _logger.LogWarning(exParse, "ExtraParameters parse hatası"); 
                }
            }

            if (!int.TryParse(userIdStr, out int userId)) 
            { 
                _logger.LogError("❌ UserId parse edilemedi | Echo={E} | UserIdStr={Uid}", callback.Echo, userIdStr); 
                return; 
            }

            _logger.LogInformation("👤 Kullanıcı aranıyor | UserId={Id}", userId);
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null) 
            { 
                _logger.LogError("❌ Kullanıcı bulunamadı | UserId={Id}", userId); 
                return; 
            }

            _logger.LogInformation("✅ Kullanıcı bulundu | UserId={Id} | Email={Email} | Mevcut Kredi={Credits}", 
                userId, user.Email, user.Credits);

            var package = FindPackage(productCode);
            if (package is null) 
            { 
                _logger.LogError("❌ Paket bulunamadı | ProductCode={Code}", productCode); 
                return; 
            }

            _logger.LogInformation("📦 Paket bulundu | ProductCode={Code} | Name={Name} | Credits={Cred} | IsCredit={IsCred}", 
                package.ProductCode, package.Name, package.Credits, package.IsCredit);

            var oldCredits = user.Credits;

            if (package.IsCredit)
            {
                // Kredi paketi → sadece kredi ekle
                user.Credits += package.Credits;
                _logger.LogInformation("💰 Kredi ekleniyor | UserId={Id} | Eski={Old} | Eklenen={Add} | Yeni={New}", 
                    userId, oldCredits, package.Credits, user.Credits);
            }
            else
            {
                // Üyelik paketi → üyelik süresini uzat + paket tipini güncelle
                var now = DateTime.UtcNow;
                user.PackageType    = package.Name;
                user.MembershipStart = now;
                user.MembershipEnd  = now.AddDays(package.DurationDays);
                _logger.LogInformation("👑 Üyelik aktifleştiriliyor | UserId={Id} | Paket={Pkg} | Başlangıç={Start} | Bitiş={End}",
                    userId, package.Name, user.MembershipStart, user.MembershipEnd);
            }

            _logger.LogInformation("💾 PaymentHistory kaydı oluşturuluyor...");
            _dbContext.PaymentHistories.Add(new PaymentHistory
            {
                UserId        = userId,
                OrderId       = callback.OrderId ?? "",
                TransactionId = callback.TransactionId ?? "",
                ProductCode   = package.ProductCode,
                PackageName   = package.Name,
                Amount        = callback.Amount / 100m,
                Currency      = "TRY",
                CreditsAdded  = package.IsCredit ? package.Credits : 0,
                Status        = "SUCCESS",
                PaymentDate   = DateTime.UtcNow
            });

            _logger.LogInformation("💾 SaveChangesAsync çağrılıyor...");
            var changeCount = await _dbContext.SaveChangesAsync();
            _logger.LogInformation("✅ Veritabanı güncellendi | Değişiklik sayısı={Count} | Yeni kredi={Credits}", 
                changeCount, user.Credits);

            // Doğrulama için tekrar kontrol et
            var verifyUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (verifyUser != null)
            {
                _logger.LogInformation("🔍 Doğrulama | DB'deki güncel kredi={Credits}", verifyUser.Credits);
            }
        }
        catch (Exception ex) 
        { 
            _logger.LogError(ex, "❌ Aktivasyon hatası | OrderId={Oid} | Message={Msg}", 
                callback.OrderId, ex.Message); 
        }
    }

    private async Task SaveFailedPaymentAsync(ToslaCallbackDto callback)
    {
        try
        {
            var userIdStr = (callback.Echo ?? "").Split('|').FirstOrDefault() ?? "";
            if (!int.TryParse(userIdStr, out int userId)) return;
            _dbContext.PaymentHistories.Add(new PaymentHistory
            {
                UserId = userId, OrderId = callback.OrderId ?? "",
                TransactionId = callback.TransactionId ?? "",
                Amount = callback.Amount / 100m, Currency = "TRY",
                Status = "FAILED", PaymentDate = DateTime.UtcNow,
                ErrorMessage = $"Code:{callback.Code} Bank:{callback.BankResponseCode} {callback.BankResponseMessage}"
            });
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex) { _logger.LogError(ex, "Başarısız ödeme kaydı hatası"); }
    }

    // ─────────────────────────────────────────────────────────────────────────
    private FgsTradePackage? FindPackage(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        // Önce tam eşleşme dene
        var exact = _packages.FirstOrDefault(p =>
            p.ProductCode == code ||
            p.Alias.Equals(code, StringComparison.OrdinalIgnoreCase) ||
            p.Name.Equals(code,  StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        // Frontend'den gelen kısa isimler için fallback haritası
        // Örn: "professional" → "pro_monthly", "business" → "business_monthly"
        var aliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "starter",      "starter_monthly"  },
            { "basic",        "basic_monthly"    },
            { "professional", "pro_monthly"      },
            { "pro",          "pro_monthly"      },
            { "business",     "business_monthly" },
            { "enterprise",   "enterprise_yearly"},
            { "ultimate",     "ultimate_yearly"  },
        };

        if (aliasMap.TryGetValue(code, out var mappedAlias))
            return _packages.FirstOrDefault(p =>
                p.Alias.Equals(mappedAlias, StringComparison.OrdinalIgnoreCase));

        return null;
    }

    private static string ComputeHash(string input)
    {
        using var sha = SHA512.Create();
        return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(input)));
    }

    private static ToslaPaymentResponseDto Fail(string msg, string code) =>
        new() { Success = false, ErrorMessage = msg, ErrorCode = code };

    private static int GetInt(JsonElement el, params string[] keys)
    {
        foreach (var k in keys)
            if (el.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number) return v.GetInt32();
        return -1;
    }

    private static string? GetStr(JsonElement el, params string[] keys)
    {
        foreach (var k in keys)
            if (el.TryGetProperty(k, out var v))
                return v.ValueKind == JsonValueKind.String ? v.GetString()
                     : v.ValueKind == JsonValueKind.Number ? v.GetInt64().ToString() : null;
        return null;
    }
}

// ─── Inquiry DTOs ─────────────────────────────────────────────────────────────
public class ToslaInquiryResponseDto
{
    public int Code { get; set; }
    public string? Message { get; set; }
    public int Count { get; set; }
    public List<ToslaTransactionDto>? Transactions { get; set; }
}

public class ToslaTransactionDto
{
    public string? OrderId { get; set; }
    public string? BankResponseCode { get; set; }
    public string? BankResponseMessage { get; set; }
    public string? AuthCode { get; set; }
    public long Amount { get; set; }
    public int InstallmentCount { get; set; }
    public long TransactionId { get; set; }
}