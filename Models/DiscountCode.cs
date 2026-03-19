using System.ComponentModel.DataAnnotations;

namespace TradeScout.API.Models;

/// <summary>
/// İndirim kodu modeli
/// </summary>
public class DiscountCode
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// İndirim kodu (Örn: FGS10_AB12CD)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// İndirim yüzdesi (Örn: 10 = %10 indirim)
    /// </summary>
    [Required]
    public int DiscountPercentage { get; set; }

    /// <summary>
    /// Maksimum kullanım sayısı
    /// </summary>
    [Required]
    public int MaxUses { get; set; }

    /// <summary>
    /// Şu ana kadar kaç kez kullanıldı
    /// </summary>
    [Required]
    public int CurrentUses { get; set; }

    /// <summary>
    /// Kod aktif mi?
    /// </summary>
    [Required]
    public bool IsActive { get; set; }

    /// <summary>
    /// Kod oluşturulma tarihi
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Son kullanma tarihi (opsiyonel)
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Açıklama (opsiyonel)
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }
}
