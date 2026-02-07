using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradeScout.API.Services;

namespace TradeScout.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProxyController : ControllerBase
{
    private readonly ProxyManager _proxyManager;
    private readonly ILogger<ProxyController> _logger;

    public ProxyController(ProxyManager proxyManager, ILogger<ProxyController> logger)
    {
        _proxyManager = proxyManager;
        _logger = logger;
    }

    /// <summary>
    /// Get all proxy statuses
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetProxyStatuses()
    {
        try
        {
            var statuses = _proxyManager.GetProxyStatuses();
            
            return Ok(new
            {
                success = true,
                totalProxies = statuses.Count,
                healthyProxies = statuses.Count(p => p.IsHealthy),
                proxies = statuses
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting proxy statuses");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Test all configured proxies
    /// </summary>
    [HttpPost("test-all")]
    public async Task<IActionResult> TestAllProxies()
    {
        try
        {
            _logger.LogInformation("🧪 Testing all proxies...");
            
            var results = await _proxyManager.TestAllProxiesAsync();
            
            var workingProxies = results.Where(r => r.IsSuccessful).ToList();
            var avgResponseTime = workingProxies.Any() 
                ? workingProxies.Average(r => r.ResponseTime) 
                : 0;

            var summary = new
            {
                success = true,
                totalProxies = results.Count,
                workingProxies = workingProxies.Count,
                failedProxies = results.Count(r => !r.IsSuccessful),
                averageResponseTime = avgResponseTime,
                results = results.Select(r => new
                {
                    proxy = r.ProxyAddress,
                    status = r.IsSuccessful ? "✅ Working" : "❌ Failed",
                    responseTime = r.IsSuccessful ? $"{r.ResponseTime:F2}s" : "N/A",
                    error = r.ErrorMessage
                })
            };

            _logger.LogInformation(
                "✅ Proxy test completed: {Working}/{Total} working", 
                summary.workingProxies, 
                summary.totalProxies
            );

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing proxies");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Test a specific proxy
    /// </summary>
    [HttpPost("test/{index}")]
    public async Task<IActionResult> TestProxy(int index)
    {
        try
        {
            var statuses = _proxyManager.GetProxyStatuses();
            
            if (index < 0 || index >= statuses.Count)
            {
                return BadRequest(new { success = false, error = "Invalid proxy index" });
            }

            // Test the proxy at the given index
            _logger.LogInformation("🧪 Testing proxy at index {Index}...", index);
            
            var results = await _proxyManager.TestAllProxiesAsync();
            var result = results[index];

            return Ok(new
            {
                success = true,
                proxy = result.ProxyAddress,
                isWorking = result.IsSuccessful,
                responseTime = result.IsSuccessful ? $"{result.ResponseTime:F2}s" : "N/A",
                error = result.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing proxy");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}
