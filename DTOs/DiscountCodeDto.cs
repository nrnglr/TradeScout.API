using System.ComponentModel.DataAnnotations;

namespace TradeScout.API.DTOs;

/// <summary>
/// İndirim kodu doğrulama isteği
/// </summary>
public class ValidateDiscountCodeDto
{
    [Required(ErrorMessage = "İndirim kodu gereklidir")]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Paket kodu gereklidir")]
    public string PackageCode { get; set; } = string.Empty;
}

/// <summary>
/// İndirim kodu doğrulama yanıtı
/// </summary>
public class DiscountCodeValidationDto
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public int DiscountPercentage { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal DiscountedPrice { get; set; }
    public decimal DiscountAmount { get; set; }
}
