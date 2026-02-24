using System.ComponentModel.DataAnnotations;

namespace TradeScout.API.DTOs;

/// <summary>
/// DTO for submitting feedback
/// </summary>
public class FeedbackDto
{
    [Required(ErrorMessage = "Ad Soyad zorunludur")]
    [MaxLength(100, ErrorMessage = "Ad Soyad en fazla 100 karakter olabilir")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email zorunludur")]
    [EmailAddress(ErrorMessage = "Geçerli bir email adresi giriniz")]
    [MaxLength(150, ErrorMessage = "Email en fazla 150 karakter olabilir")]
    public string Email { get; set; } = string.Empty;

    [MaxLength(20, ErrorMessage = "Telefon numarası en fazla 20 karakter olabilir")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Başlık zorunludur")]
    [MaxLength(255, ErrorMessage = "Başlık en fazla 255 karakter olabilir")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mesaj zorunludur")]
    [MaxLength(2000, ErrorMessage = "Mesaj en fazla 2000 karakter olabilir")]
    [MinLength(10, ErrorMessage = "Mesaj en az 10 karakter olmalıdır")]
    public string Message { get; set; } = string.Empty;

    [MaxLength(50, ErrorMessage = "Feedback türü en fazla 50 karakter olabilir")]
    public string? FeedbackType { get; set; } // "complaint", "suggestion", "question", "other"
}

/// <summary>
/// DTO for feedback response
/// </summary>
public class FeedbackResponseDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? FeedbackType { get; set; }
    public bool IsRead { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? RepliedAt { get; set; }
    public string? Reply { get; set; }
}
