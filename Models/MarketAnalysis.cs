using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradeScout.API.Models;

/// <summary>
/// Market Analysis entity - Pazar analizi aramaları kayıtları
/// </summary>
[Table("MarketAnalyses")]
public class MarketAnalysis
{
    [Key]
    [Column("Id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Kullanıcı ID (nullable - anonim aramalar için)
    /// </summary>
    [Column("UserId")]
    public int? UserId { get; set; }

    /// <summary>
    /// İlişkili kullanıcı
    /// </summary>
    [ForeignKey("UserId")]
    public User? User { get; set; }

    /// <summary>
    /// GTIP/HS Kodu
    /// </summary>
    [Required]
    [Column("HsCode")]
    [MaxLength(20)]
    public string HsCode { get; set; } = string.Empty;

    /// <summary>
    /// Ürün adı
    /// </summary>
    [Required]
    [Column("ProductName")]
    [MaxLength(255)]
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Hedef ülke (ihracat yapılacak ülke)
    /// </summary>
    [Required]
    [Column("TargetCountry")]
    [MaxLength(100)]
    public string TargetCountry { get; set; } = string.Empty;

    /// <summary>
    /// Menşei ülke
    /// </summary>
    [Column("OriginCountry")]
    [MaxLength(100)]
    public string OriginCountry { get; set; } = "Türkiye";

    /// <summary>
    /// Rapor içeriği (Markdown formatında)
    /// </summary>
    [Column("ReportContent")]
    public string? ReportContent { get; set; }

    /// <summary>
    /// Rapor başarıyla oluşturuldu mu?
    /// </summary>
    [Column("IsSuccessful")]
    public bool IsSuccessful { get; set; } = true;

    /// <summary>
    /// Hata mesajı (başarısız ise)
    /// </summary>
    [Column("ErrorMessage")]
    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// PDF olarak indirildi mi?
    /// </summary>
    [Column("PdfDownloaded")]
    public bool PdfDownloaded { get; set; } = false;

    /// <summary>
    /// PDF indirme tarihi
    /// </summary>
    [Column("PdfDownloadedAt")]
    public DateTime? PdfDownloadedAt { get; set; }

    /// <summary>
    /// Rapor oluşturulma tarihi
    /// </summary>
    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Kullanıcının IP adresi
    /// </summary>
    [Column("IpAddress")]
    [MaxLength(50)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// Kullanıcı tarayıcı bilgisi
    /// </summary>
    [Column("UserAgent")]
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// Rapor görüntülenme sayısı
    /// </summary>
    [Column("ViewCount")]
    public int ViewCount { get; set; } = 1;

    /// <summary>
    /// Rapor favorilere eklendi mi?
    /// </summary>
    [Column("IsFavorite")]
    public bool IsFavorite { get; set; } = false;

    /// <summary>
    /// Rapor notu (kullanıcı tarafından eklenebilir)
    /// </summary>
    [Column("Notes")]
    [MaxLength(1000)]
    public string? Notes { get; set; }
}
