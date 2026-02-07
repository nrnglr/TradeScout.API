# 🚀 TradeScout Scraper API - Google Maps Veri Çekme Sistemi

## 📋 Özellikler

- ✅ **Güvenli Google Maps Scraping** (Ban korumalı)
- ✅ **JWT Authentication** (Token tabanlı güvenlik)
- ✅ **Kredi Sistemi** (Her işletme 1 kredi)
- ✅ **Excel Export** (ClosedXML ile)
- ✅ **Rate Limiting** (Her 20 işletmede 60 saniye bekleme)
- ✅ **İnsan Benzetimi** (3-7 saniye rastgele bekleme)
- ✅ **User-Agent Rotasyonu** (Bot detection'dan kaçınma)
- ✅ **PostgreSQL Database** (Veri saklama)
- ✅ **Asenkron İşleme** (Timeout korumalı)

---

## 🛡️ Ban Koruma Mekanizmaları

### 1. İnsan Benzetimi
- Her işletme tıklaması arasında **3-7 saniye** rastgele bekleme
- Yavaş ve kesik kesik scroll yapma
- Smooth scroll animation

### 2. Rate Limiting
- Her **20 işletme** sonrası **60 saniye** dinlenme
- Toplam işlem süresi uzatılır ancak ban riski minimize edilir

### 3. User-Agent Rotasyonu
- Her scraping işinde farklı tarayıcı kimliği
- Chrome, Firefox, Safari rotasyonu

### 4. Headless Chrome
- Arka planda çalışır (GUI yok)
- Kaynak tasarrufu
- Bot detection önleme

---

## 🚦 API Endpoints

### 1. **Scrape Businesses**
```http
POST /api/scraper/scrape
Authorization: Bearer {token}
Content-Type: application/json

{
  "category": "Restaurant",
  "city": "İstanbul",
  "country": "Türkiye",
  "language": "tr",
  "maxResults": 20
}
```

**Response:**
```json
{
  "jobId": 1,
  "status": "Completed",
  "message": "Başarıyla 20 işletme bulundu ve kaydedildi.",
  "totalResults": 20,
  "creditsUsed": 20,
  "businesses": [
    {
      "businessName": "Örnek Restaurant",
      "address": "İstanbul, Türkiye",
      "phone": "+90 212 XXX XX XX",
      "website": "https://example.com",
      "rating": 4.5,
      "reviewCount": 150,
      "workingHours": "09:00-22:00",
      "category": "Restaurant",
      "city": "İstanbul",
      "country": "Türkiye",
      "googleMapsUrl": "https://maps.google.com/..."
    }
  ],
  "downloadUrl": "/api/scraper/download/1"
}
```

### 2. **Download Excel**
```http
GET /api/scraper/download/{jobId}
Authorization: Bearer {token}
```

**Response:** Excel dosyası indirilir (`application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`)

### 3. **Get Scraping History**
```http
GET /api/scraper/history
Authorization: Bearer {token}
```

**Response:**
```json
[
  {
    "id": 1,
    "category": "Restaurant",
    "city": "İstanbul",
    "status": "Completed",
    "totalResults": 20,
    "creditsUsed": 20,
    "createdAt": "2026-02-07T10:30:00Z"
  }
]
```

### 4. **Get Credits**
```http
GET /api/scraper/credits
Authorization: Bearer {token}
```

**Response:**
```json
{
  "credits": 980,
  "packageType": "Free",
  "fullName": "Ahmet Yılmaz",
  "email": "ahmet@example.com"
}
```

---

## 💾 Veritabanı Tabloları

### Businesses
```sql
CREATE TABLE "Businesses" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INTEGER NOT NULL,
    "BusinessName" VARCHAR(300) NOT NULL,
    "Address" VARCHAR(500),
    "Phone" VARCHAR(50),
    "Website" VARCHAR(500),
    "Rating" DECIMAL(3,2),
    "ReviewCount" INTEGER,
    "WorkingHours" VARCHAR(1000),
    "Category" VARCHAR(200),
    "City" VARCHAR(100),
    "Country" VARCHAR(100),
    "Language" VARCHAR(50),
    "GoogleMapsUrl" VARCHAR(1000),
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY ("UserId") REFERENCES "Users"("Id")
);
```

### ScrapingJobs
```sql
CREATE TABLE "ScrapingJobs" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INTEGER NOT NULL,
    "Category" VARCHAR(200) NOT NULL,
    "City" VARCHAR(100) NOT NULL,
    "Country" VARCHAR(100),
    "Language" VARCHAR(50) DEFAULT 'tr',
    "Status" VARCHAR(50) DEFAULT 'Pending',
    "TotalResults" INTEGER DEFAULT 0,
    "CreditsUsed" INTEGER DEFAULT 0,
    "ErrorMessage" VARCHAR(1000),
    "StartedAt" TIMESTAMP,
    "CompletedAt" TIMESTAMP,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY ("UserId") REFERENCES "Users"("Id")
);
```

---

## ⚙️ Kurulum

### 1. Gerekli Paketler
```bash
dotnet add package Selenium.WebDriver
dotnet add package Selenium.WebDriver.ChromeDriver
dotnet add package ClosedXML
dotnet add package Bogus
```

### 2. ChromeDriver Yükleme

**macOS:**
```bash
brew install chromedriver
```

**Windows:**
- [ChromeDriver](https://chromedriver.chromium.org/) indir
- PATH'e ekle

**Linux:**
```bash
wget https://chromedriver.storage.googleapis.com/LATEST_RELEASE
wget https://chromedriver.storage.googleapis.com/$(cat LATEST_RELEASE)/chromedriver_linux64.zip
unzip chromedriver_linux64.zip
sudo mv chromedriver /usr/local/bin/
sudo chmod +x /usr/local/bin/chromedriver
```

### 3. Veritabanı Migration (İsteğe Bağlı)
```bash
# Migration oluştur
dotnet ef migrations add AddScraperTables

# Veritabanını güncelle
dotnet ef database update
```

**VEYA** Manuel SQL çalıştır:
```sql
-- Yukarıdaki Businesses ve ScrapingJobs tablolarını oluştur
```

---

## 🧪 Test Etme

### cURL ile Test
```bash
# 1. Login
TOKEN=$(curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@tradescout.com","password":"Test123!"}' \
  | jq -r '.token')

# 2. Scrape
curl -X POST http://localhost:5000/api/scraper/scrape \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "category": "Kafe",
    "city": "İstanbul",
    "country": "Türkiye",
    "language": "tr",
    "maxResults": 10
  }' | jq

# 3. Download Excel
curl -X GET "http://localhost:5000/api/scraper/download/1" \
  -H "Authorization: Bearer $TOKEN" \
  -o sonuclar.xlsx
```

### JavaScript/React Örneği
```javascript
const scrapeBusinesses = async () => {
  const token = localStorage.getItem('token');
  
  const response = await fetch('http://localhost:5000/api/scraper/scrape', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({
      category: 'Restaurant',
      city: 'İstanbul',
      country: 'Türkiye',
      language: 'tr',
      maxResults: 20
    })
  });

  const data = await response.json();
  console.log('Scraped:', data.totalResults, 'businesses');
  
  // Excel indir
  window.location.href = `http://localhost:5000${data.downloadUrl}?token=${token}`;
};
```

---

## ⚠️ Önemli Notlar

### 1. Kredi Sistemi
- Her işletme verisi **1 kredi** tüketir
- Yeni kullanıcılar **5 kredi** ile başlar
- Yetersiz kredide **402 Payment Required** hatası döner

### 2. Rate Limiting
- Her 20 işletmede **60 saniye** bekleme (ban koruması)
- 100 işletme için yaklaşık **20 dakika** sürer
- İptal etmek için: `Ctrl+C` veya frontend'den cancel

### 3. ChromeDriver Uyumluluk
- ChromeDriver versiyonu Chrome tarayıcı versiyonu ile uyumlu olmalı
- Hata alırsanız: `dotnet remove package Selenium.WebDriver.ChromeDriver` sonra istediğiniz versiyonu yükleyin

### 4. Headless Mod
- Varsayılan olarak **headless** mod aktif (tarayıcı görünmez)
- Debug için headless'i kapatın:
  ```csharp
  // options.AddArgument("--headless=new"); // Bu satırı yorum satırı yapın
  ```

### 5. Proxy Kullanımı (İsteğe Bağlı)
Ban riskini daha da azaltmak için proxy kullanabilirsiniz:
```csharp
options.AddArgument("--proxy-server=http://proxy-ip:port");
```

---

## 📊 Performans

| İşletme Sayısı | Tahmini Süre | Kredi Kullanımı |
|----------------|--------------|-----------------|
| 10             | ~2-3 dakika  | 10              |
| 20             | ~5-7 dakika  | 20              |
| 50             | ~15-20 dakika| 50              |
| 100            | ~30-35 dakika| 100             |

---

## 🔐 Güvenlik

- ✅ JWT token ile korumalı endpoint'ler
- ✅ Kullanıcı kredi kontrolü
- ✅ SQL injection koruması (EF Core parametreli sorgular)
- ✅ XSS koruması
- ✅ CORS politikası

---

## 🐛 Sorun Giderme

### ChromeDriver Hatası
```
SessionNotCreatedException: session not created
```
**Çözüm:** ChromeDriver versiyonunu güncelleyin
```bash
dotnet remove package Selenium.WebDriver.ChromeDriver
dotnet add package Selenium.WebDriver.ChromeDriver --version 144.0.7559.13300
```

### Timeout Hatası
```
WebDriverTimeoutException: Timeout waiting for element
```
**Çözüm:** İnternet bağlantınızı kontrol edin veya timeout süresini artırın

### Ban/Captcha
```
Google CAPTCHA görünüyorsa
```
**Çözüm:** 
- MaxResults değerini azaltın (10-20)
- Rate limiting süresini artırın (60 sn → 120 sn)
- Proxy kullanın

---

## 📝 Geliştirme Planı

- [ ] Proxy rotation desteği
- [ ] CAPTCHA çözme entegrasyonu
- [ ] Background job processing (Hangfire)
- [ ] WebSocket ile real-time progress
- [ ] Paket sistemi (Premium/Enterprise)
- [ ] Email bildirimleri

---

## 📄 Lisans

Bu proje TradeScout tarafından geliştirilmiştir. Tüm hakları saklıdır.

---

**Happy Scraping! 🎉**
