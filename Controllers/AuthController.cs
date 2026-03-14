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
    private readonly IEmailService _emailService;

    public AuthController(
        ApplicationDbContext context, 
        IJwtService jwtService,
        ILogger<AuthController> logger,
        IEmailService emailService)
    {
        _context = context;
        _jwtService = jwtService;
        _logger = logger;
        _emailService = emailService;
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
            // Log incoming registration data for debugging
            _logger.LogInformation("📝 Kayıt talebesi alındı - Email: {Email}, FullName: {FullName}", 
                registerDto?.Email ?? "NULL", 
                registerDto?.FullName ?? "NULL");

            // Check if model is valid
            if (!ModelState.IsValid)
            {
                // Log validation errors in detail
                var errors = ModelState.Values.SelectMany(v => v.Errors).ToList();
                _logger.LogError("❌ Model Validation Hataları: {ErrorCount} hata", errors.Count);
                
                foreach (var error in errors)
                {
                    _logger.LogError("   - {ErrorMessage}", error.ErrorMessage);
                }
                
                // Return detailed error response
                var errorDetails = ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );
                
                return BadRequest(new { 
                    message = "Validation hataları oluştu",
                    errors = errorDetails 
                });
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
                Credits = 2,
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

    /// <summary>
    /// Request password reset - sends a code to user's email
    /// </summary>
    [HttpPost("reset-password-request")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPasswordRequest([FromBody] ResetPasswordRequestDto request)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

            if (user == null)
            {
                // Don't reveal if user exists or not for security
                return Ok(new { message = "Eğer bu e-posta adresi kayıtlıysa, şifre sıfırlama kodu gönderildi." });
            }

            // Generate 6-digit code
            var resetCode = new Random().Next(100000, 999999).ToString();
            user.PasswordResetCode = resetCode;
            user.PasswordResetExpiry = DateTime.UtcNow.AddMinutes(15); // 15 dakika geçerli

            await _context.SaveChangesAsync();

            // Send email with reset code
            var emailBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; margin: 0; padding: 20px; }}
        .container {{ max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .header {{ text-align: center; margin-bottom: 30px; }}
        .header h1 {{ color: #2563eb; margin: 0; }}
        .code-box {{ background-color: #f0f9ff; border: 2px dashed #2563eb; padding: 20px; text-align: center; margin: 20px 0; border-radius: 8px; }}
        .code {{ font-size: 32px; font-weight: bold; color: #1e40af; letter-spacing: 5px; }}
        .warning {{ color: #dc2626; font-size: 14px; margin-top: 20px; }}
        .footer {{ color: #6b7280; font-size: 12px; text-align: center; margin-top: 30px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>� FGS Trade</h1>
            <p>Şifre Sıfırlama Kodu</p>
        </div>
        <p>Merhaba,</p>
        <p>Şifre sıfırlama talebiniz için doğrulama kodunuz aşağıdadır:</p>
        <div class='code-box'>
            <span class='code'>{resetCode}</span>
        </div>
        <p class='warning'>⚠️ Bu kod 15 dakika içinde geçerliliğini yitirecektir.</p>
        <p>Eğer bu talebi siz yapmadıysanız, bu e-postayı görmezden gelebilirsiniz.</p>
        <div class='footer'>
            <p>FGS Trade - Akıllı İş Keşfi Platformu</p>
        </div>
    </div>
</body>
</html>";

            await _emailService.SendEmailAsync(user.Email, "FGS Trade - Şifre Sıfırlama Kodu", emailBody);
            _logger.LogInformation("🔑 Şifre sıfırlama kodu gönderildi: {Email}", user.Email);

            return Ok(new { message = "Şifre sıfırlama kodu e-posta adresinize gönderildi." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Şifre sıfırlama isteği hatası: {Email}", request.Email);
            return StatusCode(500, new { message = "Bir hata oluştu." });
        }
    }

    /// <summary>
    /// Reset password with code
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

            if (user == null)
            {
                return BadRequest(new { message = "Geçersiz e-posta adresi." });
            }

            if (user.PasswordResetCode != request.Code)
            {
                return BadRequest(new { message = "Geçersiz sıfırlama kodu." });
            }

            if (user.PasswordResetExpiry == null || user.PasswordResetExpiry < DateTime.UtcNow)
            {
                return BadRequest(new { message = "Sıfırlama kodu süresi dolmuş. Lütfen yeni kod isteyin." });
            }

            // Update password
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.PasswordResetCode = null;
            user.PasswordResetExpiry = null;

            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ Şifre sıfırlandı: {Email}", user.Email);

            return Ok(new { message = "Şifreniz başarıyla sıfırlandı. Giriş yapabilirsiniz." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Şifre sıfırlama hatası: {Email}", request.Email);
            return StatusCode(500, new { message = "Bir hata oluştu." });
        }
    }

    /// <summary>
    /// Send email verification code
    /// </summary>
    [HttpPost("send-verification")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendVerificationCode([FromBody] EmailRequestDto request)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

            if (user == null)
            {
                return NotFound(new { message = "Kullanıcı bulunamadı." });
            }

            if (user.IsEmailVerified)
            {
                return Ok(new { message = "E-posta adresi zaten doğrulanmış." });
            }

            // Generate 6-digit code
            var verificationCode = new Random().Next(100000, 999999).ToString();
            user.EmailVerificationCode = verificationCode;
            user.EmailVerificationExpiry = DateTime.UtcNow.AddMinutes(30); // 30 dakika geçerli

            await _context.SaveChangesAsync();

            // Send email with verification code
            var emailBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; margin: 0; padding: 20px; }}
        .container {{ max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .header {{ text-align: center; margin-bottom: 30px; }}
        .header h1 {{ color: #10b981; margin: 0; }}
        .code-box {{ background-color: #ecfdf5; border: 2px dashed #10b981; padding: 20px; text-align: center; margin: 20px 0; border-radius: 8px; }}
        .code {{ font-size: 32px; font-weight: bold; color: #047857; letter-spacing: 5px; }}
        .warning {{ color: #f59e0b; font-size: 14px; margin-top: 20px; }}
        .footer {{ color: #6b7280; font-size: 12px; text-align: center; margin-top: 30px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>✉️ FGS Trade</h1>
            <p>E-posta Doğrulama Kodu</p>
        </div>
        <p>Merhaba {user.FullName},</p>
        <p>FGS Trade'e hoş geldiniz! E-posta adresinizi doğrulamak için aşağıdaki kodu kullanın:</p>
        <div class='code-box'>
            <span class='code'>{verificationCode}</span>
        </div>
        <p class='warning'>⏰ Bu kod 30 dakika içinde geçerliliğini yitirecektir.</p>
        <p>E-posta adresinizi doğruladıktan sonra tüm özelliklere erişebilirsiniz.</p>
        <div class='footer'>
            <p>FGS Trade - Akıllı İş Keşfi Platformu</p>
        </div>
    </div>
</body>
</html>";

            await _emailService.SendEmailAsync(user.Email, "FGS Trade - E-posta Doğrulama Kodu", emailBody);
            _logger.LogInformation("📧 E-posta doğrulama kodu gönderildi: {Email}", user.Email);

            return Ok(new { message = "Doğrulama kodu e-posta adresinize gönderildi." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Doğrulama kodu gönderme hatası: {Email}", request.Email);
            return StatusCode(500, new { message = "Bir hata oluştu." });
        }
    }

    /// <summary>
    /// Verify email with code
    /// </summary>
    [HttpPost("verify-email")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDto request)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

            if (user == null)
            {
                return BadRequest(new { message = "Geçersiz e-posta adresi." });
            }

            if (user.IsEmailVerified)
            {
                return Ok(new { message = "E-posta adresi zaten doğrulanmış." });
            }

            if (user.EmailVerificationCode != request.Code)
            {
                return BadRequest(new { message = "Geçersiz doğrulama kodu." });
            }

            if (user.EmailVerificationExpiry == null || user.EmailVerificationExpiry < DateTime.UtcNow)
            {
                return BadRequest(new { message = "Doğrulama kodu süresi dolmuş. Lütfen yeni kod isteyin." });
            }

            // Verify email
            user.IsEmailVerified = true;
            user.EmailVerificationCode = null;
            user.EmailVerificationExpiry = null;

            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ E-posta doğrulandı: {Email}", user.Email);

            return Ok(new { message = "E-posta adresiniz başarıyla doğrulandı." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "E-posta doğrulama hatası: {Email}", request.Email);
            return StatusCode(500, new { message = "Bir hata oluştu." });
        }
    }
}

// DTOs for password reset and email verification
public class ResetPasswordRequestDto
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordDto
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class EmailRequestDto
{
    public string Email { get; set; } = string.Empty;
}

public class VerifyEmailDto
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}
