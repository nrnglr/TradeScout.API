using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using TradeScout.API.DTOs;
using System.Collections.Concurrent;

namespace TradeScout.API.Services;

/// <summary>
/// Parallel Google Maps scraper with multiple proxies for high-speed scraping
/// </summary>
public interface IParallelGoogleMapsScraperService
{
    Task<List<BusinessDto>> ScrapeBusinessesParallelAsync(string category, string city, string? country, string language, int maxResults, CancellationToken cancellationToken = default);
}

public class ParallelGoogleMapsScraperService : IParallelGoogleMapsScraperService
{
    private readonly ILogger<ParallelGoogleMapsScraperService> _logger;
    private readonly ProxyManager _proxyManager;
    private readonly Random _random = new();

    private readonly string[] _userAgents = new[]
    {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.1 Safari/605.1.15",
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
    };

    public ParallelGoogleMapsScraperService(ILogger<ParallelGoogleMapsScraperService> logger, ProxyManager proxyManager)
    {
        _logger = logger;
        _proxyManager = proxyManager;
    }

    public async Task<List<BusinessDto>> ScrapeBusinessesParallelAsync(
        string category,
        string city,
        string? country,
        string language,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("🚀 PARALLEL Scraping başlatılıyor: {Category} - {City}, Hedef: {MaxResults}", category, city, maxResults);

        // Tüm işletmeleri toplamak için thread-safe koleksiyon
        var allBusinesses = new ConcurrentBag<BusinessDto>();
        var processedUrls = new ConcurrentDictionary<string, bool>();

        // Available proxies
        var availableProxies = _proxyManager.GetHealthyProxies();
        var proxyCount = availableProxies.Count;
        
        if (proxyCount == 0)
        {
            _logger.LogWarning("⚠️ Hiç sağlıklı proxy yok, proxy olmadan devam ediliyor");
            availableProxies.Add(null!); // Null proxy ekle (proxy'siz çalış)
            proxyCount = 1;
        }

        _logger.LogInformation("💪 {Count} proxy ile paralel scraping yapılacak", proxyCount);

        // Her proxy için kaç işletme alacağını hesapla
        var resultsPerProxy = (int)Math.Ceiling((double)maxResults / proxyCount);
        _logger.LogInformation("📊 Her proxy {PerProxy} işletme toplayacak", resultsPerProxy);

        // Paralel scraping taskları oluştur
        var scrapingTasks = availableProxies.Select(async (proxy, index) =>
        {
            var proxyId = proxy?.Address ?? "no-proxy";
            _logger.LogInformation("🔄 Proxy #{Index} ({ProxyId}) başlatılıyor...", index + 1, proxyId);

            try
            {
                var businesses = await ScrapeWithSingleProxy(
                    proxy,
                    category,
                    city,
                    country,
                    language,
                    resultsPerProxy,
                    index,
                    cancellationToken);

                // Bulunan işletmeleri ana koleksiyona ekle (duplicate kontrolü ile)
                foreach (var business in businesses)
                {
                    {
                        allBusinesses.Add(business);
                    }
                }

                _logger.LogInformation("✅ Proxy #{Index} tamamlandı: {Count} işletme bulundu", index + 1, businesses.Count);
                
                // Başarılı proxy
                if (proxy != null)
                {
                    _proxyManager.ReportProxySuccess(proxy);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Proxy #{Index} ({ProxyId}) başarısız oldu", index + 1, proxyId);
                
                if (proxy != null)
                {
                    _proxyManager.ReportProxyFailure(proxy);
                }
            }
        }).ToList();

        // Tüm paralel scraping'lerin bitmesini bekle
        await Task.WhenAll(scrapingTasks);

        // Sonuçları listele ve maxResults'a göre kes
        var finalResults = allBusinesses
            .DistinctBy(b => b.BusinessName)
            .Take(maxResults)
            .ToList();

        var duration = DateTime.UtcNow - startTime;
        _logger.LogInformation("🎉 PARALLEL Scraping tamamlandı! {Count}/{Target} işletme bulundu, Süre: {Duration:mm\\:ss}", 
            finalResults.Count, maxResults, duration);

        return finalResults;
    }

    private async Task<List<BusinessDto>> ScrapeWithSingleProxy(
        Models.ProxyConfig? proxyConfig,
        string category,
        string city,
        string? country,
        string language,
        int maxResults,
        int proxyIndex,
        CancellationToken cancellationToken)
    {
        var businesses = new List<BusinessDto>();
        IWebDriver? driver = null;

        try
        {
            // WebDriver konfigüre et
            driver = ConfigureWebDriver(proxyConfig, proxyIndex);

            // Google Maps URL'ini oluştur
            var searchQuery = BuildSearchQuery(category, city, country);
            var googleMapsUrl = $"https://www.google.com/maps/search/{Uri.EscapeDataString(searchQuery)}";

            _logger.LogInformation("📍 Proxy #{Index}: URL açılıyor: {Url}", proxyIndex + 1, googleMapsUrl);

            // Sayfayı aç
            driver.Navigate().GoToUrl(googleMapsUrl);

            // İlk yüklenme beklemesi (daha kısa - paralel çalıştığımız için)
            await Task.Delay(_random.Next(2000, 3000), cancellationToken);

            // Cookie popup'ı kapat (varsa)
            await CloseCookiePopup(driver, cancellationToken);

            // Sonuç listesini bekle
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));
            wait.Until(d => d.FindElements(By.CssSelector("div[role='article']")).Count > 0);

            _logger.LogInformation("✅ Proxy #{Index}: Sonuç listesi yüklendi", proxyIndex + 1);

            int processedCount = 0;
            int scrollAttempts = 0;
            const int maxScrollAttempts = 30;
            var processedUrls = new HashSet<string>();
            int lastProcessedIndex = 0;

            while (processedCount < maxResults && scrollAttempts < maxScrollAttempts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Mevcut işletmeleri al
                var businessElements = driver.FindElements(By.CssSelector("div[role='article']"));

                // Yeni işletmeleri işle
                for (int i = lastProcessedIndex; i < businessElements.Count && processedCount < maxResults; i++)
                {
                    try
                    {
                        var element = businessElements[i];

                        // İşletmeye tıkla
                        await ScrollToElement(driver, element, cancellationToken);
                        await Task.Delay(_random.Next(300, 600), cancellationToken); // Daha kısa bekleme
                        element.Click();

                        // Detayların yüklenmesini bekle (daha kısa - proxy kullanıyoruz)
                        await Task.Delay(_random.Next(2000, 3000), cancellationToken);

                        // İşletme verilerini çek
                        var business = await ExtractBusinessData(driver, category, city, country, cancellationToken);

                        {
                            {
                                businesses.Add(business);
                                processedCount++;

                                if (processedCount % 5 == 0)
                                {
                                    _logger.LogInformation("📊 Proxy #{Index}: {Current}/{Target} işletme toplandı",
                                        proxyIndex + 1, processedCount, maxResults);
                                }

                                if (processedCount >= maxResults)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("⚠️ Proxy #{Index}: İşletme atlandı: {Message}", proxyIndex + 1, ex.Message);
                        continue;
                    }
                }

                lastProcessedIndex = businessElements.Count;

                if (processedCount >= maxResults)
                {
                    break;
                }

                // Daha fazla sonuç için scroll
                var previousCount = businessElements.Count;
                await ScrollResultsList(driver, cancellationToken);
                scrollAttempts++;
                await Task.Delay(_random.Next(1500, 2500), cancellationToken); // Daha kısa bekleme

                // Yeni sonuçların yüklenmesini bekle
                await Task.Delay(2000, cancellationToken);

                var newBusinessElements = driver.FindElements(By.CssSelector("div[role='article']"));
                if (newBusinessElements.Count == previousCount)
                {
                    _logger.LogInformation("⚠️ Proxy #{Index}: Daha fazla sonuç yok", proxyIndex + 1);
                    break;
                }
            }

            _logger.LogInformation("✅ Proxy #{Index}: {Count} işletme toplandı", proxyIndex + 1, businesses.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Proxy #{Index}: Scraping hatası", proxyIndex + 1);
            throw;
        }
        finally
        {
            driver?.Quit();
            driver?.Dispose();
        }

        return businesses;
    }

    private IWebDriver ConfigureWebDriver(Models.ProxyConfig? proxyConfig, int proxyIndex)
    {
        var options = new ChromeOptions();

        // Headless mod (arka planda çalışır)
        options.AddArgument("--headless=new");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-blink-features=AutomationControlled");
        options.AddExcludedArgument("enable-automation");
        options.AddAdditionalOption("useAutomationExtension", false);

        // Rastgele User-Agent (her proxy farklı)
        var userAgent = _userAgents[proxyIndex % _userAgents.Length];
        options.AddArgument($"user-agent={userAgent}");

        // Proxy yapılandırması
        if (proxyConfig != null)
        {
            options = _proxyManager.ConfigureProxyForDriver(options, proxyConfig);
            _logger.LogInformation("🔌 Proxy #{Index} yapılandırıldı: {Address}", proxyIndex + 1, proxyConfig.Address);
        }

        // WebDriver oluştur
        var driver = new ChromeDriver(options);
        driver.Manage().Window.Size = new System.Drawing.Size(1920, 1080);
        driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);

        // WebDriver properties'ini gizle (bot detection'dan kaçın)
        driver.ExecuteScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");

        return driver;
    }

    private string BuildSearchQuery(string category, string city, string? country)
    {
        // Şehir önce, sonra ülke, sonra kategori (daha iyi sonuç için)
        var query = $"{city}";

        if (!string.IsNullOrEmpty(country))
        {
            query += $", {country}";
        }

        query += $" {category}";

        return query;
    }

    private async Task CloseCookiePopup(IWebDriver driver, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(500, cancellationToken);
            var acceptButtons = driver.FindElements(By.XPath("//button[contains(., 'Accept') or contains(., 'Kabul') or contains(., 'Tamam')]"));
            if (acceptButtons.Any())
            {
                acceptButtons.First().Click();
                await Task.Delay(300, cancellationToken);
            }
        }
        catch { }
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
            return null;
        }
    }

    private async Task ScrollResultsList(IWebDriver driver, CancellationToken cancellationToken)
    {
        try
        {
            var scrollableDiv = driver.FindElement(By.CssSelector("div[role='feed']"));
            var js = (IJavaScriptExecutor)driver;
            var scrollHeight = (long)js.ExecuteScript("return arguments[0].scrollHeight", scrollableDiv);
            var currentScroll = 0L;
            var scrollStep = 500; // Daha hızlı scroll

            while (currentScroll < scrollHeight)
            {
                cancellationToken.ThrowIfCancellationRequested();
                js.ExecuteScript($"arguments[0].scrollTop += {scrollStep}", scrollableDiv);
                currentScroll += scrollStep;
                await Task.Delay(_random.Next(100, 300), cancellationToken); // Daha kısa bekleme
            }
        }
        catch { }
    }

    private async Task ScrollToElement(IWebDriver driver, IWebElement element, CancellationToken cancellationToken)
    {
        try
        {
            var js = (IJavaScriptExecutor)driver;
            js.ExecuteScript("arguments[0].scrollIntoView({behavior: 'smooth', block: 'center'});", element);
            await Task.Delay(_random.Next(200, 400), cancellationToken); // Daha kısa bekleme
        }
        catch { }
    }
}
