using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TradeScout.API.Data;
using TradeScout.API.Services;

var builder = WebApplication.CreateBuilder(args);

// ===== POSTGRESQL CONFIGURATION =====
// Configure PostgreSQL connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

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
        ClockSkew = TimeSpan.Zero // Token süre toleransı yok
    };
});

builder.Services.AddAuthorization();

// ===== CORS CONFIGURATION =====
// Configure CORS to allow React frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",      // React development
                "http://localhost:5173",      // Vite development
                "http://localhost:4200",      // Angular development
                "http://localhost:3001"       // Alternatif React port
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .SetIsOriginAllowedToAllowWildcardSubdomains();
    });
});

// ===== SERVICE REGISTRATIONS =====
// Register JWT Service
builder.Services.AddScoped<IJwtService, JwtService>();

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

// CORS middleware - En üstte olmalı (UseHttpsRedirection'dan önce)
app.UseCors("AllowReactApp");

// Development'da HTTPS yönlendirmeyi kapat
// app.UseHttpsRedirection();

// Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ===== RUN APPLICATION =====
app.Run();
