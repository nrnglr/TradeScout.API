using System.ComponentModel.DataAnnotations;

namespace TradeScout.API.DTOs;

/// <summary>
/// DTO for starting a scraping request
/// Supports both searchQuery (simple) and category/city (advanced) formats
/// </summary>
public class ScrapeRequestDto
{
    /// <summary>
    /// Simple search query (e.g., "restaurants in Istanbul")
    /// If provided, Category and City are not required
    /// </summary>
    [MaxLength(300, ErrorMessage = "Arama sorgusu en fazla 300 karakter olabilir")]
    public string? SearchQuery { get; set; }

    /// <summary>
    /// Business category (e.g., "restaurant", "mobilya")
    /// Required if SearchQuery is not provided
    /// </summary>
    [MaxLength(200, ErrorMessage = "İş alanı en fazla 200 karakter olabilir")]
    public string? Category { get; set; }

    /// <summary>
    /// City name (e.g., "Istanbul", "Gaziantep")
    /// Required if SearchQuery is not provided
    /// </summary>
    [MaxLength(100, ErrorMessage = "Şehir en fazla 100 karakter olabilir")]
    public string? City { get; set; }

    /// <summary>
    /// Country name (optional, defaults to Turkey)
    /// </summary>
    [MaxLength(100, ErrorMessage = "Ülke en fazla 100 karakter olabilir")]
    public string? Country { get; set; }

    /// <summary>
    /// Search language (default: "tr")
    /// </summary>
    [MaxLength(50)]
    public string Language { get; set; } = "tr";

    /// <summary>
    /// Maximum number of results to scrape (1-100)
    /// </summary>
    [Range(1, 100, ErrorMessage = "Maksimum sonuç sayısı 1 ile 100 arasında olmalıdır")]
    public int MaxResults { get; set; } = 20;

    /// <summary>
    /// Validates that either SearchQuery or (Category + City) is provided
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(SearchQuery) || 
               (!string.IsNullOrWhiteSpace(Category) && !string.IsNullOrWhiteSpace(City));
    }

    /// <summary>
    /// Gets the final search query for Google Maps
    /// </summary>
    public string GetSearchQuery()
    {
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            return SearchQuery;
        }

        // Build query from Category + City + Country
        var parts = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(Category))
            parts.Add(Category);
        
        if (!string.IsNullOrWhiteSpace(City))
            parts.Add(City);
        
        if (!string.IsNullOrWhiteSpace(Country))
            parts.Add(Country);

        return string.Join(" ", parts);
    }
}
