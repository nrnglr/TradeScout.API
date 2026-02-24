using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TradeScout.API.Data;
using TradeScout.API.Services;

var builder = WebApplication.CreateBuilder(args);

// ===== LOAD ENVIRONMENT VARIABLES =====
// Load .env file for Development (manual parsing)
if (builder.Environment.IsDevelopment())
{
    // Try multiple paths to find .env file
    var possiblePaths = new[]
    {
        Path.Combine(AppContext.BaseDirectory, "..", "..", ".env"), // From bin/Debug/net9.0
        Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"), // From TradeScout.API folder
        Path.Combine(Directory.GetCurrentDirectory(), ".env") // In TradeScout.API folder
    };

    string? envPath = null;
    foreach (var path in possiblePaths)
    {
        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath))
        {
            envPath = fullPath;
            Console.WriteLine($"✅ .env dosyası bulundu: {fullPath}");
            break;
        }
    }

    if (envPath != null && File.Exists(envPath))
    {
        foreach (var line in File.ReadAllLines(envPath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            var parts = line.Split('=');
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                var value = parts[1].Trim();
                Environment.SetEnvironmentVariable(key, value);
                
                // Debug log
                if (key == "GEMINI_API_KEY")
                {
                    Console.WriteLine($"✅ GEMINI_API_KEY yüklendi: {(string.IsNullOrEmpty(value) ? "(boş)" : "****")}");
                    Console.WriteLine($"   Kontrol: Environment.GetEnvironmentVariable('GEMINI_API_KEY') = {(Environment.GetEnvironmentVariable("GEMINI_API_KEY")?.Length ?? 0)} chars");
                }
            }
        }
    }
    else
    {
        Console.WriteLine("⚠️ .env dosyası bulunamadı");
    }
}

// ===== POSTGRESQL CONFIGURATION =====
// Configure PostgreSQL connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Log connection string (mask password)
var maskedConnectionString = System.Text.RegularExpressions.Regex.Replace(
    connectionString, 
    @"Password=[^;]*", 
    "Password=****"
);
Console.WriteLine($"📦 Database Connection: {maskedConnectionString}");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    
    // ⚠️ Detaylı logging sadece Development'ta
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging(); // Shows parameter values
        options.EnableDetailedErrors(); // Shows detailed error information
    }
});

// ===== JWT AUTHENTICATION CONFIGURATION =====
// Get JWT settings from configuration
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] 
    ?? throw new InvalidOperationException("JWT SecretKey is not configured");
var issuer = jwtSettings["Issuer"] 
    ?? throw new InvalidOperationException("JWT Issuer is not configured");
var audience = jwtSettings["Audience"] 
    ?? throw new InvalidOperationException("JWT Audience is not configured");

// Configure JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false; // Development için false, Production'da true olmalı
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero, // Token süre toleransı yok
        
        // ÖNEMLİ: Claim type mapping'i kapat
        // .NET Core claim type'ları otomatik dönüştürüyor, bunu engelleyelim
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role
    };
    
    // Claim mapping'i tamamen kapat
    options.MapInboundClaims = false;
});

builder.Services.AddAuthorization();

// ===== CORS CONFIGURATION =====
// Configure CORS to allow React frontend (Development + Production)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy
            .WithOrigins(
                // Development - localhost
                "http://localhost:3000",      // React development
                "http://127.0.0.1:3000",      // React development (127.0.0.1 variant)
                "http://localhost:5173",      // Vite development
                "http://127.0.0.1:5173",      // Vite development (127.0.0.1 variant)
                "http://localhost:4200",      // Angular development
                "http://127.0.0.1:4200",      // Angular development (127.0.0.1 variant)
                "http://localhost:3001",      // Alternatif React port
                "http://127.0.0.1:3001",      // Alternatif React port (127.0.0.1 variant)
                // Production
                "https://fgstrade.com",       // Production HTTPS
                "http://fgstrade.com",        // Production HTTP
                "https://www.fgstrade.com",   // Production HTTPS with www
                "http://www.fgstrade.com"     // Production HTTP with www
            )
            .AllowAnyMethod()                 // GET, POST, PUT, DELETE, OPTIONS, etc.
            .AllowAnyHeader()                 // Content-Type, Authorization, etc.
            .AllowCredentials()               // Allow cookies and credentials
            .WithExposedHeaders("Content-Disposition"); // For file downloads
    });
});

// ===== PROXY CONFIGURATION =====
// Configure Proxy Settings
builder.Services.Configure<TradeScout.API.Models.ProxySettings>(
    builder.Configuration.GetSection("ProxySettings"));

// ===== SERVICE REGISTRATIONS =====
// Register JWT Service
builder.Services.AddScoped<IJwtService, JwtService>();

// Register Email Service
builder.Services.AddScoped<IEmailService, SmtpEmailService>();

// Register Proxy Manager
builder.Services.AddSingleton<ProxyManager>();

// Register HttpClient for Gemini AI
builder.Services.AddHttpClient();

// Register Scraper Services
builder.Services.AddScoped<IGoogleMapsScraperService, GoogleMapsScraperService>();
builder.Services.AddScoped<IParallelGoogleMapsScraperService, ParallelGoogleMapsScraperService>();
builder.Services.AddScoped<IGeminiSearchService, GeminiSearchService>(); // ✨ Gemini AI Service
builder.Services.AddScoped<IExcelExportService, ExcelExportService>();

// ===== CONTROLLER CONFIGURATION =====
builder.Services.AddControllers();

// ===== SWAGGER/OPENAPI CONFIGURATION =====
// Geçici olarak devre dışı - Swagger paket uyumsuzluğu
// builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen();

// ===== BUILD APPLICATION =====
var app = builder.Build();

// ===== MIDDLEWARE PIPELINE =====
// Configure the HTTP request pipeline
// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI(options =>
//     {
//         options.SwaggerEndpoint("/swagger/v1/swagger.json", "TradeScout API v1");
//         options.RoutePrefix = string.Empty; // Swagger UI'ı root'ta aç (http://localhost:5000)
//     });
// }

// ⚠️ Exception Handling - CORS'tan bile önce (en kritik)
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// ⚠️ CORS middleware - Hataların CORS başlıkları kaybetmemesi için erken konumlandı
app.UseCors("AllowReactApp");

// HTTPS yönlendirmesi (Production'da aktif olmalı)
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

// Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// ===== API ENDPOINTS =====
// Root health check endpoint
app.MapGet("/", () => new
{
    status = "ok",
    message = "TradeScout API is running",
    version = "1.0.0",
    timestamp = DateTime.UtcNow,
    endpoints = new
    {
        auth = "/api/auth",
        scraper = "/api/scraper",
        proxy = "/api/proxy"
    }
}).WithTags("Health").AllowAnonymous();

app.MapControllers();

// ===== RUN APPLICATION =====
app.Run();
