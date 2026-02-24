using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using TradeScout.API.Data;
using TradeScout.API.DTOs;
using TradeScout.API.Models;
using TradeScout.API.Services;
using BCrypt.Net;

namespace TradeScout.API.Controllers;

/// <summary>
/// Authentication controller for user registration and login
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ApplicationDbContext context, 
        IJwtService jwtService,
        ILogger<AuthController> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    /// <param name="registerDto">User registration data</param>
    /// <returns>Authentication response with JWT token</returns>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto registerDto)
    {
        try
        {
            // Check if model is valid
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Check if email already exists
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == registerDto.Email.ToLower());

            if (existingUser != null)
            {
                return Conflict(new { message = "Bu email adresi zaten kullanılıyor." });
            }

            // Hash the password using BCrypt
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password);

            // Create new user
            var user = new User
            {
                FullName = registerDto.FullName,
                Email = registerDto.Email.ToLower(),
                PasswordHash = passwordHash,
                CompanyName = registerDto.CompanyName,
                Address = registerDto.Address,
                City = registerDto.City,
                Country = registerDto.Country,
                Phone = registerDto.Phone,
                Website = registerDto.Website,
                UserType = registerDto.UserType,
                Credits = 3,
                PackageType = "Free",
                Role = "User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastLogin = null
            };

            // Save to database
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Generate JWT token
            var token = _jwtService.GenerateToken(user);

            // Return response
            var response = new AuthResponseDto
            {
                Token = token,
                FullName = user.FullName,
                Email = user.Email,
                Credits = user.Credits,
                Role = user.Role,
                PackageType = user.PackageType
            };

            _logger.LogInformation("Yeni kullanıcı kaydedildi: {Email}", user.Email);

            return CreatedAtAction(nameof(Register), new { id = user.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kullanıcı kaydı sırasında hata oluştu: {Email}", registerDto.Email);
            return StatusCode(500, new { message = "Kayıt işlemi sırasında bir hata oluştu." });
        }
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    /// <param name="loginDto">User login credentials</param>
    /// <returns>Authentication response with JWT token</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto loginDto)
    {
        try
        {
            // Check if model is valid
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Find user by email
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == loginDto.Email.ToLower());

            if (user == null)
            {
                return Unauthorized(new { message = "Email veya şifre hatalı." });
            }

            // Check if user is active
            if (!user.IsActive)
            {
                return Unauthorized(new { message = "Hesabınız devre dışı bırakılmıştır. Lütfen destek ekibi ile iletişime geçin." });
            }

            // Verify password using BCrypt
            var isPasswordValid = BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash);

            if (!isPasswordValid)
            {
                return Unauthorized(new { message = "Email veya şifre hatalı." });
            }

            // Update last login time
            user.LastLogin = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Generate JWT token
            var token = _jwtService.GenerateToken(user);

            // Return response
            var response = new AuthResponseDto
            {
                Token = token,
                FullName = user.FullName,
                Email = user.Email,
                Credits = user.Credits,
                Role = user.Role,
                PackageType = user.PackageType
            };

            _logger.LogInformation("Kullanıcı giriş yaptı: {Email}", user.Email);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Giriş işlemi sırasında hata oluştu: {Email}", loginDto.Email);
            return StatusCode(500, new { message = "Giriş işlemi sırasında bir hata oluştu." });
        }
    }
}
