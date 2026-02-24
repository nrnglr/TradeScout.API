# 🚀 PRODUCTION'A ATARKEN DEĞİŞTİRİLMESİ GEREKEN ADIMLAR

## ✅ Yapılan Değişiklikler

### 1. appsettings.json
- ✅ Database Connection: `localhost` → `PRODUCTION_DB_HOST`
- ✅ JWT Secret: Test key → `PRODUCTION_JWT_SECRET_KEY_MIN_32_CHARS_RANDOM_GENERATED`
- ✅ Logging Level: `Information` → `Warning`

### 2. appsettings.Production.json (YENİ)
- ✅ Tüm production ayarları hazır
- ✅ Placeholder'lar konulmuş (değiştirilmesi gereken değerler)

### 3. Program.cs
- ✅ CORS otomatik olarak environment'a göre değişiyor
  - Development: localhost'a açık
  - Production: PRODUCTION_DOMAIN'a açık

### 4. Frontend .env.production
- ✅ API URL: `https://api.PRODUCTION_DOMAIN` (değiştirilmesi gerekiyor)

---

## 🔴 HEMEN DEĞİŞTİRİLMESİ GEREKEN ŞEYLER

### Backend Ayarları (appsettings.json & appsettings.Production.json)

Aşağıdaki placeholder'ları **GERÇEK değerlerle değiştir:**

```bash
# Database
PRODUCTION_DB_HOST = 127.0.0.1 (veya IP adres)
PRODUCTION_DB_USER = user
PRODUCTION_DB_PASSWORD = password123

# JWT Secret (ZORUNLU - GÜÇLÜ RANDOM KEY OLMALı)
PRODUCTION_JWT_SECRET_KEY_MIN_32_CHARS_RANDOM_GENERATED = ???

# Gemini API
PRODUCTION_GEMINI_API_KEY = (Google Gemini API Key)

# Email SMTP
PRODUCTION_MAIL_SERVER = mail sunucusu
PRODUCTION_EMAIL@yourdomain.com = email adresi
PRODUCTION_SMTP_PASSWORD = email şifresi

# Proxy (isteğe bağlı)
PRODUCTION_PROXY_1 = proxy IP:PORT

# Frontend Domain
PRODUCTION_DOMAIN = yourdomain.com
```

---

## 📋 PRODUCTION DEPLOYMENT YAPILMASI

### 1. Backend Deployment

```bash
# 1. Production ortamında appsettings.Production.json'u güncelle
# (Tüm placeholder'ları gerçek değerlerle doldur)

# 2. Database'i güncelle
dotnet ef database update --configuration Release

# 3. Build et
dotnet build -c Release

# 4. Publish et
dotnet publish -c Release -o ./publish

# 5. Sunucuya kopyala ve çalıştır
# Veya Docker ile deploy et
```

### 2. Frontend Deployment

```bash
# 1. Production build yap
npm run build

# 2. .env.production'da API URL'ni güncelle
# REACT_APP_API_URL=https://api.yourdomain.com

# 3. Build output'unu sunucuya deploy et
# (Nginx, Apache, CDN vs.)
```

---

## 🔐 ÖNEMLİ GÜVENLİK NOTLARI

- ✅ `appsettings.json` → `.gitignore`'a ekle (production değerleriyle)
- ✅ `appsettings.Production.json` → Çok önemli, güvenli şekilde sakla
- ✅ `.env.production` → `.gitignore`'a ekle
- ✅ JWT Secret → Güçlü ve random olmalı (`openssl rand -base64 32`)
- ✅ Database şifre → Güçlü olmalı (16+ karakter)
- ✅ Proxy şifreler → Production'da farklı olmalı
- ✅ Email şifresi → App password kullan, gerçek password değil

---

## 📝 KONTROL LİSTESİ (Deployment Öncesi)

- [ ] Database bağlantısı test edildi mi?
- [ ] JWT secret key güçlü ve random mi?
- [ ] Email SMTP ayarları doğru mu?
- [ ] Frontend API URL doğru mu?
- [ ] HTTPS/SSL sertifikası yüklü mü?
- [ ] Database backup alındı mı?
- [ ] Tüm tests pass ediyor mu?
- [ ] Staging'de test edildi mi?
- [ ] Production domains .gitignore'da mı?
- [ ] Health check endpoint'i çalışıyor mu?

---

## 🆘 Hızlı Referans

| Dosya | Ne Değiştirilmesi | Durum |
|-------|-----------------|-------|
| appsettings.json | Database, JWT, Logging | ✅ Güncellendi |
| appsettings.Production.json | Production values | ✅ Oluşturuldu |
| Program.cs | CORS (otomatik) | ✅ Otomatik |
| .env.production | API URL | ⚠️ Placeholder var |

---

## 🎯 SONRAKI ADIMLAR

1. **Tüm placeholder'ları gerçek değerlerle doldur**
2. **Production veritabanına migration'ları çalıştır**
3. **Frontend build yap ve deploy et**
4. **Backend deploy et**
5. **Testing yapılsın** (login, search, export)
6. **Monitoring ayarlanmalı** (logs, health check)
7. **Backup prosedürü** hazırlanmalı

---

**Bu dosyayı PRODUCTION_DEPLOYMENT_CHECKLIST.md ile birlikte kullan!**
