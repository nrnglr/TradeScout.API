using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradeScout.API.Models;

/// <summary>
/// Kullanıcının aktif Paratika aboneliğini takip eder.
/// Paratika tarafında plan + kart kaydı var, biz burada mirror tutuyoruz.
/// </summary>
[Table("Subscriptions")]
public class Subscription
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>Aboneliği olan kullanıcı</summary>
    [Required]
    public int UserId { get; set; }

    /// <summary>
    /// Paratika'daki recurring plan kodu.
    /// ADDRECURRINGPLAN'a gönderdiğimiz PLANCODE — biz üretiyoruz: "FGS-{userId}-{alias}"
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string PlanCode { get; set; } = string.Empty;

    /// <summary>Paket alias'ı: starter_monthly, pro_monthly vs.</summary>
    [Required]
    [MaxLength(50)]
    public string PackageAlias { get; set; } = string.Empty;

    /// <summary>Paket adı: Starter, Pro, Business</summary>
    [MaxLength(50)]
    public string PackageName { get; set; } = string.Empty;

    /// <summary>Aylık veya yıllık tutar (TL)</summary>
    [Required]
    public decimal Amount { get; set; }

    /// <summary>Periyot: MONTHLY veya YEARLY</summary>
    [Required]
    [MaxLength(10)]
    public string Period { get; set; } = "MONTHLY"; // MONTHLY | YEARLY

    /// <summary>
    /// ACTIVE   → abonelik çalışıyor
    /// CANCELLED → kullanıcı iptal etti
    /// FAILED    → son çekim başarısız, yeniden deneniyor
    /// EXPIRED   → Paratika planı sona erdi
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "ACTIVE";

    /// <summary>Abonelik başlangıç tarihi (ilk ödeme tarihi)</summary>
    [Required]
    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    /// <summary>Bir sonraki çekim tarihi (Paratika'nın çekeceği tarih)</summary>
    public DateTime? NextBillingDate { get; set; }

    /// <summary>İptal tarihi</summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>İptal sebebi</summary>
    [MaxLength(500)]
    public string? CancelReason { get; set; }

    /// <summary>
    /// Paratika callback'ten gelen cardToken — 
    /// ADDRECURRINGPLANCARD'a bu token gönderilir
    /// </summary>
    [MaxLength(128)]
    public string? CardToken { get; set; }

    /// <summary>Kart son 4 hane (gösterim için)</summary>
    [MaxLength(4)]
    public string? CardLastFour { get; set; }

    /// <summary>Kart markası: VISA, MASTERCARD vs.</summary>
    [MaxLength(20)]
    public string? CardBrand { get; set; }

    /// <summary>İndirim kodu (ilk ödemede kullanıldıysa)</summary>
    [MaxLength(50)]
    public string? DiscountCode { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("UserId")]
    public virtual User? User { get; set; }
}