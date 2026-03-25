using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Drawing;
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
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var fontPath = Path.Combine(basePath, "Fonts");

            var regularFontPath = Path.Combine(fontPath, "Amiri-Regular.ttf");
            var boldFontPath = Path.Combine(fontPath, "Amiri-Bold.ttf");

            if (!File.Exists(regularFontPath))
            {
                _logger.LogError($"❌ Font bulunamadı: {regularFontPath}");
                return;
            }

            // 🔥 CRITICAL: stream değil byte kullan
            var regularFontBytes = File.ReadAllBytes(regularFontPath);
            FontManager.RegisterFont(regularFontBytes, "Amiri");

            _logger.LogInformation("✅ Amiri Regular yüklendi");

            if (File.Exists(boldFontPath))
            {
                var boldFontBytes = File.ReadAllBytes(boldFontPath);
                FontManager.RegisterFont(boldFontBytes, "Amiri");
                _logger.LogInformation("✅ Amiri Bold yüklendi");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "⚠️ Font yükleme hatası");
        }
    }

    public byte[] GenerateAnalysisPdf(string reportContent, string productName, string targetCountry)
    {
        var isArabic = ContainsArabic(reportContent);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);

                // 🔥 CRITICAL: font burada uygulanıyor
                page.DefaultTextStyle(x => x
                    .FontFamily("Amiri")
                    .FontSize(10));

                if (isArabic)
                    page.ContentFromRightToLeft();

                // HEADER
                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("FGS TRADE")
                                .FontSize(22)
                                .Bold()
                                .FontColor(Colors.Blue.Darken2);

                            col.Item().Text("Global Ticari İstihbarat Platformu")
                                .FontSize(10)
                                .FontColor(Colors.Grey.Darken1);
                        });

                        row.ConstantItem(120)
                            .AlignRight()
                            .Text(DateTime.Now.ToString("dd MMMM yyyy"))
                            .FontSize(10);
                    });

                    header.Item().PaddingTop(10)
                        .LineHorizontal(1)
                        .LineColor(Colors.Blue.Darken2);
                });

                // CONTENT
                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Item().PaddingBottom(10).Text($"{productName} - {targetCountry} Pazar Analizi")
                        .FontSize(18)
                        .Bold()
                        .FontColor(Colors.Blue.Darken3);

                    var sections = ParseMarkdownContent(reportContent);

                    foreach (var section in sections)
                        RenderSection(col, section);
                });

                // FOOTER
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("© 2026 FGS Trade - Sayfa ");
                    text.CurrentPageNumber();
                });
            });
        }).GeneratePdf();
    }

    private void RenderSection(ColumnDescriptor col, ContentSection section)
    {
        switch (section.Type)
        {
            case ContentType.H1:
                col.Item().PaddingTop(15).Text(section.Text)
                    .FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                break;

            case ContentType.H2:
                col.Item().PaddingTop(12).Text(section.Text)
                    .FontSize(14).SemiBold().FontColor(Colors.Blue.Darken1);
                break;

            case ContentType.ListItem:
                col.Item().PaddingLeft(15).Text($"• {section.Text}")
                    .FontSize(10);
                break;

            case ContentType.Paragraph:
                col.Item().PaddingVertical(3).Text(section.Text)
                    .FontSize(10)
                    .LineHeight(1.4f);
                break;

            case ContentType.Table:
                if (section.TableData != null)
                {
                    col.Item().PaddingVertical(5).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            foreach (var _ in section.TableData.Headers)
                                columns.RelativeColumn();
                        });

                        foreach (var header in section.TableData.Headers)
                        {
                            table.Cell().Background(Colors.Blue.Darken2)
                                .Padding(5)
                                .Text(header)
                                .FontSize(9)
                                .Bold()
                                .FontColor(Colors.White);
                        }

                        foreach (var row in section.TableData.Rows)
                        {
                            foreach (var cell in row)
                            {
                                table.Cell()
                                    .BorderBottom(0.5f)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(4)
                                    .Text(cell)
                                    .FontSize(9);
                            }
                        }
                    });
                }
                break;
        }
    }

    private List<ContentSection> ParseMarkdownContent(string markdown)
    {
        var sections = new List<ContentSection>();
        var lines = markdown.Split('\n');

        bool inTable = false;
        var tableData = new TableData();

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.StartsWith("|"))
            {
                if (line.Contains("---")) continue;

                var cells = line.Split('|', StringSplitOptions.RemoveEmptyEntries)
                                .Select(x => x.Trim()).ToList();

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
                sections.Add(new ContentSection
                {
                    Type = ContentType.Table,
                    TableData = tableData
                });
                inTable = false;
            }

            if (line.StartsWith("# "))
                sections.Add(new ContentSection { Type = ContentType.H1, Text = line.Substring(2) });

            else if (line.StartsWith("## "))
                sections.Add(new ContentSection { Type = ContentType.H2, Text = line.Substring(3) });

            else if (line.StartsWith("- ") || line.StartsWith("* "))
                sections.Add(new ContentSection { Type = ContentType.ListItem, Text = line.Substring(2) });

            else
                sections.Add(new ContentSection { Type = ContentType.Paragraph, Text = line });
        }

        if (inTable)
        {
            sections.Add(new ContentSection
            {
                Type = ContentType.Table,
                TableData = tableData
            });
        }

        return sections;
    }

    private static bool ContainsArabic(string text)
    {
        return !string.IsNullOrEmpty(text) &&
               text.Any(c => c >= '\u0600' && c <= '\u06FF');
    }

    private enum ContentType
    {
        H1,
        H2,
        Paragraph,
        ListItem,
        Table
    }

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
}