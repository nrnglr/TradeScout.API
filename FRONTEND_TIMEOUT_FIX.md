# Frontend Timeout Sorunu - Çözüm

## 🚨 Sorun
Scraping 120 saniye (2 dakika) içinde tamamlanamadı ve frontend timeout hatası verdi.

## ✅ Çözüm 1: Frontend Timeout'u Artırın (ÖNERİLEN)

### `src/services/api.ts` dosyasını güncelleyin:

```typescript
import axios from 'axios';

const API_BASE_URL = 'http://localhost:5000';

export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
  timeout: 300000, // ✅ 5 dakika (300000ms) - ÖNCE: 120000ms
});

// ...existing interceptors...
```

## ✅ Çözüm 2: Scraper Timeout'u Ayarlayın

### `src/services/scraper.ts` dosyasında:

```typescript
export const scraperService = {
  async scrape(data: ScrapeRequest) {
    // Sadece scraping için özel timeout
    const response = await apiClient.post('/api/scraper/scrape', data, {
      timeout: 600000, // ✅ 10 dakika - Scraping uzun sürebilir
    });
    return response.data;
  },
  
  // Diğer metodlar normal timeout kullanır
};
```

## ✅ Çözüm 3: İlerleme Göstergesi Ekleyin (İLERİ SEVİYE)

Scraping sırasında kullanıcıya feedback verin:

```typescript
const [progress, setProgress] = useState(0);
const [status, setStatus] = useState('');

// Fake progress simulation
const progressInterval = setInterval(() => {
  setProgress((prev) => {
    if (prev >= 95) return prev;
    return prev + 5;
  });
}, 3000); // Her 3 saniyede %5 arttır

try {
  const result = await scraperService.scrape(data);
  clearInterval(progressInterval);
  setProgress(100);
} catch (err) {
  clearInterval(progressInterval);
}
```

## 📊 Scraping Süreleri (Tahmini)

| Firma Sayısı | Beklenen Süre | Timeout Önerisi |
|--------------|---------------|-----------------|
| 5 firma      | 30-60 saniye  | 180 saniye (3 dk) |
| 10 firma     | 1-2 dakika    | 300 saniye (5 dk) |
| 20 firma     | 2-4 dakika    | 420 saniye (7 dk) |
| 50 firma     | 5-10 dakika   | 900 saniye (15 dk) |

## 🎯 Önerilen Yapılandırma

### Production Ayarları:

```typescript
// api.ts
export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  timeout: 60000, // Genel istekler için 1 dakika
});

// scraper.ts
export const scraperService = {
  async scrape(data: ScrapeRequest) {
    return await apiClient.post('/api/scraper/scrape', data, {
      timeout: 600000, // Sadece scraping için 10 dakika
      onUploadProgress: (progressEvent) => {
        // İsteğe bağlı: progress tracking
      },
    });
  },
};
```

## 🐛 Debug: Backend Hala Çalışıyor mu?

Timeout olsa bile backend arka planda scraping'e devam edebilir. Kontrol etmek için:

```bash
# Backend loglarını kontrol edin
curl http://localhost:5000/api/scraper/history \
  -H "Authorization: Bearer YOUR_TOKEN"

# Son job'ın durumunu görün
# Status: "InProgress" - Hala çalışıyor
# Status: "Completed" - Tamamlandı
# Status: "Failed" - Başarısız
```

## ✅ Hızlı Test

Timeout'u artırdıktan sonra yeniden deneyin:

1. **Frontend'de timeout'u 300000ms yapın**
2. **Daha az firma deneyin** (maxResults: 3)
3. **Scraping'i başlatın**
4. **Bekleyin** (1-2 dakika)
5. **Sonuçları görün**

---

**Son güncelleme:** 2026-02-07 18:30  
**Durum:** Backend çalışıyor, frontend timeout artırılmalı
