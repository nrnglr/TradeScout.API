# TradeScout API - Authentication & User Management
cd /Users/nuranguler/Desktop/TradeScout/FGS_APİ/TradeScout.API && dotnet run
TradeScout SaaS platformu için geliştirilmiş profesyonel .NET 9 Web API. Bu proje JWT tabanlı kimlik doğrulama, kullanıcı yönetimi ve PostgreSQL veritabanı entegrasyonu içerir.

## 🚀 Teknoloji Stack

- **.NET 9.0** - Web API Framework
- **PostgreSQL** - İlişkisel Veritabanı
- **Entity Framework Core 9.0** - ORM (Code-First Yaklaşımı)
- **BCrypt.Net-Next** - Şifre Hashleme
- **JWT Bearer Authentication** - Token Tabanlı Kimlik Doğrulama
- **Swagger/OpenAPI** - API Dokümantasyonu

## 📋 Özellikler

- ✅ Health Check Endpoint (API Durum Kontrolü)
- ✅ Kullanıcı Kaydı (Register)
- ✅ Kullanıcı Girişi (Login)
- ✅ JWT Token Üretimi
- ✅ BCrypt ile Şifre Güvenliği
- ✅ Email Unique Validation
- ✅ CORS Desteği (React/Frontend için)
- ✅ Swagger UI Entegrasyonu
- ✅ Profesyonel Hata Yönetimi
- ✅ Code-First Database Migration

## 🗂️ Proje Yapısı

```
TradeScout.API/
├── Controllers/
│   └── AuthController.cs          # Kimlik doğrulama endpoint'leri
├── Data/
│   └── ApplicationDbContext.cs    # EF Core Database Context
├── DTOs/
│   ├── RegisterDto.cs             # Kayıt DTO'su
│   ├── LoginDto.cs                # Giriş DTO'su
│   └── AuthResponseDto.cs         # Kimlik doğrulama yanıt DTO'su
├── Models/
│   └── User.cs                    # Kullanıcı Entity
├── Services/
│   └── JwtService.cs              # JWT Token Üretim Servisi
├── Program.cs                     # Uygulama yapılandırması
└── appsettings.json              # Konfigürasyon ayarları
```

## ⚙️ Kurulum

### 1. Gereksinimler

- .NET 9.0 SDK
- PostgreSQL 14+ (veya Docker ile PostgreSQL)
- IDE: Visual Studio 2022, VS Code veya Rider

### 2. PostgreSQL Veritabanı Kurulumu

#### Docker ile PostgreSQL (Önerilen):

```bash
docker run --name tradescout-postgres \
  -e POSTGRES_PASSWORD=your_password_here \
  -e POSTGRES_DB=tradescout_dev_db \
  -p 5432:5432 \
  -d postgres:16
```

#### Manuel PostgreSQL Kurulumu:
PostgreSQL'i [resmi websiteden](https://www.postgresql.org/download/) indirip kurun ve bir veritabanı oluşturun:

```sql
CREATE DATABASE tradescout_dev_db;
```

### 3. Projeyi Klonlayın ve Bağımlılıkları Yükleyin

```bash
cd TradeScout.API
dotnet restore
```

### 4. Veritabanı Bağlantısını Yapılandırın

`appsettings.Development.json` dosyasını açın ve PostgreSQL bağlantı bilgilerinizi güncelleyin:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=tradescout_dev_db;Username=postgres;Password=your_password_here"
  }
}
```

### 5. Database Migration'ı Çalıştırın

```bash
# Migration oluştur
dotnet ef migrations add InitialCreate

# Veritabanını oluştur/güncelle
dotnet ef database update
```

### 6. Uygulamayı Çalıştırın

```bash
dotnet run
```

Uygulama varsayılan olarak şu adreste çalışacak:
- **HTTP:** http://localhost:5000
- **HTTPS:** https://localhost:5001
- **Swagger UI:** http://localhost:5000/

## 📡 API Endpoints

### Health Check

#### GET / (Root Endpoint)
API durumunu kontrol etmek için kullanılır.

```http
GET /
```

**Yanıt (200 OK):**
```json
{
  "status": "ok",
  "message": "TradeScout API is running",
  "version": "1.0.0",
  "timestamp": "2026-02-07T17:52:36.411581Z",
  "endpoints": {
    "auth": "/api/auth",
    "scraper": "/api/scraper"
  }
}
```

Daha fazla bilgi için: [HEALTH_CHECK.md](HEALTH_CHECK.md)

### Authentication Endpoints

#### 1. Register (Kayıt Ol)
```http
POST /api/auth/register
Content-Type: application/json

{
  "fullName": "Ahmet Yılmaz",
  "email": "ahmet@example.com",
  "password": "Sifre123!",
  "companyName": "ABC Tech"
}
```

**Başarılı Yanıt (201 Created):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "fullName": "Ahmet Yılmaz",
  "email": "ahmet@example.com",
  "credits": 5,
  "role": "User",
  "packageType": "Free"
}
```

#### 2. Login (Giriş Yap)
```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "ahmet@example.com",
  "password": "Sifre123!"
}
```

**Başarılı Yanıt (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "fullName": "Ahmet Yılmaz",
  "email": "ahmet@example.com",
  "credits": 5,
  "role": "User",
  "packageType": "Free"
}
```

### Health Check Endpoint

#### 1. API Durum Kontrolü
```http
GET /api/health
```

**Başarılı Yanıt (200 OK):**
```json
{
  "status": "Healthy"
}
```

## 🔐 JWT Token Kullanımı

API'den aldığınız token'ı diğer endpoint'lerde kullanmak için Authorization header'ına ekleyin:

```http
GET /api/protected-endpoint
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Frontend'de Token Kullanımı (JavaScript/React):

```javascript
// Login işlemi
const response = await fetch('http://localhost:5000/api/auth/login', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
  },
  body: JSON.stringify({
    email: 'ahmet@example.com',
    password: 'Sifre123!'
  })
});

const data = await response.json();
// Token'ı localStorage'a kaydet
localStorage.setItem('token', data.token);

// Token ile korumalı endpoint'e istek
const protectedResponse = await fetch('http://localhost:5000/api/protected-endpoint', {
  headers: {
    'Authorization': `Bearer ${localStorage.getItem('token')}`
  }
});
```

## 🗄️ Veritabanı Şeması

### Users Tablosu

| Sütun         | Tip          | Açıklama                          | Varsayılan |
|---------------|--------------|-----------------------------------|------------|
| Id            | int (PK)     | Kullanıcı ID (Auto Increment)     | -          |
| FullName      | string(100)  | Kullanıcının tam adı              | -          |
| Email         | string(150)  | Email (Unique)                    | -          |
| PasswordHash  | string(255)  | BCrypt ile hashlenmiş şifre       | -          |
| CompanyName   | string(100)? | Şirket adı (Opsiyonel)            | null       |
| Credits       | int          | Kullanıcı kredisi                 | 5          |
| PackageType   | string(50)   | Paket tipi                        | "Free"     |
| Role          | string(50)   | Kullanıcı rolü                    | "User"     |
| CreatedAt     | DateTime     | Kayıt tarihi                      | NOW()      |

## 🛡️ Güvenlik

- ✅ Şifreler BCrypt ile hashlenmiş
- ✅ JWT token'lar HMAC-SHA256 ile imzalanmış
- ✅ Email validation
- ✅ Password minimum uzunluk kontrolü
- ✅ CORS politikaları tanımlı
- ✅ HTTPS desteği

## 🔧 Konfigürasyon

### JWT Ayarları

`appsettings.json` içinde:

```json
{
  "JwtSettings": {
    "SecretKey": "TradeScout_Super_Secret_Key_2024_MinLength32Characters!",
    "Issuer": "TradeScout.API",
    "Audience": "TradeScout.Client",
    "ExpirationMinutes": "1440"
  }
}
```

**Önemli:** Production ortamında `SecretKey`'i mutlaka değiştirin ve environment variable olarak saklayın!

### CORS Ayarları

`Program.cs` içinde tanımlı CORS politikası:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",      // React
                "http://localhost:5173",      // Vite
                "http://localhost:4200"       // Angular
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});
```

## 🧪 Test Etme

### cURL ile Test

```bash
# Register
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"fullName":"Test User","email":"test@example.com","password":"Test123!","companyName":"Test Corp"}'

# Login
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!"}'
```

### Postman Collection

Swagger UI'da tüm endpoint'leri test edebilir veya Postman collection oluşturabilirsiniz.

## 📝 EF Core Migration Komutları

```bash
# Yeni migration oluştur
dotnet ef migrations add MigrationName

# Veritabanını güncelle
dotnet ef database update

# Son migration'ı geri al
dotnet ef migrations remove

# Belirli bir migration'a geri dön
dotnet ef database update PreviousMigrationName

# Migration listesini görüntüle
dotnet ef migrations list
```

## 🚀 Production'a Deployment

### 1. Environment Variables (Önerilen)

Production'da hassas bilgileri environment variable olarak saklayın:

```bash
export ConnectionStrings__DefaultConnection="Host=prod-server;Database=tradescout_prod;..."
export JwtSettings__SecretKey="your-super-secret-production-key"
```

### 2. appsettings.Production.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=prod-server;Port=5432;Database=tradescout_prod;..."
  },
  "JwtSettings": {
    "SecretKey": "${JWT_SECRET_KEY}",
    "ExpirationMinutes": "60"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### 3. Publish

```bash
dotnet publish -c Release -o ./publish
```

## � Additional Documentation

For more detailed information, please refer to these documents:

- **[API_REFERENCE.md](API_REFERENCE.md)** - Quick reference guide for all API endpoints
- **[HEALTH_CHECK.md](HEALTH_CHECK.md)** - Health check endpoint documentation
- **[SCRAPER_README.md](SCRAPER_README.md)** - Google Maps scraper implementation details
- **[FRONTEND_KULLANIM.md](FRONTEND_KULLANIM.md)** - Frontend integration guide (React/Next.js)
- **[KURULUM.md](KURULUM.md)** - Detailed installation instructions (Turkish)
- **[PROJECT_SUMMARY.md](PROJECT_SUMMARY.md)** - Overall project summary

## �📞 İletişim ve Destek

Sorularınız veya önerileriniz için:
- Email: support@tradescout.com
- GitHub Issues: [Create an issue]

## 📄 Lisans

Bu proje TradeScout tarafından geliştirilmiştir. Tüm hakları saklıdır.

---

**Geliştirici Notları:**

- ⚠️ Production ortamında `appsettings.json` içindeki hassas bilgileri mutlaka değiştirin
- ⚠️ PostgreSQL şifrenizi güçlü tutun
- ⚠️ JWT SecretKey minimum 32 karakter olmalı
- ⚠️ HTTPS'i production'da aktif edin (`RequireHttpsMetadata = true`)
- ✅ Migration'ları source control'e ekleyin
- ✅ Düzenli olarak NuGet paketlerini güncelleyin

**Happy Coding! 🎉**
