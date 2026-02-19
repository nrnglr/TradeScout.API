using ClosedXML.Excel;
using TradeScout.API.DTOs;

namespace TradeScout.API.Services;

/// <summary>
/// Excel export service for business data
/// </summary>
public interface IExcelExportService
{
    byte[] ExportToExcel(List<BusinessDto> businesses, string category, string city);
}

public class ExcelExportService : IExcelExportService
{
    public byte[] ExportToExcel(List<BusinessDto> businesses, string category, string city)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add($"{category} - {city}");

        // Header'ları oluştur
        worksheet.Cell(1, 1).Value = "Firma Adı";
        worksheet.Cell(1, 2).Value = "Adres";
        worksheet.Cell(1, 3).Value = "Website";
        worksheet.Cell(1, 4).Value = "E-mail";
        worksheet.Cell(1, 5).Value = "Telefon";
        worksheet.Cell(1, 6).Value = "Mobil";
        worksheet.Cell(1, 7).Value = "Şehir";
        worksheet.Cell(1, 8).Value = "Ülke";
        worksheet.Cell(1, 9).Value = "Sosyal Medya";
        worksheet.Cell(1, 10).Value = "Notlar";

        // Header'ları bold yap ve arka plan rengi ekle
        var headerRange = worksheet.Range(1, 1, 1, 10);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Verileri ekle
        int row = 2;
        foreach (var business in businesses)
        {
            worksheet.Cell(row, 1).Value = business.BusinessName;
            worksheet.Cell(row, 2).Value = business.Address ?? "";
            worksheet.Cell(row, 3).Value = business.Website ?? "";
            worksheet.Cell(row, 4).Value = business.Email ?? "";
            worksheet.Cell(row, 5).Value = business.Phone ?? "";
            worksheet.Cell(row, 6).Value = business.Mobile ?? "";
            worksheet.Cell(row, 7).Value = business.City ?? "";
            worksheet.Cell(row, 8).Value = business.Country ?? "";
            worksheet.Cell(row, 9).Value = business.SocialMedia ?? "";
            worksheet.Cell(row, 10).Value = business.Comments ?? "";

            row++;
        }

        // Sütun genişliklerini otomatik ayarla
        worksheet.Columns().AdjustToContents();

        // Excel dosyasını byte array olarak dön
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
