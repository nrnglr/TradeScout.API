# 🔧 Ücretsiz Proxy Test Rehberi

## 📋 Adım Adım Kurulum

### 1. Ücretsiz Proxy Listesi Alın

Aşağıdaki sitelerden ücretsiz proxy'ler bulabilirsiniz:

- **Free Proxy List**: https://www.free-proxy-list.net/
- **Proxy Scrape**: https://proxyscrape.com/free-proxy-list
- **GeoNode**: https://geonode.com/free-proxy-list
- **ProxyScan**: https://www.proxyscan.io/

### 2. Proxy Formatı

Proxy'ler şu formatta olmalı:
```
http://IP:PORT
socks5://IP:PORT
```

Örnek:
```
http://103.152.112.145:80
http://195.201.231.90:8080
socks5://185.251.89.108:1080
```

### 3. appsettings.json'a Proxy Ekleyin

`/Users/nuranguler/Desktop/TradeScout/FGS_APİ/TradeScout.API/appsettings.json` dosyasını açın ve `ProxySettings.Proxies` bölümünü güncelleyin:

```json
{
  "ProxySettings": {
    "EnableProxy": true,
    "RotationType": "RoundRobin",
    "TestProxiesOnStartup": true,
    "ProxyTimeout": 30,
    "Proxies": [
      {
        "Address": "http://103.152.112.145:80",
        "Username": "",
        "Password": "",
        "Enabled": true
      },
      {
        "Address": "http://195.201.231.90:8080",
        "Username": "",
        "Password": "",
        "Enabled": true
      },
      {
        "Address": "http://185.251.89.108:3128",
        "Username": "",
        "Password": "",
        "Enabled": true
      }
    ]
  }
}
```

**Not:** Eğer proxy authentication gerektiriyorsa, `Username` ve `Password` alanlarını doldurun.

### 4. API'yi Başlatın

Terminal'de:
```bash
cd /Users/nuranguler/Desktop/TradeScout/FGS_APİ/TradeScout.API
dotnet run
```

### 5. Proxy'leri Test Edin

#### Option A: Test Script Kullanın (Önerilen)

```bash
# Script'i çalıştırılabilir yapın
chmod +x test-proxies.sh

# Test'i çalıştırın
./test-proxies.sh
```

#### Option B: Manuel Test (curl)

**a) Token Alın:**
```bash
TOKEN=$(curl -s -X POST "http://localhost:5000/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!"}' | jq -r '.token')

echo "Token: $TOKEN"
```

**b) Proxy Durumunu Görüntüleyin:**
```bash
curl -X GET "http://localhost:5000/api/proxy/status" \
  -H "Authorization: Bearer $TOKEN" | jq '.'
```

**c) Tüm Proxy'leri Test Edin:**
```bash
curl -X POST "http://localhost:5000/api/proxy/test-all" \
  -H "Authorization: Bearer $TOKEN" | jq '.'
```

**d) Tek Bir Proxy Test Edin:**
```bash
# İlk proxy'yi test et (index 0)
curl -X POST "http://localhost:5000/api/proxy/test/0" \
  -H "Authorization: Bearer $TOKEN" | jq '.'
```

#### Option C: Postman Kullanın

1. **Login Request:**
   - Method: POST
   - URL: `http://localhost:5000/api/auth/login`
   - Body (JSON):
   ```json
   {
     "email": "test@example.com",
     "password": "Test123!"
   }
   ```
   - Token'ı kopyalayın

2. **Test All Proxies:**
   - Method: POST
   - URL: `http://localhost:5000/api/proxy/test-all`
   - Headers: `Authorization: Bearer YOUR_TOKEN`

3. **Get Proxy Status:**
   - Method: GET
   - URL: `http://localhost:5000/api/proxy/status`
   - Headers: `Authorization: Bearer YOUR_TOKEN`

## 📊 Test Sonuçları Yorumlama

### Başarılı Proxy
```json
{
  "proxy": "http://103.152.112.145:80",
  "status": "✅ Working",
  "responseTime": "2.34s",
  "error": null
}
```

### Başarısız Proxy
```json
{
  "proxy": "http://195.201.231.90:8080",
  "status": "❌ Failed",
  "responseTime": "N/A",
  "error": "Timeout: Timed out receiving message from renderer"
}
```

## 🎯 Proxy Rotation Stratejileri

`appsettings.json` içinde `RotationType` değiştirerek farklı stratejiler deneyebilirsiniz:

### 1. RoundRobin (Önerilen)
```json
"RotationType": "RoundRobin"
```
- Her istekte sırayla proxy kullanır
- Adil dağılım sağlar
- **En dengeli seçenek**

### 2. Random
```json
"RotationType": "Random"
```
- Rastgele proxy seçer
- Tahmin edilmesi zor
- Bazı proxy'lere daha fazla yük binebilir

### 3. Sequential
```json
"RotationType": "Sequential"
```
- İlk çalışan proxy'yi sürekli kullanır
- Basit ama etkili değil

## 🔍 Sık Karşılaşılan Sorunlar

### 1. "No healthy proxies available"
**Çözüm:** 
- Daha fazla proxy ekleyin
- `ProxyTimeout` değerini artırın (örn: 60)
- Proxy listesini yenileyin

### 2. "Timeout" Hataları
**Çözüm:**
```json
"ProxyTimeout": 60
```

### 3. Tüm Proxy'ler Başarısız
**Çözüm:**
- Başka kaynaklardan proxy deneyin
- SOCKS5 proxy'leri deneyin
- Premium proxy servisi düşünün

### 4. Proxy Authentication Required
**Çözüm:**
```json
{
  "Address": "http://proxy.example.com:8080",
  "Username": "your_username",
  "Password": "your_password",
  "Enabled": true
}
```

## 🚀 Scraping İle Test

Proxy'leri canlı scraping ile test edin:

```bash
curl -X POST "http://localhost:5000/api/scraper/scrape" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "category": "restaurant",
    "city": "Istanbul",
    "country": "Turkey",
    "language": "tr",
    "maxResults": 10
  }' | jq '.'
```

**Loglarda görecekleriniz:**
```
[INFO] Selected proxy: http://103.152.112.145:80 (Failures: 0)
[INFO] 🚀 Scraping başlatılıyor: restaurant - Istanbul
[INFO] Configured Chrome driver with proxy: http://103.152.112.145:80
```

## 📈 Performans İpuçları

1. **10-20 Proxy Kullanın:**
   - Çeşitlilik ban riskini azaltır
   - Rotasyon daha etkili olur

2. **Düzenli Test:**
   - Her 1 saatte bir proxy'leri test edin
   - Çalışmayanları devre dışı bırakın

3. **Failure Threshold:**
   - Kod otomatik olarak 3 hata sonrası proxy'yi disable eder
   - Logları takip edin

4. **Coğrafi Çeşitlilik:**
   - Farklı ülkelerden proxy kullanın
   - Türkiye için Avrupa proxy'leri tercih edin

## 🎓 Önerilen Ücretsiz Proxy Kaynakları

### Günlük Güncellenen:
1. **Free Proxy List** (https://www.free-proxy-list.net/)
   - HTTP/HTTPS proxy'ler
   - Anonim ve elite proxy'ler
   - Gerçek zamanlı test edilmiş

2. **Proxy Scrape** (https://proxyscrape.com/free-proxy-list)
   - HTTP, SOCKS4, SOCKS5
   - API ile toplu alma
   - Yüksek uptime oranı

3. **GeoNode** (https://geonode.com/free-proxy-list)
   - Lokasyon filtresi
   - Google'a özel test edilmiş
   - Premium kalite ücretsiz proxy'ler

### Premium Alternatifler (Ücretli):
- **BrightData** (eski Luminati): $500/ay'dan başlar
- **Smartproxy**: $75/ay
- **Proxy-Cheap**: $50/ay
- **IPRoyal**: $1/GB

## 📞 Destek

Sorunlarla karşılaşırsanız:
1. Loglara bakın: `dotnet run` çıktısı
2. Proxy durumunu kontrol edin: `/api/proxy/status`
3. Test sonuçlarını inceleyin: `/api/proxy/test-all`

**Happy Scraping! 🎉**
