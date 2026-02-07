using Microsoft.Extensions.Options;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using TradeScout.API.Models;

namespace TradeScout.API.Services;

public class ProxyManager
{
    private readonly ProxySettings _settings;
    private readonly ILogger<ProxyManager> _logger;
    private int _currentProxyIndex = 0;
    private readonly object _lock = new object();
    private readonly Random _random = new Random();

    public ProxyManager(IOptions<ProxySettings> settings, ILogger<ProxyManager> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public ProxyConfig? GetNextProxy()
    {
        if (!_settings.EnableProxy || !_settings.Proxies.Any())
        {
            _logger.LogInformation("Proxy disabled or no proxies configured");
            return null;
        }

        lock (_lock)
        {
            var healthyProxies = _settings.Proxies.Where(p => p.IsHealthy).ToList();
            
            if (!healthyProxies.Any())
            {
                _logger.LogWarning("No healthy proxies available! Resetting failure counts...");
                // Reset failure counts to give proxies another chance
                foreach (var proxy in _settings.Proxies)
                {
                    proxy.FailureCount = 0;
                }
                healthyProxies = _settings.Proxies.Where(p => p.Enabled).ToList();
            }

            if (!healthyProxies.Any())
            {
                _logger.LogError("No enabled proxies available!");
                return null;
            }

            ProxyConfig? selectedProxy = null;

            switch (_settings.RotationType.ToLower())
            {
                case "random":
                    selectedProxy = healthyProxies[_random.Next(healthyProxies.Count)];
                    break;
                case "sequential":
                    selectedProxy = healthyProxies.First();
                    break;
                case "roundrobin":
                default:
                    _currentProxyIndex = _currentProxyIndex % healthyProxies.Count;
                    selectedProxy = healthyProxies[_currentProxyIndex];
                    _currentProxyIndex++;
                    break;
            }

            if (selectedProxy != null)
            {
                selectedProxy.LastUsed = DateTime.UtcNow;
                _logger.LogInformation($"Selected proxy: {MaskProxyAddress(selectedProxy.Address)} (Failures: {selectedProxy.FailureCount})");
            }

            return selectedProxy;
        }
    }

    public void ReportProxyFailure(ProxyConfig proxy)
    {
        lock (_lock)
        {
            proxy.FailureCount++;
            proxy.LastFailure = DateTime.UtcNow;
            _logger.LogWarning($"Proxy {MaskProxyAddress(proxy.Address)} failed. Failure count: {proxy.FailureCount}");
            
            if (proxy.FailureCount >= 3)
            {
                _logger.LogError($"Proxy {MaskProxyAddress(proxy.Address)} disabled due to repeated failures");
            }
        }
    }

    public void ReportProxySuccess(ProxyConfig proxy)
    {
        lock (_lock)
        {
            if (proxy.FailureCount > 0)
            {
                proxy.FailureCount--;
                _logger.LogInformation($"Proxy {MaskProxyAddress(proxy.Address)} succeeded. Failure count decreased to: {proxy.FailureCount}");
            }
        }
    }

    public ChromeOptions ConfigureProxyForDriver(ChromeOptions options, ProxyConfig? proxyConfig)
    {
        if (proxyConfig == null)
        {
            _logger.LogInformation("No proxy configured, using direct connection");
            return options;
        }

        try
        {
            var proxy = new Proxy
            {
                Kind = ProxyKind.Manual,
                HttpProxy = proxyConfig.Address,
                SslProxy = proxyConfig.Address
            };

            options.Proxy = proxy;
            
            _logger.LogInformation($"Configured Chrome driver with proxy: {MaskProxyAddress(proxyConfig.Address)}");
            
            return options;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error configuring proxy {MaskProxyAddress(proxyConfig.Address)}");
            return options;
        }
    }

    public async Task<List<ProxyTestResult>> TestAllProxiesAsync()
    {
        var results = new List<ProxyTestResult>();

        foreach (var proxy in _settings.Proxies)
        {
            var result = await TestProxyAsync(proxy);
            results.Add(result);
        }

        return results;
    }

    public async Task<ProxyTestResult> TestProxyAsync(ProxyConfig proxyConfig)
    {
        var result = new ProxyTestResult
        {
            ProxyAddress = MaskProxyAddress(proxyConfig.Address),
            StartTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation($"🧪 Testing proxy: {result.ProxyAddress}");

            // Create HttpClientHandler with proxy
            var handler = new HttpClientHandler
            {
                Proxy = new System.Net.WebProxy(proxyConfig.Address)
                {
                    Credentials = !string.IsNullOrEmpty(proxyConfig.Username) 
                        ? new System.Net.NetworkCredential(proxyConfig.Username, proxyConfig.Password)
                        : null
                },
                UseProxy = true
            };

            using var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(_settings.ProxyTimeout)
            };

            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            // Test with a simple HTTP request
            var response = await httpClient.GetAsync("https://www.google.com");
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode && (content.Contains("Google") || content.Contains("google")))
            {
                result.IsSuccessful = true;
                result.ResponseTime = (DateTime.UtcNow - result.StartTime).TotalSeconds;
                ReportProxySuccess(proxyConfig);
                _logger.LogInformation($"✅ Proxy {result.ProxyAddress} is working! Response time: {result.ResponseTime:F2}s");
            }
            else
            {
                result.IsSuccessful = false;
                result.ErrorMessage = $"HTTP {response.StatusCode}: Content not recognized";
                ReportProxyFailure(proxyConfig);
                _logger.LogWarning($"❌ Proxy {result.ProxyAddress} failed: {result.ErrorMessage}");
            }
        }
        catch (TaskCanceledException ex)
        {
            result.IsSuccessful = false;
            result.ErrorMessage = "Timeout - Proxy did not respond within time limit";
            result.ResponseTime = (DateTime.UtcNow - result.StartTime).TotalSeconds;
            ReportProxyFailure(proxyConfig);
            _logger.LogError($"⏱️ Proxy {result.ProxyAddress} timeout after {result.ResponseTime:F2}s");
        }
        catch (Exception ex)
        {
            result.IsSuccessful = false;
            result.ErrorMessage = ex.Message;
            result.ResponseTime = (DateTime.UtcNow - result.StartTime).TotalSeconds;
            ReportProxyFailure(proxyConfig);
            _logger.LogError($"❌ Proxy {result.ProxyAddress} failed: {ex.Message}");
        }

        return result;
    }

    public List<ProxyStatus> GetProxyStatuses()
    {
        return _settings.Proxies.Select(p => new ProxyStatus
        {
            Address = MaskProxyAddress(p.Address),
            Enabled = p.Enabled,
            FailureCount = p.FailureCount,
            IsHealthy = p.IsHealthy,
            LastUsed = p.LastUsed,
            LastFailure = p.LastFailure
        }).ToList();
    }

    private string MaskProxyAddress(string address)
    {
        try
        {
            var uri = new Uri(address);
            return $"{uri.Scheme}://{uri.Host}:{uri.Port}";
        }
        catch
        {
            return address;
        }
    }
}

public class ProxyTestResult
{
    public string ProxyAddress { get; set; } = string.Empty;
    public bool IsSuccessful { get; set; }
    public double ResponseTime { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartTime { get; set; }
}

public class ProxyStatus
{
    public string Address { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int FailureCount { get; set; }
    public bool IsHealthy { get; set; }
    public DateTime? LastUsed { get; set; }
    public DateTime? LastFailure { get; set; }
}
