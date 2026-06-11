using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Drawing;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace TradeScout.API.Services;

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
        LoadFonts();
    }

    private void LoadFonts()
    {
        try
        {
            string regPath = "/usr/share/fonts/Amiri-Regular.ttf";
            string boldPath = "/usr/share/fonts/opentype/fonts-hosny-amiri/Amiri-Bold.ttf";

            if (File.Exists(regPath))
            {
                using var stream = File.OpenRead(regPath);
                FontManager.RegisterFont(stream);
                _logger.LogInformation("✅ Amiri Regular (Sistem Yolu) başarıyla yüklendi.");
            }
            else
            {
                _logger.LogWarning("⚠️ Amiri Regular bulunamadı: {Path}. Varsayılan font kullanılacak.", regPath);
            }

            if (File.Exists(boldPath))
            {
                using var stream = File.OpenRead(boldPath);
                FontManager.RegisterFont(stream);
                _logger.LogInformation("✅ Amiri Bold (Sistem Yolu) başarıyla yüklendi.");
            }
            else
            {
                _logger.LogWarning("⚠️ Amiri Bold bulunamadı: {Path}. Varsayılan font kullanılacak.", boldPath);
            }
        }
        catch (Exception ex)
        {
            // Font yüklenemese bile PDF üretimi devam etsin, process çökmesin
            _logger.LogError(ex, "⚠️ Font yükleme hatası. PDF varsayılan fontla üretilecek.");
        }
    }

    public byte[] GenerateAnalysisPdf(string reportContent, string productName, string targetCountry)
    {
        _logger.LogInformation("📄 PDF oluşturuluyor: {Product} - {Country}", productName, targetCountry);

        // Null guard — boş içerikle QuestPDF patlar
        reportContent ??= string.Empty;
        productName ??= "Ürün";
        targetCountry ??= "Hedef Ülke";

        try
        {
            var isArabic = ContainsArabic(reportContent);
            var sections = ParseMarkdownContent(reportContent);

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Size(PageSizes.A4);

                    page.DefaultTextStyle(x => x
                        .FontSize(10)
                        .FontFamily("Amiri"));

                    if (isArabic)
                        page.ContentFromRightToLeft();

                    page.Header().Column(headerCol =>
                    {
                        headerCol.Item().Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("FGS TRADE").FontSize(24).Bold().FontColor(Colors.Blue.Darken2);
                                col.Item().Text("Global Ticari İstihbarat Platformu").FontSize(10).FontColor(Colors.Grey.Darken1);
                            });
                            row.ConstantItem(120).AlignRight()
                                .Text(DateTime.Now.ToString("dd MMMM yyyy"))
                                .FontSize(10);
                        });
                        headerCol.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Blue.Darken2);
                    });

                    page.Content().PaddingVertical(15).Column(col =>
                    {
                        col.Item().PaddingBottom(10)
                            .Text($"{productName} - {targetCountry} Pazar Analizi")
                            .FontSize(18).Bold().FontColor(Colors.Blue.Darken3);

                        foreach (var section in sections)
                        {
                            try
                            {
                                RenderSection(col, section);
                            }
                            catch (Exception sectionEx)
                            {
                                // Tek bir bölüm patlarsa tüm PDF'i çökerme, o bölümü atla
                                _logger.LogWarning(sectionEx,
                                    "⚠️ Bölüm render edilemedi, atlanıyor. Type={Type}, Text={Text}",
                                    section.Type, section.Text?.Length > 50 ? section.Text[..50] : section.Text);
                            }
                        }
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("© 2026 FGS Trade - Sayfa ");
                        text.CurrentPageNumber();
                    });
                });
            }).GeneratePdf();

            _logger.LogInformation("✅ PDF başarıyla oluşturuldu: {Size} bytes", pdfBytes.Length);
            return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ PDF oluşturma hatası: {Product} - {Country}", productName, targetCountry);

            // ÖNEMLİ: throw yerine fallback PDF dön — process çökmesin
            return GenerateFallbackPdf(productName, targetCountry, ex.Message);
        }
    }

    /// <summary>
    /// Asıl PDF üretilemezse en basit hata PDF'ini döner.
    /// Process hiçbir zaman çökmez.
    /// </summary>
    private byte[] GenerateFallbackPdf(string productName, string targetCountry, string errorMessage)
    {
        try
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Size(PageSizes.A4);
                    page.Content().Column(col =>
                    {
                        col.Item().Text("FGS TRADE - Rapor").FontSize(20).Bold();
                        col.Item().PaddingTop(10).Text($"{productName} - {targetCountry}").FontSize(14);
                        col.Item().PaddingTop(20).Text("Rapor şu an oluşturulamadı. Lütfen daha sonra tekrar deneyin.")
                            .FontSize(11).FontColor(Colors.Red.Medium);
                    });
                });
            }).GeneratePdf();
        }
        catch
        {
            // Son çare — tamamen boş bir byte dizisi dön, yine de exception fırlatma
            _logger.LogCritical("❌ Fallback PDF de oluşturulamadı!");
            return Array.Empty<byte>();
        }
    }

    private void RenderSection(ColumnDescriptor col, ContentSection section)
    {
        switch (section.Type)
        {
            case ContentType.H1:
                col.Item().PaddingTop(15)
                    .Text(section.Text ?? "")
                    .FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                break;

            case ContentType.H2:
                col.Item().PaddingTop(12)
                    .Text(section.Text ?? "")
                    .FontSize(14).SemiBold().FontColor(Colors.Blue.Darken1);
                break;

            case ContentType.ListItem:
                col.Item().PaddingLeft(15)
                    .Text($"• {section.Text ?? ""}")
                    .FontSize(10);
                break;

            case ContentType.Table:
                if (section.TableData != null
                    && section.TableData.Headers.Count > 0)
                {
                    col.Item().PaddingVertical(5).Table(table =>
                    {
                        var headerCount = section.TableData.Headers.Count;

                        table.ColumnsDefinition(columns =>
                        {
                            for (int i = 0; i < headerCount; i++)
                                columns.RelativeColumn();
                        });

                        // Header row
                        foreach (var header in section.TableData.Headers)
                            table.Cell()
                                .Background(Colors.Blue.Darken2)
                                .Padding(5)
                                .Text(header ?? "")
                                .FontSize(9).Bold().FontColor(Colors.White);

                        // Data rows — hücre sayısı header sayısıyla eşleşmiyorsa doldur/kes
                        foreach (var row in section.TableData.Rows)
                        {
                            var cells = row.Count >= headerCount
                                ? row.Take(headerCount).ToList()
                                : row.Concat(Enumerable.Repeat("", headerCount - row.Count)).ToList();

                            foreach (var cell in cells)
                                table.Cell()
                                    .BorderBottom(0.5f)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(4)
                                    .Text(cell ?? "")
                                    .FontSize(9);
                        }
                    });
                }
                break;

            case ContentType.Paragraph:
                if (!string.IsNullOrWhiteSpace(section.Text))
                    col.Item().PaddingVertical(3)
                        .Text(section.Text)
                        .FontSize(10).LineHeight(1.4f);
                break;
        }
    }

    private List<ContentSection> ParseMarkdownContent(string markdown)
    {
        var sections = new List<ContentSection>();
        if (string.IsNullOrEmpty(markdown)) return sections;

        var lines = markdown.Split('\n');
        var inTable = false;
        var tableData = new TableData();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.StartsWith("|"))
            {
                // Ayraç satırını atla (|---|---|)
                if (line.Contains("---")) continue;

                var cells = line.Split('|', StringSplitOptions.RemoveEmptyEntries)
                                .Select(c => c.Trim())
                                .ToList();

                if (!inTable)
                {
                    inTable = true;
                    tableData = new TableData { Headers = cells };
                }
                else
                {
                    tableData.Rows.Add(cells);
                }
                continue;
            }
            else if (inTable)
            {
                sections.Add(new ContentSection { Type = ContentType.Table, TableData = tableData });
                inTable = false;
                tableData = new TableData();
            }

            if (line.StartsWith("# "))
                sections.Add(new ContentSection { Type = ContentType.H1, Text = line[2..].Trim() });
            else if (line.StartsWith("## "))
                sections.Add(new ContentSection { Type = ContentType.H2, Text = line[3..].Trim() });
            else if (line.StartsWith("- ") || line.StartsWith("* "))
                sections.Add(new ContentSection
                {
                    Type = ContentType.ListItem,
                    Text = Regex.Replace(line, @"^[-*]\s+", "").Trim()
                });
            else
                sections.Add(new ContentSection { Type = ContentType.Paragraph, Text = line });
        }

        // Dosya tablo ile bitiyorsa flush et
        if (inTable)
            sections.Add(new ContentSection { Type = ContentType.Table, TableData = tableData });

        return sections;
    }

    private enum ContentType { H1, H2, Bold, Paragraph, ListItem, Table }

    private class ContentSection
    {
        public ContentType Type { get; set; }
        public string Text { get; set; } = "";
        public TableData? TableData { get; set; }
    }

    private class TableData
    {
        public List<string> Headers { get; set; } = new();
        public List<List<string>> Rows { get; set; } = new();
    }

    private static bool ContainsArabic(string text) =>
        !string.IsNullOrEmpty(text) && text.Any(c => c >= '\u0600' && c <= '\u06FF');
}