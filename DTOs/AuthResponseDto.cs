namespace TradeScout.API.DTOs;

/// <summary>
/// DTO for authentication response
/// </summary>
public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Credits { get; set; }
    public string Role { get; set; } = string.Empty;
    public string PackageType { get; set; } = string.Empty;
}
