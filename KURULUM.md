# TradeScout API - Kurulum ve Kullanım Özeti

## ✅ Proje Başarıyla Oluşturuldu!

### 📦 Yüklenen Paketler

1. **Npgsql.EntityFrameworkCore.PostgreSQL** (v9.0.2) - PostgreSQL provider
2. **Microsoft.EntityFrameworkCore.Tools** (v9.0.2) - EF Core migration araçları
3. **BCrypt.Net-Next** (v4.0.3) - Şifre hashleme
4. **Microsoft.AspNetCore.Authentication.JwtBearer** (v9.0.2) - JWT authentication
5. **System.IdentityModel.Tokens.Jwt** (v8.15.0) - JWT token üretimi
6. **Swashbuckle.AspNetCore** (v10.1.0) - Swagger/OpenAPI

### 📁 Oluşturulan Dosyalar

```
TradeScout.API/
├── Controllers/
│   └── AuthController.cs           ✅ Register & Login endpoints
├── Data/
│   └── ApplicationDbContext.cs     ✅ EF Core DbContext
├── DTOs/
│   ├── RegisterDto.cs              ✅ Kayıt DTO
│   ├── LoginDto.cs                 ✅ Giriş DTO
│   └── AuthResponseDto.cs          ✅ Yanıt DTO
├── Models/
│   └── User.cs                     ✅ User entity
├── Services/
│   └── JwtService.cs               ✅ JWT token servisi
├── Program.cs                      ✅ Tam yapılandırma
├── appsettings.json                ✅ Production ayarları
├── appsettings.Development.json    ✅ Development ayarları
├── README.md                       ✅ Detaylı dokümantasyon
└── .gitignore                      ✅ Git ignore kuralları
```

---

## 🚀 ÖNEMLİ: İlk Çalıştırma Adımları

### 1️⃣ PostgreSQL Kurulumu

**Seçenek A: Docker ile (Önerilen)**
```bash
docker run --name tradescout-postgres \
  -e POSTGRES_PASSWORD=Abc123! \
  -e POSTGRES_DB=tradescout_dev_db \
  -p 5432:5432 \
  -d postgres:16
```

**Seçenek B: Manuel Kurulum**
- PostgreSQL'i yükleyin: https://www.postgresql.org/download/
- Yeni veritabanı oluşturun: `CREATE DATABASE tradescout_dev_db;`

### 2️⃣ Connection String Güncelleme

`appsettings.Development.json` dosyasını açın ve bağlantı stringini güncelleyin:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=tradescout_dev_db;Username=postgres;Password=SİZİN_ŞİFRENİZ"
  }
}
```

### 3️⃣ Database Migration

```bash
# Terminal'de TradeScout.API klasörüne gidin
cd /Users/nuranguler/Desktop/TradeScout/FGS_APİ/TradeScout.API

# Migration oluştur
dotnet ef migrations add InitialCreate

# Veritabanını oluştur
dotnet ef database update
```

### 4️⃣ Uygulamayı Çalıştır

```bash
dotnet run
```

Uygulama şu adreste çalışacak:
- **Swagger UI:** http://localhost:5000
- **API Base:** http://localhost:5000/api

---

## 🧪 API'yi Test Etme

### Swagger UI ile (Kolay Yol)

1. Tarayıcıda aç: http://localhost:5000
2. `/api/auth/register` endpoint'ini bul
3. "Try it out" butonuna tıkla
4. JSON body'yi doldur ve "Execute" butonuna tıkla

### cURL ile Test

**Kayıt Ol:**
```bash
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "fullName": "Test Kullanıcı",
    "email": "test@example.com",
    "password": "Test123!",
    "companyName": "Test Şirketi"
  }'
```

**Giriş Yap:**
```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!"
  }'
```

### Beklenen Yanıt:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "fullName": "Test Kullanıcı",
  "email": "test@example.com",
  "credits": 5,
  "role": "User",
  "packageType": "Free"
}
```

---

## 🔐 Güvenlik Notları

⚠️ **Production'a geçmeden önce MUTLAKA değiştirin:**

1. **JWT Secret Key** (appsettings.json):
   - Minimum 32 karakter
   - Güçlü ve rastgele olmalı
   - Environment variable olarak saklanmalı

2. **PostgreSQL Şifresi**:
   - Güçlü şifre kullanın
   - Production'da environment variable olarak saklanmalı

3. **HTTPS**:
   - Production'da mutlaka HTTPS kullanın
   - `RequireHttpsMetadata = true` yapın (Program.cs'te)

---

## 📚 Ek Kaynaklar

- **Detaylı Dokümantasyon:** `README.md` dosyasına bakın
- **EF Core Komutları:** README.md içinde
- **Frontend Entegrasyonu:** README.md içinde JavaScript örnekleri var

---

## 🆘 Yardım

Sorularınız için:
- README.md dosyasını okuyun
- PostgreSQL'in çalıştığından emin olun: `docker ps` veya `pg_isready`
- Migration'ları kontrol edin: `dotnet ef migrations list`

---

## ✅ Kontrol Listesi

- [ ] PostgreSQL yüklendi ve çalışıyor
- [ ] Connection string güncellendi
- [ ] Migration oluşturuldu (`dotnet ef migrations add InitialCreate`)
- [ ] Veritabanı güncellendi (`dotnet ef database update`)
- [ ] Uygulama başarıyla çalışıyor (`dotnet run`)
- [ ] Swagger UI açıldı (http://localhost:5000)
- [ ] Register endpoint test edildi
- [ ] Login endpoint test edildi

---

**🎉 Tebrikler! TradeScout API'niz hazır!**

Projeyi başarıyla derledik ve çalıştırmaya hazır hale getirdik.
Şimdi yukarıdaki adımları takip ederek veritabanını oluşturun ve API'nizi test edin.

**Happy Coding! 🚀**
