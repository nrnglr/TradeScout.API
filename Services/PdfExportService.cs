using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Drawing; 
using System.IO; 
using System.Text.RegularExpressions;

namespace TradeScout.API.Services;

// 🚀 1. INTERFACE TANIMI (Hata almamak için burada kalmalı)
public interface IPdfExportService
{
    byte[] GenerateAnalysisPdf(string reportContent, string productName, string targetCountry);
}

public class PdfExportService : IPdfExportService
{
    private readonly ILogger<PdfExportService> _logger;

    public PdfExportService(ILogger<PdfExportService> logger)
    {
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;

        // 🚀 2. AKILLI FONT YÜKLEME (Mac & Linux Uyumluluğu)
        try
        {
            // Uygulamanın çalıştığı klasörü al (bin/Debug veya bin/Release altı)
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var fontPath = Path.Combine(baseDir, "Fonts");

            // Eğer bin altında bulamazsa (bazı sunucu senaryoları için), ana dizine bak
            if (!Directory.Exists(fontPath) || !File.Exists(Path.Combine(fontPath, "Amiri-Regular.ttf")))
            {
                fontPath = "/home/fgstrade.com/app/backend/Fonts";
            }

            _logger.LogInformation("🔍 Fontlar şu dizinden yüklenmeye çalışılıyor: {Path}", fontPath);

            // Dosya isimleri sunucudakiyle (Amiri-Regular.ttf) birebir aynı olmalı
            var regularFont = Path.Combine(fontPath, "Amiri-Regular.ttf");
            var boldFont = Path.Combine(fontPath, "Amiri-Bold.ttf");

            if (File.Exists(regularFont))
            {
                using var stream = File.OpenRead(regularFont);
                FontManager.RegisterFont(stream);
                _logger.LogInformation("✅ Amiri Regular fontu başarıyla yüklendi.");
            }
            else 
            {
                _logger.LogError("❌ KRİTİK HATA: Font dosyası fiziksel olarak bulunamadı: {Path}", regularFont);
            }

            if (File.Exists(boldFont))
            {
                using var stream = File.OpenRead(boldFont);
                FontManager.RegisterFont(stream);
                _logger.LogInformation("✅ Amiri Bold fontu başarıyla yüklendi.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "⚠️ Font yükleme işlemi sırasında bir hata oluştu.");
        }
    }

    public byte[] GenerateAnalysisPdf(string reportContent, string productName, string targetCountry)
    {
        _logger.LogInformation("📄 PDF oluşturma işlemi başlatıldı: {Product}", productName);

        try
        {
            var isArabic = ContainsArabic(reportContent);

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Size(PageSizes.A4);
                    
                    // 🚀 3. FONT AYARI: Linux'ta Arial olmadığı için her durumda Amiri kullanıyoruz.
                    // Amiri fontu Latin (Türkçe/İngilizce) karakterleri de destekler.
                    page.DefaultTextStyle(x => x
                        .FontSize(10)
                        .FontFamily("Amiri") 
                        .Fallback(f => f.FontFamily("Amiri")));

                    if (isArabic)
                    {
                        page.ContentFromRightToLeft(); // Sağdan sola yazım desteği
                    }

                    // Header (Başlık)
                    page.Header().Column(headerCol =>
                    {
                        headerCol.Item().Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("FGS TRADE")
                                    .FontSize(24)
                                    .Bold()
                                    .FontColor(Colors.Blue.Darken2);

                                col.Item().Text("Global Ticari İstihbarat Platformu")
                                    .FontSize(10)
                                    .FontColor(Colors.Grey.Darken1);
                            });

                            row.ConstantItem(120).AlignRight().Text(DateTime.Now.ToString("dd MMMM yyyy"))
                                .FontSize(10);
                        });
                        headerCol.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Blue.Darken2);
                    });

                    // Content (İçerik)
                    page.Content().PaddingVertical(15).Column(col =>
                    {
                        col.Item().PaddingBottom(10).Text($"{productName} - {targetCountry} Pazar Analizi")
                            .FontSize(18)
                            .Bold()
                            .FontColor(Colors.Blue.Darken3);

                        var sections = ParseMarkdownContent(reportContent);
                        foreach (var section in sections)
                        {
                            RenderSection(col, section);
                        }
                    });

                    // Footer (Alt Bilgi)
                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("© 2026 FGS Trade - Sayfa ");
                        text.CurrentPageNumber();
                    });
                });
            }).GeneratePdf();

            _logger.LogInformation("✅ PDF başarıyla üretildi.");
            return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ PDF üretimi sırasında teknik bir hata oluştu!");
            throw;
        }
    }

    private void RenderSection(ColumnDescriptor col, ContentSection section)
    {
        switch (section.Type)
        {
            case ContentType.H1:
                col.Item().PaddingTop(15).Text(section.Text).FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                break;
            case ContentType.H2:
                col.Item().PaddingTop(12).Text(section.Text).FontSize(14).SemiBold().FontColor(Colors.Blue.Darken1);
                break;
            case ContentType.ListItem:
                col.Item().PaddingLeft(15).Text($"• {section.Text}").FontSize(10);
                break;
            case ContentType.Table:
                if (section.TableData != null)
                {
                    col.Item().PaddingVertical(5).Table(table =>
                    {
                        table.ColumnsDefinition(columns => { foreach (var _ in section.TableData.Headers) columns.RelativeColumn(); });
                        foreach (var header in section.TableData.Headers)
                        {
                            table.Cell().Background(Colors.Blue.Darken2).Padding(5).Text(header).FontSize(9).Bold().FontColor(Colors.White);
                        }
                        foreach (var row in section.TableData.Rows)
                        {
                            foreach (var cell in row)
                            {
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(cell).FontSize(9);
                            }
                        }
                    });
                }
                break;
            case ContentType.Paragraph:
                if (!string.IsNullOrWhiteSpace(section.Text)) col.Item().PaddingVertical(3).Text(section.Text).FontSize(10).LineHeight(1.4f);
                break;
        }
    }

    private List<ContentSection> ParseMarkdownContent(string markdown)
    {
        var sections = new List<ContentSection>();
        var lines = markdown.Split('\n');
        var inTable = false;
        var tableData = new TableData();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.StartsWith("|"))
            {
                if (line.Contains("---")) continue;
                var cells = line.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).ToList();
                if (!inTable) { inTable = true; tableData = new TableData { Headers = cells }; }
                else { tableData.Rows.Add(cells); }
                continue;
            }
            else if (inTable) { sections.Add(new ContentSection { Type = ContentType.Table, TableData = tableData }); inTable = false; tableData = new TableData(); }

            if (line.StartsWith("# ")) sections.Add(new ContentSection { Type = ContentType.H1, Text = line.Substring(2).Trim() });
            else if (line.StartsWith("## ")) sections.Add(new ContentSection { Type = ContentType.H2, Text = line.Substring(3).Trim() });
            else if (line.StartsWith("- ") || line.StartsWith("* ")) sections.Add(new ContentSection { Type = ContentType.ListItem, Text = Regex.Replace(line, @"^[-*]\s+", "").Trim() });
            else sections.Add(new ContentSection { Type = ContentType.Paragraph, Text = line });
        }
        if (inTable) sections.Add(new ContentSection { Type = ContentType.Table, TableData = tableData });
        return sections;
    }

    private enum ContentType { H1, H2, Bold, Paragraph, ListItem, Table }
    private class ContentSection { public ContentType Type { get; set; } public string Text { get; set; } = ""; public TableData? TableData { get; set; } }
    private class TableData { public List<string> Headers { get; set; } = new(); public List<List<string>> Rows { get; set; } = new(); }

    private static bool ContainsArabic(string text) => !string.IsNullOrEmpty(text) && text.Any(c => c >= '\u0600' && c <= '\u06FF');
}