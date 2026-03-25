using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Drawing; 
using System.IO; 
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

        // 🚀 DİNAMİK FONT YÜKLEME (Local ve Sunucu Uyumlu)
        try
        {
            var rootPath = AppDomain.CurrentDomain.BaseDirectory; 
            
            // Regular Font Yükleme
            var regularFontPath = Path.Combine(rootPath, "Fonts", "Amiri-Regular.ttf");
            if (File.Exists(regularFontPath))
            {
                using var stream = File.OpenRead(regularFontPath);
                FontManager.RegisterFont(stream);
                _logger.LogInformation("✅ Amiri Regular fontu başarıyla yüklendi: {Path}", regularFontPath);
            }
            else
            {
                _logger.LogError("❌ Amiri Regular fontu bulunamadı: {Path}", regularFontPath);
            }

            // Bold Font Yükleme
            var boldFontPath = Path.Combine(rootPath, "Fonts", "Amiri-Bold.ttf");
            if (File.Exists(boldFontPath))
            {
                using var stream = File.OpenRead(boldFontPath);
                FontManager.RegisterFont(stream);
                _logger.LogInformation("✅ Amiri Bold fontu başarıyla yüklendi: {Path}", boldFontPath);
            }
            else
            {
                _logger.LogError("❌ Amiri Bold fontu bulunamadı: {Path}", boldFontPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Fontlar yüklenirken beklenmedik bir hata oluştu.");
        }
    }

    public byte[] GenerateAnalysisPdf(string reportContent, string productName, string targetCountry)
    {
        _logger.LogInformation("📄 PDF oluşturuluyor: {Product} - {Country}", productName, targetCountry);

        try
        {
            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Size(PageSizes.A4);
                    
                    var isArabic = ContainsArabic(reportContent); //

                    // FONT VE YÖN AYARI
                    page.DefaultTextStyle(x => x
                        .FontSize(10)
                        .FontFamily(isArabic ? "Amiri" : "Arial")
                        .Fallback(f => f.FontFamily("Arial")));

                    if (isArabic)
                    {
                        page.ContentFromRightToLeft(); //
                    }

                    // Header
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

                            row.ConstantItem(120).AlignRight().Column(col =>
                            {
                                col.Item().Text(DateTime.Now.ToString("dd MMMM yyyy"))
                                    .FontSize(10)
                                    .FontColor(Colors.Grey.Darken2);

                                col.Item().Text("Pazar Analiz Raporu")
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Medium);
                            });
                        });

                        headerCol.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Blue.Darken2);
                    });

                    // Content
                    page.Content().PaddingVertical(15).Column(col =>
                    {
                        col.Item().PaddingBottom(10).Text($"{productName} - {targetCountry} Pazar Analizi")
                            .FontSize(18)
                            .Bold()
                            .FontColor(Colors.Blue.Darken3);

                        var sections = ParseMarkdownContent(reportContent); //
                        foreach (var section in sections)
                        {
                            RenderSection(col, section);
                        }
                    });

                    // Footer
                    page.Footer().Column(footerCol =>
                    {
                        footerCol.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        footerCol.Item().PaddingTop(5).Row(row =>
                        {
                            row.RelativeItem().Text("© 2026 FGS Trade - Tüm hakları saklıdır.")
                                .FontSize(8)
                                .FontColor(Colors.Grey.Medium);

                            row.RelativeItem().AlignCenter().Text(text =>
                            {
                                text.Span("Sayfa ").FontSize(8).FontColor(Colors.Grey.Medium);
                                text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                                text.Span(" / ").FontSize(8).FontColor(Colors.Grey.Medium);
                                text.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                            });

                            row.RelativeItem().AlignRight().Text("www.fgstrade.com")
                                .FontSize(8)
                                .FontColor(Colors.Blue.Medium);
                        });
                    });
                });
            }).GeneratePdf();

            _logger.LogInformation("✅ PDF başarıyla oluşturuldu: {Size} bytes", pdfBytes.Length);
            return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ PDF oluşturma hatası");
            throw;
        }
    }

    private void RenderSection(ColumnDescriptor col, ContentSection section)
    {
        switch (section.Type) //
        {
            case ContentType.H1:
                col.Item().PaddingTop(15).PaddingBottom(5).Text(section.Text).FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                break;
            case ContentType.H2:
                col.Item().PaddingTop(12).PaddingBottom(4).Text(section.Text).FontSize(14).SemiBold().FontColor(Colors.Blue.Darken1);
                break;
            case ContentType.H3:
                col.Item().PaddingTop(8).PaddingBottom(3).Text(section.Text).FontSize(12).SemiBold().FontColor(Colors.Grey.Darken3);
                break;
            case ContentType.Bold:
                col.Item().PaddingVertical(2).Text(section.Text).FontSize(10).Bold();
                break;
            case ContentType.ListItem:
                col.Item().PaddingLeft(15).PaddingVertical(1).Text($"• {section.Text}").FontSize(10);
                break;
            case ContentType.Table:
                if (section.TableData != null && section.TableData.Headers.Count > 0)
                {
                    col.Item().PaddingVertical(5).Table(table =>
                    {
                        table.ColumnsDefinition(columns => { foreach (var _ in section.TableData.Headers) columns.RelativeColumn(); });
                        foreach (var header in section.TableData.Headers)
                        {
                            table.Cell().Background(Colors.Blue.Darken2).Padding(5).Text(header).FontSize(9).Bold().FontColor(Colors.White);
                        }
                        var isAlternate = false;
                        foreach (var row in section.TableData.Rows)
                        {
                            var bgColor = isAlternate ? Colors.Grey.Lighten4 : Colors.White;
                            foreach (var cell in row)
                            {
                                table.Cell().Background(bgColor).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(cell).FontSize(9);
                            }
                            isAlternate = !isAlternate;
                        }
                    });
                }
                break;
            case ContentType.Paragraph:
                if (!string.IsNullOrWhiteSpace(section.Text)) col.Item().PaddingVertical(3).Text(section.Text).FontSize(10).LineHeight(1.4f);
                break;
            case ContentType.JsonBlock:
                break;
        }
    }

    private List<ContentSection> ParseMarkdownContent(string markdown)
    {
        var sections = new List<ContentSection>(); //
        var lines = markdown.Split('\n');
        var inJsonBlock = false;
        var inTable = false;
        var tableData = new TableData();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("```json") || (line == "```" && inJsonBlock))
            {
                inJsonBlock = line.StartsWith("```json");
                continue;
            }
            if (inJsonBlock) continue;

            if (line.StartsWith("|") && line.EndsWith("|"))
            {
                if (line.Contains("---") || line.Contains(":-")) continue;
                var cells = line.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).ToArray();
                if (!inTable) { inTable = true; tableData = new TableData { Headers = cells.ToList() }; }
                else { tableData.Rows.Add(cells.ToList()); }
                continue;
            }
            else if (inTable)
            {
                sections.Add(new ContentSection { Type = ContentType.Table, TableData = tableData });
                inTable = false;
                tableData = new TableData();
            }

            if (line.StartsWith("# ")) { sections.Add(new ContentSection { Type = ContentType.H1, Text = line.Substring(2).Trim() }); continue; }
            if (line.StartsWith("## ")) { sections.Add(new ContentSection { Type = ContentType.H2, Text = line.Substring(3).Trim() }); continue; }
            if (line.StartsWith("### ")) { sections.Add(new ContentSection { Type = ContentType.H3, Text = line.Substring(4).Trim() }); continue; }
            if (line.StartsWith("**") && line.EndsWith("**")) { sections.Add(new ContentSection { Type = ContentType.Bold, Text = line.Trim('*').Trim() }); continue; }
            if (line.StartsWith("- ") || line.StartsWith("* ") || Regex.IsMatch(line, @"^\d+\.\s"))
            {
                var listText = Regex.Replace(line, @"^[-*]\s+|^\d+\.\s+", "").Trim();
                listText = Regex.Replace(listText, @"\*\*([^*]+)\*\*", "$1");
                sections.Add(new ContentSection { Type = ContentType.ListItem, Text = listText });
                continue;
            }

            if (!string.IsNullOrWhiteSpace(line))
            {
                var cleanText = Regex.Replace(line, @"\*\*([^*]+)\*\*", "$1");
                sections.Add(new ContentSection { Type = ContentType.Paragraph, Text = cleanText });
            }
        }
        if (inTable && tableData.Headers.Count > 0) sections.Add(new ContentSection { Type = ContentType.Table, TableData = tableData });
        return sections;
    }

    private enum ContentType { H1, H2, H3, Bold, Paragraph, ListItem, Table, JsonBlock }
    private class ContentSection { public ContentType Type { get; set; } public string Text { get; set; } = ""; public TableData? TableData { get; set; } }
    private class TableData { public List<string> Headers { get; set; } = new(); public List<List<string>> Rows { get; set; } = new(); }

    private static bool ContainsArabic(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.Any(c => c >= '\u0600' && c <= '\u06FF');
    }
}