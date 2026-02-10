using Mscc.GenerativeAI;
using System.Text.Json;
using System.Net.Http;
using System.Text;
using TradeScout.API.DTOs;

namespace TradeScout.API.Services;

/// <summary>
/// Gemini AI + Google Search Tool for ultra-fast business discovery
/// </summary>
public interface IGeminiSearchService
{
    Task<List<BusinessDto>> SearchBusinessesAsync(string sector, string city, string? country, int maxResults, CancellationToken cancellationToken = default);
}

public class GeminiSearchService : IGeminiSearchService
{
    private readonly ILogger<GeminiSearchService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _geminiApiKey;

    public GeminiSearchService(
        ILogger<GeminiSearchService> logger, 
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _geminiApiKey = _configuration["GeminiSettings:ApiKey"] 
            ?? throw new InvalidOperationException("Gemini API Key is not configured in appsettings.json");
    }

    public async Task<List<BusinessDto>> SearchBusinessesAsync(
        string sector,
        string city,
        string? country,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🤖 Gemini AI Search başlatılıyor: {Sector} - {City}, Hedef: {MaxResults}", 
                sector, city, maxResults);

            var startTime = DateTime.UtcNow;

            // Build search prompt
            var location = string.IsNullOrEmpty(country) ? city : $"{city}, {country}";
            var prompt = BuildSearchPrompt(sector, location, maxResults);

            _logger.LogInformation("📝 Gemini Prompt created");

            // Use direct HTTP API call (v1 API - stable)
            var httpClient = _httpClientFactory.CreateClient();
            var apiUrl = $"https://generativelanguage.googleapis.com/v1/models/gemini-2.0-flash-lite:generateContent?key={_geminiApiKey}";
            
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            _logger.LogInformation("🤖 Calling Gemini API (gemini-2.0-flash-lite v1)...");
            
            var httpResponse = await httpClient.PostAsync(apiUrl, jsonContent, cancellationToken);
            var responseContent = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogError("❌ Gemini API Error: {Content}", responseContent);
                throw new Exception($"Gemini API failed: {responseContent}");
            }

            _logger.LogInformation("✅ Gemini API response received");

            // Parse response
            var jsonResponse = JsonDocument.Parse(responseContent);
            var text = jsonResponse.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrEmpty(text))
            {
                _logger.LogWarning("⚠️ Gemini AI boş yanıt döndü");
                return new List<BusinessDto>();
            }

            _logger.LogInformation("✅ Gemini AI yanıt alındı. Parsing ediliyor...");
            _logger.LogDebug("📄 Raw Response: {Response}", text);

            // Parse JSON response
            var businesses = ParseGeminiResponse(text, sector, city, country);

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("🎉 Gemini Search tamamlandı! {Count} işletme bulundu, Süre: {Duration:ss} saniye", 
                businesses.Count, duration);

            return businesses.Take(maxResults).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Gemini Search hatası");
            throw;
        }
    }

    private string BuildSearchPrompt(string sector, string location, int maxResults)
    {
        return $@"GÖREV: Sen ""TradeScout"" isimli profesyonel pazar araştırması yazılımının veri toplama motorusun. 
Kullanıcının verdiği {{Sektör}} ve {{Şehir}} bilgilerini kullanarak Google Search ile en güncel işletme verilerini topla.

SEKTÖR: {sector}
KONUM: {location}
HEDEF: En az {maxResults} işletme bul

STRATEJİ:
1. Google Search kullanarak belirtilen konumdaki işletmeleri tespit et
2. Gerçek, aktif ve güncel işletmeleri seç
3. Telefon, adres, web sitesi gibi iletişim bilgilerini bul
4. Rating ve yorum sayılarını ekle

FORMAT KURALLARI (KRİTİK):
- Yanıtını SADECE aşağıdaki JSON formatında dön
- Başına veya sonuna açıklama ekleme
- Eğer veri bulamazsan boş bir array [] dön
- JSON'un geçerli olduğundan emin ol

JSON YAPISI:
[
  {{
    ""businessName"": ""İşletme Adı"",
    ""address"": ""Tam Adres"",
    ""phone"": ""Telefon No"",
    ""website"": ""Web Adresi"",
    ""email"": ""E-posta (Varsa)"",
    ""rating"": 4.5,
    ""reviewCount"": 120,
    ""category"": ""{sector}"",
    ""city"": ""{location.Split(',')[0].Trim()}"",
    ""country"": ""{(location.Contains(",") ? location.Split(',')[1].Trim() : "Turkey")}"",
    ""googleMapsUrl"": ""Google Maps Link (Varsa)""
  }}
]

ÖNEMLİ: Sadece JSON array döndür, başka hiçbir metin ekleme!";
    }

    private List<BusinessDto> ParseGeminiResponse(string responseText, string sector, string city, string? country)
    {
        try
        {
            // Clean response (remove markdown code blocks if present)
            responseText = responseText.Trim();
            
            if (responseText.StartsWith("```json"))
            {
                responseText = responseText.Substring(7);
            }
            else if (responseText.StartsWith("```"))
            {
                responseText = responseText.Substring(3);
            }
            
            if (responseText.EndsWith("```"))
            {
                responseText = responseText.Substring(0, responseText.Length - 3);
            }
            
            responseText = responseText.Trim();

            _logger.LogDebug("🧹 Cleaned Response: {Response}", responseText);

            // Parse JSON
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var businesses = JsonSerializer.Deserialize<List<BusinessDto>>(responseText, options);

            if (businesses == null || !businesses.Any())
            {
                _logger.LogWarning("⚠️ JSON parse edildi ama işletme bulunamadı");
                return new List<BusinessDto>();
            }

            // Ensure all businesses have category, city, country
            foreach (var business in businesses)
            {
                business.Category ??= sector;
                business.City ??= city;
                business.Country ??= country;
            }

            _logger.LogInformation("✅ {Count} işletme başarıyla parse edildi", businesses.Count);
            return businesses;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "❌ JSON parse hatası. Response: {Response}", responseText);
            
            // Try to extract JSON from text if it's embedded
            var jsonMatch = System.Text.RegularExpressions.Regex.Match(responseText, @"\[.*\]", 
                System.Text.RegularExpressions.RegexOptions.Singleline);
            
            if (jsonMatch.Success)
            {
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    var businesses = JsonSerializer.Deserialize<List<BusinessDto>>(jsonMatch.Value, options);
                    
                    if (businesses != null && businesses.Any())
                    {
                        _logger.LogInformation("✅ Regex ile {Count} işletme çıkarıldı", businesses.Count);
                        
                        foreach (var business in businesses)
                        {
                            business.Category ??= sector;
                            business.City ??= city;
                            business.Country ??= country;
                        }
                        
                        return businesses;
                    }
                }
                catch
                {
                    // Fall through to return empty list
                }
            }

            return new List<BusinessDto>();
        }
    }
}
