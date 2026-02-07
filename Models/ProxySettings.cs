namespace TradeScout.API.Models;

public class ProxySettings
{
    public bool EnableProxy { get; set; }
    public string RotationType { get; set; } = "RoundRobin"; // RoundRobin, Random, Sequential
    public bool TestProxiesOnStartup { get; set; }
    public int ProxyTimeout { get; set; } = 30;
    public List<ProxyConfig> Proxies { get; set; } = new();
}

public class ProxyConfig
{
    public string Address { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int FailureCount { get; set; } = 0;
    public DateTime? LastUsed { get; set; }
    public DateTime? LastFailure { get; set; }
    public bool IsHealthy => FailureCount < 3 && Enabled;
}
