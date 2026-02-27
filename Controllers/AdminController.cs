using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TradeScout.API.Data;
using TradeScout.API.Services;

namespace TradeScout.API.Controllers;

/// <summary>
/// Admin operations for managing users and credits
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminController> _logger;
    private readonly IGeminiSearchService _geminiService;

    public AdminController(
        ApplicationDbContext context, 
        ILogger<AdminController> logger,
        IGeminiSearchService geminiService)
    {
        _context = context;
        _logger = logger;
        _geminiService = geminiService;
    }

    /// <summary>
    /// Update a user's credits and max results limit
    /// Only admins can perform this action
    /// </summary>
    [HttpPost("update-user/{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> UpdateUserCredits(
        int userId,
        [FromBody] UpdateUserCreditsRequest request)
    {
        try
        {
            // Check if user is admin
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole != "Admin")
            {
                return Forbid();
            }

            // Validate request
            if (request == null || (request.Credits == null && request.MaxResultsPerSearch == null))
            {
                return BadRequest(new
                {
                    message = "Lütfen güncellenecek alana değer sağlayın",
                    example = new { Credits = 50, MaxResultsPerSearch = 100 }
                });
            }

            // Find user
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = $"Kullanıcı ID {userId} bulunamadı" });
            }

            var oldCredits = user.Credits;
            var oldMaxResults = user.MaxResultsPerSearch;

            // Update credits if provided
            if (request.Credits.HasValue)
            {
                if (request.Credits.Value < 0)
                {
                    return BadRequest(new { message = "Kredi negatif olamaz" });
                }
                user.Credits = request.Credits.Value;
            }

            // Update max results per search if provided
            if (request.MaxResultsPerSearch.HasValue)
            {
                if (request.MaxResultsPerSearch.Value < 1 || request.MaxResultsPerSearch.Value > 1000)
                {
                    return BadRequest(new { message = "Max firma sayısı 1-1000 arasında olmalıdır" });
                }
                user.MaxResultsPerSearch = request.MaxResultsPerSearch.Value;
            }

            // Save changes
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Admin güncellemesi: Kullanıcı {UserId} ({Email}) - Credits: {OldCredits} -> {NewCredits}, MaxResults: {OldMaxResults} -> {NewMaxResults}",
                user.Id, user.Email, oldCredits, user.Credits, oldMaxResults, user.MaxResultsPerSearch);

            return Ok(new
            {
                message = "✅ Kullanıcı başarıyla güncellendi",
                user = new
                {
                    user.Id,
                    user.FullName,
                    user.Email,
                    user.Credits,
                    user.MaxResultsPerSearch,
                    changes = new
                    {
                        creditsChanged = oldCredits != user.Credits,
                        creditsFrom = oldCredits,
                        creditsTo = user.Credits,
                        maxResultsChanged = oldMaxResults != user.MaxResultsPerSearch,
                        maxResultsFrom = oldMaxResults,
                        maxResultsTo = user.MaxResultsPerSearch
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin güncellemesi sırasında hata");
            return StatusCode(500, new { message = "Güncelleme sırasında hata oluştu", error = ex.Message });
        }
    }

    /// <summary>
    /// Get Gemini API key pool status (admin only)
    /// Shows total keys and available keys for load balancing
    /// </summary>
    [HttpGet("api-key-status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<object> GetApiKeyPoolStatus()
    {
        try
        {
            // Check if user is admin
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole != "Admin")
            {
                return Forbid();
            }

            var (totalKeys, availableKeys) = _geminiService.GetApiKeyPoolStatus();
            
            // Calculate capacity
            var maxRequestsPerMinute = totalKeys * 50; // 50 requests per key per minute (conservative)
            var estimatedConcurrentUsers = maxRequestsPerMinute / 2; // Each user ~2 requests

            return Ok(new
            {
                status = "✅ API Key Pool Active",
                totalKeys,
                availableKeys,
                usedKeys = totalKeys - availableKeys,
                capacity = new
                {
                    maxRequestsPerMinute,
                    estimatedConcurrentUsers,
                    recommendation = totalKeys < 5 
                        ? "⚠️ 200+ kullanıcı için en az 5 API key eklemeniz önerilir"
                        : "✅ Yeterli API key mevcut"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API key durumu alınamadı");
            return StatusCode(500, new { message = "API key durumu alınamadı", error = ex.Message });
        }
    }

    /// <summary>
    /// Get all users (admin only)
    /// </summary>
    [HttpGet("users")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<object>> GetAllUsers(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        try
        {
            // Check if user is admin
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole != "Admin")
            {
                return Forbid();
            }

            var totalUsers = _context.Users.Count();
            var users = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .Skip(skip)
                .Take(take)
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.Email,
                    u.Credits,
                    u.MaxResultsPerSearch,
                    u.PackageType,
                    u.Role,
                    u.IsActive,
                    u.CreatedAt,
                    u.LastLogin
                })
                .ToListAsync();

            return Ok(new
            {
                message = "Kullanıcı listesi",
                totalUsers,
                returnedCount = users.Count,
                skip,
                take,
                users
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kullanıcı listesi alınırken hata");
            return StatusCode(500, new { message = "Hata oluştu", error = ex.Message });
        }
    }

    /// <summary>
    /// Search users by email or name
    /// </summary>
    [HttpGet("search-user")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<object>> SearchUser([FromQuery] string query)
    {
        try
        {
            // Check if user is admin
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole != "Admin")
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { message = "Lütfen arama terimi sağlayın" });
            }

            var searchTerm = query.ToLower();
            var users = await _context.Users
                .Where(u => u.Email.ToLower().Contains(searchTerm) || u.FullName.ToLower().Contains(searchTerm))
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.Email,
                    u.Credits,
                    u.MaxResultsPerSearch,
                    u.PackageType,
                    u.Role,
                    u.IsActive,
                    u.CreatedAt,
                    u.LastLogin
                })
                .ToListAsync();

            return Ok(new
            {
                message = $"{users.Count} kullanıcı bulundu",
                query,
                users
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kullanıcı araması sırasında hata");
            return StatusCode(500, new { message = "Hata oluştu", error = ex.Message });
        }
    }
}

/// <summary>
/// Request model for updating user credits
/// </summary>
public class UpdateUserCreditsRequest
{
    /// <summary>
    /// New credit amount (optional)
    /// </summary>
    public int? Credits { get; set; }

    /// <summary>
    /// New max results per search limit (optional)
    /// </summary>
    public int? MaxResultsPerSearch { get; set; }
}