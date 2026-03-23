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

    /// <summary>
    /// Kullanıcının 30 günlük ücretsiz deneme süresinde olup olmadığını kontrol et
    /// </summary>
    bool IsInFreeTrial(int userId);
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

        // Gemini uzun yanıt verebiliyor — 5 dakika timeout
        _httpClient.Timeout = TimeSpan.FromMinutes(5);

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
        var lang = request.Language ?? "Türkçe";
        var origin = request.OriginCountry ?? "Türkiye";
        var today = DateTime.UtcNow.ToString("dd MMMM yyyy");

        return $@"Sen WTO ve ITC Trade Map uzmanı küresel ticaret analistisin.
Aşağıdaki ürün ve pazar için kapsamlı bir İhracat Yol Haritası Raporu yaz.

PARAMETRELER:
- HS Code: {request.HsCode}
- Ürün: {request.ProductName}
- Hedef Pazar: {request.TargetCountry}
- Menşei: {origin}
- Dil: {lang}
- Tarih: {today}

YAZIM KURALLARI:
- Tüm içerik {lang} dilinde olacak
- Tablolarda gerçek sayısal veriler kullan, boş bırakma
- Her bölüm en az 3-4 paragraf içersin
- Markdown tablo formatı: | Başlık | Başlık | şeklinde, ayraç satırı: |---|---|
- Asla sadece tire veya boşluktan oluşan satır yazma

RAPOR YAPISI (sırayla yaz):

# {request.ProductName} - {request.TargetCountry} İhracat Yol Haritası

**GTIP:** {request.HsCode} | **Hedef:** {request.TargetCountry} | **Menşei:** {origin} | **Tarih:** {today}

## 1. Gümrük ve Mevzuat Analizi

### 1.1 Ticaret Anlaşmaları
{origin} ile {request.TargetCountry} arasındaki ticaret anlaşmalarını (STA, GB, tercihli vb.) analiz et.

### 1.2 Vergi Yapısı
Gümrük Vergisi, KDV, ÖTV, Anti-Damping, Diğer Vergiler ve Toplam Vergi Yükü için gerçek oranlarla tablo yaz.
Tablo formatı: | Vergi Türü | Oran (%) | Açıklama |

### 1.3 Teknik Sertifikalar
Zorunlu sertifikaları tablo olarak listele.
Tablo formatı: | Sertifika | Zorunluluk | Süre | Tahmini Maliyet (USD) |

### 1.4 Landed Cost Simülasyonu (10.000 USD FOB)
FOB, Navlun, Sigorta, CIF, Gümrük, KDV, Diğer, Toplam Landed Cost, %20 Kar Marjı, Önerilen Satış Fiyatı değerlerini hesaplayıp tablo yaz.
Tablo formatı: | Kalem | Tutar (USD) | Açıklama |

## 2. Pazar Hacmi ve Trendler

### 2.1 İthalat Trendi (2023-2025)
3 yıl için tablo yaz: | Yıl | İthalat (Milyon USD) | Değişim (%) | Açıklama |
Ardından CAGR hesapla.

### 2.2 Tüketici Davranışları ve Kültürel Faktörler
Tüketici tercihleri, kültürel etkiler, ekonomik faktörler, mevsimsel etkiler hakkında detaylı yaz.

### 2.3 Pazar Büyüklüğü ve Potansiyel
Toplam pazar büyüklüğü, büyüme potansiyeli ve {origin} için erişilebilir pazar (SAM) değerlerini belirt.

## 3. Rekabet ve Pazar Payı

### 3.1 Rakip Ülke Analizi
İlk 5 rakibi tablo olarak yaz: | Sıra | Ülke | Pazar Payı (%) | İthalat (M USD) | Strateji | Güçlü Yönler |
Son satırda {origin} yer alsın.

### 3.2 {origin} Konumu
Mevcut pazar payı, güçlü/zayıf yönler ve avantajları yaz.

### 3.3 Fiyat Segmentasyonu
Ekonomik/Orta/Premium segmentleri tablo olarak yaz: | Segment | Fiyat Aralığı USD | Fiyat Yerel Para | Hedef Kitle | Pazar Payı % |
Önerilen giriş segmenti ve gerekçesi.

## 4. Lojistik ve Tedarik Zinciri

### 4.1 Taşıma Modları
Karayolu/Denizyolu/Havayolu/Demiryolu için tablo: | Mod | Süre (Gün) | Maliyet USD/Ton | Avantajlar | Dezavantajlar | Uygunluk |

### 4.2 Önerilen Rotalar
Ana rota ve alternatif rotaları süre ve maliyet ile birlikte açıkla.

### 4.3 Gümrük İşlemleri
Ortalama gümrükleme süresi, zorunlu belgeler, dikkat edilmesi gerekenler.

## 5. Stratejik SWOT Analizi

### 5.1 SWOT Tablosu
İki tablo yaz:
Tablo 1: | Güçlü Yönler | Zayıf Yönler | (4 satır)
Tablo 2: | Fırsatlar | Tehditler | (4 satır)

### 5.2 Risk Analizi
Tablo: | Risk | Seviye | Açıklama | Önlem | — Kur, Siyasi, Ticaret, Rekabet, Tedarik riskleri.

## 6. Mevsimsel Talep Analizi
Tablo: | Dönem | Talep (1-10) | Açıklama | Öneri | — Q1/Q2/Q3/Q4 için.
En yoğun dönem ve stok önerisi.

## 7. Stratejik Öneriler

### 7.1 Kısa Vadeli (0-6 Ay)
En az 5 madde.

### 7.2 Orta Vadeli (6-12 Ay)
En az 5 madde.

### 7.3 Uzun Vadeli (12+ Ay)
En az 5 madde.

## 8. Sonuç
Kapsamlı değerlendirme, genel tavsiye (İlerle/Dikkatli Ol/Bekle), başarı olasılığı (%), kaynaklar.

## ChartData

Aşağıdaki JSON bloğunu birebir bu formatta yaz, 0 değerleri gerçek rakamlarla doldur:

```json
{{
  ""marketTrend"": {{""labels"": [""2023"",""2024"",""2025""], ""values"": [0,0,0], ""unit"": ""Milyon USD"", ""cagr"": 0}},
  ""marketShare"": {{""labels"": [""Ulke1"",""Ulke2"",""Ulke3"",""{origin}"",""Diger""], ""values"": [0,0,0,0,0], ""unit"": ""%""}},
  ""seasonalDemand"": {{""labels"": [""Oca"",""Sub"",""Mar"",""Nis"",""May"",""Haz"",""Tem"",""Agu"",""Eyl"",""Eki"",""Kas"",""Ara""], ""values"": [0,0,0,0,0,0,0,0,0,0,0,0], ""unit"": ""Talep Endeksi""}},
  ""costBreakdown"": {{""labels"": [""FOB"",""Navlun"",""Sigorta"",""Gumruk"",""KDV"",""Diger""], ""values"": [10000,0,0,0,0,0], ""totalLandedCost"": 0, ""unit"": ""USD""}},
  ""successMetrics"": {{""successRate"": 0, ""marketPotential"": ""Yuksek"", ""competitionLevel"": ""Orta"", ""recommendation"": ""Ilerle""}}
}}
```

ZORUNLU: JSON'daki tum 0 degerleri gercek rakamlarla degistir. marketShare toplamı 100 olmali. JSON'dan sonra hicbir sey yazma.
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
                temperature = 0.4,
                topK = 40,
                topP = 0.90,
                maxOutputTokens = 32768  // 16384 raporu kesiyor, 65536 timeout yapıyor — 32768 denge noktası
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Retry logic
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromMinutes(3));
                var response = await _httpClient.PostAsync(url, content, cts.Token);
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

    /// <summary>
    /// Kullanıcının kayıt tarihinden itibaren 30 günlük ücretsiz deneme döneminde olup olmadığını kontrol et
    /// </summary>
    public bool IsInFreeTrial(int userId)
    {
        try
        {
            var user = _dbContext.Users.Find(userId);
            if (user == null) return false;
            var daysSinceRegistration = (DateTime.UtcNow - user.CreatedAt).TotalDays;
            var inTrial = daysSinceRegistration <= 30;
            _logger.LogInformation(
                "🎯 FreeTrial kontrol | UserId={Id} | KayıtTarihi={Date} | GünFarkı={Days:F1} | ÜcretsizMi={Free}",
                userId, user.CreatedAt, daysSinceRegistration, inTrial);
            return inTrial;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ IsInFreeTrial kontrol hatası | UserId={Id}", userId);
            return false; // Hata durumunda güvenli taraf: ücretli say
        }
    }
}