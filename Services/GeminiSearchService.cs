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
        
        // Get API key from environment variable first, then from appsettings
        _geminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") 
            ?? _configuration["GeminiSettings:ApiKey"] 
            ?? throw new InvalidOperationException("Gemini API Key not found in environment variable GEMINI_API_KEY or appsettings.json");
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

            // Use direct HTTP API call (v1beta API)
            var httpClient = _httpClientFactory.CreateClient();
            var apiUrl = $"https://generativelanguage.googleapis.com/v1/models/gemini-2.5-flash:generateContent?key={_geminiApiKey}";
            
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

            _logger.LogInformation("🤖 Calling Gemini API (gemini-2.5-flash v1)...");
            
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
        return $@"TASK: You are the data collection engine of ""TradeScout"" professional market research software. 
Use Google Search with the sector and location provided by the user to collect the latest business data.

SECTOR: {sector}
LOCATION: {location}
TARGET: Find at least {maxResults} businesses

STRATEGY:
1. Use Google Search to identify businesses in the specified location
2. Select real, active, and current businesses
3. Find contact information: phone, address, website, email
4. Identify social media profiles (LinkedIn, Instagram, Facebook, etc.)
5. Include rating and review count if available

FORMATTING RULES (CRITICAL):
- Return ONLY the JSON format below
- Do NOT add any explanation or text before/after
- If no data found, return empty array []
- Ensure JSON is valid
- Set missing fields to null (NOT empty string!)

JSON STRUCTURE:
[
  {{
    ""businessName"": ""Company Name"",
    ""address"": ""Full Address (Street, No, City, Postal Code)"",
    ""phone"": ""Phone Number or null"",
    ""mobile"": ""Mobile Number or null"",
    ""email"": ""Email Address or null"",
    ""website"": ""Website URL or null"",
    ""socialMedia"": ""LinkedIn/Instagram/Facebook URL or null"",
    ""comments"": ""Brief company info (sector, employees, etc) or null"",
    ""rating"": 4.5,
    ""reviewCount"": 120,
    ""category"": ""{sector}"",
    ""city"": ""{location.Split(',')[0].Trim()}"",
    ""country"": ""{(location.Contains(",") ? location.Split(',')[1].Trim() : "Turkey")}""
  }}
]

IMPORTANT: Return ONLY the JSON array, nothing else!";
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
