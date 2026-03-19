using TradeScout.API.Data;
using TradeScout.API.Models;

namespace TradeScout.API.Scripts;

/// <summary>
/// 250 adet FGS10_ indirim kodu oluşturan script
/// </summary>
public static class DiscountCodeSeeder
{
    /// <summary>
    /// Veritabanına 250 adet benzersiz indirim kodu ekler
    /// </summary>
    public static async Task SeedDiscountCodesAsync(ApplicationDbContext context)
    {
        // Zaten kod varsa ekleme
        if (context.DiscountCodes.Any())
        {
            Console.WriteLine("⚠️ İndirim kodları zaten mevcut, seed atlanıyor.");
            return;
        }

        Console.WriteLine("🎫 250 adet FGS10_ indirim kodu oluşturuluyor...");

        var codes = new List<DiscountCode>();
        var existingCodes = new HashSet<string>();

        for (int i = 0; i < 250; i++)
        {
            string code;
            
            // Benzersiz kod üret
            do
            {
                code = GenerateUniqueCode();
            } 
            while (existingCodes.Contains(code));

            existingCodes.Add(code);

            codes.Add(new DiscountCode
            {
                Code = code,
                DiscountPercentage = 10,  // %10 indirim
                MaxUses = 1000,           // Maksimum 1000 kullanım
                CurrentUses = 0,          // Henüz kullanılmadı
                IsActive = true,          // Aktif
                CreatedAt = DateTime.UtcNow,
                Description = "FGSTrade %10 İndirim Kodu (1000 kullanımlık)"
            });

            // Her 50 kodda bir ilerleme göster
            if ((i + 1) % 50 == 0)
            {
                Console.WriteLine($"   ✅ {i + 1}/250 kod oluşturuldu...");
            }
        }

        // Veritabanına ekle
        await context.DiscountCodes.AddRangeAsync(codes);
        await context.SaveChangesAsync();

        Console.WriteLine($"🎉 {codes.Count} adet indirim kodu başarıyla eklendi!");
        Console.WriteLine($"📋 Örnek kodlar:");
        
        // İlk 5 kodu göster
        foreach (var code in codes.Take(5))
        {
            Console.WriteLine($"   - {code.Code}");
        }
    }

    /// <summary>
    /// FGS10_XXXXXX formatında benzersiz kod üretir
    /// </summary>
    private static string GenerateUniqueCode()
    {
        const string prefix = "FGS10_";
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        
        var random = new Random();
        var randomPart = new char[6];

        for (int i = 0; i < 6; i++)
        {
            randomPart[i] = chars[random.Next(chars.Length)];
        }

        return prefix + new string(randomPart);
    }

    /// <summary>
    /// Tüm kodları konsola yazdır (debug için)
    /// </summary>
    public static async Task PrintAllCodesAsync(ApplicationDbContext context)
    {
        var codes = context.DiscountCodes
            .OrderBy(c => c.Code)
            .ToList();

        Console.WriteLine($"\n📋 Toplam {codes.Count} İndirim Kodu:\n");
        
        foreach (var code in codes)
        {
            Console.WriteLine($"{code.Code} | %{code.DiscountPercentage} indirim | {code.CurrentUses}/{code.MaxUses} kullanım | Aktif: {code.IsActive}");
        }
    }
}
