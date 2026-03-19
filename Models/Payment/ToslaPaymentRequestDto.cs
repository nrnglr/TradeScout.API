namespace TradeScout.API.Models.Payment;

/// <summary>
/// Tosla ödeme sistemi için request DTO
/// Kullanıcı bir pakete tıkladığında Tosla'ya gönderilecek veri
/// </summary>
public class ToslaPaymentRequestDto
{
    /// <summary>
    /// Kullanıcı ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>
    /// Ürün/Paket kodu (örn: 1274716)
    /// </summary>
    public string ProductCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Ödeme tutarı (örn: 99)
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Para birimi (varsayılan: USD)
    /// </summary>
    public string Currency { get; set; } = "USD";
    
    /// <summary>
    /// Taksit sayısı (yıllık paketlerde 12'ye kadar çıkabilir)
    /// </summary>
    public int Installment { get; set; } = 1;
    
    /// <summary>
    /// İndirim kodu (opsiyonel)
    /// </summary>
    public string? DiscountCode { get; set; }
}

/// <summary>
/// Tosla ödeme başlatma yanıtı
/// </summary>
public class ToslaPaymentResponseDto
{
    /// <summary>
    /// İşlem başarılı mı?
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Ödeme URL'si (kullanıcı buraya yönlendirilecek)
    /// Ortak Ödeme Sayfası: {BaseUrl}/threeDSecure/{ThreeDSessionId}
    /// </summary>
    public string? PaymentUrl { get; set; }
    
    /// <summary>
    /// İşlem referans numarası
    /// </summary>
    public string? TransactionId { get; set; }
    
    /// <summary>
    /// 3D Session ID - Tosla'dan dönen benzersiz session numarası
    /// </summary>
    public string? ThreeDSessionId { get; set; }
    
    /// <summary>
    /// Hata mesajı (varsa)
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Hata kodu (varsa)
    /// </summary>
    public string? ErrorCode { get; set; }
}

/// <summary>
/// Tosla callback (webhook) verisi
/// 3D İşlem sonucu callbackUrl'e POST edilir
/// Doküman: BankResponseCode = "00" ise ödeme başarılı
/// </summary>
public class ToslaCallbackDto
{
    /// <summary>
    /// 0 ise işlem servis isteği başarılıdır. Diğer numaralar hatalıdır.
    /// </summary>
    public int Code { get; set; }
    
    /// <summary>
    /// İşleme ait mesaj
    /// </summary>
    public string? Message { get; set; }
    
    /// <summary>
    /// İşlemin sipariş numarası
    /// </summary>
    public string? OrderId { get; set; }
    
    /// <summary>
    /// Banka tarafındaki işlem statü kodu. 
    /// "00" ise ödeme başarılı, diğer tüm durumlar başarısız.
    /// </summary>
    public string? BankResponseCode { get; set; }
    
    /// <summary>
    /// Banka tarafındaki işlem statü mesajı
    /// </summary>
    public string? BankResponseMessage { get; set; }
    
    /// <summary>
    /// İşlemin bankadaki otorizasyon kodu
    /// </summary>
    public string? AuthCode { get; set; }
    
    /// <summary>
    /// Host Reference Number
    /// </summary>
    public string? HostReferenceNumber { get; set; }
    
    /// <summary>
    /// İşlemin sistemdeki numarası
    /// </summary>
    public string? TransactionId { get; set; }
    
    /// <summary>
    /// İşlemde kullanılan kartın hamili
    /// </summary>
    public string? CardHolderName { get; set; }
    
    /// <summary>
    /// Ödeme tutarı (kuruş cinsinden, örn: 9900 = 99 TL)
    /// </summary>
    public long Amount { get; set; }
    
    /// <summary>
    /// 3D MdStatus değeri (1 = başarılı)
    /// </summary>
    public string? MdStatus { get; set; }
    
    /// <summary>
    /// İşlem durumu (1 = başarılı)
    /// </summary>
    public int RequestStatus { get; set; }
    
    /// <summary>
    /// Hash (doğrulama için - Tosla API'nin gönderdiği)
    /// </summary>
    public string? Hash { get; set; }
    
    /// <summary>
    /// Echo - gönderdiğimiz bilgi geri döner
    /// Format: UserId|ProductCode
    /// </summary>
    public string? Echo { get; set; }
    
    /// <summary>
    /// Extra Parameters - JSON formatında ek bilgiler
    /// </summary>
    public string? ExtraParameters { get; set; }
}

/// <summary>
/// FGS Trade paket tanımları
/// </summary>
public static class FgsTradePackages
{
    /// <summary>
    /// Tüm paketler
    /// </summary>
    public static readonly Dictionary<string, PackageInfo> All = new()
    {
        { "1274715", new PackageInfo("1274715", "Starter", 15m, 10, "Aylık") },
        { "1274739", new PackageInfo("1274739", "Basic", 39m, 30, "Aylık") },
        { "1274779", new PackageInfo("1274779", "Professional", 79m, 75, "Aylık") },
        { "1274716", new PackageInfo("1274716", "Business", 99m, 100, "Aylık") },
        { "1274740", new PackageInfo("1274740", "Enterprise", 299m, 500, "Yıllık") },
        { "1274780", new PackageInfo("1274780", "Ultimate", 599m, 1500, "Yıllık") }
    };

    /// <summary>
    /// Paket koduna göre kredi miktarını döndür
    /// </summary>
    public static int GetCredits(string productCode)
    {
        return All.TryGetValue(productCode, out var package) ? package.Credits : 0;
    }

    /// <summary>
    /// Paket kodunun geçerli olup olmadığını kontrol et
    /// </summary>
    public static bool IsValidPackage(string productCode)
    {
        return All.ContainsKey(productCode);
    }
}

/// <summary>
/// Paket bilgisi
/// </summary>
public class PackageInfo
{
    public string Code { get; }
    public string Name { get; }
    public decimal Price { get; }
    public int Credits { get; }
    public string Period { get; }

    public PackageInfo(string code, string name, decimal price, int credits, string period)
    {
        Code = code;
        Name = name;
        Price = price;
        Credits = credits;
        Period = period;
    }
}
