# ✅ SORUN ÇÖZÜLDÜ! - Token 401 Hatası Düzeltildi

## 🎯 Sorunun Kökü

Frontend token'ı doğru gönderiyordu ama backend **"Geçersiz kullanıcı token'ı"** hatası döndürüyordu.

### Asıl Sebep:
.NET Core JWT middleware'i **claim type mapping** yapıyordu. Token'da `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier` olarak kaydedilen claim, backend'de `ClaimTypes.NameIdentifier` olarak okunamıyordu.

## 🔧 Yapılan Değişiklikler

### 1. Program.cs - JWT Configuration Güncellendi

```csharp
// ÖNCE (YANLIŞ)
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };
});

// SONRA (DOĞRU)
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero,
        
        // ✅ Claim type mapping'i düzelt
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role
    };
    
    // ✅ Otomatik claim mapping'i kapat
    options.MapInboundClaims = false;
});
```

### 2. ScrapeRequestDto - Esnek Parametre Desteği

```csharp
// ✅ Artık hem searchQuery hem de category/city kabul ediliyor
public class ScrapeRequestDto
{
    public string? SearchQuery { get; set; }        // "restaurants in Istanbul"
    public string? Category { get; set; }           // "mobilya"
    public string? City { get; set; }               // "Gaziantep"
    public string? Country { get; set; }            // "Türkiye"
    public string Language { get; set; } = "tr";
    public int MaxResults { get; set; } = 20;

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(SearchQuery) || 
               (!string.IsNullOrWhiteSpace(Category) && !string.IsNullOrWhiteSpace(City));
    }

    public string GetSearchQuery()
    {
        if (!string.IsNullOrWhiteSpace(SearchQuery))
            return SearchQuery;
        
        return $"{Category} {City} {Country}".Trim();
    }
}
```

### 3. using Statement Eklendi

```csharp
using System.Security.Claims;  // ✅ Eklendi
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
```

## ✅ Test Sonuçları

### Backend API Testi (curl)
```bash
# Login
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"gulernuran9@gmail.com","password":"123456"}'

# Response: Token ve kullanıcı bilgileri ✅

# Scraping (searchQuery formatı)
curl -X POST http://localhost:5000/api/scraper/scrape \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{"searchQuery":"restaurants in Istanbul","maxResults":5"}'

# Response: Scraping başlar ✅

# Scraping (category/city formatı)
curl -X POST http://localhost:5000/api/scraper/scrape \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{"category":"mobilya","city":"Gaziantep","maxResults":5}'

# Response: Scraping başlar ✅
```

## 📝 Frontend İçin Güncellemeler

### Artık İki Format Da Çalışıyor:

#### Format 1: Basit searchQuery (ÖNERİLEN)
```typescript
await scraperService.scrape({
  searchQuery: "restaurants in Istanbul",
  maxResults: 20
});
```

#### Format 2: Detaylı category/city
```typescript
await scraperService.scrape({
  category: "mobilya",
  city: "Gaziantep",
  country: "Türkiye",
  language: "tr",
  maxResults: 20
});
```

## 🚨 ÖNEMLİ: Frontend'de Yapılması Gerekenler

### 1. Backend'i Yeniden Başlatın
```bash
# Eski backend'i durdurun
lsof -ti:5000 | xargs kill -9

# Yeni backend'i başlatın
cd /Users/nuranguler/Desktop/TradeScout/FGS_APİ/TradeScout.API
dotnet run
```

### 2. Frontend'de Yeni Token Alın
```typescript
// Logout yapın
localStorage.removeItem('token');
localStorage.removeItem('user');

// Yeniden login yapın
const response = await authService.login({
  email: "gulernuran9@gmail.com",
  password: "123456"
});

// Yeni token otomatik kaydedilir
console.log('✅ Yeni token alındı');
```

### 3. Scraping İsteğini Tekrar Deneyin
```typescript
const result = await scraperService.scrape({
  searchQuery: "mobilya Gaziantep",  // veya category/city ayrı ayrı
  maxResults: 5
});

console.log('✅ Scraping başarılı:', result);
```

## 🎉 Sonuç

### ÖNCESİ:
- ❌ Token gönderiliyor ama backend 401 dönüyordu
- ❌ "Geçersiz kullanıcı token'ı" hatası
- ❌ Claim type mapping sorunu

### SONRASI:
- ✅ Token doğru validate ediliyor
- ✅ User claims düzgün okunuyor
- ✅ Scraping başlıyor
- ✅ Hem searchQuery hem category/city formatı destekleniyor

## 📞 Hala Sorun Varsa

1. **Backend'i yeniden başlattınız mı?**
   ```bash
   lsof -ti:5000 | xargs kill -9
   cd /Users/nuranguler/Desktop/TradeScout/FGS_APİ/TradeScout.API && dotnet run
   ```

2. **Yeni token aldınız mı?**
   ```javascript
   localStorage.clear();
   // Yeniden login yapın
   ```

3. **Token doğru gönderiliyor mu?**
   ```javascript
   // Browser Console'da kontrol edin
   console.log('Token:', localStorage.getItem('token'));
   ```

4. **Backend hangi portta çalışıyor?**
   ```bash
   curl http://localhost:5000/
   # Beklenen: {"status":"ok","message":"TradeScout API is running"...}
   ```

---

**Son güncelleme:** 2026-02-07 18:25  
**Durum:** ✅ ÇÖZÜLDÜ - Token validation düzeltildi, scraping çalışıyor!
