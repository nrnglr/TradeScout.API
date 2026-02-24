# 🚀 Production Deployment Checklist

**Son Güncelleme:** 24 Şubat 2026  
**Durum:** Hazırlanıyor

---

## 📋 Dağıtım Öncesi Kontrol Listesi

### ✅ 1. Database Bağlantısı

- [ ] **Host Adresini Değiştir**
  ```json
  // appsettings.json & appsettings.Production.json
  "DefaultConnection": "Host=PRODUCTION_DB_HOST;Port=5432;Database=fgstrade;Username=PRODUCTION_USER;Password=PRODUCTION_STRONG_PASSWORD"
  ```
  - Şimki: `localhost`
  - Production: Production sunucusu IP/Domain

- [ ] **Database Kullanıcı ve Şifre Güncelle**
  - Güçlü ve karmaşık şifre kullan
  - Şifre: 16+ karakter, büyük/küçük/sayı/özel karakter içermeli

- [ ] **Database Migrations Çalıştır**
  ```bash
  dotnet ef database update --configuration Release
  ```

---

### ✅ 2. Security - JWT Settings

- [ ] **Secret Key Değiştir** (ÇOK ÖNEMLİ!)
  ```json
  "JwtSettings": {
    "SecretKey": "RANDOM_PRODUCTION_SECRET_KEY_MIN_32_CHARS",
    "Issuer": "TradeScout.API",
    "Audience": "TradeScout.Client",
    "ExpirationMinutes": "1440"
  }
  ```
  - Yeni secret key oluştur: `openssl rand -base64 32`
  - Şimki: Test key
  - Eski test key'leri geçersiz kıl

- [ ] **Secret Key'i Environment Variable'da Sakla**
  ```bash
  export JWT_SECRET="your_generated_secret_here"
  ```

---

### ✅ 3. API Keys

- [ ] **Gemini API Key Ekle**
  ```json
  "GeminiSettings": {
    "ApiKey": "YOUR_REAL_GEMINI_API_KEY"
  }
  ```
  - `.env` dosyasına ekle
  - `.gitignore`'a ekle (commit etme!)

- [ ] **Tüm Secret'ları Environment Variables'a Taşı**
  ```bash
  # appsettings.json içinde:
  "ApiKey": "${GEMINI_API_KEY}"
  ```

---

### ✅ 4. Email Konfigürasyonu

- [ ] **SMTP Ayarlarını Güncelle**
  ```json
  "EmailSettings": {
    "SmtpHost": "PRODUCTION_MAIL_SERVER",
    "SmtpPort": "587",
    "SmtpUsername": "PRODUCTION_EMAIL@yourdomain.com",
    "SmtpPassword": "PRODUCTION_SMTP_PASSWORD",
    "FromEmail": "noreply@yourdomain.com",
    "FromName": "TradeScout",
    "AdminEmail": "admin@yourdomain.com"
  }
  ```
  - Şimki: FGSTrade test sunucusu
  - Üretim: Gerçek mail sunucusu

- [ ] **Email Test Gönder**
  - Admin email'e test email gönder
  - Gelen kutusu ve spam klasörünü kontrol et

---

### ✅ 5. Proxy Settings

- [ ] **Test Proxy'leri Kontrol Et**
  ```json
  "ProxySettings": {
    "EnableProxy": true/false,  // İhtiyaca göre
    "TestProxiesOnStartup": false,
    "Proxies": [
      // Production proxy'leri ekle
    ]
  }
  ```

- [ ] **Proxy Testi Kapat**
  ```json
  "TestProxiesOnStartup": false  // Performance için
  ```

---

### ✅ 6. Logging Ayarları

- [ ] **Log Level'ı Minimize Et**
  ```json
  "Logging": {
    "LogLevel": {
      "Default": "Warning",  // Information → Warning
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"  // Information → Warning
    }
  }
  ```

- [ ] **Sensitive Data Logging'i Kapat**
  ```csharp
  // Program.cs içinde BU satırları KALDIR veya COMMENT ET:
  // .LogTo(Console.WriteLine, LogLevel.Information)
  // .EnableSensitiveDataLogging()  // ❌ PRODUCTION'DA KAPALI
  ```

- [ ] **Log Dosyalarını Harici Sistemde Sakla**
  - Application Insights
  - ELK Stack
  - Serilog + dosya sistemi

---

### ✅ 7. CORS Konfigürasyonu

- [ ] **Localhost'tan Uzaklaş**
  ```csharp
  // Program.cs içinde:
  options.AddPolicy("AllowProduction", policy =>
  {
      policy.WithOrigins(
          "https://yourdomain.com",
          "https://www.yourdomain.com"
      )
      .AllowAnyMethod()
      .AllowAnyHeader()
      .AllowCredentials();
  });
  ```

- [ ] **Frontend URL'ini Güncelle**
  - Şimki: `http://localhost:3000`
  - Production: `https://yourdomain.com`

- [ ] **CORS Test Et**
  ```bash
  curl -H "Origin: https://yourdomain.com" \
       -H "Access-Control-Request-Method: POST" \
       https://api.yourdomain.com/api/auth/login -v
  ```

---

### ✅ 8. HTTPS & SSL

- [ ] **SSL Sertifikası Yükle**
  - Let's Encrypt kullan (ücretsiz)
  - veya DigiCert/Comodo

- [ ] **HTTPS Yönlendirmesini Aktif Et**
  ```csharp
  // Program.cs
  app.UseHttpsRedirection();
  app.UseHsts();  // HSTS header ekle
  ```

- [ ] **Port Ayarlarını Güncelle**
  - HTTP: 80 → HTTPS: 443
  - launchSettings.json'ı kontrol et

---

### ✅ 9. Frontend Konfigürasyonu

- [ ] **API Base URL'ini Güncelle** (`/tradescout/src/services/api.ts`)
  ```typescript
  // PRODUCTION:
  const API_BASE_URL = "https://api.yourdomain.com"
  
  // Development:
  const API_BASE_URL = "http://localhost:5100"
  ```

- [ ] **Environment'a Göre Ayır**
  ```typescript
  const API_BASE_URL = process.env.REACT_APP_API_URL || "http://localhost:5100"
  ```

- [ ] **.env.production Dosyası Oluştur**
  ```
  REACT_APP_API_URL=https://api.yourdomain.com
  REACT_APP_ENV=production
  ```

---

### ✅ 10. Performance Optimizasyonu

- [ ] **Entity Framework Query Optimization**
  - N+1 query problemi kontrol et
  - `.Include()` kullan
  - `.Select()` ile sadece gerekli alanları al

- [ ] **Caching Ekle** (Redis)
  ```csharp
  // Sık sorgulan verileri cache'le
  services.AddStackExchangeRedisCache(options =>
  {
      options.Configuration = connectionString;
  });
  ```

- [ ] **Database Index'leri Kontrol Et**
  ```sql
  -- User tablosunda index kontrol et
  CREATE INDEX idx_email ON Users(Email);
  ```

- [ ] **Connection Pool Ayarlarını Optimize Et**
  ```json
  "DefaultConnection": "Host=...;MaxPoolSize=50;MinPoolSize=10;"
  ```

---

### ✅ 11. Backup & Disaster Recovery

- [ ] **Günlük Database Backup Planı Yap**
  ```bash
  # PostgreSQL backup script
  pg_dump -h PROD_HOST -U PROD_USER -d fgstrade > backup_$(date +%Y%m%d).sql
  ```

- [ ] **Backup'ları Başka Sunucuya Sakla**
  - AWS S3
  - Azure Blob Storage
  - Google Cloud Storage

- [ ] **Restore Test Yap**
  - Gerçek verileri staging'e geri yükle
  - Tüm işlemlerin çalıştığını doğrula

---

### ✅ 12. Monitoring & Alerting

- [ ] **Health Check Endpoint'i Aktif Et**
  ```csharp
  app.MapHealthChecks("/health");
  ```

- [ ] **Monitoring Tool Kur**
  - Application Insights
  - New Relic
  - Datadog

- [ ] **Alert'ler Konfigüre Et**
  - High CPU usage
  - Database connection errors
  - API response time > 2 seconds
  - Email send failures

- [ ] **Error Tracking Aktif Et**
  - Sentry
  - Rollbar
  - Bugsnag

---

### ✅ 13. Load Testing

- [ ] **Load Test Çalıştır**
  ```bash
  # Apache JMeter veya k6 kullan
  k6 run load_test.js
  ```

- [ ] **Expected Load Belirle**
  - Concurrent users: ?
  - Requests per second: ?
  - Peak hours: ?

- [ ] **Bottleneck'leri Belirle ve Düzelt**

---

### ✅ 14. Security Audit

- [ ] **SQL Injection Test Et**
  - Tüm input'ları validation yap
  - Parameterized queries kullan (zaten yapıldı)

- [ ] **CORS Headers Kontrol Et**
  ```bash
  curl -I https://api.yourdomain.com/api/auth/login
  ```

- [ ] **API Rate Limiting Ekle**
  ```csharp
  services.AddRateLimiting(options =>
  {
      options.AddFixedWindowLimiter("api", config =>
      {
          config.PermitLimit = 100;
          config.Window = TimeSpan.FromMinutes(1);
      });
  });
  ```

- [ ] **Password Security**
  - BCrypt zaten kullanılıyor ✅
  - Min 8 karakter, strong password policy

- [ ] **JWT Token Security**
  - Token expiration: 1440 dakika (24 saat) kontrol et
  - Refresh token mekanizması ekle (isteğe bağlı)

---

### ✅ 15. Documentation & Runbook

- [ ] **API Documentation Güncelle**
  - Swagger/OpenAPI documentation
  - Base URL'yi production'a ayarla

- [ ] **Deployment Runbook Oluştur**
  - Step-by-step deployment prosedürü
  - Rollback prosedürü
  - Emergency kontakt bilgileri

- [ ] **Known Issues & Solutions Dokümante Et**

---

### ✅ 16. Pre-Deployment Final Checks

- [ ] **Git Repository Temiz**
  ```bash
  git status  # Clean working directory
  git log --oneline -5  # Son commitleri kontrol et
  ```

- [ ] **Tüm Tests Pass**
  ```bash
  dotnet test
  ```

- [ ] **Code Review Tamamlandı**
  - Code quality tools (SonarQube, Code Analysis)
  - Security scanning (WhiteSource, Snyk)

- [ ] **Staging'de Tam Test Yapıldı**
  - Register, Login, Search, Export işlemleri
  - Email gönderimi
  - Proxy kullanımı

- [ ] **Database Backup Alındı**

- [ ] **Rollback Plan Hazır**

---

## 🚨 Deployment Sırasında

### Deployment Script
```bash
#!/bin/bash
set -e

# 1. Güncel kodu çek
git pull origin main

# 2. Dependencies yükle
dotnet restore

# 3. Build et
dotnet build -c Release

# 4. Database migrations çalıştır
dotnet ef database update --configuration Release

# 5. Tests çalıştır (isteğe bağlı)
# dotnet test

# 6. Publish et
dotnet publish -c Release -o ./publish

# 7. Eski versiyonu yedekle
cp -r /app /app.backup.$(date +%Y%m%d_%H%M%S)

# 8. Yeni versiyonu deploy et
cp -r ./publish/* /app/

# 9. Servisi restart et
sudo systemctl restart tradescout-api

# 10. Health check
sleep 5
curl https://api.yourdomain.com/health

echo "✅ Deployment başarılı!"
```

---

## ✅ Post-Deployment Checklist

- [ ] **Frontend bağlantısını test et**
  - Login yapabilir mi?
  - Search yapabilir mi?
  - Export yapabilir mi?

- [ ] **Email notification'ları test et**
  - Registration email
  - Password reset email
  - Admin notifications

- [ ] **Database'i kontrol et**
  - Tables oluşturuldu mu?
  - Migrations başarılı mı?
  - Data integrity OK mi?

- [ ] **Logs'ları kontrol et**
  - Hata var mı?
  - Performance iyi mi?
  - Suspicious activity var mı?

- [ ] **Monitoring aktif mi?**
  - Health checks çalışıyor mu?
  - Alerts gönderiliyor mu?

- [ ] **Backup'lar alınıyor mu?**

- [ ] **Users'a duyur**
  - Sistem aktif ve canlı
  - Release notes paylaş

---

## 🆘 Acil Durum Prosedürü

### Immediate Issues Çözümü

1. **Database Connection Hatası**
   ```bash
   # Connection string kontrol et
   # Database sunucusunun çalışıp çalışmadığını kontrol et
   psql -h PROD_HOST -U PROD_USER -d fgstrade
   ```

2. **API Timeout/Crash**
   ```bash
   # Logs'ları kontrol et
   journalctl -u tradescout-api -f
   
   # Memory/CPU kontrol et
   top
   ```

3. **Email Gönderilmiyor**
   ```bash
   # SMTP ayarlarını kontrol et
   # Mail log'ları kontrol et
   tail -f /var/log/mail.log
   ```

4. **Quick Rollback**
   ```bash
   cd /app.backup.YYYYMMDD_HHMMSS
   cp -r . /app/
   sudo systemctl restart tradescout-api
   ```

---

## 📞 İletişim & Supportlar

| Sorun | Kişi | Phone | Email |
|-------|------|-------|-------|
| Database | DBA | +90... | dba@... |
| Infrastructure | DevOps | +90... | devops@... |
| Application | Dev Lead | +90... | lead@... |
| Emergency | CTO | +90... | cto@... |

---

## 📝 Deployment Notları

```
Deployment Tarihi: ___________
Kimin Tarafından: ___________
Başlangıç Saati: ___________
Bitiş Saati: ___________
Sonuç: ✅ Başarılı / ❌ Başarısız

Notlar:
_________________________________
_________________________________
_________________________________
```

---

**Not:** Bu checklist'i her deployment'dan önce kullan ve tamamlandığını işaretle!
