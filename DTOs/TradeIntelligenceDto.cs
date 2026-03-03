namespace TradeScout.API.DTOs;

/// <summary>
/// Trade Intelligence Report Request DTO
/// </summary>
public class TradeIntelligenceRequestDto
{
    /// <summary>
    /// GTIP/HS Code (örn: 87116000)
    /// </summary>
    public string HsCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Ürün İsmi (örn: Elektrikli Bisiklet)
    /// </summary>
    public string ProductName { get; set; } = string.Empty;
    
    /// <summary>
    /// Hedef Ülke - İhracat yapılacak ülke (örn: Bulgaristan)
    /// </summary>
    public string TargetCountry { get; set; } = string.Empty;
    
    /// <summary>
    /// Menşei Ülke - Ürünün üretildiği ülke (örn: Türkiye)
    /// </summary>
    public string OriginCountry { get; set; } = "Türkiye";
}

/// <summary>
/// Trade Intelligence Report Response DTO
/// </summary>
public class TradeIntelligenceReportDto
{
    /// <summary>
    /// Rapor ID
    /// </summary>
    public string ReportId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Rapor oluşturulma tarihi
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// İstek bilgileri
    /// </summary>
    public TradeIntelligenceRequestDto Request { get; set; } = new();
    
    /// <summary>
    /// Rapor içeriği (Markdown formatında)
    /// </summary>
    public string ReportContent { get; set; } = string.Empty;
    
    /// <summary>
    /// Rapor bölümleri (ayrıştırılmış)
    /// </summary>
    public TradeReportSections? Sections { get; set; }
    
    /// <summary>
    /// İşlem başarılı mı?
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Hata mesajı (varsa)
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Trade Report parsed sections
/// </summary>
public class TradeReportSections
{
    /// <summary>
    /// 1. İthalat Vergi Yapısı ve Maliyet Analizi
    /// </summary>
    public TaxStructureSection? TaxStructure { get; set; }
    
    /// <summary>
    /// 2. Pazar Hacmi ve Trendler
    /// </summary>
    public MarketVolumeSection? MarketVolume { get; set; }
    
    /// <summary>
    /// 3. Rekabet ve Pazar Payı
    /// </summary>
    public CompetitionSection? Competition { get; set; }
    
    /// <summary>
    /// 4. Fiyatlandırma ve Pazarlama Stratejisi
    /// </summary>
    public PricingStrategySection? PricingStrategy { get; set; }
}

public class TaxStructureSection
{
    public string Summary { get; set; } = string.Empty;
    public List<TaxItem> Taxes { get; set; } = new();
    public string CostSimulation { get; set; } = string.Empty;
}

public class TaxItem
{
    public string TaxType { get; set; } = string.Empty; // Gümrük Vergisi, KDV, ÖTV, vb.
    public string Rate { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public class MarketVolumeSection
{
    public string Summary { get; set; } = string.Empty;
    public List<YearlyImportData> ImportData { get; set; } = new();
    public string CAGR { get; set; } = string.Empty; // Compound Annual Growth Rate
    public string TrendAnalysis { get; set; } = string.Empty;
}

public class YearlyImportData
{
    public int Year { get; set; }
    public string ImportValue { get; set; } = string.Empty; // Milyon USD/EUR
    public string GrowthRate { get; set; } = string.Empty;
}

public class CompetitionSection
{
    public string Summary { get; set; } = string.Empty;
    public List<CompetitorCountry> Competitors { get; set; } = new();
    public string OriginCountryAnalysis { get; set; } = string.Empty;
}

public class CompetitorCountry
{
    public string Country { get; set; } = string.Empty;
    public string MarketShare { get; set; } = string.Empty;
    public string ImportValue { get; set; } = string.Empty;
    public string Strengths { get; set; } = string.Empty;
}

public class PricingStrategySection
{
    public string Summary { get; set; } = string.Empty;
    public PriceSegments? PriceSegments { get; set; }
    public string SuccessRate { get; set; } = string.Empty;
    public string RecommendedStrategy { get; set; } = string.Empty;
    public List<string> StrategicRecommendations { get; set; } = new();
}

public class PriceSegments
{
    public string EntryLevel { get; set; } = string.Empty;
    public string MidRange { get; set; } = string.Empty;
    public string Premium { get; set; } = string.Empty;
}

/// <summary>
/// Convert existing report content to PDF request
/// </summary>
public class ConvertToPdfRequest
{
    /// <summary>
    /// Rapor içeriği (Markdown formatında) - ReportContent veya MarkdownContent kullanılabilir
    /// </summary>
    public string ReportContent { get; set; } = string.Empty;
    
    /// <summary>
    /// Alternatif: Markdown içeriği (Frontend uyumluluğu için)
    /// </summary>
    public string? MarkdownContent { get; set; }
    
    /// <summary>
    /// HS Kodu
    /// </summary>
    public string? HsCode { get; set; }
    
    /// <summary>
    /// Ürün adı (PDF başlığı için)
    /// </summary>
    public string? ProductName { get; set; }
    
    /// <summary>
    /// Hedef ülke (PDF başlığı için)
    /// </summary>
    public string? TargetCountry { get; set; }
    
    /// <summary>
    /// Menşei ülke
    /// </summary>
    public string? OriginCountry { get; set; }
    
    /// <summary>
    /// Gerçek içeriği döndür (ReportContent veya MarkdownContent hangisi doluysa)
    /// </summary>
    public string GetContent() => 
        !string.IsNullOrWhiteSpace(ReportContent) ? ReportContent : 
        !string.IsNullOrWhiteSpace(MarkdownContent) ? MarkdownContent : string.Empty;
}
