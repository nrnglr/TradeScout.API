namespace TradeScout.API.Models.Payment;

/// <summary>
/// FGSTrade paket modeli
/// </summary>
public class FgsTradePackage
{
    public string ProductCode    { get; set; } = string.Empty;
    public string Alias          { get; set; } = string.Empty;
    public string Name           { get; set; } = string.Empty;
    public string NameTr         { get; set; } = string.Empty;
    public decimal PriceUsd      { get; set; }
    public decimal PriceTry      { get; set; }
    public int Credits           { get; set; }  // Eklenecek kredi (üyelikte 0, kredi paketinde dolu)
    public int DurationDays      { get; set; }  // Üyelik süresi (kredi paketinde 0)
    public int MaxInstallment    { get; set; }  // Aylık=1, Yıllık=12
    public bool IsYearly         { get; set; }  // Yıllık paket mi?
    public bool IsCredit         { get; set; }  // Kredi paketi mi?
    public string Description    { get; set; } = string.Empty;
}