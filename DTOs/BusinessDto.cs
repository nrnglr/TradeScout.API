namespace TradeScout.API.DTOs;

/// <summary>
/// DTO for business data response
/// </summary>
public class BusinessDto
{
    public string BusinessName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? SocialMedia { get; set; }
    public string? Comments { get; set; }
    public decimal? Rating { get; set; }
    public int? ReviewCount { get; set; }
    public string? WorkingHours { get; set; }
    public string? Category { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
}

/// <summary>
/// DTO for scraping job response
/// </summary>
public class ScrapeResponseDto
{
    public int JobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int TotalResults { get; set; }
    public int CreditsUsed { get; set; }
    public int RemainingCredits { get; set; }  // ✅ Kalan kredi bilgisi
    public List<BusinessDto> Businesses { get; set; } = new();
    public string? DownloadUrl { get; set; }
}
