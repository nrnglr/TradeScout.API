using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradeScout.API.Data;
using TradeScout.API.Models;
using TradeScout.API.Models.Payment;

namespace TradeScout.API.Services;

// ─── Interface ────────────────────────────────────────────────────────────────
public interface IParatikaPaymentService
{
    /// <summary>
    /// Adım 1: Session token al, kullanıcıyı Paratika ödeme sayfasına yönlendir.
    /// Hem ilk abonelik ödemesi hem de extra kredi satışı buradan geçer.
    /// </summary>
    Task<ParatikaPaymentResponseDto> InitializePaymentAsync(ParatikaPaymentRequestDto request);

    /// <summary>
    /// Adım 2a: Paratika ödeme sayfasından dönen callback'i işle.
    /// Başarılıysa: üyelik aktifleştir + recurring plan oluştur + kart bağla.
    /// </summary>
    Task<ParatikaCallbackResult> ProcessCallbackAsync(ParatikaCallbackDto callback);

    /// <summary>
    /// Adım 2b (güvenlik ağı): Frontend dönüşte verify isteği atar.
    /// Tarayıcı kapanması durumunda QUERYTRANSACTION ile kontrol et.
    /// </summary>
    Task<PaymentVerificationResult> VerifyAndProcessPaymentAsync(string merchantPaymentId);

    /// <summary>
    /// Recurring notification: Paratika otomatik çekim yaptığında bu endpoint'e bildirir.
    /// Üyeliği uzat, PaymentHistory kaydet.
    /// </summary>
    Task ProcessRecurringNotificationAsync(ParatikaRecurringNotificationDto notification);

    /// <summary>Kullanıcının aktif aboneliğini iptal et (Paratika'da planı sil)</summary>
    Task<bool> CancelSubscriptionAsync(int userId, string? reason = null);

    /// <summary>Kullanıcının aktif aboneliğini getir</summary>
    Task<Subscription?> GetActiveSubscriptionAsync(int userId);

    /// <summary>Paket listesi</summary>
    List<FgsTradePackage> GetAvailablePackages();

    /// <summary>İşlem sorgula (tarayıcı kapanması durumu için)</summary>
    Task<ParatikaQueryResponseDto?> QueryTransactionAsync(string merchantPaymentId);
}

// ─── DTO'lar ──────────────────────────────────────────────────────────────────

public class ParatikaPaymentRequestDto
{
    public string  ProductCode  { get; set; } = string.Empty;
    public string  UserId       { get; set; } = string.Empty;
    public int     Installment  { get; set; } = 1;
    public string? DiscountCode { get; set; }
}

public class ParatikaPaymentResponseDto
{
    public bool    Success           { get; set; }
    public string? PaymentUrl        { get; set; }
    public string? SessionToken      { get; set; }
    public string? MerchantPaymentId { get; set; }
    public string? ErrorMessage      { get; set; }
    public string? ErrorCode         { get; set; }
}

public class ParatikaCallbackDto
{
    // Paratika'nın RETURNURL'e POST ettiği alanlar
    public string? MerchantPaymentId { get; set; }
    public string? ApiMerchantId     { get; set; }
    public string? SessionToken      { get; set; }
    public string? CustomerId        { get; set; }
    public string? PgTranId          { get; set; }
    public string? PgTranDate        { get; set; }
    public string? PgTranRefId       { get; set; }
    public string? PgTranApprCode    { get; set; }
    public string? ResponseCode      { get; set; }
    public string? ResponseMsg       { get; set; }
    public string? ErrorCode         { get; set; }
    public string? ErrorMsg          { get; set; }
    public string? PgTranErrorCode   { get; set; }  // Banka ISO hata kodu (51, 05, 14 vb.)
    public string? Amount            { get; set; }
    public string? Currency          { get; set; }
    public string? InstallmentCount  { get; set; }
    public string? CardToken         { get; set; }   // ← recurring için kritik
    public string? CardBrand         { get; set; }
    public string? CardPanMasked     { get; set; }
    public string? PaymentSystem     { get; set; }
    // Hash doğrulama
    public string? Random            { get; set; }
    public string? SdSha512          { get; set; }
    // Bizim koyduğumuz meta veri
    public string? CustomData        { get; set; }
}

public class ParatikaCallbackResult
{
    public bool    Success           { get; set; }
    public bool    IsSubscription    { get; set; }
    public string? MerchantPaymentId { get; set; }
    public string? ErrorMessage      { get; set; }
    public string? BankErrorCode     { get; set; }  // Frontend hata mesajı için banka ISO kodu
}

public class ParatikaRecurringNotificationDto
{
    // Paratika'nın notification URL'ine gönderdiği alanlar
    public string? PlanCode          { get; set; }
    public string? MerchantPaymentId { get; set; }
    public string? PgTranId          { get; set; }
    public string? ResponseCode      { get; set; }
    public string? ResponseMsg       { get; set; }
    public string? Amount            { get; set; }
    public string? Currency          { get; set; }
    public string? CardToken         { get; set; }
    public string? CardPanMasked     { get; set; }
    public string? ErrorCode         { get; set; }
    public string? Error             { get; set; }
}

public class ParatikaQueryResponseDto
{
    public string? ResponseCode      { get; set; }
    public string? ResponseMsg       { get; set; }
    public string? PgTranId          { get; set; }
    public string? PgTranApprCode    { get; set; }
    public string? Amount            { get; set; }
    public string? InstallmentCount  { get; set; }
    public string? MerchantPaymentId { get; set; }
    public string? CardPanMasked     { get; set; }
    public string? ErrorCode         { get; set; }
    public string? Error             { get; set; }
}

// ─── CustomData (session token isteğinde saklanan meta veri) ─────────────────
internal class ParatikaCustomData
{
    public string  UserId          { get; set; } = "";
    public string  ProductCode     { get; set; } = "";
    public int     Credits         { get; set; }
    public bool    IsYearly        { get; set; }
    public bool    IsCredit        { get; set; }  // true = extra kredi, recurring YOK
    public int     DurationDays    { get; set; }
    public string? DiscountCode    { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal OriginalPrice   { get; set; }
    public decimal DiscountedPrice { get; set; }
}

// ─── Service ──────────────────────────────────────────────────────────────────
public class ParatikaPaymentService : IParatikaPaymentService
{
    private readonly HttpClient                      _httpClient;
    private readonly ILogger<ParatikaPaymentService> _logger;
    private readonly ApplicationDbContext            _dbContext;

    private readonly string _merchantUser;
    private readonly string _merchantPassword;
    private readonly string _merchant;
    private readonly string _merchantSecretKey;   // hash doğrulama için
    private readonly string _apiBaseUrl;
    private readonly string _hostedPageBaseUrl;
    private readonly string _callbackUrl;
    private readonly string _recurringNotifUrl;

    // ─── Paket listesi ────────────────────────────────────────────────────────
    // IsCredit=true  → extra kredi, tek seferlik ödeme, recurring KURULMAZ
    // IsCredit=false → abonelik paketi, recurring KURULUR
    private readonly List<FgsTradePackage> _packages = new()
    {
        // Aylık abonelikler
        new() { ProductCode="1274715", Alias="starter_monthly",  Name="Starter",         NameTr="Başlangıç",        PriceUsd=15m,  PriceTry=1m,     Credits=10,  DurationDays=30,  MaxInstallment=1,  IsYearly=false, IsCredit=false },
        new() { ProductCode="1274739", Alias="pro_monthly",      Name="Pro",              NameTr="Profesyonel",      PriceUsd=39m,  PriceTry=1677m,  Credits=40,  DurationDays=30,  MaxInstallment=1,  IsYearly=false, IsCredit=false },
        new() { ProductCode="1274779", Alias="business_monthly", Name="Business",         NameTr="İş",               PriceUsd=79m,  PriceTry=3397m,  Credits=100, DurationDays=30,  MaxInstallment=1,  IsYearly=false, IsCredit=false },
        // Yıllık abonelikler
        new() { ProductCode="1274716", Alias="starter_yearly",   Name="Starter Yıllık",  NameTr="Başlangıç Yıllık", PriceUsd=99m,  PriceTry=4257m,  Credits=10,  DurationDays=365, MaxInstallment=12, IsYearly=true,  IsCredit=false },
        new() { ProductCode="1274740", Alias="pro_yearly",       Name="Pro Yıllık",       NameTr="Profesyonel Yıllık",PriceUsd=299m, PriceTry=12857m, Credits=40,  DurationDays=365, MaxInstallment=12, IsYearly=true,  IsCredit=false },
        new() { ProductCode="1274780", Alias="business_yearly",  Name="Business Yıllık",  NameTr="İş Yıllık",       PriceUsd=599m, PriceTry=25757m, Credits=100, DurationDays=365, MaxInstallment=12, IsYearly=true,  IsCredit=false },
        // Extra kredi - tek seferlik, recurring YOK
        new() { ProductCode="1274710", Alias="credit_10",  Name="10 Kredi",  NameTr="10 Ekstra Kredi",  PriceUsd=10m, PriceTry=430m,  Credits=10,  DurationDays=0, MaxInstallment=1, IsYearly=false, IsCredit=true },
        new() { ProductCode="1274725", Alias="credit_25",  Name="25 Kredi",  NameTr="25 Ekstra Kredi",  PriceUsd=20m, PriceTry=860m,  Credits=25,  DurationDays=0, MaxInstallment=1, IsYearly=false, IsCredit=true },
        new() { ProductCode="1274750", Alias="credit_50",  Name="50 Kredi",  NameTr="50 Ekstra Kredi",  PriceUsd=35m, PriceTry=1505m, Credits=50,  DurationDays=0, MaxInstallment=1, IsYearly=false, IsCredit=true },
        new() { ProductCode="1247100", Alias="credit_100", Name="100 Kredi", NameTr="100 Ekstra Kredi", PriceUsd=60m, PriceTry=2580m, Credits=100, DurationDays=0, MaxInstallment=1, IsYearly=false, IsCredit=true },
    };

    // ─── Constructor ──────────────────────────────────────────────────────────
    public ParatikaPaymentService(
        HttpClient httpClient,
        ILogger<ParatikaPaymentService> logger,
        IConfiguration configuration,
        ApplicationDbContext dbContext)
    {
        _httpClient = httpClient;
        _logger     = logger;
        _dbContext  = dbContext;

        _merchantUser      = Env("PARATIKA_MERCHANT_USER",      configuration["ParatikaSettings:MerchantUser"]      ?? "");
        _merchantPassword  = Env("PARATIKA_MERCHANT_PASSWORD",  configuration["ParatikaSettings:MerchantPassword"]  ?? "");
        _merchant          = Env("PARATIKA_MERCHANT",           configuration["ParatikaSettings:Merchant"]          ?? "10013146");
        _merchantSecretKey = Env("PARATIKA_SECRET_KEY",         configuration["ParatikaSettings:SecretKey"]         ?? "");
        _apiBaseUrl        = Env("PARATIKA_API_BASE_URL",       configuration["ParatikaSettings:ApiBaseUrl"]        ?? "https://vpos.paratika.com.tr/paratika/api/v2").TrimEnd('/');
        _hostedPageBaseUrl = Env("PARATIKA_HOSTED_PAGE_URL",    configuration["ParatikaSettings:HostedPageBaseUrl"] ?? "https://vpos.paratika.com.tr/merchant/post/sale").TrimEnd('/');
        _callbackUrl       = Env("PARATIKA_CALLBACK_URL",       configuration["ParatikaSettings:CallbackUrl"]       ?? "https://api.fgstrade.com/api/payment/paratika/callback");
        _recurringNotifUrl = Env("PARATIKA_RECURRING_NOTIF_URL",configuration["ParatikaSettings:RecurringNotifUrl"] ?? "https://api.fgstrade.com/api/payment/paratika/recurring-notification");

        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        if (string.IsNullOrEmpty(_merchantUser) || string.IsNullOrEmpty(_merchantPassword))
            _logger.LogError("!!! PARATIKA CREDENTIALS EKSİK !!!");
        else
            _logger.LogInformation("ParatikaPaymentService hazır | Merchant={M} | Api={Url}", _merchant, _apiBaseUrl);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 1. ÖDEME BAŞLATMA — Session Token al, Hosted Page URL döndür
    // ═════════════════════════════════════════════════════════════════════════
    public async Task<ParatikaPaymentResponseDto> InitializePaymentAsync(ParatikaPaymentRequestDto request)
    {
        try
        {
            var package = FindPackage(request.ProductCode);
            if (package is null)
                return Fail("Geçersiz paket kodu.", "INVALID_PRODUCT");

            // Taksit: sadece yıllık aboneliklerde uygulanır
            int installment = 1;
            if (package.IsYearly && !package.IsCredit)
                installment = Math.Clamp(request.Installment, 1, package.MaxInstallment);

            // İndirim
            decimal finalPrice        = package.PriceTry;
            decimal discountPercent   = 0;

            if (!string.IsNullOrWhiteSpace(request.DiscountCode))
            {
                var dc = await _dbContext.DiscountCodes
                    .FirstOrDefaultAsync(d => d.Code.ToUpper() == request.DiscountCode.ToUpper()
                                           && d.IsActive
                                           && d.CurrentUses < d.MaxUses
                                           && (!d.ExpiresAt.HasValue || d.ExpiresAt.Value >= DateTime.UtcNow));
                if (dc != null)
                {
                    discountPercent = dc.DiscountPercentage;
                    finalPrice     -= finalPrice * discountPercent / 100m;
                    _logger.LogInformation("💰 İndirim: {Code} %{Pct} → {Final} TL", request.DiscountCode, discountPercent, finalPrice);
                }
                else
                {
                    _logger.LogWarning("⚠️ Geçersiz indirim kodu: {Code}", request.DiscountCode);
                }
            }

            // MerchantPaymentId — max 20 karakter (Raiffeisen uyumluluğu için kısa tutuyoruz)
            var ts            = DateTime.UtcNow.AddHours(3).ToString("yyMMddHHmm");  // 10 karakter
            var uid           = (request.UserId.Length > 7 ? request.UserId[..7] : request.UserId).PadLeft(7, '0');
            var merchantPayId = $"FGS{ts}{uid}";  // 20 karakter
            if (merchantPayId.Length > 20) merchantPayId = merchantPayId[..20];

            // CustomData — callback'te paket bilgisini geri almak için
            var customData = JsonSerializer.Serialize(new ParatikaCustomData
            {
                UserId          = request.UserId,
                ProductCode     = package.ProductCode,
                Credits         = package.Credits,
                IsYearly        = package.IsYearly,
                IsCredit        = package.IsCredit,
                DurationDays    = package.DurationDays,
                DiscountCode    = request.DiscountCode,
                DiscountPercent = discountPercent,
                OriginalPrice   = package.PriceTry,
                DiscountedPrice = finalPrice
            });

            var amountStr = finalPrice.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

            // Session Token isteği
            var form = new Dictionary<string, string>
            {
                ["ACTION"]            = "SESSIONTOKEN",
                ["MERCHANTUSER"]      = _merchantUser,
                ["MERCHANTPASSWORD"]  = _merchantPassword,
                ["MERCHANT"]          = _merchant,
                ["MERCHANTPAYMENTID"] = merchantPayId,
                ["AMOUNT"]            = amountStr,
                ["CURRENCY"]          = "TRY",
                ["INSTALLMENTCOUNT"]  = installment.ToString(),
                ["SESSIONTYPE"]       = "PAYMENTSESSION",
                ["CUSTOMER"]          = request.UserId,
                ["RETURNURL"]         = _callbackUrl,
                ["CUSTOMDATA"]        = customData,
                ["LANG"]              = "tr",
                ["ORDERITEMS"]        = JsonSerializer.Serialize(new[]
                {
                    new
                    {
                        productCode = package.ProductCode,
                        name        = package.Name,
                        description = package.Name,
                        quantity    = 1,
                        amount      = finalPrice
                    }
                }),
            };

            _logger.LogInformation("▶ Paratika SessionToken | PayId={Id} | Paket={Pkg} | Taksit={Inst} | Tutar={Amt} TL | OrderItems={OI}",
                merchantPayId, package.Name, installment, amountStr, form["ORDERITEMS"]);

            var resp = await _httpClient.PostAsync(_apiBaseUrl, new FormUrlEncodedContent(form));
            var raw  = await resp.Content.ReadAsStringAsync();

            _logger.LogInformation("◀ Paratika SessionToken yanıt | HTTP={Status} | {Body}",
                (int)resp.StatusCode, raw.Length > 400 ? raw[..400] : raw);

            if (!resp.IsSuccessStatusCode)
                return Fail("Ödeme sistemi ile iletişim kurulamadı", ((int)resp.StatusCode).ToString());

            using var doc        = JsonDocument.Parse(raw, new JsonDocumentOptions { });
            var root             = doc.RootElement;
            var respCode         = GetStr(root, "responseCode", "RESPONSECODE");
            var respMsg          = GetStr(root, "responseMsg",  "RESPONSEMSG",  "errorMsg", "ERRORMSG") ?? "Bilinmeyen hata";
            var sessionToken     = GetStr(root, "sessionToken", "SESSIONTOKEN");

            if (respCode != "00" || string.IsNullOrEmpty(sessionToken))
            {
                _logger.LogError("❌ Paratika SessionToken hata | Code={Code} | Msg={Msg}", respCode, respMsg);
                return Fail(respMsg, respCode ?? "ERR");
            }

            // Non-Direct (HPP) URL — kullanıcı Paratika'nın sayfasına gider, kart bilgisini orada girer
            var paymentUrl = $"{_hostedPageBaseUrl}/{sessionToken}";

            _logger.LogInformation("✅ Paratika SessionToken hazır | Token={Token} | Url={Url}", sessionToken, paymentUrl);

            return new ParatikaPaymentResponseDto
            {
                Success           = true,
                PaymentUrl        = paymentUrl,
                SessionToken      = sessionToken,
                MerchantPaymentId = merchantPayId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Paratika InitializePayment hatası");
            return Fail("Sistem hatası.", "SYSTEM_ERROR");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 2. CALLBACK — Paratika ödeme sonucunu buraya POST eder
    //    Başarılıysa:
    //      a) Üyeliği aktifleştir / kredi ekle
    //      b) Abonelik paketiyse → ADDRECURRINGPLAN + ADDRECURRINGPLANCARD
    // ═════════════════════════════════════════════════════════════════════════
    public async Task<ParatikaCallbackResult> ProcessCallbackAsync(ParatikaCallbackDto callback)
    {
        try
        {
            _logger.LogInformation(
                "🔔 Paratika Callback | PayId={Id} | Code={Code} | CardToken={Token}",
                callback.MerchantPaymentId, callback.ResponseCode,
                string.IsNullOrEmpty(callback.CardToken) ? "YOK" : "VAR");

            // Hash doğrulama (opsiyonel ama önerilir)
            if (!string.IsNullOrEmpty(_merchantSecretKey) && !VerifyCallbackHash(callback))
            {
                _logger.LogWarning("⚠️ Paratika callback hash doğrulaması başarısız | PayId={Id}", callback.MerchantPaymentId);
                // Hash yanlışsa işlemi reddet
                return new ParatikaCallbackResult { Success = false, ErrorMessage = "Hash doğrulaması başarısız" };
            }

            if (callback.ResponseCode != "00")
            {
                _logger.LogWarning("❌ Paratika ödeme başarısız | Code={Code} | Err={Err}", callback.ResponseCode, callback.ErrorMsg);
                await SaveFailedPaymentAsync(callback);
                return new ParatikaCallbackResult { Success = false, MerchantPaymentId = callback.MerchantPaymentId, ErrorMessage = callback.ErrorMsg, BankErrorCode = callback.PgTranErrorCode ?? callback.ResponseCode };
            }

            // CustomData'yı parse et
            var customData = ParseCustomData(callback.CustomData);
            if (customData == null)
            {
                // Paratika CUSTOMDATA'yı geri göndermeyebilir — fallback: MerchantPaymentId'den UserId çıkar
                _logger.LogWarning("⚠️ CustomData boş, MerchantPaymentId'den UserId çıkarılıyor | PayId={Id}", callback.MerchantPaymentId);
                var fallbackUserId = ExtractUserIdFromPaymentId(callback.MerchantPaymentId);
                if (fallbackUserId == 0)
                {
                    _logger.LogError("❌ CustomData ve MerchantPaymentId'den UserId alınamadı | PayId={Id}", callback.MerchantPaymentId);
                    return new ParatikaCallbackResult { Success = false, ErrorMessage = "CustomData parse hatası" };
                }
                // Tutara göre paket tahmin et
                var fallbackAmount = ParseAmount(callback.Amount);
                var fallbackProductCode = GuessProductCodeFromAmount(fallbackAmount);
                customData = new ParatikaCustomData
                {
                    UserId          = fallbackUserId.ToString(),
                    ProductCode     = fallbackProductCode,
                    Credits         = 0,   // FindPackage'dan doldurulacak
                    IsCredit        = false,
                    IsYearly        = false,
                    DurationDays    = 30,
                    DiscountPercent = 0,
                    OriginalPrice   = fallbackAmount,
                    DiscountedPrice = fallbackAmount
                };
                _logger.LogInformation("✅ Fallback customData oluşturuldu | UserId={Uid} | ProductCode={Pc}", fallbackUserId, fallbackProductCode);
            }

            // Duplicate kontrolü
            var exists = await _dbContext.PaymentHistories
                .AnyAsync(p => p.OrderId == callback.MerchantPaymentId && p.Status == "SUCCESS");
            if (exists)
            {
                _logger.LogWarning("⚠️ Duplicate callback engellendi | PayId={Id}", callback.MerchantPaymentId);
                return new ParatikaCallbackResult { Success = true, MerchantPaymentId = callback.MerchantPaymentId };
            }

            // Kullanıcı ve paket bul
            if (!int.TryParse(customData.UserId, out int userId))
            {
                _logger.LogError("❌ UserId parse hatası: {Uid}", customData.UserId);
                return new ParatikaCallbackResult { Success = false, ErrorMessage = "UserId geçersiz" };
            }

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogError("❌ Kullanıcı bulunamadı | UserId={Id}", userId);
                return new ParatikaCallbackResult { Success = false, ErrorMessage = "Kullanıcı bulunamadı" };
            }

            var package = FindPackage(customData.ProductCode);
            if (package == null)
            {
                _logger.LogError("❌ Paket bulunamadı | Code={Code}", customData.ProductCode);
                return new ParatikaCallbackResult { Success = false, ErrorMessage = "Paket bulunamadı" };
            }

            // İndirim kodu kullanım sayısını artır
            await IncrementDiscountCodeUsageAsync(customData.DiscountCode);

            // Üyelik/Kredi aktifleştir
            await ActivateMembershipOrCreditsAsync(user, package);

            // Ödeme kaydı
            decimal finalAmount = customData.DiscountedPrice > 0 ? customData.DiscountedPrice : package.PriceTry;
            await SavePaymentHistoryAsync(callback, package, userId, customData, finalAmount);

            bool isSubscription = !package.IsCredit;

            // ── Abonelik paketi → Recurring Plan kur ─────────────────────────
            if (isSubscription)
            {
                if (string.IsNullOrEmpty(callback.CardToken))
                {
                    // HPP'de kullanıcı "kartı kaydet" işaretlememişse token gelmez.
                    // Bu durumda recurring kuramayız — log at, aboneliği yine de kaydet
                    // ama "kart kaydedilmedi" uyarısı ver.
                    _logger.LogWarning("⚠️ CardToken YOK — HPP'de kart kaydedilmemiş. Recurring kurulamıyor | PayId={Id}", callback.MerchantPaymentId);
                    await SaveSubscriptionWithoutCardAsync(userId, package, customData, callback);
                }
                else
                {
                    _logger.LogInformation("🔄 Recurring plan kuruluyor | UserId={Id} | Paket={Pkg}", userId, package.Name);
                    await SetupRecurringAsync(userId, package, callback, customData, finalAmount);
                }
            }

            _logger.LogInformation("🎉 Paratika callback tamamlandı | PayId={Id} | UserId={Id2} | IsSubscription={Sub}",
                callback.MerchantPaymentId, userId, isSubscription);

            return new ParatikaCallbackResult
            {
                Success           = true,
                IsSubscription    = isSubscription,
                MerchantPaymentId = callback.MerchantPaymentId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Paratika callback hatası | PayId={Id}", callback.MerchantPaymentId);
            return new ParatikaCallbackResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 3. RECURRING NOTIFICATION — Paratika otomatik çekim yaptığında bildirir
    //    Panel → Settings → Notification URL buraya kayıtlı olmalı
    // ═════════════════════════════════════════════════════════════════════════
    public async Task ProcessRecurringNotificationAsync(ParatikaRecurringNotificationDto notification)
    {
        try
        {
            _logger.LogInformation(
                "🔄 Recurring Notification | PlanCode={Plan} | Code={Code} | Amount={Amt}",
                notification.PlanCode, notification.ResponseCode, notification.Amount);

            // Aboneliği bul
            var subscription = await _dbContext.Subscriptions
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.PlanCode == notification.PlanCode && s.Status == "ACTIVE");

            if (subscription == null)
            {
                _logger.LogWarning("⚠️ Recurring: Abonelik bulunamadı | PlanCode={Plan}", notification.PlanCode);
                return;
            }

            if (notification.ResponseCode != "00")
            {
                // Çekim başarısız
                _logger.LogWarning("❌ Recurring çekim başarısız | PlanCode={Plan} | Code={Code} | Err={Err}",
                    notification.PlanCode, notification.ResponseCode, notification.Error);

                subscription.Status    = "FAILED";
                subscription.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                // Başarısız ödeme kaydı
                _dbContext.PaymentHistories.Add(new PaymentHistory
                {
                    UserId        = subscription.UserId,
                    OrderId       = notification.MerchantPaymentId ?? $"REC-{notification.PlanCode}-{DateTime.UtcNow:yyyyMMdd}",
                    TransactionId = notification.PgTranId ?? "",
                    Amount        = ParseAmount(notification.Amount),
                    Currency      = "TRY",
                    ProductCode   = "",
                    PackageName   = subscription.PackageName,
                    Status        = "FAILED",
                    PaymentDate   = DateTime.UtcNow,
                    ErrorMessage  = $"Recurring başarısız | Code:{notification.ResponseCode} | {notification.Error}"
                });
                await _dbContext.SaveChangesAsync();
                return;
            }

            // Çekim başarılı → Üyelik süresini uzat
            var package = FindPackage(subscription.PackageAlias);
            if (package == null)
            {
                _logger.LogError("❌ Recurring: Paket bulunamadı | Alias={Alias}", subscription.PackageAlias);
                return;
            }

            var user = subscription.User;
            if (user == null)
            {
                _logger.LogError("❌ Recurring: Kullanıcı bulunamadı | UserId={Id}", subscription.UserId);
                return;
            }

            // Üyelik süresini uzat
            var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
            user.MembershipEnd = DateTime.SpecifyKind(
                (user.MembershipEnd.HasValue && user.MembershipEnd.Value > now
                    ? user.MembershipEnd.Value
                    : now).AddDays(package.DurationDays),
                DateTimeKind.Utc);
            user.Credits += package.Credits;
            user.PackageType = package.Name;
            _dbContext.Users.Update(user);

            // Abonelik kaydını güncelle
            subscription.Status          = "ACTIVE";
            subscription.NextBillingDate = now.AddDays(package.DurationDays);
            subscription.UpdatedAt       = now;

            // Başarılı ödeme kaydı
            var orderId = notification.MerchantPaymentId ?? $"REC-{notification.PlanCode}-{now:yyyyMMdd}";
            _dbContext.PaymentHistories.Add(new PaymentHistory
            {
                UserId        = subscription.UserId,
                OrderId       = orderId,
                TransactionId = notification.PgTranId ?? "",
                ProductCode   = package.ProductCode,
                PackageName   = package.Name,
                Amount        = ParseAmount(notification.Amount),
                Currency      = "TRY",
                CreditsAdded  = package.Credits,
                Status        = "SUCCESS",
                PaymentDate   = now,
                CardLastFour  = notification.CardPanMasked?.Length >= 4 ? notification.CardPanMasked[^4..] : null
            });

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "✅ Recurring çekim işlendi | PlanCode={Plan} | UserId={Id} | Yeni bitiş={End} | Kredi={Cred}",
                notification.PlanCode, subscription.UserId, user.MembershipEnd, user.Credits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Recurring notification hatası | PlanCode={Plan}", notification.PlanCode);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 4. ABONELİK İPTALİ — Paratika'da planı deaktif et, DB'de güncelle
    // ═════════════════════════════════════════════════════════════════════════
    public async Task<bool> CancelSubscriptionAsync(int userId, string? reason = null)
    {
        try
        {
            var subscription = await _dbContext.Subscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == "ACTIVE");

            if (subscription == null)
            {
                _logger.LogWarning("⚠️ İptal edilecek aktif abonelik bulunamadı | UserId={Id}", userId);
                return false;
            }

            // Paratika'da planı deaktif et (DELETERECURRINGPLANCARD)
            var cardDeleted = await DeleteRecurringPlanCardAsync(subscription.PlanCode, subscription.CardToken);
            if (!cardDeleted)
                _logger.LogWarning("⚠️ Paratika'da kart silinemedi, yine de DB'de iptal ediliyor | PlanCode={Plan}", subscription.PlanCode);

            // DB güncelle
            subscription.Status      = "CANCELLED";
            subscription.CancelledAt = DateTime.UtcNow;
            subscription.CancelReason = reason ?? "Kullanıcı tarafından iptal edildi";
            subscription.UpdatedAt   = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("✅ Abonelik iptal edildi | UserId={Id} | PlanCode={Plan}", userId, subscription.PlanCode);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Abonelik iptal hatası | UserId={Id}", userId);
            return false;
        }
    }

    public async Task<Subscription?> GetActiveSubscriptionAsync(int userId)
        => await _dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == "ACTIVE");

    // ═════════════════════════════════════════════════════════════════════════
    // 5. VERİFY — Tarayıcı kapanma güvenlik ağı
    // ═════════════════════════════════════════════════════════════════════════
    public async Task<PaymentVerificationResult> VerifyAndProcessPaymentAsync(string merchantPaymentId)
    {
        try
        {
            _logger.LogInformation("🔍 Paratika Verify | PayId={Id}", merchantPaymentId);

            var existing = await _dbContext.PaymentHistories
                .Where(p => p.OrderId == merchantPaymentId && p.Status == "SUCCESS")
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                var existingUser = await _dbContext.Users.FindAsync(existing.UserId);
                return new PaymentVerificationResult { Success = true, IsAlreadyProcessed = true, CreditsAdded = existing.CreditsAdded, PackageName = existing.PackageName, UserId = existing.UserId, MembershipEnd = existingUser?.MembershipEnd };
            }

            var query = await QueryTransactionAsync(merchantPaymentId);
            if (query == null || query.ResponseCode != "00")
                return new PaymentVerificationResult { Success = false, ErrorMessage = query?.ResponseMsg ?? "Sorgu başarısız" };

            // Callback simülasyonu
            var fakeCallback = new ParatikaCallbackDto
            {
                MerchantPaymentId = merchantPaymentId,
                ResponseCode      = "00",
                ResponseMsg       = "Onaylı (Verified)",
                PgTranId          = query.PgTranId,
                Amount            = query.Amount,
                InstallmentCount  = query.InstallmentCount,
                CardPanMasked     = query.CardPanMasked,
                // Not: verify senaryosunda cardToken gelmiyor, recurring kurulmayacak
                // Kullanıcı bir sonraki girişte bilgilendirilmeli
            };

            var result = await ProcessCallbackAsync(fakeCallback);
            if (!result.Success)
                return new PaymentVerificationResult { Success = false, ErrorMessage = result.ErrorMessage };

            var payment = await _dbContext.PaymentHistories
                .Where(p => p.OrderId == merchantPaymentId && p.Status == "SUCCESS")
                .FirstOrDefaultAsync();

            var user = payment != null ? await _dbContext.Users.FindAsync(payment.UserId) : null;
            return new PaymentVerificationResult
            {
                Success            = true,
                IsAlreadyProcessed = false,
                CreditsAdded       = payment?.CreditsAdded ?? 0,
                PackageName        = payment?.PackageName ?? "",
                UserId             = payment?.UserId ?? 0,
                MembershipEnd      = user?.MembershipEnd
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Paratika Verify hatası | PayId={Id}", merchantPaymentId);
            return new PaymentVerificationResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public List<FgsTradePackage> GetAvailablePackages() => _packages;

    // ═════════════════════════════════════════════════════════════════════════
    // PRIVATE: Recurring Plan Kurma Akışı
    // Adım 1 → ADDRECURRINGPLAN (plan oluştur)
    // Adım 2 → ADDRECURRINGPLANCARD (kart bağla → ilk çekimden sonra otomatik çalışır)
    // ═════════════════════════════════════════════════════════════════════════
    private async Task SetupRecurringAsync(
        int userId, FgsTradePackage package,
        ParatikaCallbackDto callback, ParatikaCustomData customData, decimal finalAmount)
    {
        var planCode = BuildPlanCode(userId, package.Alias);
        var period   = package.IsYearly ? "YEARLY" : "MONTHLY";
        var amountStr = finalAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        // ── Adım 1: Plan oluştur ──────────────────────────────────────────────
        _logger.LogInformation("🔄 ADDRECURRINGPLAN | PlanCode={Plan} | Period={Per} | Amount={Amt}", planCode, period, amountStr);

        var planForm = new Dictionary<string, string>
        {
            ["ACTION"]           = "ADDRECURRINGPLAN",
            ["MERCHANTUSER"]     = _merchantUser,
            ["MERCHANTPASSWORD"] = _merchantPassword,
            ["MERCHANT"]         = _merchant,
            ["PLANCODE"]         = planCode,
            ["DESCRIPTION"]      = $"FGSTrade {package.NameTr} Aboneliği",
            ["PERIOD"]           = period,   // MONTHLY veya YEARLY
            ["AMOUNT"]           = amountStr,
            ["CURRENCY"]         = "TRY",
            ["MAXPAYMENTCOUNT"]  = "0",      // 0 = sonsuz (kullanıcı iptal edene kadar)
            ["NOTIFICATIONURL"]  = _recurringNotifUrl,
        };

        var planResp = await _httpClient.PostAsync(_apiBaseUrl, new FormUrlEncodedContent(planForm));
        var planRaw  = await planResp.Content.ReadAsStringAsync();
        _logger.LogInformation("◀ ADDRECURRINGPLAN yanıt | {Body}", planRaw.Length > 300 ? planRaw[..300] : planRaw);

        using var planDoc  = JsonDocument.Parse(planRaw);
        var planRoot       = planDoc.RootElement;
        var planCode_resp  = GetStr(planRoot, "RESPONSECODE");

        if (planCode_resp != "00")
        {
            var planErr = GetStr(planRoot, "RESPONSEMSG") ?? "Plan oluşturma hatası";
            _logger.LogError("❌ ADDRECURRINGPLAN başarısız | Code={Code} | Msg={Msg}", planCode_resp, planErr);
            // Plan kurulamazsa üyelik zaten aktifleşti, ama recurring çalışmayacak
            await SaveSubscriptionWithoutCardAsync(userId, package, customData, callback);
            return;
        }

        _logger.LogInformation("✅ Recurring plan oluşturuldu | PlanCode={Plan}", planCode);

        // ── Adım 2: Kart bağla ───────────────────────────────────────────────
        _logger.LogInformation("🔄 ADDRECURRINGPLANCARD | PlanCode={Plan} | CardToken={Token}", planCode, callback.CardToken);

        var cardForm = new Dictionary<string, string>
        {
            ["ACTION"]           = "ADDRECURRINGPLANCARD",
            ["MERCHANTUSER"]     = _merchantUser,
            ["MERCHANTPASSWORD"] = _merchantPassword,
            ["MERCHANT"]         = _merchant,
            ["PLANCODE"]         = planCode,
            ["CARDTOKEN"]        = callback.CardToken!,
            // İlk çekim zaten yapıldı (callback = ilk ödeme), bir sonraki çekim period sonra
            // STARTDATE boş bırakılırsa Paratika bugünden itibaren sayar
            // Biz bir period sonrasını veriyoruz:
            ["STARTDATE"]        = GetNextBillingDate(package).ToString("yyyy-MM-dd"),
        };

        var cardResp = await _httpClient.PostAsync(_apiBaseUrl, new FormUrlEncodedContent(cardForm));
        var cardRaw  = await cardResp.Content.ReadAsStringAsync();
        _logger.LogInformation("◀ ADDRECURRINGPLANCARD yanıt | {Body}", cardRaw.Length > 300 ? cardRaw[..300] : cardRaw);

        using var cardDoc  = JsonDocument.Parse(cardRaw);
        var cardRoot       = cardDoc.RootElement;
        var cardCode_resp  = GetStr(cardRoot, "RESPONSECODE");

        if (cardCode_resp != "00")
        {
            var cardErr = GetStr(cardRoot, "RESPONSEMSG") ?? "Kart bağlama hatası";
            _logger.LogError("❌ ADDRECURRINGPLANCARD başarısız | Code={Code} | Msg={Msg}", cardCode_resp, cardErr);
            await SaveSubscriptionWithoutCardAsync(userId, package, customData, callback);
            return;
        }

        // ── DB'ye abonelik kaydet ─────────────────────────────────────────────
        var nextBilling = GetNextBillingDate(package);
        var subscription = new Subscription
        {
            UserId          = userId,
            PlanCode        = planCode,
            PackageAlias    = package.Alias,
            PackageName     = package.Name,
            Amount          = finalAmount,
            Period          = period,
            Status          = "ACTIVE",
            StartDate       = DateTime.UtcNow,
            NextBillingDate = nextBilling,
            CardToken       = callback.CardToken,
            CardLastFour    = callback.CardPanMasked?.Length >= 4 ? callback.CardPanMasked[^4..] : null,
            CardBrand       = callback.CardBrand,
            DiscountCode    = customData.DiscountCode,
        };

        _dbContext.Subscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "✅ Recurring tam kuruldu | UserId={Id} | PlanCode={Plan} | NextBilling={Next}",
            userId, planCode, nextBilling);
    }

    private async Task<bool> DeleteRecurringPlanCardAsync(string planCode, string? cardToken)
    {
        try
        {
            if (string.IsNullOrEmpty(cardToken)) return false;

            var form = new Dictionary<string, string>
            {
                ["ACTION"]           = "DELETERECURRINGPLANCARD",
                ["MERCHANTUSER"]     = _merchantUser,
                ["MERCHANTPASSWORD"] = _merchantPassword,
                ["MERCHANT"]         = _merchant,
                ["PLANCODE"]         = planCode,
                ["CARDTOKEN"]        = cardToken,
            };

            var resp = await _httpClient.PostAsync(_apiBaseUrl, new FormUrlEncodedContent(form));
            var raw  = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(raw);
            var code = GetStr(doc.RootElement, "RESPONSECODE");

            _logger.LogInformation("DELETERECURRINGPLANCARD | PlanCode={Plan} | Code={Code}", planCode, code);
            return code == "00";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DELETERECURRINGPLANCARD hatası | PlanCode={Plan}", planCode);
            return false;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PRIVATE: Üyelik/Kredi Aktifleştirme
    // ═════════════════════════════════════════════════════════════════════════
    private async Task ActivateMembershipOrCreditsAsync(User user, FgsTradePackage package)
    {
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        if (package.IsCredit)
        {
            user.Credits += package.Credits;
            _logger.LogInformation("💰 Kredi eklendi | UserId={Id} | +{Add} = {Total}", user.Id, package.Credits, user.Credits);
        }
        else
        {
            user.PackageType      = package.Name;
            user.MembershipStart  = now;
            // Mevcut üyelik bitmemişse üstüne ekle
            var baseDate = user.MembershipEnd.HasValue && user.MembershipEnd.Value > now
                ? user.MembershipEnd.Value : now;
            user.MembershipEnd = DateTime.SpecifyKind(baseDate.AddDays(package.DurationDays), DateTimeKind.Utc);
            user.Credits      += package.Credits;
            user.MaxResultsPerSearch = Math.Max(user.MaxResultsPerSearch, 200);

            _logger.LogInformation(
                "👑 Üyelik aktifleştirildi | UserId={Id} | Paket={Pkg} | Bitiş={End} | Kredi={Cred}",
                user.Id, package.Name, user.MembershipEnd, user.Credits);
        }

        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync();
    }

    private async Task SavePaymentHistoryAsync(
        ParatikaCallbackDto callback, FgsTradePackage package,
        int userId, ParatikaCustomData customData, decimal finalAmount)
    {
        _dbContext.PaymentHistories.Add(new PaymentHistory
        {
            UserId             = userId,
            OrderId            = callback.MerchantPaymentId ?? "",
            TransactionId      = callback.PgTranId ?? "",
            ProductCode        = package.ProductCode,
            PackageName        = package.Name,
            Amount             = finalAmount,
            Currency           = "TRY",
            CreditsAdded       = package.Credits,
            Status             = "SUCCESS",
            PaymentDate        = DateTime.UtcNow,
            Installment        = int.TryParse(callback.InstallmentCount, out int inst) ? inst : 1,
            CardLastFour       = callback.CardPanMasked?.Length >= 4 ? callback.CardPanMasked[^4..] : null,
            DiscountCode       = customData.DiscountCode,
            DiscountPercentage = customData.DiscountPercent > 0 ? (int)customData.DiscountPercent : null,
            FinalAmount        = finalAmount
        });
        await _dbContext.SaveChangesAsync();
    }

    private async Task SaveSubscriptionWithoutCardAsync(
        int userId, FgsTradePackage package,
        ParatikaCustomData customData, ParatikaCallbackDto callback)
    {
        // Recurring kurulamadı ama üyelik aktif — aboneliği "kart yok" durumunda kaydet
        var existing = await _dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == "ACTIVE");

        if (existing != null) return; // Zaten var

        _dbContext.Subscriptions.Add(new Subscription
        {
            UserId       = userId,
            PlanCode     = BuildPlanCode(userId, package.Alias),
            PackageAlias = package.Alias,
            PackageName  = package.Name,
            Amount       = customData.DiscountedPrice > 0 ? customData.DiscountedPrice : package.PriceTry,
            Period       = package.IsYearly ? "YEARLY" : "MONTHLY",
            Status       = "ACTIVE",
            StartDate    = DateTime.UtcNow,
            NextBillingDate = GetNextBillingDate(package),
            CardToken    = null, // Kart yok — otomatik yenileme olmayacak
            CardLastFour = callback.CardPanMasked?.Length >= 4 ? callback.CardPanMasked[^4..] : null,
            DiscountCode = customData.DiscountCode,
        });
        await _dbContext.SaveChangesAsync();
        _logger.LogWarning("⚠️ Abonelik kart olmadan kaydedildi (recurring yok) | UserId={Id}", userId);
    }

    private async Task SaveFailedPaymentAsync(ParatikaCallbackDto callback)
    {
        try
        {
            // UserId'yi MerchantPaymentId'den çıkar
            var uid = ExtractUserIdFromPaymentId(callback.MerchantPaymentId);
            if (uid == 0) return;

            _dbContext.PaymentHistories.Add(new PaymentHistory
            {
                UserId        = uid,
                OrderId       = callback.MerchantPaymentId ?? "",
                TransactionId = callback.PgTranId ?? "",
                Amount        = ParseAmount(callback.Amount),
                Currency      = "TRY",
                Status        = "FAILED",
                PaymentDate   = DateTime.UtcNow,
                ErrorMessage  = $"Code:{callback.ResponseCode} | {callback.ErrorMsg}"
            });
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex) { _logger.LogError(ex, "Başarısız ödeme kayıt hatası"); }
    }

    private async Task IncrementDiscountCodeUsageAsync(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;
        var dc = await _dbContext.DiscountCodes.FirstOrDefaultAsync(d => d.Code == code && d.IsActive);
        if (dc == null) return;
        dc.CurrentUses++;
        if (dc.CurrentUses >= dc.MaxUses) dc.IsActive = false;
        await _dbContext.SaveChangesAsync();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // QUERYTRANSACTION
    // ═════════════════════════════════════════════════════════════════════════
    public async Task<ParatikaQueryResponseDto?> QueryTransactionAsync(string merchantPaymentId)
    {
        try
        {
            var form = new Dictionary<string, string>
            {
                ["ACTION"]            = "QUERYTRANSACTION",
                ["MERCHANTUSER"]      = _merchantUser,
                ["MERCHANTPASSWORD"]  = _merchantPassword,
                ["MERCHANT"]          = _merchant,
                ["MERCHANTPAYMENTID"] = merchantPaymentId,
            };

            var resp = await _httpClient.PostAsync(_apiBaseUrl, new FormUrlEncodedContent(form));
            var raw  = await resp.Content.ReadAsStringAsync();

            _logger.LogInformation("QUERYTRANSACTION | PayId={Id} | Response={Body}", merchantPaymentId, raw.Length > 300 ? raw[..300] : raw);

            if (!resp.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            return new ParatikaQueryResponseDto
            {
                ResponseCode      = GetStr(root, "RESPONSECODE"),
                ResponseMsg       = GetStr(root, "RESPONSEMSG"),
                PgTranId          = GetStr(root, "PGTRANID"),
                PgTranApprCode    = GetStr(root, "PGTRANREFID"),
                Amount            = GetStr(root, "AMOUNT"),
                InstallmentCount  = GetStr(root, "INSTALLMENTCOUNT"),
                MerchantPaymentId = GetStr(root, "MERCHANTPAYMENTID"),
                CardPanMasked     = GetStr(root, "CARDPANMASKED"),
                ErrorCode         = GetStr(root, "ERRORCODE"),
                Error             = GetStr(root, "ERROR"),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QUERYTRANSACTION hatası | PayId={Id}", merchantPaymentId);
            return null;
        }
    }

    // ─── Yardımcı metodlar ────────────────────────────────────────────────────

   private bool VerifyCallbackHash(ParatikaCallbackDto cb)
{
    // Loglarda 'SD_SHA512' ve 'sdSha512' anahtarlarının her ikisinin de geldiği görünüyor.
    // Paratika dokümanına göre sıralama: merchantPaymentId | customerId | sessionToken | responseCode | random | secretKey
    
    if (string.IsNullOrEmpty(cb.SdSha512) || string.IsNullOrEmpty(cb.Random)) 
    {
        _logger.LogWarning("Hash parametreleri eksik! SdSha512: {H}, Random: {R}", cb.SdSha512, cb.Random);
        return false; 
    }

    // Paratika bazen customerId boşsa onu string.Empty olarak bekler.
    var customerId = cb.CustomerId ?? "";
    var raw = $"{cb.MerchantPaymentId}|{customerId}|{cb.SessionToken}|{cb.ResponseCode}|{cb.Random}|{_merchantSecretKey}";
    
    using var sha = SHA512.Create();
    var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
    var computedHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

    _logger.LogInformation("Hash Karşılaştırma -> Hesaplanan: {CH} | Gelen: {GH}", computedHash, cb.SdSha512.ToLowerInvariant());

    return computedHash == cb.SdSha512.ToLowerInvariant();
}

    private static string BuildPlanCode(int userId, string packageAlias)
    {
        // PlanCode unique olmalı: "FGS-{userId}-{alias}" — max 50 karakter
        var code = $"FGS-{userId}-{packageAlias}";
        return code.Length > 50 ? code[..50] : code;
    }

    private static DateTime GetNextBillingDate(FgsTradePackage package)
        => DateTime.UtcNow.AddDays(package.DurationDays);

    private static int ExtractUserIdFromPaymentId(string? payId)
    {
        if (string.IsNullOrEmpty(payId) || !payId.StartsWith("FGS") || payId.Length <= 13) return 0;
        return int.TryParse(payId.Substring(13).TrimStart('0'), out int uid) ? uid : 0;
    }

    private static decimal ParseAmount(string? amt)
    {
        if (string.IsNullOrEmpty(amt)) return 0;
        return decimal.TryParse(amt, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out decimal v) ? v : 0;
    }

    private ParatikaCustomData? ParseCustomData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<ParatikaCustomData>(json); }
        catch { return null; }
    }

    private string GuessProductCodeFromAmount(decimal amountTL)
    {
        var package = _packages.OrderBy(p => Math.Abs(p.PriceTry - amountTL)).FirstOrDefault();
        if (package != null)
        {
            _logger.LogInformation("💡 Amount'dan paket tahmini | {Amt} TL → {Pkg}", amountTL, package.Name);
            return package.ProductCode;
        }
        return "1274715"; // Starter Monthly fallback
    }

    private FgsTradePackage? FindPackage(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var exact = _packages.FirstOrDefault(p =>
            p.ProductCode == code ||
            p.Alias.Equals(code, StringComparison.OrdinalIgnoreCase) ||
            p.Name.Equals(code, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "starter", "starter_monthly" }, { "pro", "pro_monthly" },
            { "professional", "pro_monthly" }, { "business", "business_monthly" },
        };
        return map.TryGetValue(code, out var alias)
            ? _packages.FirstOrDefault(p => p.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase))
            : null;
    }

    private static string? GetStr(JsonElement el, params string[] keys)
    {
        foreach (var k in keys)
        {
            // Önce verilen key'i dene, sonra büyük harf, sonra küçük harf
            foreach (var attempt in new[] { k, k.ToUpperInvariant(), k.ToLowerInvariant() })
            {
                if (el.TryGetProperty(attempt, out var v))
                    return v.ValueKind == JsonValueKind.String ? v.GetString()
                         : v.ValueKind == JsonValueKind.Number ? v.GetInt64().ToString() : null;
            }
        }
        return null;
    }

    private static string Env(string key, string fallback)
        => (Environment.GetEnvironmentVariable(key) ?? fallback).Trim();

    private static ParatikaPaymentResponseDto Fail(string msg, string code)
        => new() { Success = false, ErrorMessage = msg, ErrorCode = code };
}