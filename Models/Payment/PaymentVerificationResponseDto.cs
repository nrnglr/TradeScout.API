namespace TradeScout.API.Models.Payment;

/// <summary>
/// Payment verification response DTO
/// Frontend'den gelen verify isteğine dönen response
/// </summary>
public class PaymentVerificationResponseDto
{
    /// <summary>
    /// İşlem başarılı mı?
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// İşlem mesajı
    /// </summary>
    public string? Message { get; set; }
    
    /// <summary>
    /// Sipariş numarası
    /// </summary>
    public string? OrderId { get; set; }
    
    /// <summary>
    /// Eklenen kredi miktarı
    /// </summary>
    public int CreditsAdded { get; set; }
    
    /// <summary>
    /// Paket adı
    /// </summary>
    public string? PackageName { get; set; }
    
    /// <summary>
    /// Bu ödeme daha önce işlendi mi?
    /// </summary>
    public bool IsAlreadyProcessed { get; set; }
    
    /// <summary>
    /// Kullanıcı ID (opsiyonel)
    /// </summary>
    public int? UserId { get; set; }
    
    /// <summary>
    /// Üyelik bitiş tarihi (eğer üyelik paketiyse)
    /// </summary>
    public DateTime? MembershipEnd { get; set; }
}

/// <summary>
/// Payment verification result (internal)
/// </summary>
public class PaymentVerificationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int CreditsAdded { get; set; }
    public string? PackageName { get; set; }
    public bool IsAlreadyProcessed { get; set; }
    public int UserId { get; set; }
}
