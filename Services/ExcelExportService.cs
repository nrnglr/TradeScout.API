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
        worksheet.Cell(1, 1).Value = "İşletme Adı";
        worksheet.Cell(1, 2).Value = "Adres";
        worksheet.Cell(1, 3).Value = "Telefon";
        worksheet.Cell(1, 4).Value = "Website";
        worksheet.Cell(1, 5).Value = "Puan";
        worksheet.Cell(1, 6).Value = "Yorum Sayısı";
        worksheet.Cell(1, 7).Value = "Çalışma Saatleri";
        worksheet.Cell(1, 8).Value = "Kategori";
        worksheet.Cell(1, 9).Value = "Şehir";
        worksheet.Cell(1, 10).Value = "Ülke";
        worksheet.Cell(1, 11).Value = "Google Maps URL";

        // Header'ları bold yap ve arka plan rengi ekle
        var headerRange = worksheet.Range(1, 1, 1, 11);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Verileri ekle
        int row = 2;
        foreach (var business in businesses)
        {
            worksheet.Cell(row, 1).Value = business.BusinessName;
            worksheet.Cell(row, 2).Value = business.Address ?? "";
            worksheet.Cell(row, 3).Value = business.Phone ?? "";
            worksheet.Cell(row, 4).Value = business.Website ?? "";
            worksheet.Cell(row, 5).Value = business.Rating?.ToString() ?? "";
            worksheet.Cell(row, 6).Value = business.ReviewCount?.ToString() ?? "";
            worksheet.Cell(row, 7).Value = business.WorkingHours ?? "";
            worksheet.Cell(row, 8).Value = business.Category ?? "";
            worksheet.Cell(row, 9).Value = business.City ?? "";
            worksheet.Cell(row, 10).Value = business.Country ?? "";
            worksheet.Cell(row, 11).Value = business.GoogleMapsUrl ?? "";

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
