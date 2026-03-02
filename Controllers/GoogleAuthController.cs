using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;
using TradeScout.API.Data;
using TradeScout.API.Services;
using TradeScout.API.Models; // <-- 1. USER MODELİNİN OLDUĞU NAMESPACE (Bunu kontrol et)

namespace TradeScout.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class GoogleAuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IJwtService _jwtService;

        public GoogleAuthController(ApplicationDbContext context, IJwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            try
            {
                // request null kontrolü (CS8602 uyarısını engeller)
                if (request == null || string.IsNullOrEmpty(request.AccessToken))
                    return BadRequest(new { message = "Token boş olamaz." });

                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync($"https://www.googleapis.com/oauth2/v3/userinfo?access_token={request.AccessToken}");
                
                if (!response.IsSuccessStatusCode)
                    return BadRequest(new { message = "Google token geçersiz veya süresi dolmuş." });

                var content = await response.Content.ReadAsStringAsync();
                
                // Deserialize işlemi (null gelme ihtimaline karşı kontrol)
                var googleUser = JsonConvert.DeserializeObject<GoogleUserInfo>(content);
                if (googleUser == null || string.IsNullOrEmpty(googleUser.Email))
                    return BadRequest(new { message = "Google kullanıcı bilgileri alınamadı." });

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == googleUser.Email);

                if (user == null)
                {
                    user = new User
                    {
                        FullName = googleUser.Name ?? "Google User",
                        Email = googleUser.Email,
                        Credits = 10,
                        Role = "User",
                        PackageType = "Free",
                        CreatedAt = DateTime.UtcNow,
                        PasswordHash = "GOOGLE_USER" 
                    };
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }

                string mySystemToken = _jwtService.GenerateToken(user); 

                return Ok(new 
                { 
                    token = mySystemToken, 
                    fullName = user.FullName,
                    email = user.Email,
                    credits = user.Credits,
                    role = user.Role,
                    packageType = user.PackageType
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sunucu hatası: " + ex.Message });
            }
        }
    }

    // Helper sınıfları null uyarılarını engellemek için "?" ile güncelledik
    public class GoogleLoginRequest { public string? AccessToken { get; set; } }
    public class GoogleUserInfo 
    { 
        public string? Email { get; set; } 
        public string? Name { get; set; } 
        public string? Picture { get; set; } 
    }
}