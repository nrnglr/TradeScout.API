using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TradeScout.API.Models;

namespace TradeScout.API.Services;

/// <summary>
/// Service for generating and validating JWT tokens
/// </summary>
public interface IJwtService
{
    string GenerateToken(User user);
}

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] 
            ?? throw new InvalidOperationException("JWT SecretKey is not configured");
        var issuer = jwtSettings["Issuer"] 
            ?? throw new InvalidOperationException("JWT Issuer is not configured");
        var audience = jwtSettings["Audience"] 
            ?? throw new InvalidOperationException("JWT Audience is not configured");
        var expirationMinutes = int.Parse(jwtSettings["ExpirationMinutes"] ?? "60");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("Credits", user.Credits.ToString()),
            new Claim("PackageType", user.PackageType)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
