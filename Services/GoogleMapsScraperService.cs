using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using TradeScout.API.DTOs;
using Bogus;

namespace TradeScout.API.Services;

/// <summary>
/// Google Maps scraper service with ban protection
/// </summary>
public interface IGoogleMapsScraperService
{
    Task<List<BusinessDto>> ScrapeBusinessesAsync(string category, string city, string? country, string language, int maxResults, CancellationToken cancellationToken = default);
}

public class GoogleMapsScraperService : IGoogleMapsScraperService
{
    private readonly ILogger<GoogleMapsScraperService> _logger;
    private readonly ProxyManager _proxyManager;
    private readonly Random _random = new();
    private readonly Faker _faker = new();

    // Ban koruması için User-Agent listesi
    private readonly string[] _userAgents = new[]
    {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.1 Safari/605.1.15",
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
    };

    public GoogleMapsScraperService(ILogger<GoogleMapsScraperService> logger, ProxyManager proxyManager)
    {
        _logger = logger;
        _proxyManager = proxyManager;
    }

    public async Task<List<BusinessDto>> ScrapeBusinessesAsync(
        string category, 
        string city, 
        string? country, 
        string language, 
        int maxResults, 
        CancellationToken cancellationToken = default)
    {
        var businesses = new List<BusinessDto>();
        IWebDriver? driver = null;

        try
        {
            _logger.LogInformation("🚀 Scraping başlatılıyor: {Category} - {City}", category, city);

            // Proxy seç
            var selectedProxy = _proxyManager.GetNextProxy();
            
            // Selenium WebDriver'ı konfigüre et
            driver = ConfigureWebDriver(selectedProxy);

            // Google Maps URL'ini oluştur
            var searchQuery = BuildSearchQuery(category, city, country, language);
            var googleMapsUrl = $"https://www.google.com/maps/search/{Uri.EscapeDataString(searchQuery)}";

            _logger.LogInformation("📍 URL: {Url}", googleMapsUrl);

            // Sayfayı aç
            driver.Navigate().GoToUrl(googleMapsUrl);

            // İlk yüklenme beklemesi (insan gibi davran)
            await HumanLikeDelay(3000, 5000, cancellationToken);

            // Cookie popup'ı kapat (varsa)
            await CloseCookiePopup(driver, cancellationToken);

            // Sonuç listesini bekle
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            wait.Until(d => d.FindElements(By.CssSelector("div[role='article']")).Count > 0);

            _logger.LogInformation("✅ Sonuç listesi yüklendi");

            int processedCount = 0;
            int scrollAttempts = 0;
            const int maxScrollAttempts = 50; // Daha fazla scroll denemesi (100 firma için yeterli)
            var processedUrls = new HashSet<string>(); // Duplicate kontrolü
            int lastProcessedIndex = 0; // Hangi index'ten başlayacağımızı takip et

            _logger.LogInformation("🎯 Hedef: {MaxResults} işletme bulunacak", maxResults);

            while (processedCount < maxResults && scrollAttempts < maxScrollAttempts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Mevcut işletmeleri al
                var businessElements = driver.FindElements(By.CssSelector("div[role='article']"));
                _logger.LogInformation("📊 Sayfada görünen işletme sayısı: {Count}, İşlenen: {Processed}/{Target}", 
                    businessElements.Count, processedCount, maxResults);

                // Yeni işletmeleri işle (daha önce işlenenleri atla)
                for (int i = lastProcessedIndex; i < businessElements.Count && processedCount < maxResults; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var element = businessElements[i];

                        // İşletmeye tıkla (detayları görmek için)
                        await ScrollToElement(driver, element, cancellationToken);
                        await HumanLikeDelay(500, 1000, cancellationToken);

                        element.Click();
                        _logger.LogInformation("👆 İşletme tıklandı: {Current}/{Target}", processedCount + 1, maxResults);

                        // Detayların yüklenmesini bekle (insan gibi)
                        await HumanLikeDelay(3000, 7000, cancellationToken);

                        // İşletme verilerini çek
                        var business = await ExtractBusinessData(driver, category, city, country, cancellationToken);

                        // Duplicate kontrolü (aynı işletmeyi tekrar ekleme)
                        if (business != null)
                        {
                            businesses.Add(business);
                            processedCount++;
                            
                            _logger.LogInformation("✅ İşletme eklendi: {Name} ({Current}/{Target})", 
                                business.BusinessName, processedCount, maxResults);

                            // TAM SAYIYA ULAŞILDIYSA DUR!
                            if (processedCount >= maxResults)
                            {
                                _logger.LogInformation("🎉 Hedef sayıya ulaşıldı! {Count} işletme bulundu.", processedCount);
                                break;
                            }
                        }
                        else
                        {
                            _logger.LogInformation("⏭️  İşletme verisi çekilemedi");
                        }

                        // Her 20 işletmeden sonra 60 saniye dinlen (BAN KORUMASIN)
                        if (processedCount % 20 == 0 && processedCount > 0 && processedCount < maxResults)
                        {
                            _logger.LogWarning("⏸️  Ban koruması: 60 saniye bekleniyor... ({Current}/{Target})", 
                                processedCount, maxResults);
                            await Task.Delay(60000, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ İşletme verisi çekilemedi, atlanıyor");
                        continue;
                    }
                }

                // Son işlenen index'i güncelle
                lastProcessedIndex = businessElements.Count;

                // TAM SAYIYA ULAŞILDIYSA DÖNGÜDEN ÇIK
                if (processedCount >= maxResults)
                {
                    _logger.LogInformation("✅ İstenen {MaxResults} işletme bulundu, scraping durduruluyor.", maxResults);
                    break;
                }

                // Daha fazla sonuç için scroll yap
                _logger.LogInformation("📜 Daha fazla sonuç için scroll yapılıyor... ({Current}/{Target})", 
                    processedCount, maxResults);
                
                var previousCount = businessElements.Count;
                await ScrollResultsList(driver, cancellationToken);
                scrollAttempts++;
                await HumanLikeDelay(3000, 5000, cancellationToken); // Daha uzun bekleme

                // Yeni sonuçların yüklenmesini bekle
                await Task.Delay(3000, cancellationToken);

                // Eğer yeni sonuç gelmiyorsa (son sayfaya gelindi) döngüden çık
                var newBusinessElements = driver.FindElements(By.CssSelector("div[role='article']"));
                if (newBusinessElements.Count == previousCount)
                {
                    _logger.LogWarning("⚠️ Daha fazla sonuç bulunamadı. Son sayfaya ulaşıldı.");
                    break;
                }
            }

            if (processedCount < maxResults)
            {
                _logger.LogWarning("⚠️ Hedef sayıya ulaşılamadı. Bulunan: {Found}, Hedef: {Target}", 
                    processedCount, maxResults);
            }
            else
            {
                _logger.LogInformation("🎉 Scraping başarıyla tamamlandı! Tam {Count} işletme bulundu.", businesses.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Scraping hatası");
            
            // Proxy kullanıyorsak ve hata varsa rapor et
            var currentProxy = _proxyManager.GetNextProxy();
            if (currentProxy != null)
            {
                _proxyManager.ReportProxyFailure(currentProxy);
            }
            
            throw;
        }
        finally
        {
            driver?.Quit();
            driver?.Dispose();
        }

        return businesses;
    }

    private IWebDriver ConfigureWebDriver(Models.ProxyConfig? proxyConfig = null)
    {
        var options = new ChromeOptions();

        // Headless mod (arka planda çalışır)
        options.AddArgument("--headless=new");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-blink-features=AutomationControlled");
        options.AddExcludedArgument("enable-automation");
        options.AddAdditionalOption("useAutomationExtension", false);

        // Rastgele User-Agent
        var userAgent = _userAgents[_random.Next(_userAgents.Length)];
        options.AddArgument($"user-agent={userAgent}");

        // Proxy yapılandırması
        if (proxyConfig != null)
        {
            options = _proxyManager.ConfigureProxyForDriver(options, proxyConfig);
        }

        // WebDriver oluştur
        var driver = new ChromeDriver(options);
        driver.Manage().Window.Size = new System.Drawing.Size(1920, 1080);
        driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);

        // WebDriver properties'ini gizle (bot detection'dan kaçın)
        driver.ExecuteScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");

        return driver;
    }

    private string BuildSearchQuery(string category, string city, string? country, string language)
    {
        // Google Maps için daha spesifik sorgu formatı
        // Şehir adını önce yazıp sonra kategori ekleyerek daha iyi sonuç alıyoruz
        var query = $"{city}";
        
        if (!string.IsNullOrEmpty(country))
        {
            query += $", {country}";
        }
        
        query += $" {category}";
        
        _logger.LogInformation("🔍 Arama sorgusu: {Query}", query);
        return query;
    }

    private async Task CloseCookiePopup(IWebDriver driver, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(1000, cancellationToken);
            var acceptButtons = driver.FindElements(By.XPath("//button[contains(., 'Accept') or contains(., 'Kabul') or contains(., 'Tamam')]"));
            if (acceptButtons.Any())
            {
                acceptButtons.First().Click();
                _logger.LogInformation("🍪 Cookie popup kapatıldı");
                await Task.Delay(500, cancellationToken);
            }
        }
        catch
        {
            // Cookie popup yoksa devam et
        }
    }

    private async Task<BusinessDto?> ExtractBusinessData(
        IWebDriver driver, 
        string category, 
        string city, 
        string? country, 
        CancellationToken cancellationToken)
    {
        try
        {
            var business = new BusinessDto
            {
                Category = category,
                City = city,
                Country = country
            };

            // İşletme adı
            try
            {
                var nameElement = driver.FindElement(By.CssSelector("h1.DUwDvf"));
                business.BusinessName = nameElement.Text;
            }
            catch { }

            // Adres
            try
            {
                var addressElement = driver.FindElement(By.CssSelector("button[data-item-id*='address']"));
                business.Address = addressElement.GetAttribute("aria-label")?.Replace("Address: ", "").Replace("Adres: ", "");
            }
            catch { }

            // Telefon
            try
            {
                var phoneElements = driver.FindElements(By.CssSelector("button[data-item-id*='phone']"));
                if (phoneElements.Any())
                {
                    business.Phone = phoneElements.First().GetAttribute("aria-label")?.Replace("Phone: ", "").Replace("Telefon: ", "");
                }
            }
            catch { }

            // Website
            try
            {
                var websiteElements = driver.FindElements(By.CssSelector("a[data-item-id*='authority']"));
                if (websiteElements.Any())
                {
                    business.Website = websiteElements.First().GetAttribute("href");
                }
            }
            catch { }

            // Rating ve Review Count
            try
            {
                var ratingElement = driver.FindElement(By.CssSelector("div.F7nice span[role='img']"));
                var ratingText = ratingElement.GetAttribute("aria-label");
                
                // "4.5 stars, 120 reviews" formatında parse et
                var parts = ratingText.Split(',');
                if (parts.Length >= 1)
                {
                    var ratingStr = parts[0].Replace(" stars", "").Replace(" yıldız", "").Trim();
                    if (decimal.TryParse(ratingStr, out var rating))
                    {
                        business.Rating = rating;
                    }
                }
                
                if (parts.Length >= 2)
                {
                    var reviewStr = parts[1].Replace(" reviews", "").Replace(" yorum", "").Trim();
                    if (int.TryParse(reviewStr, out var reviewCount))
                    {
                        business.ReviewCount = reviewCount;
                    }
                }
            }
            catch { }

            // Çalışma saatleri
            try
            {
                var hoursElements = driver.FindElements(By.CssSelector("button[data-item-id*='oh']"));
                if (hoursElements.Any())
                {
                    business.WorkingHours = hoursElements.First().GetAttribute("aria-label");
                }
            }
            catch { }

            // Google Maps URL

            // En az isim olmalı
            if (string.IsNullOrEmpty(business.BusinessName))
            {
                return null;
            }

            return business;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ İşletme verisi çıkarılamadı");
            return null;
        }
    }

    private async Task ScrollResultsList(IWebDriver driver, CancellationToken cancellationToken)
    {
        try
        {
            // Sonuç listesi container'ını bul
            var scrollableDiv = driver.FindElement(By.CssSelector("div[role='feed']"));
            
            // Yavaş ve kesik kesik scroll (insan gibi)
            var js = (IJavaScriptExecutor)driver;
            var scrollHeight = (long)js.ExecuteScript("return arguments[0].scrollHeight", scrollableDiv);
            var currentScroll = 0L;
            var scrollStep = 300; // Her seferinde 300px scroll

            while (currentScroll < scrollHeight)
            {
                cancellationToken.ThrowIfCancellationRequested();

                js.ExecuteScript($"arguments[0].scrollTop += {scrollStep}", scrollableDiv);
                currentScroll += scrollStep;

                // Her scroll'da kısa bekle (insan gibi)
                await Task.Delay(_random.Next(200, 500), cancellationToken);
            }

            _logger.LogInformation("📜 Sayfa kaydırıldı");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Scroll yapılamadı");
        }
    }

    private async Task ScrollToElement(IWebDriver driver, IWebElement element, CancellationToken cancellationToken)
    {
        try
        {
            var js = (IJavaScriptExecutor)driver;
            js.ExecuteScript("arguments[0].scrollIntoView({behavior: 'smooth', block: 'center'});", element);
            await Task.Delay(_random.Next(300, 700), cancellationToken);
        }
        catch { }
    }

    private async Task HumanLikeDelay(int minMs, int maxMs, CancellationToken cancellationToken)
    {
        var delay = _random.Next(minMs, maxMs);
        await Task.Delay(delay, cancellationToken);
    }
}
