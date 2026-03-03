using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradeScout.API.Data;
using TradeScout.API.DTOs;
using TradeScout.API.Models;

namespace TradeScout.API.Services;

/// <summary>
/// Trade Intelligence Service Interface
/// </summary>
public interface ITradeIntelligenceService
{
    /// <summary>
    /// Ticari istihbarat raporu oluştur
    /// </summary>
    Task<TradeIntelligenceReportDto> GenerateReportAsync(TradeIntelligenceRequestDto request, int? userId = null, string? ipAddress = null, string? userAgent = null);
    
    /// <summary>
    /// Kullanıcının pazar analizi geçmişini getir
    /// </summary>
    Task<List<MarketAnalysis>> GetUserAnalysisHistoryAsync(int userId, int page = 1, int pageSize = 10);
    
    /// <summary>
    /// Belirli bir analizi ID ile getir
    /// </summary>
    Task<MarketAnalysis?> GetAnalysisByIdAsync(int id, int? userId = null);
    
    /// <summary>
    /// PDF indirme durumunu güncelle
    /// </summary>
    Task MarkPdfDownloadedAsync(int analysisId);
    
    /// <summary>
    /// Analizi favorilere ekle/çıkar
    /// </summary>
    Task<bool> ToggleFavoriteAsync(int analysisId, int userId);
    
    /// <summary>
    /// Analize not ekle
    /// </summary>
    Task<bool> AddNoteAsync(int analysisId, int userId, string note);
}

/// <summary>
/// Trade Intelligence Service - Gemini AI ile ticari istihbarat raporları oluşturur
/// </summary>
public class TradeIntelligenceService : ITradeIntelligenceService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TradeIntelligenceService> _logger;
    private readonly ApplicationDbContext _dbContext;
    private readonly List<string> _apiKeys;
    private int _currentKeyIndex = 0;
    private readonly object _keyLock = new();

    public TradeIntelligenceService(
        HttpClient httpClient,
        ILogger<TradeIntelligenceService> logger,
        IConfiguration configuration,
        ApplicationDbContext dbContext)
    {
        _httpClient = httpClient;
        _logger = logger;
        _dbContext = dbContext;
        
        // Load API keys from environment and configuration
        _apiKeys = LoadApiKeys(configuration);
        
        if (_apiKeys.Count == 0)
        {
            _logger.LogWarning("⚠️ No Gemini API keys configured for Trade Intelligence Service");
        }
        else
        {
            _logger.LogInformation("✅ Trade Intelligence Service initialized with {Count} API key(s)", _apiKeys.Count);
        }
    }

    private List<string> LoadApiKeys(IConfiguration configuration)
    {
        var keys = new List<string>();
        
        // Primary key from environment
        var primaryKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (!string.IsNullOrEmpty(primaryKey))
            keys.Add(primaryKey);
        
        // Additional keys from environment (GEMINI_API_KEY_1, GEMINI_API_KEY_2, etc.)
        for (int i = 1; i <= 10; i++)
        {
            var key = Environment.GetEnvironmentVariable($"GEMINI_API_KEY_{i}");
            if (!string.IsNullOrEmpty(key) && !keys.Contains(key))
                keys.Add(key);
        }
        
        // Fallback to configuration
        if (keys.Count == 0)
        {
            var configKey = configuration["GeminiSettings:ApiKey"];
            if (!string.IsNullOrEmpty(configKey))
                keys.Add(configKey);
        }
        
        return keys;
    }

    private string GetNextApiKey()
    {
        if (_apiKeys.Count == 0)
            throw new InvalidOperationException("No Gemini API keys configured");
        
        lock (_keyLock)
        {
            var key = _apiKeys[_currentKeyIndex];
            _currentKeyIndex = (_currentKeyIndex + 1) % _apiKeys.Count;
            return key;
        }
    }

    public async Task<TradeIntelligenceReportDto> GenerateReportAsync(TradeIntelligenceRequestDto request, int? userId = null, string? ipAddress = null, string? userAgent = null)
    {
        var report = new TradeIntelligenceReportDto
        {
            Request = request
        };

        // Veritabanına kayıt oluştur
        var marketAnalysis = new MarketAnalysis
        {
            UserId = userId,
            HsCode = request.HsCode,
            ProductName = request.ProductName,
            TargetCountry = request.TargetCountry,
            OriginCountry = request.OriginCountry ?? "Türkiye",
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("📊 Trade Intelligence Report generating for HS:{HsCode}, Product:{Product}, Target:{Target}, Origin:{Origin}",
                request.HsCode, request.ProductName, request.TargetCountry, request.OriginCountry);

            var prompt = BuildPrompt(request);
            var content = await CallGeminiAsync(prompt);

            if (string.IsNullOrEmpty(content))
            {
                report.Success = false;
                report.ErrorMessage = "Rapor oluşturulamadı. Lütfen tekrar deneyin.";
                
                // Başarısız kaydı veritabanına ekle
                marketAnalysis.IsSuccessful = false;
                marketAnalysis.ErrorMessage = report.ErrorMessage;
                _dbContext.MarketAnalyses.Add(marketAnalysis);
                await _dbContext.SaveChangesAsync();
                
                return report;
            }

            report.ReportContent = content;
            report.Success = true;
            
            // Parse sections (basic parsing)
            report.Sections = ParseReportSections(content);

            // Başarılı kaydı veritabanına ekle
            marketAnalysis.IsSuccessful = true;
            marketAnalysis.ReportContent = content;
            _dbContext.MarketAnalyses.Add(marketAnalysis);
            await _dbContext.SaveChangesAsync();
            
            // Report'a analysis ID'sini ekle
            report.ReportId = marketAnalysis.Id.ToString();

            _logger.LogInformation("✅ Trade Intelligence Report generated and saved (ID: {Id}) for {Product} -> {Target}",
                marketAnalysis.Id, request.ProductName, request.TargetCountry);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Trade Intelligence Report generation failed");
            report.Success = false;
            report.ErrorMessage = $"Rapor oluşturulurken hata: {ex.Message}";
            
            // Hata kaydını veritabanına ekle
            marketAnalysis.IsSuccessful = false;
            marketAnalysis.ErrorMessage = ex.Message.Length > 500 ? ex.Message.Substring(0, 500) : ex.Message;
            
            try
            {
                _dbContext.MarketAnalyses.Add(marketAnalysis);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "❌ Failed to save error record to database");
            }
            
            return report;
        }
    }

    private string BuildPrompt(TradeIntelligenceRequestDto request)
    {
        return $@"
Sen profesyonel bir global ticaret analistisin. Dünya Ticaret Örgütü (WTO) ve ITC Trade Map verilerine hakimsin.

**Analiz Parametreleri:**
- HS Code (GTIP): {request.HsCode}
- Ürün: {request.ProductName}
- Hedef Pazar: {request.TargetCountry}
- Menşei Ülke: {request.OriginCountry}

**Analiz Kuralları:**
1. Vergi hesaplamalarını {request.OriginCountry} menşeli bir ürünün {request.TargetCountry} pazarına girişi üzerinden yap
2. Pazar hacmi verilerini son 3 yılı kapsayacak şekilde (2023-2025) oluştur
3. Lojistik kısmında {request.OriginCountry}'den {request.TargetCountry}'ye en uygun taşıma modlarını analiz et
4. ASCII sanat çizgileri (===, ---, ***, ~~~) KULLANMA - sadece Markdown formatı
5. Raporun sonuna mutlaka JSON ChartData bloğu ekle

# {request.ProductName} - {request.TargetCountry} Pazar Analizi Raporu

**GTIP Kodu:** {request.HsCode}  
**Hedef Pazar:** {request.TargetCountry}  
**Menşei Ülke:** {request.OriginCountry}  
**Rapor Tarihi:** {DateTime.UtcNow:dd MMMM yyyy}

## 1. İthalat Vergi Yapısı ve Maliyet Analizi

{request.TargetCountry} pazarına {request.OriginCountry} menşeli ürün girişi için vergi yapısı:

| Vergi Türü | Oran (%) | Tutar (USD) | Açıklama |
|------------|----------|-------------|----------|
| Gümrük Vergisi | X | X | Detay |
| KDV | X | X | Detay |
| ÖTV | X | X | Varsa |
| Anti-Damping | X | X | Varsa |
| **Toplam Vergi Yükü** | X | X | Toplam |

**Örnek Maliyet Simülasyonu (10.000 USD FOB değerinde ürün):**

| Kalem | Tutar (USD) |
|-------|-------------|
| FOB Değeri | 10.000 |
| Navlun + Sigorta | X |
| CIF Değeri | X |
| Gümrük Vergisi | X |
| KDV | X |
| Diğer Vergiler | X |
| **Toplam Landed Cost** | X |

## 2. Pazar Hacmi ve Trendler

{request.TargetCountry} pazarının son 3 yıllık ithalat verileri:

| Yıl | İthalat (Milyon USD) | Değişim (%) | Açıklama |
|-----|---------------------|-------------|----------|
| 2023 | X | - | Baz yıl |
| 2024 | X | X% | Trend açıklaması |
| 2025 | X | X% | Trend açıklaması |

**CAGR (Bileşik Yıllık Büyüme):** X%

**Pazar Büyüklüğü:** X Milyon USD (2025)

**Pazar Trendleri:**
- Trend 1: Açıklama
- Trend 2: Açıklama
- Trend 3: Açıklama

## 3. Rekabet ve Pazar Payı

{request.TargetCountry} pazarında tedarikçi ülkelerin pazar payları:

| Sıra | Ülke | Pazar Payı (%) | İthalat (Milyon USD) | Rekabet Avantajı |
|------|------|----------------|---------------------|------------------|
| 1 | Ülke1 | X | X | Avantaj |
| 2 | Ülke2 | X | X | Avantaj |
| 3 | Ülke3 | X | X | Avantaj |
| 4 | Ülke4 | X | X | Avantaj |
| 5 | {request.OriginCountry} | X | X | Avantaj |

**{request.OriginCountry} SWOT Analizi:**

| Güçlü Yönler | Zayıf Yönler |
|--------------|--------------|
| + Madde 1 | - Madde 1 |
| + Madde 2 | - Madde 2 |

| Fırsatlar | Tehditler |
|-----------|-----------|
| + Madde 1 | - Madde 1 |
| + Madde 2 | - Madde 2 |

## 4. Lojistik ve Taşımacılık Analizi

{request.OriginCountry}'den {request.TargetCountry}'ye taşıma seçenekleri:

| Taşıma Modu | Süre (Gün) | Tahmini Maliyet (USD/Ton) | Uygunluk |
|-------------|------------|---------------------------|----------|
| Karayolu | X | X | Açıklama |
| Denizyolu | X | X | Açıklama |
| Havayolu | X | X | Açıklama |
| Demiryolu | X | X | Açıklama |

**Önerilen Taşıma Modu:** X - Gerekçe

**Ana Lojistik Rotalar:**
- Rota 1: Çıkış - Varış noktaları
- Rota 2: Alternatif rota

**Gümrük İşlemleri Süresi:** X-X iş günü

## 5. Mevsimsel Talep Analizi

{request.TargetCountry} pazarında {request.ProductName} için aylık talep dağılımı:

| Dönem | Talep Seviyesi | Açıklama |
|-------|----------------|----------|
| Ocak-Mart (Q1) | Düşük/Orta/Yüksek | Neden |
| Nisan-Haziran (Q2) | Düşük/Orta/Yüksek | Neden |
| Temmuz-Eylül (Q3) | Düşük/Orta/Yüksek | Neden |
| Ekim-Aralık (Q4) | Düşük/Orta/Yüksek | Neden |

**En Yoğun Satış Dönemi:** X ayları
**Stok Hazırlık Önerisi:** X aylarında stok artırımı

## 6. Fiyatlandırma Stratejisi

| Segment | Fiyat Aralığı (USD) | Hedef Kitle | Pazar Payı (%) |
|---------|---------------------|-------------|----------------|
| Ekonomik | X - X | Açıklama | X |
| Orta | X - X | Açıklama | X |
| Premium | X - X | Açıklama | X |

**Başarı Şansı:** X%
**Gerekçe:** Detaylı açıklama

**Önerilen Giriş Stratejisi:** Strateji adı
- Detay 1
- Detay 2
- Detay 3

## 7. Stratejik Öneriler

1. **Öneri 1:** Detaylı açıklama
2. **Öneri 2:** Detaylı açıklama
3. **Öneri 3:** Detaylı açıklama
4. **Öneri 4:** Detaylı açıklama
5. **Öneri 5:** Detaylı açıklama

## 8. Sonuç ve Genel Değerlendirme

Kapsamlı değerlendirme paragrafı - pazar potansiyeli, riskler ve fırsatlar özeti.

**Tavsiye:** Yatırım yapılmalı/Dikkatli olunmalı/Beklenilmeli

**Kaynaklar:** WTO, ITC Trade Map, Eurostat, {request.TargetCountry} Gümrük İdaresi, UN Comtrade

## ChartData (Grafik Verileri)

Aşağıdaki JSON bloğu frontend'de grafik oluşturmak için kullanılacak. Tüm sayıları gerçekçi verilerle doldur:

```json
{{
  ""reportInfo"": {{
    ""hsCode"": ""{request.HsCode}"",
    ""product"": ""{request.ProductName}"",
    ""targetCountry"": ""{request.TargetCountry}"",
    ""originCountry"": ""{request.OriginCountry}""
  }},
  ""marketTrend"": {{
    ""labels"": [""2023"", ""2024"", ""2025""],
    ""values"": [sayı1, sayı2, sayı3],
    ""unit"": ""Milyon USD"",
    ""cagr"": yüzde
  }},
  ""marketShare"": {{
    ""labels"": [""Ülke1"", ""Ülke2"", ""Ülke3"", ""Ülke4"", ""{request.OriginCountry}""],
    ""values"": [yüzde1, yüzde2, yüzde3, yüzde4, yüzde5],
    ""unit"": ""%""
  }},
  ""seasonalDemand"": {{
    ""labels"": [""Oca"", ""Şub"", ""Mar"", ""Nis"", ""May"", ""Haz"", ""Tem"", ""Ağu"", ""Eyl"", ""Eki"", ""Kas"", ""Ara""],
    ""values"": [ay1, ay2, ay3, ay4, ay5, ay6, ay7, ay8, ay9, ay10, ay11, ay12],
    ""unit"": ""Talep Endeksi (100 baz)""
  }},
  ""logistics"": {{
    ""modes"": [""Karayolu"", ""Denizyolu"", ""Havayolu""],
    ""durations"": [gün1, gün2, gün3],
    ""costs"": [maliyet1, maliyet2, maliyet3],
    ""recommended"": ""En uygun mod""
  }},
  ""costBreakdown"": {{
    ""labels"": [""FOB"", ""Navlun"", ""Sigorta"", ""Gümrük"", ""KDV"", ""Diğer""],
    ""values"": [10000, navlun, sigorta, gümrük, kdv, diğer],
    ""unit"": ""USD""
  }},
  ""priceSegments"": {{
    ""labels"": [""Ekonomik"", ""Orta"", ""Premium""],
    ""minValues"": [min1, min2, min3],
    ""maxValues"": [max1, max2, max3],
    ""marketShare"": [pay1, pay2, pay3],
    ""unit"": ""USD""
  }},
  ""successMetrics"": {{
    ""successRate"": yüzde,
    ""marketPotential"": ""Yüksek/Orta/Düşük"",
    ""competitionLevel"": ""Yüksek/Orta/Düşük"",
    ""entryBarrier"": ""Yüksek/Orta/Düşük""
  }}
}}
```

ÖNEMLİ: JSON'daki tüm ""sayı"", ""yüzde"", ""gün"", ""maliyet"" gibi yer tutucuları gerçekçi rakamlarla değiştir. Tırnak içinde sayı yazma.
";
    }

    private async Task<string?> CallGeminiAsync(string prompt)
    {
        var apiKey = GetNextApiKey();
        var url = $"https://generativelanguage.googleapis.com/v1/models/gemini-2.5-flash:generateContent?key={apiKey}";

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
            },
            generationConfig = new
            {
                temperature = 0.7,
                topK = 40,
                topP = 0.95,
                maxOutputTokens = 8192 // Large output for detailed report
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Retry logic
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var response = await _httpClient.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("candidates", out var candidates) &&
                        candidates.GetArrayLength() > 0)
                    {
                        var firstCandidate = candidates[0];
                        if (firstCandidate.TryGetProperty("content", out var contentObj) &&
                            contentObj.TryGetProperty("parts", out var parts) &&
                            parts.GetArrayLength() > 0)
                        {
                            var textPart = parts[0];
                            if (textPart.TryGetProperty("text", out var textElement))
                            {
                                return textElement.GetString();
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Gemini API error (attempt {Attempt}): {Status} - {Body}",
                        attempt, response.StatusCode, responseBody);

                    // Try different key on error
                    if (attempt < 3)
                    {
                        apiKey = GetNextApiKey();
                        url = $"https://generativelanguage.googleapis.com/v1/models/gemini-2.5-flash:generateContent?key={apiKey}";
                        await Task.Delay(1000 * attempt); // Exponential backoff
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gemini API request failed (attempt {Attempt})", attempt);
                if (attempt < 3)
                    await Task.Delay(1000 * attempt);
            }
        }

        return null;
    }

    private TradeReportSections ParseReportSections(string content)
    {
        var sections = new TradeReportSections();

        try
        {
            // Basic section extraction
            var lines = content.Split('\n');
            var currentSection = "";
            var sectionContent = new StringBuilder();

            foreach (var line in lines)
            {
                if (line.StartsWith("## 1.") || line.Contains("İthalat Vergi Yapısı"))
                {
                    currentSection = "tax";
                    sectionContent.Clear();
                }
                else if (line.StartsWith("## 2.") || line.Contains("Pazar Hacmi"))
                {
                    if (currentSection == "tax")
                    {
                        sections.TaxStructure = new TaxStructureSection
                        {
                            Summary = sectionContent.ToString().Trim()
                        };
                    }
                    currentSection = "market";
                    sectionContent.Clear();
                }
                else if (line.StartsWith("## 3.") || line.Contains("Rekabet"))
                {
                    if (currentSection == "market")
                    {
                        sections.MarketVolume = new MarketVolumeSection
                        {
                            Summary = sectionContent.ToString().Trim()
                        };
                    }
                    currentSection = "competition";
                    sectionContent.Clear();
                }
                else if (line.StartsWith("## 4.") || line.Contains("Fiyatlandırma"))
                {
                    if (currentSection == "competition")
                    {
                        sections.Competition = new CompetitionSection
                        {
                            Summary = sectionContent.ToString().Trim()
                        };
                    }
                    currentSection = "pricing";
                    sectionContent.Clear();
                }
                else if (line.StartsWith("## 5.") || line.Contains("Stratejik Öneriler"))
                {
                    if (currentSection == "pricing")
                    {
                        sections.PricingStrategy = new PricingStrategySection
                        {
                            Summary = sectionContent.ToString().Trim()
                        };
                    }
                    currentSection = "strategy";
                    sectionContent.Clear();
                }
                else
                {
                    sectionContent.AppendLine(line);
                }
            }

            // Handle last section
            if (currentSection == "pricing" && sections.PricingStrategy == null)
            {
                sections.PricingStrategy = new PricingStrategySection
                {
                    Summary = sectionContent.ToString().Trim()
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse report sections");
        }

        return sections;
    }

    /// <summary>
    /// Kullanıcının pazar analizi geçmişini getir
    /// </summary>
    public async Task<List<MarketAnalysis>> GetUserAnalysisHistoryAsync(int userId, int page = 1, int pageSize = 10)
    {
        try
        {
            var analyses = await _dbContext.MarketAnalyses
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return analyses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to get user analysis history for UserId: {UserId}", userId);
            return new List<MarketAnalysis>();
        }
    }

    /// <summary>
    /// Belirli bir analizi ID ile getir
    /// </summary>
    public async Task<MarketAnalysis?> GetAnalysisByIdAsync(int id, int? userId = null)
    {
        try
        {
            var query = _dbContext.MarketAnalyses.AsQueryable();
            
            if (userId.HasValue)
            {
                query = query.Where(a => a.Id == id && a.UserId == userId);
            }
            else
            {
                query = query.Where(a => a.Id == id);
            }

            var analysis = await query.FirstOrDefaultAsync();
            
            // Görüntüleme sayısını artır
            if (analysis != null)
            {
                analysis.ViewCount++;
                await _dbContext.SaveChangesAsync();
            }

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to get analysis by ID: {Id}", id);
            return null;
        }
    }

    /// <summary>
    /// PDF indirme durumunu güncelle
    /// </summary>
    public async Task MarkPdfDownloadedAsync(int analysisId)
    {
        try
        {
            var analysis = await _dbContext.MarketAnalyses.FindAsync(analysisId);
            if (analysis != null)
            {
                analysis.PdfDownloaded = true;
                analysis.PdfDownloadedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("✅ PDF download marked for analysis ID: {Id}", analysisId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to mark PDF downloaded for ID: {Id}", analysisId);
        }
    }

    /// <summary>
    /// Analizi favorilere ekle/çıkar
    /// </summary>
    public async Task<bool> ToggleFavoriteAsync(int analysisId, int userId)
    {
        try
        {
            var analysis = await _dbContext.MarketAnalyses
                .FirstOrDefaultAsync(a => a.Id == analysisId && a.UserId == userId);
            
            if (analysis == null)
            {
                _logger.LogWarning("Analysis not found or user mismatch: {Id}, {UserId}", analysisId, userId);
                return false;
            }

            analysis.IsFavorite = !analysis.IsFavorite;
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("✅ Favorite toggled for analysis ID: {Id}, IsFavorite: {IsFavorite}", 
                analysisId, analysis.IsFavorite);
            
            return analysis.IsFavorite;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to toggle favorite for ID: {Id}", analysisId);
            return false;
        }
    }

    /// <summary>
    /// Analize not ekle
    /// </summary>
    public async Task<bool> AddNoteAsync(int analysisId, int userId, string note)
    {
        try
        {
            var analysis = await _dbContext.MarketAnalyses
                .FirstOrDefaultAsync(a => a.Id == analysisId && a.UserId == userId);
            
            if (analysis == null)
            {
                _logger.LogWarning("Analysis not found or user mismatch: {Id}, {UserId}", analysisId, userId);
                return false;
            }

            analysis.Notes = note?.Length > 1000 ? note.Substring(0, 1000) : note;
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("✅ Note added for analysis ID: {Id}", analysisId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to add note for ID: {Id}", analysisId);
            return false;
        }
    }
}
