using Mscc.GenerativeAI;
using System.Text.Json;
using System.Net.Http;
using System.Text;
using System.Collections.Concurrent;
using System.Linq; // .Last() gibi sorgular için eklendi
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
    private const int MAX_REQUESTS_PER_KEY_PER_MINUTE = 12; // gemini-1.5-flash free tier = 15/min, conservative
    private const int COOLDOWN_SECONDS = 5; // Cooldown between same key usage

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
    /// </summary>
    private List<string> LoadApiKeys()
    {
        var keys = new List<string>();

        var singleKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (!string.IsNullOrEmpty(singleKey)) keys.Add(singleKey);

        for (int i = 1; i <= 20; i++)
        {
            var key = Environment.GetEnvironmentVariable($"GEMINI_API_KEY_{i}");
            if (!string.IsNullOrEmpty(key) && !keys.Contains(key)) keys.Add(key);
        }

        var configKeys = _configuration.GetSection("GeminiSettings:ApiKeys").Get<string[]>();
        if (configKeys != null)
        {
            foreach (var key in configKeys)
            {
                if (!string.IsNullOrEmpty(key) && !keys.Contains(key)) keys.Add(key);
            }
        }

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

            for (int attempts = 0; attempts < _apiKeys.Count; attempts++)
            {
                _currentKeyIndex = (_currentKeyIndex + 1) % _apiKeys.Count;
                var key = _apiKeys[_currentKeyIndex];

                if (_apiKeyLastUsed.TryGetValue(key, out var lastUsed))
                {
                    if ((now - lastUsed).TotalSeconds < COOLDOWN_SECONDS && _apiKeys.Count > 1) continue;
                }

                _apiKeyRequestCount.AddOrUpdate(key, 1, (k, count) =>
                {
                    if (_apiKeyLastUsed.TryGetValue(k, out var last) && last < oneMinuteAgo) return 1;
                    return count + 1;
                });

                var currentCount = _apiKeyRequestCount.GetOrAdd(key, 0);
                if (currentCount > MAX_REQUESTS_PER_KEY_PER_MINUTE && _apiKeys.Count > 1)
                {
                    _logger.LogWarning("⚠️ API key {Index} reached rate limit ({Count} requests/min)", _currentKeyIndex, currentCount);
                    continue;
                }

                _apiKeyLastUsed[key] = now;
                return key;
            }

            _logger.LogWarning("⚠️ All API keys are rate limited. Using key 0 anyway.");
            return _apiKeys[0];
        }
    }

    public (int TotalKeys, int AvailableKeys) GetApiKeyPoolStatus()
    {
        var now = DateTime.UtcNow;
        var oneMinuteAgo = now.AddMinutes(-1);

        var availableKeys = _apiKeys.Count(key =>
        {
            var count = _apiKeyRequestCount.GetOrAdd(key, 0);
            if (_apiKeyLastUsed.TryGetValue(key, out var lastUsed) && lastUsed < oneMinuteAgo) return true;
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
            _logger.LogInformation("🚀 TradeScout Çoklu Arama Modu Başlatıldı: {Sector} - {City}", sector, city);

            var location = string.IsNullOrEmpty(country) ? city : $"{city}, {country}";

            // YENİ: 3 farklı arama stratejisi ile paralel çağır
            var tasks = new[]
            {
                CallGeminiApiAsync(BuildSearchPromptWithOffset(sector, location, maxResults, 0, 0), sector, city, country, 0, cancellationToken),
                CallGeminiApiAsync(BuildSearchPromptWithOffset(sector + " manufacturer exporter", location, maxResults, 0, 1), sector, city, country, 1, cancellationToken),
                CallGeminiApiAsync(BuildSearchPromptWithOffset(sector + " wholesaler supplier", location, maxResults, 0, 2), sector, city, country, 2, cancellationToken),
                CallGeminiApiAsync(BuildSearchPromptWithOffset(sector + " üreticileri fabrikası toptancıları", location, maxResults, 0, 3), sector, city, country, 3, cancellationToken),
            };

            await Task.WhenAll(tasks);

            // Bütün görevlerden gelen sonuçları birleştir, benzersiz olanları al
            var allFound = tasks.SelectMany(t => t.Result)
                .Where(b => !string.IsNullOrEmpty(b.Website)) // Sadece web sitesi olanları al
                .GroupBy(b => b.Website?.ToLower())
                .Select(g => g.First())
                .ToList();

            _logger.LogInformation("🔍 Gemini toplam {Count} benzersiz sonuç döndürdü. Şimdi web siteleri doğrulanıyor...", allFound.Count);

            var verifiedBusinesses = new List<BusinessDto>();
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            foreach (var business in allFound)
            {
                // GERÇEKLİK KONTROLÜ
                bool isLive = await CheckIfWebsiteIsReal(client, business.Website!);

                if (isLive)
                {
                    business.ConfidenceScore = 0.99m;
                    verifiedBusinesses.Add(business);
                    _logger.LogInformation("✅ Doğrulandı: {Website}", business.Website);
                }
                else
                {
                    _logger.LogWarning("❌ Uydurma veya Kapalı Site Elendi: {Website}", business.Website);
                }

                if (verifiedBusinesses.Count >= maxResults) break;
            }

            _logger.LogInformation("🎉 Filtreleme Sonrası: {Count} GERÇEK işletme sisteme alındı.", verifiedBusinesses.Count);
            return verifiedBusinesses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Gemini Search hatası");
            throw;
        }
    }

    // YENİ VE GÜÇLENDİRİLMİŞ URL KONTROL METODU
    private async Task<bool> CheckIfWebsiteIsReal(HttpClient client, string url)
    {
        try
        {
            if (!url.StartsWith("http")) url = "https://" + url;
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (compatible; TradeScout/1.0)");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            // 200, 301, 302, 403, 405, 406 hepsi gerçek site demektir
            var validCodes = new[] { 200, 301, 302, 403, 405, 406 };
            return validCodes.Contains((int)response.StatusCode);
        }
        catch
        {
            return false;
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
        int batchIndex,
        CancellationToken cancellationToken = default)
    {
        const int maxRetries = 3;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var apiKey = GetNextApiKey();
                var keyIndex = _apiKeys.IndexOf(apiKey);

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromMinutes(3);

                var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[] { new { text = prompt } }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.1,
                        responseMimeType = "application/json"
                    }
                };

                var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                _logger.LogInformation("🤖 Batch #{BatchIndex} - Calling Gemini API (key #{KeyIndex}, attempt {Attempt})...",
                    batchIndex, keyIndex, attempt);

                var httpResponse = await httpClient.PostAsync(apiUrl, jsonContent, cancellationToken);
                var responseContent = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    if ((int)httpResponse.StatusCode == 429 && attempt < maxRetries)
                    {
                        int waitSeconds = attempt * 15;
                        _logger.LogWarning("⚠️ Batch #{BatchIndex} - Rate limit (429), {Wait}s bekleniyor (deneme {Attempt}/{Max})...",
                            batchIndex, waitSeconds, attempt, maxRetries);
                        await Task.Delay(TimeSpan.FromSeconds(waitSeconds), cancellationToken);
                        continue;
                    }

                    _logger.LogError("❌ Batch #{BatchIndex} - Gemini API Error: {Content}", batchIndex, responseContent);
                    return new List<BusinessDto>();
                }

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
                _logger.LogInformation("✅ Batch #{BatchIndex} - {Count} işletme bulundu", batchIndex, businesses.Count);

                return businesses;
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("⚠️ Batch #{BatchIndex} - İstek iptal edildi", batchIndex);
                return new List<BusinessDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Batch #{BatchIndex} hatası (deneme {Attempt}/{Max})", batchIndex, attempt, maxRetries);
                if (attempt < maxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                    continue;
                }
                return new List<BusinessDto>();
            }
        }

        return new List<BusinessDto>();
    }

    // YENİ GENİŞLETİLMİŞ PROMPT MİMARİSİ
    private string BuildSearchPromptWithOffset(string sector, string location, int targetCount, int offset, int batchIndex)
    {
        return $@"
# ROLE
You are a Senior B2B Trade Intelligence Specialist. Find REAL businesses.

# GOAL
Find {targetCount} real, active businesses in: {sector} / {location}

# SEARCH STRATEGY (USE ALL OF THESE):
1. Search Google: site:linkedin.com/company ""{sector}"" ""{location}""
2. Search trade directories: kompass.com, europages.com, alibaba.com for {sector} in {location}  
3. Search local chambers of commerce and industry associations
4. Search ""{sector} manufacturer {location}"", ""{sector} exporter {location}"", ""{sector} wholesaler {location}""
5. Try variations: related industries, suppliers, sub-sectors

# DEEP SEARCH STRATEGY:
- Do not just list the top 10 most famous companies.
- Dig deeper into page 2 and page 3 of search results.
- Look specifically for SMEs (Small and Medium Enterprises) and local industrial zones (OSB).

# CRITICAL RULES
1. Only include companies with a REAL, WORKING website
2. Email must match website domain
3. Return as many REAL companies as you can find, up to {targetCount}
4. Better to return 5 real than 50 fake

# OUTPUT FORMAT - ONLY JSON ARRAY
[
  {{
    ""businessName"": ""Company Name"",
    ""address"": ""City, {location}"",
    ""email"": ""info@company.com"",
    ""website"": ""https://www.company.com"",
    ""contextualData"": ""One sentence about their products/exports."",
    ""hsCodes"": [""1234""],
    ""confidenceScore"": 0.9,
    ""category"": ""{sector}"",
    ""city"": ""{location.Split(',')[0].Trim()}"",
    ""country"": ""{(location.Contains(",") ? location.Split(',').Last().Trim() : location)}""
  }}
]";
    }

    private List<BusinessDto> ParseGeminiResponse(string responseText, string sector, string city, string? country)
    {
        try
        {
            responseText = responseText.Trim();

            if (responseText.StartsWith("```json")) responseText = responseText.Substring(7);
            else if (responseText.StartsWith("```")) responseText = responseText.Substring(3);

            if (responseText.EndsWith("```")) responseText = responseText.Substring(0, responseText.Length - 3);

            responseText = responseText.Trim();

            if (!responseText.StartsWith("[") && !responseText.StartsWith("{"))
            {
                _logger.LogWarning("⚠️ Gemini JSON döndürmedi (düz metin). İçerik: {Preview}",
                    responseText.Length > 200 ? responseText.Substring(0, 200) + "..." : responseText);
                return new List<BusinessDto>();
            }

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
                catch { }
            }

            return new List<BusinessDto>();
        }
    }

    public async Task<(List<BusinessDto> EnrichedBusinesses, int SuccessfulCount)> EnrichBusinessesAsync(
        List<BusinessDto> businesses,
        int batchSize = 60,
        CancellationToken cancellationToken = default)
    {
        if (businesses == null || !businesses.Any()) return (new List<BusinessDto>(), 0);

        var allResults = new List<BusinessDto>();
        var successfulCount = 0;
        var totalBatches = (int)Math.Ceiling((double)businesses.Count / batchSize);

        _logger.LogInformation("🚀 Enrichment başlatılıyor: {TotalCount} firma, {BatchCount} batch ({BatchSize}'lik)",
            businesses.Count, totalBatches, batchSize);

        var enrichmentTasks = new List<Task<(List<BusinessDto> EnrichedBatch, int SuccessCount)>>();

        for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
        {
            var batch = businesses.Skip(batchIndex * batchSize).Take(batchSize).ToList();

            _logger.LogInformation("📦 Batch {Current}/{Total} PARALEL olarak kuyruğa eklendi ({Count} firma)...",
                batchIndex + 1, totalBatches, batch.Count);

            enrichmentTasks.Add(SafeEnrichBatchAsync(batch, batchIndex + 1, totalBatches));
        }

        var enrichmentResults = await Task.WhenAll(enrichmentTasks);

        foreach (var (enrichedBatch, batchSuccessCount) in enrichmentResults)
        {
            allResults.AddRange(enrichedBatch);
            successfulCount += batchSuccessCount;
        }

        _logger.LogInformation("🎉 Enrichment tamamlandı: {TotalCount} firma, {SuccessCount} başarılı",
            allResults.Count, successfulCount);

        return (allResults, successfulCount);
    }

    private async Task<(List<BusinessDto> EnrichedBatch, int SuccessCount)> SafeEnrichBatchAsync(List<BusinessDto> batch, int currentBatch, int totalBatches)
    {
        try
        {
            var result = await EnrichBatchAsync(batch);
            _logger.LogInformation("✅ Batch {Current}/{Total} tamamlandı: {SuccessCount} başarılı", currentBatch, totalBatches, result.SuccessCount);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Batch {Current}/{Total} başarısız. Original data kullanılıyor.", currentBatch, totalBatches);
            return (batch, 0);
        }
    }

    private async Task<(List<BusinessDto> EnrichedBatch, int SuccessCount)> EnrichBatchAsync(List<BusinessDto> batch)
    {
        var prompt = BuildEnrichmentPrompt(batch);
        var apiKey = GetNextApiKey();

        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromMinutes(3);

        var apiUrl = $"[https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=](https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=){apiKey}";
        var requestBody = new
        {
            contents = new[]
     {
        new { parts = new[] { new { text = prompt } } }
    },
            // BU KISMI EKLİYORUZ (Google Arama Motorunu Kullanması İçin)
            tools = new[]
     {
        new { googleSearch = new { } }
    },
            generationConfig = new
            {
                temperature = 0.2, // Biraz daha fazla çeşitlilik (0.1'den 0.2'ye çıkardık)
                responseMimeType = "application/json"
            }
        };

        var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var httpResponse = await httpClient.PostAsync(apiUrl, jsonContent);
        var responseContent = await httpResponse.Content.ReadAsStringAsync();

        if (!httpResponse.IsSuccessStatusCode)
        {
            _logger.LogError("❌ Gemini API Enrichment Error: {Content}", responseContent);
            throw new Exception($"Gemini API failed: {responseContent}");
        }

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

        var enrichedList = ParseEnrichmentResponse(text, batch);
        var successCount = enrichedList.Count(b => HasValidContactInfo(b.Email) || HasValidContactInfo(b.Mobile));

        return (enrichedList, successCount);
    }

    private bool HasValidContactInfo(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var lowerValue = value.ToLowerInvariant().Trim();

        if (lowerValue.Contains("not found") || lowerValue.Contains("notfound") ||
            lowerValue.Contains("bulunamadı") || lowerValue.Contains("yok") ||
            lowerValue == "n/a" || lowerValue == "na" || lowerValue == "-" || lowerValue == "null")
        {
            return false;
        }

        return true;
    }

    private string BuildEnrichmentPrompt(List<BusinessDto> batch)
{
    var businessList = new StringBuilder();
    for (int i = 0; i < batch.Count; i++)
    {
        var b = batch[i];
        businessList.AppendLine($"{i + 1}. \"{b.BusinessName}\" - Web: {b.Website} - Loc: {b.Address ?? b.City}");
    }

    return $@"TASK: Deep Research & Cold Email Personalization Engine.
Find specific contact persons, official phone numbers, social media profiles, and recent company activities for the following leads.

BUSINESSES:
{businessList}

INSTRUCTIONS:
1. Find the Decision Maker's name (CEO, Export Manager, or Procurement Lead) if possible.
2. Find the most direct official email address.
3. Find their official Phone/Mobile number (including country code).
4. Find their official LinkedIn, Instagram, or Facebook company profile link.
5. Extract a ""Trigger Event"" (e.g., ""Won an award in 2025"", ""Expanded to German market""). 

JSON FORMAT:
[
  {{
    ""index"": 1,
    ""decisionMaker"": ""John Doe - Export Manager"",
    ""email"": ""j.doe@company.com"",
    ""triggerEvent"": ""Their recent participation in the Anuga Fair 2024"",
    ""mobile"": ""+90 555 123 4567"",
    ""socialMedia"": ""https://www.linkedin.com/company/example""
  }}
]

CRITICAL: Do NOT hallucinate. If you can't find a specific data point, return null for that field.";
}
    private List<BusinessDto> ParseEnrichmentResponse(string responseText, List<BusinessDto> originalBatch)
    {
        try
        {
            responseText = responseText.Trim();

            if (responseText.StartsWith("```json")) responseText = responseText.Substring(7);
            else if (responseText.StartsWith("```")) responseText = responseText.Substring(3);
            if (responseText.EndsWith("```")) responseText = responseText.Substring(0, responseText.Length - 3);

            responseText = responseText.Trim();

            var enrichments = JsonSerializer.Deserialize<List<EnrichmentResult>>(responseText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (enrichments == null) return originalBatch;

            foreach (var enrichment in enrichments)
            {
                var index = enrichment.Index - 1;
                if (index >= 0 && index < originalBatch.Count)
                {
                    var business = originalBatch[index];

                    if (HasValidContactInfo(enrichment.Email)) business.Email = enrichment.Email;
                    if (HasValidContactInfo(enrichment.Mobile)) business.Mobile = enrichment.Mobile;
                    if (HasValidContactInfo(enrichment.SocialMedia)) business.SocialMedia = enrichment.SocialMedia;
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

    private class EnrichmentResult
    {
        public int Index { get; set; }
        public string? Email { get; set; }
        public string? Mobile { get; set; }
        public string? SocialMedia { get; set; }
    }
}