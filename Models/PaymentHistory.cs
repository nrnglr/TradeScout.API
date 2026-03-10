using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradeScout.API.Models;

/// <summary>
/// Ödeme geçmişi entity - Tüm ödeme işlemlerini kaydeder
/// </summary>
[Table("PaymentHistories")]
public class PaymentHistory
{
    [Key]
    [Column("Id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Ödeme yapan kullanıcının ID'si
    /// </summary>
    [Required]
    [Column("UserId")]
    public int UserId { get; set; }

    /// <summary>
    /// Sipariş numarası (FGS-yyyyMMddHHmmss-UserId-randomhex formatında)
    /// </summary>
    [Required]
    [Column("OrderId")]
    [MaxLength(100)]
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// Tosla işlem numarası
    /// </summary>
    [Column("TransactionId")]
    [MaxLength(100)]
    public string TransactionId { get; set; } = string.Empty;

    /// <summary>
    /// Ürün/Paket kodu
    /// </summary>
    [Required]
    [Column("ProductCode")]
    [MaxLength(20)]
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>
    /// Paket adı
    /// </summary>
    [Column("PackageName")]
    [MaxLength(50)]
    public string PackageName { get; set; } = string.Empty;

    /// <summary>
    /// Ödeme tutarı
    /// </summary>
    [Required]
    [Column("Amount")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Para birimi (USD, TRY)
    /// </summary>
    [Required]
    [Column("Currency")]
    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Eklenen kredi miktarı
    /// </summary>
    [Required]
    [Column("CreditsAdded")]
    public int CreditsAdded { get; set; }

    /// <summary>
    /// Ödeme durumu (SUCCESS, FAILED, PENDING)
    /// </summary>
    [Required]
    [Column("Status")]
    [MaxLength(20)]
    public string Status { get; set; } = "PENDING";

    /// <summary>
    /// Ödeme tarihi
    /// </summary>
    [Required]
    [Column("PaymentDate")]
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Taksit sayısı
    /// </summary>
    [Column("Installment")]
    public int Installment { get; set; } = 1;

    /// <summary>
    /// Kart son 4 hanesi (güvenlik için sadece son 4)
    /// </summary>
    [Column("CardLastFour")]
    [MaxLength(4)]
    public string? CardLastFour { get; set; }

    /// <summary>
    /// Hata mesajı (başarısız ödemelerde)
    /// </summary>
    [Column("ErrorMessage")]
    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Kayıt oluşturulma zamanı
    /// </summary>
    [Required]
    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey("UserId")]
    public virtual User? User { get; set; }
}
