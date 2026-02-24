using System.ComponentModel.DataAnnotations;
using TradeScout.API.Models;

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

    [MaxLength(150, ErrorMessage = "Şirket adı en fazla 150 karakter olabilir")]
    public string? CompanyName { get; set; }

    [MaxLength(255, ErrorMessage = "Adres en fazla 255 karakter olabilir")]
    public string? Address { get; set; }

    [MaxLength(100, ErrorMessage = "Şehir adı en fazla 100 karakter olabilir")]
    public string? City { get; set; }

    [MaxLength(100, ErrorMessage = "Ülke adı en fazla 100 karakter olabilir")]
    public string? Country { get; set; }

    [MaxLength(20, ErrorMessage = "Telefon numarası en fazla 20 karakter olabilir")]
    [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz")]
    public string? Phone { get; set; }

    [MaxLength(255, ErrorMessage = "Web sitesi URL'si en fazla 255 karakter olabilir")]
    [Url(ErrorMessage = "Geçerli bir URL giriniz")]
    public string? Website { get; set; }

    public UserType? UserType { get; set; }
}
