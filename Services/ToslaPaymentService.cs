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
    Task<PaymentVerificationResult> VerifyAndProcessPaymentAsync(string orderId);
}

/// <summary>
/// FGSTrade Paket Yapısı:
///
/// AYLIK:
///   1274715  Starter Monthly   → $10/ay   → 5  kredi
///   1274739  Pro Monthly       → $26/ay   → 20 kredi
///   1274779  Business Monthly  → $53/ay   → 50 kredi
///
/// YILLIK:
///   1274716  Starter Annual    → $69/yıl  → 60  kredi
///   1274740  Pro Annual        → $199/yıl → 240 kredi
///   1274780  Business Annual   → $399/yıl → 600 kredi
///
/// EXTRA KREDİ:
///   1274710  10  Kredi Extra
///   1274725  25  Kredi Extra
///   1274750  50  Kredi Extra
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

    private readonly List<FgsTradePackage> _packages = new()
    {
        // Aylık paketler
        new() { ProductCode="1274715", Alias="starter_monthly",  Name="Starter",         NameTr="Başlangıç",          PriceUsd=10m,   PriceTry=470m,   Credits=5,   DurationDays=30,  MaxInstallment=1,  IsYearly=false, IsCredit=false, Description="Starter Aylık Üyelik" },
        new() { ProductCode="1274739", Alias="pro_monthly",      Name="Pro",             NameTr="Profesyonel",        PriceUsd=26m,   PriceTry=1222m,  Credits=20,  DurationDays=30,  MaxInstallment=1,  IsYearly=false, IsCredit=false, Description="Pro Aylık Üyelik" },
        new() { ProductCode="1274779", Alias="business_monthly", Name="Business",        NameTr="İş",                 PriceUsd=53m,   PriceTry=2491m,  Credits=50,  DurationDays=30,  MaxInstallment=1,  IsYearly=false, IsCredit=false, Description="Business Aylık Üyelik" },

        // Yıllık paketler
        new() { ProductCode="1274716", Alias="starter_yearly",   Name="Starter Yıllık",  NameTr="Başlangıç Yıllık",  PriceUsd=69m,   PriceTry=3243m,  Credits=60,  DurationDays=365, MaxInstallment=12, IsYearly=true,  IsCredit=false, Description="Starter Yıllık Üyelik" },
        new() { ProductCode="1274740", Alias="pro_yearly",       Name="Pro Yıllık",      NameTr="Profesyonel Yıllık", PriceUsd=199m,  PriceTry=8955m,  Credits=240, DurationDays=365, MaxInstallment=12, IsYearly=true,  IsCredit=false, Description="Pro Yıllık Üyelik" },
        new() { ProductCode="1274780", Alias="business_yearly",  Name="Business Yıllık", NameTr="İş Yıllık",          PriceUsd=399m,  PriceTry=17955m, Credits=600, DurationDays=365, MaxInstallment=12, IsYearly=true,  IsCredit=false, Description="Business Yıllık Üyelik" },

        // Extra Kredi paketleri
        new() { ProductCode="1274710", Alias="credit_10",  Name="10 Kredi",  NameTr="10 Ekstra Kredi",  PriceUsd=10m, PriceTry=450m,  Credits=10,  DurationDays=0, MaxInstallment=1, IsYearly=false, IsCredit=true, Description="10 Ekstra Kredi" },
        new() { ProductCode="1274725", Alias="credit_25",  Name="25 Kredi",  NameTr="25 Ekstra Kredi",  PriceUsd=20m, PriceTry=900m,  Credits=25,  DurationDays=0, MaxInstallment=1, IsYearly=false, IsCredit=true, Description="25 Ekstra Kredi" },
        new() { ProductCode="1274750", Alias="credit_50",  Name="50 Kredi",  NameTr="50 Ekstra Kredi",  PriceUsd=35m, PriceTry=1575m, Credits=50,  DurationDays=0, MaxInstallment=1, IsYearly=false, IsCredit=true, Description="50 Ekstra Kredi" },
        new() { ProductCode="1247100", Alias="credit_100", Name="100 Kredi", NameTr="100 Ekstra Kredi", PriceUsd=60m, PriceTry=2700m, Credits=100, DurationDays=0, MaxInstallment=1, IsYearly=false, IsCredit=true, Description="100 Ekstra Kredi" },
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
    // ÖDEME BAŞLATMA
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

            // Taksit sayısı
            int installment = 1;
            if (package.IsYearly && !package.IsCredit)
                installment = Math.Clamp(request.Installment, 1, package.MaxInstallment);

            // İndirim kodu kontrolü
            decimal finalPrice = package.PriceTry;
            decimal discountPercentage = 0;

            if (!string.IsNullOrWhiteSpace(request.DiscountCode))
            {
                var discountCode = await _dbContext.DiscountCodes
                    .FirstOrDefaultAsync(dc => dc.Code.ToUpper() == request.DiscountCode.ToUpper());

                if (discountCode != null
                    && discountCode.IsActive
                    && discountCode.CurrentUses < discountCode.MaxUses
                    && (!discountCode.ExpiresAt.HasValue || discountCode.ExpiresAt.Value >= DateTime.UtcNow))
                {
                    discountPercentage = discountCode.DiscountPercentage;
                    finalPrice = finalPrice - (finalPrice * discountPercentage / 100m);
                    _logger.LogInformation("💰 İndirim uygulandı: {Code} | %{Percent} | Orijinal: {Original} TL | İndirimli: {Final} TL",
                        request.DiscountCode, discountPercentage, package.PriceTry, finalPrice);
                }
                else
                {
                    _logger.LogWarning("⚠️ Geçersiz indirim kodu: {Code}", request.DiscountCode);
                }
            }

            // Hash parametreleri
            var rnd      = Random.Shared.Next(100000, 999999).ToString();
            var timeSpan = DateTime.UtcNow.AddHours(3).ToString("yyyyMMddHHmmss");
            var hashInput = _apiPass + _clientId + _apiUser + rnd + timeSpan;
            var hash = ComputeHash(hashInput);

            _logger.LogInformation("HASH DEBUG | Rnd={Rnd} | TimeSpan={Ts} | ApiPass={Pass} | ClientId={Cid} | ApiUser={User}",
                rnd, timeSpan, _apiPass, _clientId, _apiUser);
            _logger.LogInformation("HASH INPUT: '{Input}'", hashInput);
            _logger.LogInformation("HASH OUTPUT: '{Hash}'", hash);

            // OrderId (max 20 karakter)
            var ts      = DateTime.UtcNow.AddHours(3).ToString("yyMMddHHmm");
            var uid     = (request.UserId.Length > 7 ? request.UserId[..7] : request.UserId).PadLeft(7, '0');
            var orderId = $"FGS{ts}{uid}";
            if (orderId.Length > 20) orderId = orderId[..20];

            var amountKurus = (long)(finalPrice * 100);

            // ─── KRİTİK: PENDING kayıt yaz ───────────────────────────────────
            // Tosla callback'inde Amount=0, Echo=boş geldiği için ProductCode
            // tespit edilemiyor. Bu kaydı önceden yazarak garantiye alıyoruz.
            if (int.TryParse(request.UserId, out int userIdInt))
            {
                // Aynı orderId ile zaten PENDING kayıt varsa tekrar yazma
                var existingPending = await _dbContext.PaymentHistories
                    .FirstOrDefaultAsync(p => p.OrderId == orderId);

                if (existingPending == null)
                {
                    _dbContext.PaymentHistories.Add(new PaymentHistory
                    {
                        UserId            = userIdInt,
                        OrderId           = orderId,
                        TransactionId     = "",
                        ProductCode       = package.ProductCode,
                        PackageName       = package.Name,
                        Amount            = finalPrice,
                        Currency          = "TRY",
                        CreditsAdded      = package.Credits,
                        Status            = "PENDING",
                        PaymentDate       = DateTime.UtcNow,
                        DiscountCode      = string.IsNullOrWhiteSpace(request.DiscountCode) ? null : request.DiscountCode,
                        DiscountPercentage = discountPercentage > 0 ? (int?)discountPercentage : null,
                        FinalAmount       = finalPrice,
                    });
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation("💾 PENDING kayıt oluşturuldu | OrderId={Oid} | ProductCode={Pc} | Credits={Cred}",
                        orderId, package.ProductCode, package.Credits);
                }
            }
            // ─────────────────────────────────────────────────────────────────

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
                    userId          = request.UserId,
                    productCode     = package.ProductCode,
                    credits         = package.Credits,
                    isYearly        = package.IsYearly,
                    isCredit        = package.IsCredit,
                    durationDays    = package.DurationDays,
                    discountCode    = request.DiscountCode,
                    discountPercent = discountPercentage,
                    originalPrice   = package.PriceTry,
                    discountedPrice = finalPrice
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

            // Tosla'ya gönderim başarısız olduysa PENDING kaydı FAILED yap
            await UpdatePendingToFailed(orderId, $"Tosla Code={code}: {msg}");

            return Fail(msg, code.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ödeme başlatma hatası");
            return Fail("Sistem hatası oluştu.", "SYSTEM_ERROR");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CALLBACK İŞLEME
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
            _logger.LogError(ex, "❌ CALLBACK HATASI | OrderId={Oid}", callback.OrderId);
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AKTİVASYON
    // ─────────────────────────────────────────────────────────────────────────
    private async Task ActivateMembershipAsync(ToslaCallbackDto callback)
    {
        _logger.LogInformation("🎯 ActivateMembershipAsync başladı | OrderId={Oid}", callback.OrderId);

        try
        {
            // Duplicate kontrol — aynı OrderId ile zaten SUCCESS var mı?
            var existingSuccess = await _dbContext.PaymentHistories
                .FirstOrDefaultAsync(p => p.OrderId == (callback.OrderId ?? "") && p.Status == "SUCCESS");

            if (existingSuccess != null)
            {
                _logger.LogWarning("⚠️ Duplicate engellendi — zaten başarılı kayıt var | OrderId={Oid} | Id={Id}",
                    callback.OrderId, existingSuccess.Id);
                return;
            }

            // ── UserId tespiti ────────────────────────────────────────────────
            string userIdStr = "";

            // 1. Echo'dan
            if (!string.IsNullOrWhiteSpace(callback.Echo))
            {
                var echoParts = callback.Echo.Split('|');
                if (echoParts.Length >= 1 && !string.IsNullOrWhiteSpace(echoParts[0]))
                    userIdStr = echoParts[0].Trim();
            }

            // 2. OrderId'den (FGS{10 karakter timestamp}{userId padded})
            if (string.IsNullOrWhiteSpace(userIdStr) &&
                !string.IsNullOrEmpty(callback.OrderId) &&
                callback.OrderId.StartsWith("FGS") &&
                callback.OrderId.Length > 13)
            {
                userIdStr = callback.OrderId.Substring(13).TrimStart('0');
                _logger.LogInformation("✅ OrderId'den UserId çıkarıldı | OrderId={Oid} → UserId={Uid}",
                    callback.OrderId, userIdStr);
            }

            // 3. PENDING kayıttan
            if (string.IsNullOrWhiteSpace(userIdStr))
            {
                var pending = await _dbContext.PaymentHistories
                    .FirstOrDefaultAsync(p => p.OrderId == callback.OrderId && p.Status == "PENDING");
                if (pending != null)
                    userIdStr = pending.UserId.ToString();
            }

            if (!int.TryParse(userIdStr, out int userId))
            {
                _logger.LogError("❌ FATAL: UserId tespit edilemedi | OrderId={Oid}", callback.OrderId);
                return;
            }

            // ── ProductCode tespiti ──────────────────────────────────────────
            string productCode = "";

            // Strateji 1: Echo'dan
            if (!string.IsNullOrWhiteSpace(callback.Echo))
            {
                var echoParts = callback.Echo.Split('|');
                if (echoParts.Length >= 2 && !string.IsNullOrWhiteSpace(echoParts[1]))
                {
                    productCode = echoParts[1].Trim();
                    _logger.LogInformation("✅ Echo'dan ProductCode alındı | ProductCode={Pc}", productCode);
                }
            }

            // Strateji 2: ExtraParameters'dan
            if (string.IsNullOrWhiteSpace(productCode) && !string.IsNullOrWhiteSpace(callback.ExtraParameters))
            {
                try
                {
                    var extra = JsonSerializer.Deserialize<JsonElement>(callback.ExtraParameters);
                    if (extra.TryGetProperty("productCode", out var pcEl))
                    {
                        var pc = pcEl.GetString();
                        if (!string.IsNullOrWhiteSpace(pc))
                        {
                            productCode = pc.Trim();
                            _logger.LogInformation("✅ ExtraParameters'dan ProductCode alındı | ProductCode={Pc}", productCode);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ExtraParameters parse hatası");
                }
            }

            // Strateji 3: PENDING PaymentHistory kaydından ← ANA ÇÖZÜM
            if (string.IsNullOrWhiteSpace(productCode))
            {
                var pendingPayment = await _dbContext.PaymentHistories
                    .Where(p => p.OrderId == callback.OrderId && p.Status == "PENDING")
                    .FirstOrDefaultAsync();

                if (pendingPayment != null && !string.IsNullOrWhiteSpace(pendingPayment.ProductCode))
                {
                    productCode = pendingPayment.ProductCode;
                    _logger.LogInformation("✅ PENDING kayıttan ProductCode alındı | ProductCode={Pc} | Credits={Cred}",
                        productCode, pendingPayment.CreditsAdded);
                }
            }

            // Strateji 4: Son çare — fiyata göre tahmin (indirimde yanlış sonuç verebilir)
            if (string.IsNullOrWhiteSpace(productCode))
            {
                var amountTL = callback.Amount / 100m;
                _logger.LogWarning("⚠️ Tüm stratejiler başarısız, fiyata göre tahmin | Amount={Amt} TL", amountTL);
                productCode = GuessProductCodeFromAmount(amountTL);
            }

            // ── Kullanıcı ve paket yükle ─────────────────────────────────────
            var user = await _dbContext.Users.FindAsync(userId);
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
                // Sadece kredi ekle
                user.Credits += package.Credits;
                _logger.LogInformation("💰 KREDİ EKLENİYOR | UserId={Id} | Eski={Old} + Eklenen={Add} = Yeni={New}",
                    userId, oldCredits, package.Credits, user.Credits);
            }
            else
            {
                // Üyelik paketi → üyelik uzat + kredi ekle
                var now        = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
                var oldPackage = user.PackageType;
                var oldExpiry  = user.MembershipEnd;

                user.PackageType     = package.Name;
                user.MembershipStart = now;
                user.MembershipEnd   = DateTime.SpecifyKind(now.AddDays(package.DurationDays), DateTimeKind.Utc);
                user.Credits        += package.Credits;
                user.MaxResultsPerSearch = Math.Max(user.MaxResultsPerSearch, 200);

                _logger.LogInformation(
                    "👑 ÜYELİK + KREDİ AKTİFLEŞTİRİLİYOR | UserId={Id} | {OldPkg} → {NewPkg} | " +
                    "Bitiş: {OldExp} → {NewExp} | Kredi: {OldCred} + {Add} = {NewCred}",
                    userId, oldPackage, package.Name, oldExpiry, user.MembershipEnd,
                    oldCredits, package.Credits, user.Credits);
            }

            _dbContext.Users.Update(user);

            // ── PENDING kaydı SUCCESS'e güncelle ────────────────────────────
            var pendingRecord = await _dbContext.PaymentHistories
                .FirstOrDefaultAsync(p => p.OrderId == callback.OrderId && p.Status == "PENDING");

            if (pendingRecord != null)
            {
                pendingRecord.Status        = "SUCCESS";
                pendingRecord.TransactionId = callback.TransactionId ?? "";
                pendingRecord.Amount        = callback.Amount > 0 ? callback.Amount / 100m : pendingRecord.Amount;
                pendingRecord.FinalAmount   = pendingRecord.Amount;
                _dbContext.PaymentHistories.Update(pendingRecord);
                _logger.LogInformation("✅ PENDING kayıt SUCCESS'e güncellendi | Id={Id} | OrderId={Oid}",
                    pendingRecord.Id, callback.OrderId);
            }
            else
            {
                // PENDING kayıt yoksa yeni SUCCESS kaydı oluştur
                _dbContext.PaymentHistories.Add(new PaymentHistory
                {
                    UserId        = userId,
                    OrderId       = callback.OrderId ?? "",
                    TransactionId = callback.TransactionId ?? "",
                    ProductCode   = package.ProductCode,
                    PackageName   = package.Name,
                    Amount        = callback.Amount / 100m,
                    Currency      = "TRY",
                    CreditsAdded  = package.Credits,
                    Status        = "SUCCESS",
                    PaymentDate   = DateTime.UtcNow,
                    FinalAmount   = callback.Amount / 100m,
                });
                _logger.LogInformation("💾 Yeni SUCCESS PaymentHistory kaydı oluşturuldu | OrderId={Oid}", callback.OrderId);
            }

            // İndirim kodu güncelle
            await ProcessDiscountCodeAsync(callback.ExtraParameters, callback.OrderId);

            _logger.LogInformation("💾 SaveChangesAsync çağrılıyor...");
            var changeCount = await _dbContext.SaveChangesAsync();
            _logger.LogInformation("✅ VERİTABANI GÜNCELLENDİ | Değişiklik={Count} | Kredi={Credits} | Paket={Pkg}",
                changeCount, user.Credits, user.PackageType);

            await _dbContext.Entry(user).ReloadAsync();
            _logger.LogInformation("🔍 DOĞRULAMA | UserId={Id} | Kredi={Credits} | Paket={Pkg} | Bitiş={End}",
                user.Id, user.Credits, user.PackageType, user.MembershipEnd);

            _logger.LogInformation("🎉🎉🎉 AKTİVASYON BAŞARILI | UserId={Id} | Kredi={Credits}", userId, user.Credits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌❌❌ AKTİVASYON HATASI | OrderId={Oid}", callback.OrderId);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TOSLA INQUIRY
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<ToslaInquiryResponseDto?> InquiryPaymentAsync(string orderId)
    {
        try
        {
            _logger.LogInformation("🔍 Tosla Inquiry | OrderId={Oid}", orderId);

            var rnd      = Random.Shared.Next(100000, 999999).ToString();
            var timeSpan = DateTime.UtcNow.AddHours(3).ToString("yyyyMMddHHmmss");
            var hash     = ComputeHash(_apiPass + _clientId + _apiUser + rnd + timeSpan);

            var requestBody = new { clientId = _clientId, apiUser = _apiUser, rnd, timeSpan, hash, orderId };
            var body    = JsonSerializer.Serialize(requestBody);

            _logger.LogInformation("📤 Tosla Inquiry Request | Body={Body}", body);

            var resp = await _httpClient.PostAsync($"{_baseUrl}/inquiry",
                new StringContent(body, Encoding.UTF8, "application/json"));
            var raw  = await resp.Content.ReadAsStringAsync();

            _logger.LogInformation("📥 Tosla Inquiry Response | Status={Status} | Body={Body}", (int)resp.StatusCode, raw);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("❌ Tosla Inquiry başarısız | Status={Status}", (int)resp.StatusCode);
                return null;
            }

            return JsonSerializer.Deserialize<ToslaInquiryResponseDto>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Tosla Inquiry hatası | OrderId={Oid}", orderId);
            return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FRONTEND TETİKLEMLİ DOĞRULAMA
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<PaymentVerificationResult> VerifyAndProcessPaymentAsync(string orderId)
    {
        try
        {
            _logger.LogInformation("🔍 ÖDEME DOĞRULAMA | OrderId={Oid}", orderId);

            // Zaten işlenmiş mi?
            var existingPayment = await _dbContext.PaymentHistories
                .Where(p => p.OrderId == orderId && p.Status == "SUCCESS")
                .FirstOrDefaultAsync();

            if (existingPayment != null)
            {
                _logger.LogInformation("ℹ️ Zaten işlenmiş | OrderId={Oid} | Credits={Cred}",
                    orderId, existingPayment.CreditsAdded);
                return new PaymentVerificationResult
                {
                    Success            = true,
                    IsAlreadyProcessed = true,
                    CreditsAdded       = existingPayment.CreditsAdded,
                    PackageName        = existingPayment.PackageName,
                    UserId             = existingPayment.UserId,

                };
            }

            // Tosla'dan sorgula
            var inquiry = await InquiryPaymentAsync(orderId);
            if (inquiry == null || inquiry.Code != 0)
            {
                return new PaymentVerificationResult
                {
                    Success      = false,
                    ErrorMessage = inquiry?.Message ?? "Ödeme sorgulanamadı"
                };
            }

            var transaction = inquiry.Transactions?.FirstOrDefault();
            if (transaction == null)
            {
                return new PaymentVerificationResult
                {
                    Success      = false,
                    ErrorMessage = "İşlem detayları bulunamadı"
                };
            }

            if (transaction.BankResponseCode != "00")
            {
                await SaveFailedPaymentFromInquiry(orderId, transaction);
                return new PaymentVerificationResult
                {
                    Success      = false,
                    ErrorMessage = $"Ödeme başarısız: {transaction.BankResponseMessage}"
                };
            }

            // Başarılı → aktivasyon
            var callbackDto = new ToslaCallbackDto
            {
                Code                = 0,
                Message             = "Başarılı (Verified)",
                OrderId             = orderId,
                BankResponseCode    = transaction.BankResponseCode,
                BankResponseMessage = transaction.BankResponseMessage,
                TransactionId       = transaction.TransactionId.ToString(),
                AuthCode            = transaction.AuthCode,
                Amount              = transaction.Amount,
                RequestStatus       = 1
            };

            await ActivateMembershipAsync(callbackDto);

            var payment = await _dbContext.PaymentHistories
                .Where(p => p.OrderId == orderId && p.Status == "SUCCESS")
                .FirstOrDefaultAsync();

            if (payment == null)
            {
                return new PaymentVerificationResult
                {
                    Success      = false,
                    ErrorMessage = "Ödeme işlendi ama kayıt bulunamadı"
                };
            }

            return new PaymentVerificationResult
            {
                Success            = true,
                IsAlreadyProcessed = false,
                CreditsAdded       = payment.CreditsAdded,
                PackageName        = payment.PackageName ?? "Bilinmeyen",
                UserId             = payment.UserId,

            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ödeme doğrulama hatası | OrderId={Oid}", orderId);
            return new PaymentVerificationResult
            {
                Success      = false,
                ErrorMessage = "Sistem hatası: " + ex.Message
            };
        }
    }

    public List<FgsTradePackage> GetAvailablePackages() => _packages;

    // ─────────────────────────────────────────────────────────────────────────
    // YARDIMCI METODLAR
    // ─────────────────────────────────────────────────────────────────────────

    private async Task SaveFailedPaymentAsync(ToslaCallbackDto callback)
    {
        try
        {
            // Önce PENDING kaydı FAILED yap
            var pending = await _dbContext.PaymentHistories
                .FirstOrDefaultAsync(p => p.OrderId == callback.OrderId && p.Status == "PENDING");

            if (pending != null)
            {
                pending.Status       = "FAILED";
                pending.ErrorMessage = $"Code:{callback.Code} Bank:{callback.BankResponseCode} {callback.BankResponseMessage}";
                _dbContext.PaymentHistories.Update(pending);
            }
            else
            {
                var userIdStr = (callback.Echo ?? "").Split('|').FirstOrDefault() ?? "";
                if (!int.TryParse(userIdStr, out int userId) &&
                    !string.IsNullOrEmpty(callback.OrderId) &&
                    callback.OrderId.StartsWith("FGS") &&
                    callback.OrderId.Length > 13)
                {
                    int.TryParse(callback.OrderId.Substring(13).TrimStart('0'), out userId);
                }

                _dbContext.PaymentHistories.Add(new PaymentHistory
                {
                    UserId        = userId,
                    OrderId       = callback.OrderId ?? "",
                    TransactionId = callback.TransactionId ?? "",
                    Amount        = callback.Amount / 100m,
                    Currency      = "TRY",
                    Status        = "FAILED",
                    PaymentDate   = DateTime.UtcNow,
                    ErrorMessage  = $"Code:{callback.Code} Bank:{callback.BankResponseCode} {callback.BankResponseMessage}"
                });
            }

            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Başarısız ödeme kaydı hatası");
        }
    }

    private async Task SaveFailedPaymentFromInquiry(string orderId, ToslaTransactionDto transaction)
    {
        try
        {
            var pending = await _dbContext.PaymentHistories
                .FirstOrDefaultAsync(p => p.OrderId == orderId && p.Status == "PENDING");

            if (pending != null)
            {
                pending.Status       = "FAILED";
                pending.ErrorMessage = $"BankCode:{transaction.BankResponseCode} {transaction.BankResponseMessage}";
                _dbContext.PaymentHistories.Update(pending);
            }
            else
            {
                string userIdStr = "";
                if (!string.IsNullOrEmpty(orderId) && orderId.StartsWith("FGS") && orderId.Length > 13)
                    userIdStr = orderId.Substring(13).TrimStart('0');

                int.TryParse(userIdStr, out int userId);
                _dbContext.PaymentHistories.Add(new PaymentHistory
                {
                    UserId        = userId,
                    OrderId       = orderId,
                    TransactionId = transaction.TransactionId.ToString(),
                    Amount        = transaction.Amount / 100m,
                    Currency      = "TRY",
                    Status        = "FAILED",
                    PaymentDate   = DateTime.UtcNow,
                    ErrorMessage  = $"BankCode:{transaction.BankResponseCode} {transaction.BankResponseMessage}"
                });
            }

            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Başarısız ödeme kaydı hatası (inquiry)");
        }
    }

    private async Task UpdatePendingToFailed(string orderId, string reason)
    {
        try
        {
            var pending = await _dbContext.PaymentHistories
                .FirstOrDefaultAsync(p => p.OrderId == orderId && p.Status == "PENDING");
            if (pending != null)
            {
                pending.Status       = "FAILED";
                pending.ErrorMessage = reason;
                _dbContext.PaymentHistories.Update(pending);
                await _dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PENDING→FAILED güncelleme hatası | OrderId={Oid}", orderId);
        }
    }

    private async Task ProcessDiscountCodeAsync(string? extraParameters, string? orderId)
    {
        if (string.IsNullOrEmpty(extraParameters)) return;

        try
        {
            var extra = JsonSerializer.Deserialize<JsonElement>(extraParameters);
            if (!extra.TryGetProperty("discountCode", out var dcEl)) return;

            var discountCode = dcEl.GetString();
            if (string.IsNullOrWhiteSpace(discountCode)) return;

            var entity = await _dbContext.DiscountCodes
                .FirstOrDefaultAsync(dc => dc.Code == discountCode && dc.IsActive);

            if (entity == null) return;

            entity.CurrentUses++;
            if (entity.CurrentUses >= entity.MaxUses)
            {
                entity.IsActive = false;
                _logger.LogInformation("⚠️ İndirim kodu deaktif edildi | Code={Code}", discountCode);
            }

            _logger.LogInformation("🎟️ İndirim kodu kullanıldı | Code={Code} | Uses={Uses}", discountCode, entity.CurrentUses);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "İndirim kodu işleme hatası | OrderId={Oid}", orderId);
        }
    }

    private FgsTradePackage? FindPackage(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        var exact = _packages.FirstOrDefault(p =>
            p.ProductCode == code ||
            p.Alias.Equals(code, StringComparison.OrdinalIgnoreCase) ||
            p.Name.Equals(code, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        var aliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "starter",      "starter_monthly"  },
            { "basic",        "starter_monthly"  },
            { "professional", "pro_monthly"      },
            { "pro",          "pro_monthly"      },
            { "business",     "business_monthly" },
            { "enterprise",   "business_yearly"  },
        };

        if (aliasMap.TryGetValue(code, out var mappedAlias))
            return _packages.FirstOrDefault(p =>
                p.Alias.Equals(mappedAlias, StringComparison.OrdinalIgnoreCase));

        return null;
    }

    private string GuessProductCodeFromAmount(decimal amountTL)
    {
        var package = _packages
            .Where(p => p.PriceTry > 0)
            .OrderBy(p => Math.Abs(p.PriceTry - amountTL))
            .FirstOrDefault();

        if (package != null)
        {
            _logger.LogInformation("💡 Amount'dan paket tahmin | Amount={Amt} TL → {Pkg} ({Code})",
                amountTL, package.Name, package.ProductCode);
            return package.ProductCode;
        }

        _logger.LogWarning("⚠️ Tahmin başarısız, default Starter | Amount={Amt}", amountTL);
        return "1274715";
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

// ─── DTOs ────────────────────────────────────────────────────────────────────
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