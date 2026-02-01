using System.ComponentModel.DataAnnotations;

namespace TradeScout.API.DTOs;

/// <summary>
/// DTO for user registration
/// </summary>
public class RegisterDto
{
    [Required(ErrorMessage = "Ad Soyad zorunludur")]
    [MaxLength(100, ErrorMessage = "Ad Soyad en fazla 100 karakter olabilir")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email zorunludur")]
    [EmailAddress(ErrorMessage = "Geçerli bir email adresi giriniz")]
    [MaxLength(150, ErrorMessage = "Email en fazla 150 karakter olabilir")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre zorunludur")]
    [MinLength(6, ErrorMessage = "Şifre en az 6 karakter olmalıdır")]
    [MaxLength(100, ErrorMessage = "Şifre en fazla 100 karakter olabilir")]
    public string Password { get; set; } = string.Empty;

    [MaxLength(100, ErrorMessage = "Şirket adı en fazla 100 karakter olabilir")]
    public string? CompanyName { get; set; }
}
