# 🌐 Proxy Kurulum ve Test Rehberi

## Ücretsiz Proxy Kaynakları

### En İyi Ücretsiz Proxy Siteleri:
1. **Free Proxy List** - https://free-proxy-list.net/
2. **ProxyScrape** - https://proxyscrape.com/free-proxy-list
3. **GeoNode** - https://geonode.com/free-proxy-list
4. **Proxy-List.download** - https://www.proxy-list.download/
5. **Spys.one** - https://spys.one/en/

### Proxy Seçim Kriterleri:
- ✅ **HTTPS destekli** olmalı
- ✅ **Yüksek anonimlik** seviyesi (Elite veya Anonymous)
- ✅ **Düşük yanıt süresi** (<3 saniye)
- ✅ **Yüksek uptime** (>80%)
- ⚠️ Türkiye veya yakın ülkelerden proxy'ler daha hızlı olabilir

## Proxy Formatı

Proxy'leri şu formatta `appsettings.json` dosyasına ekleyin:

```json
"ScrapingSettings": {
  "ProxyList": [
    "http://192.168.1.1:8080",
    "http://45.123.45.67:3128",
    "http://proxy.example.com:8888"
  ]
}
```

veya kullanıcı adı/şifre gerektiren proxy'ler için:

```json
"ProxyList": [
  "http://username:password@proxy.example.com:8080"
]
```

## Hızlı Test Adımları

### 1. Proxy'leri Topla
Free-proxy-list.net'ten 10 proxy alın:
- HTTPS sütunu "yes" olanları seçin
- Anonymity "elite proxy" veya "anonymous" olanları tercih edin
- Son kontrol süresi 1 dakikadan yeni olanları seçin

### 2. Proxy'leri Sisteme Ekle

`appsettings.json` dosyasını açın ve proxy'leri ekleyin:

```json
{
  "Logging": { ... },
  "AllowedHosts": "*",
  "ConnectionStrings": { ... },
  "Jwt": { ... },
  
  "ScrapingSettings": {
    "MinDelayMs": 1500,
    "MaxDelayMs": 3000,
    "ScrollPauseMs": 1000,
    "MaxRetries": 3,
    "TimeoutSeconds": 60,
    "ProxyList": [
      "http://185.199.229.156:7492",
      "http://185.199.228.220:7300",
      "http://185.199.231.45:8382",
      "http://188.74.210.207:6286",
      "http://194.33.63.114:3128",
      "http://103.27.203.124:8080",
      "http://45.76.167.26:8080",
      "http://103.216.207.15:8080",
      "http://159.65.69.186:9300",
      "http://168.138.211.5:8080"
    ]
  }
}
```

### 3. API'yi Yeniden Başlat

```bash
# Eğer API çalışıyorsa durdur (Ctrl+C)
# Sonra yeniden başlat
dotnet run
```

### 4. Test Et

Frontend'den veya Postman'den scraping isteği gönderin:

```bash
curl -X POST http://localhost:5000/api/scraper/scrape \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "category": "restoran",
    "city": "Istanbul",
    "companyCount": 50
  }'
```

## Beklenen Sonuçlar

### ✅ Başarılı Proxy Kullanımı:
- Her istek farklı bir proxy kullanır
- Scraping hızı artar (1-3 saniye arası)
- Ban riski azalır
- Console'da "Using proxy: http://..." mesajları görünür

### ⚠️ Dikkat Edilmesi Gerekenler:
- Bazı ücretsiz proxy'ler çalışmayabilir
- Sistem otomatik olarak çalışmayan proxy'leri atlar
- Tüm proxy'ler başarısız olursa proxy'siz devam eder
- İlk 5-10 istek yavaş olabilir (proxy test etme)

## Sorun Giderme

### Proxy Çalışmıyor:
1. Proxy'nin hala aktif olduğunu kontrol edin (proxy test sitelerinden)
2. Formatın doğru olduğundan emin olun: `http://IP:PORT`
3. HTTPS proxy gerektiren siteler için `https://` kullanın
4. API loglarını kontrol edin: `dotnet run`

### Tüm Proxy'ler Başarısız:
```
⚠️ All proxies failed, continuing without proxy
```
Bu durumda:
- Yeni proxy listesi bulun
- Proxy'lerin HTTPS desteklediğinden emin olun
- Proxy test sitelerinden önce test edin

### Proxy Çok Yavaş:
- Daha hızlı proxy'ler seçin (response time <2s)
- Türkiye veya Avrupa lokasyonlu proxy'ler kullanın
- Timeout süresini artırın: `"TimeoutSeconds": 90`

## Performans Karşılaştırması

### Proxy'siz (Varsayılan):
- 20 firma: ~45-60 saniye
- 50 firma: ~2-3 dakika
- Ban riski: Orta-Yüksek

### 10 Proxy ile:
- 20 firma: ~20-30 saniye (%40-50 hız artışı)
- 50 firma: ~45-90 saniye (%50-60 hız artışı)
- Ban riski: Çok Düşük

### 50+ Ücretli Proxy ile:
- 20 firma: ~10-15 saniye (%70-80 hız artışı)
- 50 firma: ~25-40 saniye (%70-80 hız artışı)
- Ban riski: Minimal

## Test Sonrası Değerlendirme

Test yaptıktan sonra şunları kontrol edin:

1. **Hız**: Scraping süresi azaldı mı?
2. **Başarı Oranı**: Tüm firmalar başarıyla toplandı mı?
3. **Ban**: Google Maps "suspicious activity" uyarısı geldi mi?
4. **Proxy Başarı Oranı**: Kaç proxy başarılı, kaç tanesi başarısız?

### Loglardan İzleme:
```
✅ Using proxy: http://185.199.229.156:7492
✅ Scraped business: Firma Adı
✅ Using proxy: http://185.199.228.220:7300
✅ Scraped business: Firma Adı 2
```

## Gelişmiş Proxy Yönetimi (İsteğe Bağlı)

Daha gelişmiş kullanım için:

### Proxy Sağlık Kontrolü Ekle:
```csharp
// Services/ProxyHealthChecker.cs oluşturabilirsiniz
// Her proxy'nin çalışıp çalışmadığını periyodik olarak kontrol eder
```

### Akıllı Proxy Rotasyonu:
```csharp
// Başarılı proxy'lere öncelik ver
// Başarısız proxy'leri listeden çıkar
// Hızlı proxy'leri daha sık kullan
```

### Ücretli Proxy Servisleri (Öneri):
- **Bright Data** (eski Luminati) - En iyi kalite
- **SmartProxy** - Uygun fiyat/performans
- **Oxylabs** - Kurumsal seviye
- **IPRoyal** - Ekonomik seçenek

## Notlar

- 🆓 Ücretsiz proxy'ler test için iyidir ama üretimde ücretli proxy servisleri önerilir
- 🔄 Proxy listesini düzenli olarak güncelleyin (ücretsiz proxy'ler kısa ömürlüdür)
- 📊 Test sonuçlarını kaydedin ve en iyi proxy'leri belirleyin
- 🚀 Yüksek volume scraping için rotating proxy servisi kullanın

## Destek

Proxy kurulumu veya test sırasında sorun yaşarsanız:
1. API loglarını kontrol edin
2. Proxy'leri online test sitelerinden kontrol edin
3. Farklı proxy kaynakları deneyin
4. TROUBLESHOOTING.md dosyasına bakın
