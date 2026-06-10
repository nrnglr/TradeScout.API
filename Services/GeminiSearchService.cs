using Mscc.GenerativeAI;
using System.Text.Json;
using System.Net.Http;
using System.Text;
using System.Collections.Concurrent;
using System.Linq;
using TradeScout.API.DTOs;

namespace TradeScout.API.Services;

/// <summary>
/// Gemini AI + Google Search Tool for ultra-fast business discovery
/// </summary>
public interface IGeminiSearchService
{
    Task<List<BusinessDto>> SearchBusinessesAsync(
        string sector,
        string city,
        string? country,
        int maxResults,
        CancellationToken cancellationToken = default);

    Task<(List<BusinessDto> EnrichedBusinesses, int SuccessfulCount)> EnrichBusinessesAsync(
        List<BusinessDto> businesses,
        int batchSize = 8,
        CancellationToken cancellationToken = default);

    (int TotalKeys, int AvailableKeys) GetApiKeyPoolStatus();
}

public class GeminiSearchService : IGeminiSearchService
{
    private readonly ILogger<GeminiSearchService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    // API Key Pool for load balancing
    private readonly List<string> _apiKeys;
    private readonly ConcurrentDictionary<string, DateTime> _apiKeyLastUsed;
    private readonly ConcurrentDictionary<string, int> _apiKeyRequestCount;
    private readonly object _keySelectionLock = new object();
    private int _currentKeyIndex = 0;

    // Rate limiting
    private const int MAX_REQUESTS_PER_KEY_PER_MINUTE = 12;
    private const int COOLDOWN_SECONDS = 5;

    // Aynı anda en fazla 8 Gemini işlemi
    private static readonly SemaphoreSlim _geminiSemaphore = new SemaphoreSlim(8, 8);

    // Gemini API base URL
    private const string GEMINI_API_BASE = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

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

        _apiKeys = LoadApiKeys();

        if (_apiKeys.Count == 0)
            throw new InvalidOperationException("No Gemini API Keys found. Set GEMINI_API_KEY environment variable.");

        _logger.LogInformation("🔑 Gemini API Key Pool initialized with {Count} key(s)", _apiKeys.Count);

        if (_apiKeys.Count < 5)
            _logger.LogWarning("⚠️ Only {Count} API key(s) configured. For high concurrency, consider adding more keys.", _apiKeys.Count);
    }

    // ─────────────────────────────────────────────
    // API KEY MANAGEMENT
    // ─────────────────────────────────────────────

    private List<string> LoadApiKeys()
    {
        var keys = new List<string>();

        var singleKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (!string.IsNullOrEmpty(singleKey)) keys.Add(singleKey.Trim());

        for (int i = 1; i <= 20; i++)
        {
            var key = Environment.GetEnvironmentVariable($"GEMINI_API_KEY_{i}");
            if (!string.IsNullOrEmpty(key) && !keys.Contains(key.Trim())) keys.Add(key.Trim());
        }

        var configKeys = _configuration.GetSection("GeminiSettings:ApiKeys").Get<string[]>();
        if (configKeys != null)
        {
            foreach (var key in configKeys)
                if (!string.IsNullOrEmpty(key) && !keys.Contains(key.Trim())) keys.Add(key.Trim());
        }

        var configSingleKey = _configuration["GeminiSettings:ApiKey"];
        if (!string.IsNullOrEmpty(configSingleKey) && !keys.Contains(configSingleKey.Trim()))
            keys.Add(configSingleKey.Trim());

        return keys;
    }

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

                if (_apiKeys.Count > 1 &&
                    _apiKeyLastUsed.TryGetValue(key, out var lastUsed) &&
                    (now - lastUsed).TotalSeconds < COOLDOWN_SECONDS)
                    continue;

                _apiKeyRequestCount.AddOrUpdate(key, 1, (k, count) =>
                {
                    if (_apiKeyLastUsed.TryGetValue(k, out var last) && last < oneMinuteAgo) return 1;
                    return count + 1;
                });

                var currentCount = _apiKeyRequestCount.GetOrAdd(key, 0);
                if (_apiKeys.Count > 1 && currentCount > MAX_REQUESTS_PER_KEY_PER_MINUTE)
                {
                    _logger.LogWarning("⚠️ API key #{Index} rate limit ({Count} req/min)", _currentKeyIndex, currentCount);
                    continue;
                }

                _apiKeyLastUsed[key] = now;
                return key;
            }

            _logger.LogWarning("⚠️ All API keys are rate limited. Using key #0.");
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

    // ─────────────────────────────────────────────
    // STEP 1: DISCOVERY — Firma İsimlerini Bul
    // ─────────────────────────────────────────────

    public async Task<List<BusinessDto>> SearchBusinessesAsync(
        string sector,
        string city,
        string? country,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🚀 TradeScout Discovery Başlatıldı: {Sector} / {City}", sector, city);

            string location;
            if (string.IsNullOrWhiteSpace(city))
                location = country ?? "Turkey";
            else
                location = string.IsNullOrEmpty(country) ? city : $"{city}, {country}";

            // 4 farklı strateji ile paralel arama
            var tasks = new[]
            {
                CallGeminiDiscoveryAsync(BuildDiscoveryPrompt(sector, location, maxResults, 0), sector, city, country, 0, cancellationToken),
                CallGeminiDiscoveryAsync(BuildDiscoveryPrompt(sector + " manufacturer exporter", location, maxResults, 1), sector, city, country, 1, cancellationToken),
                CallGeminiDiscoveryAsync(BuildDiscoveryPrompt(sector + " wholesaler supplier", location, maxResults, 2), sector, city, country, 2, cancellationToken),
                CallGeminiDiscoveryAsync(BuildDiscoveryPrompt(sector + " üretici toptancı ihracatçı", location, maxResults, 3), sector, city, country, 3, cancellationToken),
            };

            await Task.WhenAll(tasks);

            var allFound = tasks
                .SelectMany(t => t.Result)
                .Where(b => !string.IsNullOrEmpty(b.Website))
                .GroupBy(b => b.Website?.ToLower().TrimEnd('/'))
                .Select(g => g.First())
                .ToList();

            _logger.LogInformation("🔍 Toplam {Count} benzersiz sonuç bulundu. Web siteleri doğrulanıyor...", allFound.Count);

            var verifiedBusinesses = new List<BusinessDto>();
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            foreach (var business in allFound)
            {
                bool isLive = await CheckIfWebsiteIsReal(client, business.Website!);

                if (isLive)
                {
                    business.ConfidenceScore = 0.99m;
                    verifiedBusinesses.Add(business);
                    _logger.LogInformation("✅ Doğrulandı: {Website}", business.Website);
                }
                else
                {
                    _logger.LogWarning("❌ Uydurma/Kapalı site elendi: {Website}", business.Website);
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

    private async Task<bool> CheckIfWebsiteIsReal(HttpClient client, string url)
    {
        try
        {
            if (!url.StartsWith("http")) url = "https://" + url;
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (compatible; TradeScout/1.0)");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            var validCodes = new[] { 200, 301, 302, 403, 405, 406 };
            return validCodes.Contains((int)response.StatusCode);
        }
        catch
        {
            return false;
        }
    }

    private async Task<List<BusinessDto>> CallGeminiDiscoveryAsync(
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

                await _geminiSemaphore.WaitAsync(cancellationToken);
                try
                {
                    using var httpClient = _httpClientFactory.CreateClient();
                    httpClient.Timeout = TimeSpan.FromMinutes(3);

                    var apiUrl = $"{GEMINI_API_BASE}?key={apiKey}";

                    // Discovery: googleSearch tool aktif, responseMimeType YOK (tool use ile çakışır)
                    var requestBody = new
                    {
                        contents = new[]
                        {
                            new { parts = new[] { new { text = prompt } } }
                        },
                        tools = new[]
                        {
                            new { googleSearch = new { } }
                        },
                        generationConfig = new
                        {
                            temperature = 0.1
                        }
                    };

                    using var jsonContent = new StringContent(
                        JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                    _logger.LogInformation("🤖 Discovery Batch #{BatchIndex} - Key #{KeyIndex}, Deneme {Attempt}/{Max}",
                        batchIndex, keyIndex, attempt, maxRetries);

                    using var httpResponse = await httpClient.PostAsync(apiUrl, jsonContent, cancellationToken);
                    var responseContent = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        if ((int)httpResponse.StatusCode == 429 && attempt < maxRetries)
                        {
                            int waitSeconds = attempt * 15;
                            _logger.LogWarning("⚠️ Rate limit (429), {Wait}s bekleniyor...", waitSeconds);
                            await Task.Delay(TimeSpan.FromSeconds(waitSeconds), cancellationToken);
                            continue;
                        }
                        _logger.LogError("❌ Gemini API hatası [{Status}]: {Content}",
                            (int)httpResponse.StatusCode, responseContent);
                        return new List<BusinessDto>();
                    }

                    var text = ExtractTextFromGeminiResponse(responseContent, batchIndex);
                    if (string.IsNullOrEmpty(text)) return new List<BusinessDto>();

                    var businesses = ParseDiscoveryResponse(text, sector, city, country);
                    _logger.LogInformation("✅ Discovery Batch #{BatchIndex} — {Count} işletme", batchIndex, businesses.Count);
                    return businesses;
                }
                finally
                {
                    _geminiSemaphore.Release();
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("⚠️ Discovery Batch #{BatchIndex} iptal edildi", batchIndex);
                return new List<BusinessDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Discovery Batch #{BatchIndex} hatası (deneme {Attempt}/{Max})", batchIndex, attempt, maxRetries);
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

    private string BuildDiscoveryPrompt(string sector, string location, int targetCount, int batchIndex)
    {
        return $@"# ROLE
You are a Senior B2B Trade Intelligence Specialist. Find REAL, ACTIVE businesses only.

# GOAL
Find {targetCount} real businesses operating in: {sector} / {location}

# SEARCH STRATEGY
1. Search Google: site:linkedin.com/company ""{sector}"" ""{location}""
2. Search trade directories: kompass.com, europages.com, alibaba.com for ""{sector}"" in ""{location}""
3. Search local chambers of commerce and industry associations in {location}
4. Search: ""{sector} manufacturer {location}"", ""{sector} exporter {location}"", ""{sector} wholesaler {location}""
5. Dig deeper — look for SMEs and companies in local industrial zones (OSB/Sanayi Sitesi)
6. Do NOT only list the top 10 most famous companies — find lesser-known real businesses too

# CRITICAL RULES
- Only include companies with a REAL, WORKING website URL
- Email address must match the company's own domain (not gmail/hotmail)
- Return as many REAL companies as you can find, up to {targetCount}
- It is better to return 5 real companies than 50 fake ones
- Do NOT invent any data

# OUTPUT FORMAT — ONLY A VALID JSON ARRAY, NO OTHER TEXT
[
  {{
    ""businessName"": ""Company Name"",
    ""address"": ""Full address or city"",
    ""email"": ""info@company.com"",
    ""website"": ""https://www.company.com"",
    ""contextualData"": ""One sentence about their main products or exports."",
    ""hsCodes"": [""1234""],
    ""confidenceScore"": 0.9,
    ""category"": ""{sector}"",
    ""city"": ""{location.Split(',')[0].Trim()}"",
    ""country"": ""{(location.Contains(",") ? location.Split(',').Last().Trim() : location)}""
  }}
]";
    }

    private List<BusinessDto> ParseDiscoveryResponse(string responseText, string sector, string city, string? country)
    {
        try
        {
            responseText = CleanJsonResponse(responseText);

            if (!responseText.StartsWith("[") && !responseText.StartsWith("{"))
            {
                _logger.LogWarning("⚠️ Gemini JSON döndürmedi. Preview: {Preview}",
                    responseText.Length > 200 ? responseText[..200] + "..." : responseText);
                return new List<BusinessDto>();
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var businesses = JsonSerializer.Deserialize<List<BusinessDto>>(responseText, options)
                             ?? new List<BusinessDto>();

            foreach (var business in businesses)
            {
                business.Category ??= sector;
                business.Country ??= country;
                if (string.IsNullOrWhiteSpace(business.City))
                    business.City = !string.IsNullOrWhiteSpace(city) ? city : (country ?? "Genel");
            }

            _logger.LogInformation("✅ {Count} işletme parse edildi", businesses.Count);
            return businesses;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "❌ Discovery JSON parse hatası");
            return TryExtractJsonArray<BusinessDto>(responseText, sector, city, country);
        }
    }

    // ─────────────────────────────────────────────
    // STEP 2: ENRICHMENT — İletişim Bilgilerini Bul
    // ─────────────────────────────────────────────

    public async Task<(List<BusinessDto> EnrichedBusinesses, int SuccessfulCount)> EnrichBusinessesAsync(
        List<BusinessDto> businesses,
        int batchSize = 8,
        CancellationToken cancellationToken = default)
    {
        if (businesses == null || !businesses.Any())
            return (new List<BusinessDto>(), 0);

        var allResults = new List<BusinessDto>();
        var successfulCount = 0;
        var totalBatches = (int)Math.Ceiling((double)businesses.Count / batchSize);

        _logger.LogInformation("📧 STEP 2: Batched enrichment başlatılıyor — {TotalCount} firma, {BatchCount} batch ({BatchSize}'lik)",
            businesses.Count, totalBatches, batchSize);

        var enrichmentTasks = new List<Task<(List<BusinessDto> EnrichedBatch, int SuccessCount)>>();

        for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
        {
            var batch = businesses.Skip(batchIndex * batchSize).Take(batchSize).ToList();
            _logger.LogInformation("📦 Batch {Current}/{Total} kuyruğa eklendi ({Count} firma)",
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

    private async Task<(List<BusinessDto> EnrichedBatch, int SuccessCount)> SafeEnrichBatchAsync(
        List<BusinessDto> batch, int currentBatch, int totalBatches)
    {
        try
        {
            var result = await EnrichBatchAsync(batch);
            _logger.LogInformation("✅ Enrichment Batch {Current}/{Total} tamamlandı: {SuccessCount} başarılı",
                currentBatch, totalBatches, result.SuccessCount);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Enrichment Batch {Current}/{Total} başarısız. Orijinal data korunuyor.",
                currentBatch, totalBatches);
            return (batch, 0);
        }
    }

    private async Task<(List<BusinessDto> EnrichedBatch, int SuccessCount)> EnrichBatchAsync(List<BusinessDto> batch)
    {
        var prompt = BuildEnrichmentPrompt(batch);
        var apiKey = GetNextApiKey();

        await _geminiSemaphore.WaitAsync();
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(3);

            // DÜZELTME: Temiz URL — markdown formatı yok
            var apiUrl = $"{GEMINI_API_BASE}?key={apiKey}";

            // DÜZELTME: googleSearch tool kullanılırken responseMimeType OLMAMALI
            // İkisi birlikte kullanılınca Gemini boş/hatalı yanıt döndürür
            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                },
                tools = new[]
                {
                    new { googleSearch = new { } }
                },
                generationConfig = new
                {
                    temperature = 0.1
                    // responseMimeType KASITLI OLARAK KALDIRILDI
                }
            };

            using var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            using var httpResponse = await httpClient.PostAsync(apiUrl, jsonContent);
            var responseContent = await httpResponse.Content.ReadAsStringAsync();

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogError("❌ Gemini Enrichment API hatası [{Status}]: {Content}",
                    (int)httpResponse.StatusCode, responseContent);
                throw new Exception($"Gemini Enrichment failed [{(int)httpResponse.StatusCode}]: {responseContent}");
            }

            var text = ExtractTextFromGeminiResponse(responseContent, -1);

            if (string.IsNullOrEmpty(text))
            {
                _logger.LogWarning("⚠️ Gemini enrichment boş yanıt döndü");
                return (batch, 0);
            }

            var enrichedList = ParseEnrichmentResponse(text, batch);
            var successCount = enrichedList.Count(b =>
                HasValidContactInfo(b.Email) || HasValidContactInfo(b.Mobile));

            _logger.LogInformation("💳 KREDİ DURUMU | Düşen={SuccessCount}", successCount);
            return (enrichedList, successCount);
        }
        finally
        {
            _geminiSemaphore.Release();
        }
    }

    private string BuildEnrichmentPrompt(List<BusinessDto> batch)
    {
        var businessList = new StringBuilder();
        for (int i = 0; i < batch.Count; i++)
        {
            var b = batch[i];
            businessList.AppendLine($"{i + 1}. {b.BusinessName} | website: {b.Website ?? "unknown"} | city: {b.City}");
        }

        return $@"You are a B2B lead researcher. Use Google Search to find REAL contact information for each company listed below.

For EACH company, search:
- ""{{company name}} email contact""
- The company's official website /contact page
- LinkedIn company page

COMPANIES TO RESEARCH:
{businessList}

Return ONLY a valid JSON array. No preamble, no explanation, no markdown code blocks.
Include ALL {batch.Count} companies in your response (use null for fields you cannot find).

JSON FORMAT:
[
  {{
    ""index"": 1,
    ""email"": ""info@example.com"",
    ""mobile"": ""+90 212 000 0000"",
    ""socialMedia"": ""https://linkedin.com/company/example"",
    ""decisionMaker"": ""Ali Yılmaz - Export Manager""
  }}
]

STRICT RULES:
- Use JSON null (not the string ""null"") when data is not found
- Do NOT invent or guess any email address or phone number
- Email must be from the company's own domain — no gmail/hotmail
- Phone numbers must include country code (e.g. +90 for Turkey)
- Return exactly {batch.Count} objects in the array, one per company";
    }

    private List<BusinessDto> ParseEnrichmentResponse(string responseText, List<BusinessDto> originalBatch)
    {
        try
        {
            responseText = CleanJsonResponse(responseText);

            // JSON array'i bulmaya çalış
            int startIndex = responseText.IndexOf('[');
            int endIndex = responseText.LastIndexOf(']');

            if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
                responseText = responseText[startIndex..(endIndex + 1)];

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var enrichments = JsonSerializer.Deserialize<List<EnrichmentResult>>(responseText, options);

            if (enrichments == null) return originalBatch;

            foreach (var enrichment in enrichments)
            {
                var index = enrichment.Index - 1;
                if (index < 0 || index >= originalBatch.Count) continue;

                var business = originalBatch[index];

                if (HasValidContactInfo(enrichment.Email))
                    business.Email = enrichment.Email;

                if (HasValidContactInfo(enrichment.Mobile))
                    business.Mobile = enrichment.Mobile;

                if (HasValidContactInfo(enrichment.SocialMedia))
                    business.SocialMedia = enrichment.SocialMedia;

                
            }

            return originalBatch;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Enrichment response parse hatası. Raw: {Raw}",
                responseText.Length > 300 ? responseText[..300] : responseText);
            return originalBatch;
        }
    }

    // ─────────────────────────────────────────────
    // YARDIMCI METODLAR
    // ─────────────────────────────────────────────

    /// <summary>
    /// Gemini API yanıtından text içeriğini güvenli şekilde çıkarır
    /// </summary>
    private string ExtractTextFromGeminiResponse(string responseContent, int batchIndex)
    {
        try
        {
            var jsonResponse = JsonDocument.Parse(responseContent);

            // finishReason kontrolü
            var candidates = jsonResponse.RootElement.GetProperty("candidates");
            if (candidates.GetArrayLength() == 0)
            {
                _logger.LogWarning("⚠️ Batch #{BatchIndex} — candidates dizisi boş", batchIndex);
                return string.Empty;
            }

            var firstCandidate = candidates[0];

            // RECITATION veya SAFETY engelini kontrol et
            if (firstCandidate.TryGetProperty("finishReason", out var finishReason))
            {
                var reason = finishReason.GetString();
                if (reason is "RECITATION" or "SAFETY" or "OTHER")
                {
                    _logger.LogWarning("⚠️ Batch #{BatchIndex} — Gemini finishReason={Reason}", batchIndex, reason);
                    return string.Empty;
                }
            }

            var text = firstCandidate
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return text ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Batch #{BatchIndex} — Gemini response parse edilemedi. Raw: {Raw}",
                batchIndex, responseContent.Length > 200 ? responseContent[..200] : responseContent);
            return string.Empty;
        }
    }

    /// <summary>
    /// Markdown kod bloklarını ve başındaki/sonundaki boşlukları temizler
    /// </summary>
    private static string CleanJsonResponse(string text)
    {
        text = text.Trim();

        if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            text = text[7..];
        else if (text.StartsWith("```"))
            text = text[3..];

        if (text.EndsWith("```"))
            text = text[..^3];

        return text.Trim();
    }

    /// <summary>
    /// JSON parse hatası durumunda [ ] arasını çıkarıp tekrar dener
    /// </summary>
    private List<BusinessDto> TryExtractJsonArray<T>(
        string responseText, string sector, string city, string? country) where T : BusinessDto
    {
        int startIndex = responseText.IndexOf('[');
        int endIndex = responseText.LastIndexOf(']');

        if (startIndex == -1 || endIndex == -1 || endIndex <= startIndex)
            return new List<BusinessDto>();

        try
        {
            string cleanJson = responseText[startIndex..(endIndex + 1)];
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var businesses = JsonSerializer.Deserialize<List<BusinessDto>>(cleanJson, options)
                             ?? new List<BusinessDto>();

            foreach (var business in businesses)
            {
                business.Category ??= sector;
                business.Country ??= country;
                if (string.IsNullOrWhiteSpace(business.City))
                    business.City = !string.IsNullOrWhiteSpace(city) ? city : (country ?? "Genel");
            }

            _logger.LogInformation("✅ IndexOf fallback ile {Count} işletme kurtarıldı", businesses.Count);
            return businesses;
        }
        catch
        {
            return new List<BusinessDto>();
        }
    }

    /// <summary>
    /// Email, telefon vb. alanların gerçek veri içerip içermediğini kontrol eder
    /// </summary>
    private bool HasValidContactInfo(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var lower = value.ToLowerInvariant().Trim();

        var invalidValues = new[]
        {
            "not found", "notfound", "bulunamadı", "bulunamadi",
            "yok", "n/a", "na", "-", "null", "none", "unknown",
            "no email", "no phone", "bilinmiyor"
        };

        return !invalidValues.Any(iv => lower == iv || lower.Contains(iv));
    }

    // ─────────────────────────────────────────────
    // İÇ SINIFLAR
    // ─────────────────────────────────────────────

    private class EnrichmentResult
    {
        public int Index { get; set; }
        public string? Email { get; set; }
        public string? Mobile { get; set; }
        public string? SocialMedia { get; set; }
        public string? DecisionMaker { get; set; }
        public string? TriggerEvent { get; set; }
        public string? ContactPerson { get; set; }
    }
}