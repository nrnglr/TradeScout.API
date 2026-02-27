using Mscc.GenerativeAI;
using System.Text.Json;
using System.Net.Http;
using System.Text;
using System.Collections.Concurrent;
using TradeScout.API.DTOs;

namespace TradeScout.API.Services;

/// <summary>
/// Gemini AI + Google Search Tool for ultra-fast business discovery
/// </summary>
public interface IGeminiSearchService
{
    Task<List<BusinessDto>> SearchBusinessesAsync(string sector, string city, string? country, int maxResults, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Enrich existing businesses with email/mobile using Gemini AI (batched processing)
    /// </summary>
    Task<(List<BusinessDto> EnrichedBusinesses, int SuccessfulCount)> EnrichBusinessesAsync(
        List<BusinessDto> businesses, 
        int batchSize = 60, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get current API key pool status
    /// </summary>
    (int TotalKeys, int AvailableKeys) GetApiKeyPoolStatus();
}

public class GeminiSearchService : IGeminiSearchService
{
    private readonly ILogger<GeminiSearchService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    
    // API Key Pool for load balancing (supports 200+ concurrent users)
    private readonly List<string> _apiKeys;
    private readonly ConcurrentDictionary<string, DateTime> _apiKeyLastUsed;
    private readonly ConcurrentDictionary<string, int> _apiKeyRequestCount;
    private readonly object _keySelectionLock = new object();
    private int _currentKeyIndex = 0;
    
    // Rate limiting settings
    private const int MAX_REQUESTS_PER_KEY_PER_MINUTE = 50; // Conservative limit
    private const int COOLDOWN_SECONDS = 2; // Cooldown between same key usage
    
    // Batch size for processing (prevents 504 Gateway Timeout)
    private const int DEFAULT_BATCH_SIZE = 60;

    public GeminiSearchService(
        ILogger<GeminiSearchService> logger, 
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _apiKeyLastUsed = new ConcurrentDictionary<string, DateTime>();
        _apiKeyRequestCount = new ConcurrentDictionary<string, int>();
        
        // Load API keys from configuration (supports multiple keys)
        _apiKeys = LoadApiKeys();
        
        if (_apiKeys.Count == 0)
        {
            throw new InvalidOperationException("No Gemini API Keys found. Set GEMINI_API_KEY environment variable or configure GeminiSettings:ApiKeys in appsettings.json");
        }
        
        _logger.LogInformation("🔑 Gemini API Key Pool initialized with {Count} key(s)", _apiKeys.Count);
        
        if (_apiKeys.Count < 5)
        {
            _logger.LogWarning("⚠️ Only {Count} API key(s) configured. For 200+ concurrent users, consider adding more keys.", _apiKeys.Count);
        }
    }
    
    /// <summary>
    /// Load API keys from environment and configuration
    /// Supports: GEMINI_API_KEY, GEMINI_API_KEY_1, GEMINI_API_KEY_2, etc.
    /// Or: GeminiSettings:ApiKeys array in appsettings.json
    /// </summary>
    private List<string> LoadApiKeys()
    {
        var keys = new List<string>();
        
        // 1. Check single environment variable
        var singleKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (!string.IsNullOrEmpty(singleKey))
        {
            keys.Add(singleKey);
        }
        
        // 2. Check numbered environment variables (GEMINI_API_KEY_1, GEMINI_API_KEY_2, etc.)
        for (int i = 1; i <= 20; i++)
        {
            var key = Environment.GetEnvironmentVariable($"GEMINI_API_KEY_{i}");
            if (!string.IsNullOrEmpty(key) && !keys.Contains(key))
            {
                keys.Add(key);
            }
        }
        
        // 3. Check appsettings.json for array of keys
        var configKeys = _configuration.GetSection("GeminiSettings:ApiKeys").Get<string[]>();
        if (configKeys != null)
        {
            foreach (var key in configKeys)
            {
                if (!string.IsNullOrEmpty(key) && !keys.Contains(key))
                {
                    keys.Add(key);
                }
            }
        }
        
        // 4. Fallback to single key in appsettings
        var configSingleKey = _configuration["GeminiSettings:ApiKey"];
        if (!string.IsNullOrEmpty(configSingleKey) && !keys.Contains(configSingleKey))
        {
            keys.Add(configSingleKey);
        }
        
        return keys;
    }
    
    /// <summary>
    /// Get the next available API key using round-robin with rate limiting
    /// </summary>
    private string GetNextApiKey()
    {
        lock (_keySelectionLock)
        {
            var now = DateTime.UtcNow;
            var oneMinuteAgo = now.AddMinutes(-1);
            
            // Try to find an available key
            for (int attempts = 0; attempts < _apiKeys.Count; attempts++)
            {
                _currentKeyIndex = (_currentKeyIndex + 1) % _apiKeys.Count;
                var key = _apiKeys[_currentKeyIndex];
                
                // Check cooldown
                if (_apiKeyLastUsed.TryGetValue(key, out var lastUsed))
                {
                    if ((now - lastUsed).TotalSeconds < COOLDOWN_SECONDS && _apiKeys.Count > 1)
                    {
                        continue; // Try next key
                    }
                }
                
                // Check rate limit (reset count if older than 1 minute)
                _apiKeyRequestCount.AddOrUpdate(key, 1, (k, count) =>
                {
                    if (_apiKeyLastUsed.TryGetValue(k, out var last) && last < oneMinuteAgo)
                    {
                        return 1; // Reset count
                    }
                    return count + 1;
                });
                
                var currentCount = _apiKeyRequestCount.GetOrAdd(key, 0);
                if (currentCount > MAX_REQUESTS_PER_KEY_PER_MINUTE && _apiKeys.Count > 1)
                {
                    _logger.LogWarning("⚠️ API key {Index} reached rate limit ({Count} requests/min)", _currentKeyIndex, currentCount);
                    continue; // Try next key
                }
                
                // Update last used time
                _apiKeyLastUsed[key] = now;
                
                return key;
            }
            
            // If all keys are rate limited, use the first one anyway
            _logger.LogWarning("⚠️ All API keys are rate limited. Using key 0 anyway.");
            return _apiKeys[0];
        }
    }
    
    /// <summary>
    /// Get current API key pool status
    /// </summary>
    public (int TotalKeys, int AvailableKeys) GetApiKeyPoolStatus()
    {
        var now = DateTime.UtcNow;
        var oneMinuteAgo = now.AddMinutes(-1);
        
        var availableKeys = _apiKeys.Count(key =>
        {
            var count = _apiKeyRequestCount.GetOrAdd(key, 0);
            if (_apiKeyLastUsed.TryGetValue(key, out var lastUsed) && lastUsed < oneMinuteAgo)
            {
                return true; // Count reset
            }
            return count < MAX_REQUESTS_PER_KEY_PER_MINUTE;
        });
        
        return (_apiKeys.Count, availableKeys);
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

            var location = string.IsNullOrEmpty(country) ? city : $"{city}, {country}";
            
            // 50'den fazla ise paralel istekler yap
            int batchSize = 50;
            int batchCount = (int)Math.Ceiling((double)maxResults / batchSize);
            batchCount = Math.Min(batchCount, 5); // Max 5 paralel istek
            
            _logger.LogInformation("📦 {BatchCount} paralel istek yapılacak (batch size: {BatchSize})", 
                batchCount, batchSize);
            
            var tasks = new List<Task<List<BusinessDto>>>();
            
            for (int i = 0; i < batchCount; i++)
            {
                int offset = i * batchSize;
                int targetCount = Math.Min(batchSize, maxResults - offset);
                var prompt = BuildSearchPromptWithOffset(sector, location, targetCount, offset, i);
                tasks.Add(CallGeminiApiAsync(prompt, sector, city, country, i));
            }
            
            var results = await Task.WhenAll(tasks);
            
            // Sonuçları birleştir ve duplicate'leri kaldır
            var allBusinesses = results
                .SelectMany(x => x)
                .GroupBy(b => b.BusinessName?.ToLower()?.Trim())
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .Select(g => g.First())
                .Take(maxResults)
                .ToList();

            _logger.LogInformation("🎉 Toplam {Count} benzersiz işletme bulundu (hedef: {MaxResults})", 
                allBusinesses.Count, maxResults);
            return allBusinesses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Gemini Search hatası");
            throw;
        }
    }

    /// <summary>
    /// Tek bir Gemini API çağrısı yapar
    /// </summary>
    private async Task<List<BusinessDto>> CallGeminiApiAsync(
        string prompt, 
        string sector, 
        string city, 
        string? country,
        int batchIndex)
    {
        try
        {
            // Get next available API key from pool (load balancing)
            var apiKey = GetNextApiKey();
            var keyIndex = _apiKeys.IndexOf(apiKey);
            
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(3);
            
            var apiUrl = $"https://generativelanguage.googleapis.com/v1/models/gemini-2.5-flash:generateContent?key={apiKey}";
            
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

            _logger.LogInformation("🤖 Batch #{BatchIndex} - Calling Gemini API (key #{KeyIndex})...", 
                batchIndex, keyIndex);
            
            var httpResponse = await httpClient.PostAsync(apiUrl, jsonContent);
            var responseContent = await httpResponse.Content.ReadAsStringAsync();

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogError("❌ Batch #{BatchIndex} - Gemini API Error: {Content}", 
                    batchIndex, responseContent);
                return new List<BusinessDto>();
            }

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
                _logger.LogWarning("⚠️ Batch #{BatchIndex} - Boş yanıt", batchIndex);
                return new List<BusinessDto>();
            }

            var businesses = ParseGeminiResponse(text, sector, city, country);
            _logger.LogInformation("✅ Batch #{BatchIndex} - {Count} işletme bulundu", 
                batchIndex, businesses.Count);
            
            return businesses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Batch #{BatchIndex} hatası", batchIndex);
            return new List<BusinessDto>();
        }
    }

    /// <summary>
    /// Offset'li arama prompt'u oluşturur (farklı sonuçlar için)
    /// </summary>
    private string BuildSearchPromptWithOffset(string sector, string location, int targetCount, int offset, int batchIndex)
    {
        var searchVariants = new[]
        {
            "", // İlk batch: normal arama
            "lesser known ", // 2. batch: daha az bilinen
            "new ", // 3. batch: yeni
            "small ", // 4. batch: küçük
            "local " // 5. batch: yerel
        };
        
        var variant = searchVariants[Math.Min(batchIndex, searchVariants.Length - 1)];
        
        return $@"TASK: You are the data collection engine of ""TradeScout"" professional market research software. 
Use Google Search with the sector and location provided by the user to collect the latest business data.

SECTOR: {variant}{sector}
LOCATION: {location}
TARGET: Find EXACTLY {targetCount} businesses. Be thorough and exhaustive. Use multiple search strategies: business directories, Google Maps, yellow pages, LinkedIn company pages, local chamber of commerce listings.

STRATEGY:
1. Search for ""{variant}{sector} in {location}"" on Google
2. Find DIFFERENT businesses than typical/famous ones
3. Select real, active, and current businesses
4. Find contact information: phone, address, website, email
5. Identify social media profiles (LinkedIn, Instagram, Facebook, etc.)
6. Include rating and review count if available
7. Return whatever you find, even if less than target

IMPORTANT: This is batch {batchIndex + 1}. Find DIFFERENT businesses than the main/popular ones!

FORMATTING RULES (CRITICAL):
- Return ONLY the JSON format below
- Do NOT add any explanation or text before/after
- If absolutely no data found, return empty array []
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

    private string BuildSearchPrompt(string sector, string location, int maxResults)
    {
        return $@"TASK: You are the data collection engine of ""TradeScout"" professional market research software. 
Use Google Search with the sector and location provided by the user to collect the latest business data.

SECTOR: {sector}
LOCATION: {location}
TARGET: Find up to {maxResults} businesses (return as many as possible, even if fewer than target)

STRATEGY:
1. Use Google Search to identify businesses in the specified location
2. Select real, active, and current businesses
3. Find contact information: phone, address, website, email
4. Identify social media profiles (LinkedIn, Instagram, Facebook, etc.)
5. Include rating and review count if available
6. If fewer than {maxResults} businesses exist in this area, return ALL available businesses

FORMATTING RULES (CRITICAL):
- Return ONLY the JSON format below
- Do NOT add any explanation or text before/after
- If absolutely no data found, return empty array []
- Return whatever businesses you can find, even if less than target
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

    /// <summary>
    /// Enrich existing businesses with email/mobile using Gemini AI (batched processing)
    /// Processes in batches of 60 to prevent 504 Gateway Timeout
    /// Returns the enriched list and count of successfully enriched records
    /// </summary>
    public async Task<(List<BusinessDto> EnrichedBusinesses, int SuccessfulCount)> EnrichBusinessesAsync(
        List<BusinessDto> businesses,
        int batchSize = 60,
        CancellationToken cancellationToken = default)
    {
        // IGNORE the incoming cancellationToken - we manage our own process
        
        if (businesses == null || !businesses.Any())
        {
            return (new List<BusinessDto>(), 0);
        }

        var allResults = new List<BusinessDto>();
        var successfulCount = 0;
        var totalBatches = (int)Math.Ceiling((double)businesses.Count / batchSize);

        _logger.LogInformation("🚀 Enrichment başlatılıyor: {TotalCount} firma, {BatchCount} batch ({BatchSize}'lik)", 
            businesses.Count, totalBatches, batchSize);

        for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
        {
            var batch = businesses
                .Skip(batchIndex * batchSize)
                .Take(batchSize)
                .ToList();

            _logger.LogInformation("📦 Batch {Current}/{Total} işleniyor ({Count} firma)...", 
                batchIndex + 1, totalBatches, batch.Count);

            try
            {
                var (enrichedBatch, batchSuccessCount) = await EnrichBatchAsync(batch);
                allResults.AddRange(enrichedBatch);
                successfulCount += batchSuccessCount;

                _logger.LogInformation("✅ Batch {Current}/{Total} tamamlandı: {SuccessCount} başarılı", 
                    batchIndex + 1, totalBatches, batchSuccessCount);

                // Small delay between batches to avoid rate limiting
                if (batchIndex < totalBatches - 1)
                {
                    await Task.Delay(500);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Batch {Current}/{Total} başarısız. Original data kullanılıyor.", 
                    batchIndex + 1, totalBatches);
                
                // Add original batch data on failure
                allResults.AddRange(batch);
            }
        }

        _logger.LogInformation("🎉 Enrichment tamamlandı: {TotalCount} firma, {SuccessCount} başarılı", 
            allResults.Count, successfulCount);

        return (allResults, successfulCount);
    }

    /// <summary>
    /// Process a single batch of businesses for enrichment
    /// </summary>
    private async Task<(List<BusinessDto> EnrichedBatch, int SuccessCount)> EnrichBatchAsync(
        List<BusinessDto> batch)
    {
        var prompt = BuildEnrichmentPrompt(batch);
        
        // Get next available API key from pool (load balancing)
        var apiKey = GetNextApiKey();
        var keyIndex = _apiKeys.IndexOf(apiKey);
        
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromMinutes(3); // 3 minute timeout per batch
        
        var apiUrl = $"https://generativelanguage.googleapis.com/v1/models/gemini-2.5-flash:generateContent?key={apiKey}";

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

        // NO cancellation token - let the request complete
        var httpResponse = await httpClient.PostAsync(apiUrl, jsonContent);
        var responseContent = await httpResponse.Content.ReadAsStringAsync();

        if (!httpResponse.IsSuccessStatusCode)
        {
            _logger.LogError("❌ Gemini API Enrichment Error: {Content}", responseContent);
            throw new Exception($"Gemini API failed: {responseContent}");
        }

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
            _logger.LogWarning("⚠️ Gemini AI enrichment boş yanıt döndü");
            return (batch, 0);
        }

        // Parse enriched data
        var enrichedList = ParseEnrichmentResponse(text, batch);

        // Count successful enrichments (has real email or mobile, not "Not Found")
        var successCount = enrichedList.Count(b => 
            HasValidContactInfo(b.Email) || HasValidContactInfo(b.Mobile));

        return (enrichedList, successCount);
    }

    /// <summary>
    /// Check if contact info is valid (not null, not empty, not "Not Found")
    /// </summary>
    private bool HasValidContactInfo(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var lowerValue = value.ToLowerInvariant().Trim();
        
        // Check for "Not Found" variations
        if (lowerValue.Contains("not found") || 
            lowerValue.Contains("notfound") ||
            lowerValue.Contains("bulunamadı") ||
            lowerValue.Contains("yok") ||
            lowerValue == "n/a" ||
            lowerValue == "na" ||
            lowerValue == "-" ||
            lowerValue == "null")
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Build enrichment prompt for a batch of businesses
    /// </summary>
    private string BuildEnrichmentPrompt(List<BusinessDto> batch)
    {
        var businessList = new StringBuilder();
        for (int i = 0; i < batch.Count; i++)
        {
            var b = batch[i];
            businessList.AppendLine($"{i + 1}. \"{b.BusinessName}\" - {b.Address ?? b.City}");
        }

        return $@"TASK: You are an AI data enrichment engine. Find contact information for the following businesses.

BUSINESSES TO ENRICH:
{businessList}

INSTRUCTIONS:
1. Search for each business and find their Email and Mobile phone number
2. If you cannot find real data, use null (DO NOT use ""Not Found"" or similar text)
3. Return ONLY valid JSON array

JSON FORMAT (return ONLY this, no explanation):
[
  {{
    ""index"": 1,
    ""email"": ""contact@example.com or null"",
    ""mobile"": ""+90 555 123 4567 or null"",
    ""socialMedia"": ""https://linkedin.com/company/xxx or null""
  }}
]

CRITICAL: 
- Return ONLY the JSON array
- Use null for missing data, NOT text like ""Not Found""
- Index corresponds to the business number above";
    }

    /// <summary>
    /// Parse enrichment response and merge with original data
    /// </summary>
    private List<BusinessDto> ParseEnrichmentResponse(string responseText, List<BusinessDto> originalBatch)
    {
        try
        {
            // Clean response
            responseText = responseText.Trim();
            
            if (responseText.StartsWith("```json"))
                responseText = responseText.Substring(7);
            else if (responseText.StartsWith("```"))
                responseText = responseText.Substring(3);
            
            if (responseText.EndsWith("```"))
                responseText = responseText.Substring(0, responseText.Length - 3);
            
            responseText = responseText.Trim();

            // Parse JSON
            var enrichments = JsonSerializer.Deserialize<List<EnrichmentResult>>(responseText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (enrichments == null)
                return originalBatch;

            // Merge enrichment data with original businesses
            foreach (var enrichment in enrichments)
            {
                var index = enrichment.Index - 1; // Convert to 0-based
                if (index >= 0 && index < originalBatch.Count)
                {
                    var business = originalBatch[index];
                    
                    // Only update if we got valid data
                    if (HasValidContactInfo(enrichment.Email))
                        business.Email = enrichment.Email;
                    
                    if (HasValidContactInfo(enrichment.Mobile))
                        business.Mobile = enrichment.Mobile;
                    
                    if (HasValidContactInfo(enrichment.SocialMedia))
                        business.SocialMedia = enrichment.SocialMedia;
                }
            }

            return originalBatch;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Enrichment response parse hatası");
            return originalBatch;
        }
    }

    /// <summary>
    /// Helper class for parsing enrichment results
    /// </summary>
    private class EnrichmentResult
    {
        public int Index { get; set; }
        public string? Email { get; set; }
        public string? Mobile { get; set; }
        public string? SocialMedia { get; set; }
    }
}
