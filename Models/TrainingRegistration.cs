using System.ComponentModel.DataAnnotations;

namespace TradeScout.API.Models;

public class TrainingRegistration
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Country { get; set; } = "Türkiye";

    [MaxLength(150)]
    public string? Occupation { get; set; }

    // Seçilen Eğitim Dönemi (Örn: "2026-11-09 | 12:30 - 13:30")
    [Required]
    [MaxLength(100)]
    public string SelectedSlot { get; set; } = string.Empty;

    // Ödeme Yöntemi: "cash" (EFT/Havale) veya "card" (Kredi Kartı)
    [Required]
    [MaxLength(20)]
    public string PaymentMethod { get; set; } = string.Empty;

    // Ödeme Durumu: "Pending" (Bekliyor), "Completed" (Tamamlandı), "Failed" (Başarısız)
    [Required]
    [MaxLength(20)]
    public string PaymentStatus { get; set; } = "Pending";

    // Tutar (Peşin: 6000, Kart: 6600)
    public decimal Amount { get; set; }

    // Benzersiz Kayıt / Sipariş Referans Kodu (Örn: TRG-20261101-X8F2)
    [Required]
    [MaxLength(50)]
    public string RegistrationCode { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
}