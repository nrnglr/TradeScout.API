# 📱 Frontend Kullanım Kılavuzu - TradeScout API

## 🎯 Firma Sayısına Göre Veri Çekme

Frontend'inizde kullanıcı **"Firma Sayısı"** alanına istediği sayıyı yazacak (örn: 10, 20, 50, 100).  
API **tam olarak o kadar firma bulunca** durur ve sonuçları döner.

---

## 🚀 API Endpoint'i

```
POST http://localhost:5000/api/scraper/scrape
Authorization: Bearer {JWT_TOKEN}
Content-Type: application/json
```

---

## 📋 Request Body (Frontend'den Gönderilecek)

```javascript
{
  "category": "Kafe",           // İş alanı (zorunlu)
  "city": "İstanbul",           // Şehir (zorunlu)
  "country": "Türkiye",         // Ülke (opsiyonel)
  "language": "tr",             // Dil (varsayılan: "tr")
  "maxResults": 10              // KULLANICININ GİRDİĞİ FİRMA SAYISI!
}
```

### ⚠️ Önemli Limitler:
- **Minimum:** 1 firma
- **Maksimum:** 100 firma
- **Önerilen:** 10-20 firma (hızlı sonuç için)

---

## 📊 Response (API'nin Döndürdüğü)

```javascript
{
  "jobId": 123,
  "status": "Completed",
  "message": "Başarıyla 10 işletme bulundu ve kaydedildi.",
  "totalResults": 10,           // Bulunan firma sayısı
  "creditsUsed": 10,            // Kullanılan kredi (her firma 1 kredi)
  "businesses": [
    {
      "businessName": "Starbucks İstanbul Taksim",
      "address": "İstiklal Caddesi No:123, Beyoğlu, İstanbul",
      "phone": "+90 212 123 45 67",
      "website": "https://www.starbucks.com.tr",
      "rating": 4.5,
      "reviewCount": 342,
      "workingHours": "08:00 - 22:00",
      "category": "Kafe",
      "city": "İstanbul",
      "country": "Türkiye",
      "googleMapsUrl": "https://www.google.com/maps/place/..."
    },
    // ... toplam 10 firma
  ],
  "downloadUrl": "/api/scraper/download/123"
}
```

---

## 💻 React/Next.js Örnek Kod

### 1. Form Component'i

```tsx
'use client';

import { useState } from 'react';

export default function ScrapeForm() {
  const [isLoading, setIsLoading] = useState(false);
  const [firmaSayisi, setFirmaSayisi] = useState(10);
  const [category, setCategory] = useState('');
  const [city, setCity] = useState('');
  const [results, setResults] = useState(null);

  const handleScrape = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);

    try {
      // Token'ı localStorage'dan al
      const token = localStorage.getItem('token');

      const response = await fetch('http://localhost:5000/api/scraper/scrape', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          category: category,
          city: city,
          country: 'Türkiye',
          language: 'tr',
          maxResults: firmaSayisi, // Kullanıcının girdiği sayı
        }),
      });

      if (!response.ok) {
        const error = await response.json();
        
        // Yetersiz kredi hatası
        if (response.status === 402) {
          alert(`Yetersiz kredi! Gerekli: ${firmaSayisi}, Mevcut: ${error.availableCredits}`);
          return;
        }
        
        throw new Error(error.message || 'Bir hata oluştu');
      }

      const data = await response.json();
      setResults(data);
      
      alert(`✅ Başarıyla ${data.totalResults} firma bulundu!`);
      
    } catch (error) {
      console.error('Scraping hatası:', error);
      alert('❌ Bir hata oluştu: ' + error.message);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="max-w-md mx-auto p-6 bg-white rounded-lg shadow-lg">
      <h2 className="text-2xl font-bold mb-4">🔍 Firma Ara</h2>
      
      <form onSubmit={handleScrape} className="space-y-4">
        {/* İş Alanı */}
        <div>
          <label className="block text-sm font-medium text-gray-700">
            İş Alanı *
          </label>
          <input
            type="text"
            value={category}
            onChange={(e) => setCategory(e.target.value)}
            placeholder="Örn: Kafe, Restaurant, Kuaför"
            className="mt-1 block w-full rounded-md border-gray-300 shadow-sm"
            required
          />
        </div>

        {/* Şehir */}
        <div>
          <label className="block text-sm font-medium text-gray-700">
            Şehir *
          </label>
          <input
            type="text"
            value={city}
            onChange={(e) => setCity(e.target.value)}
            placeholder="Örn: İstanbul, Ankara, İzmir"
            className="mt-1 block w-full rounded-md border-gray-300 shadow-sm"
            required
          />
        </div>

        {/* Firma Sayısı */}
        <div>
          <label className="block text-sm font-medium text-gray-700">
            Kaç Firma Bulunacak? *
          </label>
          <input
            type="number"
            min="1"
            max="100"
            value={firmaSayisi}
            onChange={(e) => setFirmaSayisi(parseInt(e.target.value))}
            className="mt-1 block w-full rounded-md border-gray-300 shadow-sm"
            required
          />
          <p className="mt-1 text-sm text-gray-500">
            ℹ️ Her firma 1 kredi tüketir (Min: 1, Max: 100)
          </p>
        </div>

        {/* Submit Button */}
        <button
          type="submit"
          disabled={isLoading}
          className="w-full bg-blue-600 text-white py-2 px-4 rounded-md hover:bg-blue-700 disabled:bg-gray-400"
        >
          {isLoading ? (
            <>
              ⏳ Veriler çekiliyor... (Bu işlem {Math.ceil(firmaSayisi / 20 * 5)} dakika sürebilir)
            </>
          ) : (
            <>🚀 Firma Ara</>
          )}
        </button>
      </form>

      {/* Sonuçlar */}
      {results && (
        <div className="mt-6 p-4 bg-green-50 rounded-lg">
          <h3 className="font-bold text-green-800">
            ✅ {results.totalResults} Firma Bulundu!
          </h3>
          <p className="text-sm text-gray-600 mt-2">
            {results.creditsUsed} kredi kullanıldı
          </p>
          
          {/* Excel İndir Butonu */}
          <a
            href={`http://localhost:5000${results.downloadUrl}`}
            download
            className="mt-4 inline-block bg-green-600 text-white py-2 px-4 rounded-md hover:bg-green-700"
          >
            📥 Excel İndir
          </a>
        </div>
      )}
    </div>
  );
}
```

---

## ⏱️ Tahmini Süreler

| Firma Sayısı | Tahmini Süre | Açıklama |
|--------------|--------------|----------|
| 10 firma     | ~2-3 dakika  | Hızlı test için ideal |
| 20 firma     | ~5-7 dakika  | Orta boyutlu veri |
| 50 firma     | ~15-20 dakika | 20'de 60 sn duraklatma |
| 100 firma    | ~30-35 dakika | Maksimum limit |

### ⚠️ Neden Bu Kadar Uzun?

**Ban Koruması!** Her 20 firmada 60 saniye duraklatıyoruz:
- ✅ Google'dan ban yemiyoruz
- ✅ Kullanıcı hesabı güvende
- ✅ %100 başarı oranı

---

## 🎨 Progress Bar Örneği (Gelişmiş)

```tsx
const [progress, setProgress] = useState(0);

// WebSocket veya polling ile ilerlemeyi takip et
useEffect(() => {
  if (isLoading) {
    // Her 5 saniyede bir API'ye job durumunu sor
    const interval = setInterval(async () => {
      const response = await fetch(
        `http://localhost:5000/api/scraper/job/${jobId}`,
        {
          headers: { 'Authorization': `Bearer ${token}` }
        }
      );
      const data = await response.json();
      setProgress((data.processed / data.total) * 100);
    }, 5000);

    return () => clearInterval(interval);
  }
}, [isLoading]);

return (
  <div className="w-full bg-gray-200 rounded-full h-4">
    <div
      className="bg-blue-600 h-4 rounded-full transition-all"
      style={{ width: `${progress}%` }}
    />
    <p className="text-center text-sm mt-2">
      {progress.toFixed(0)}% Tamamlandı
    </p>
  </div>
);
```

---

## 🔐 Kredi Kontrolü

Veri çekmeden önce kullanıcının yeterli kredisi olup olmadığını kontrol et:

```javascript
// Kredi bakiyesini al
const getCredits = async () => {
  const token = localStorage.getItem('token');
  const response = await fetch('http://localhost:5000/api/scraper/credits', {
    headers: { 'Authorization': `Bearer ${token}` }
  });
  const data = await response.json();
  return data.credits; // Örn: 50
};

// Form submit'ten önce kontrol
const handleScrape = async (e) => {
  e.preventDefault();
  
  const availableCredits = await getCredits();
  
  if (availableCredits < firmaSayisi) {
    alert(`❌ Yetersiz kredi!\n\nGerekli: ${firmaSayisi}\nMevcut: ${availableCredits}\n\nLütfen kredi satın alın.`);
    return;
  }
  
  // Scraping işlemine devam et...
};
```

---

## 📥 Excel İndirme

```javascript
// Otomatik indirme
const downloadExcel = (jobId) => {
  const token = localStorage.getItem('token');
  const url = `http://localhost:5000/api/scraper/download/${jobId}`;
  
  // Yöntem 1: <a> tag ile
  const link = document.createElement('a');
  link.href = `${url}?token=${token}`;
  link.download = `TradeScout_${new Date().toISOString()}.xlsx`;
  link.click();
  
  // Yöntem 2: Fetch ile
  fetch(url, {
    headers: { 'Authorization': `Bearer ${token}` }
  })
    .then(response => response.blob())
    .then(blob => {
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'sonuclar.xlsx';
      a.click();
    });
};
```

---

## 🧪 Test Senaryosu

```javascript
// 1. Kullanıcı login yaptı
localStorage.setItem('token', 'eyJhbGci...');

// 2. Form dolduruldu
const requestData = {
  category: "Kafe",
  city: "İstanbul",
  country: "Türkiye",
  language: "tr",
  maxResults: 10  // ← Kullanıcı "10" yazdı
};

// 3. API'ye istek gönder
const response = await fetch('http://localhost:5000/api/scraper/scrape', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json',
  },
  body: JSON.stringify(requestData)
});

// 4. Sonuç
const data = await response.json();
console.log(`✅ ${data.totalResults} firma bulundu`); // "10 firma bulundu"

// 5. API tam 10 firma bulunca durdu!
```

---

## ❓ Sık Sorulan Sorular

### 1. **API hep tam sayıda firma bulur mu?**
✅ Evet! Örneğin 10 firma istediyseniz, **tam 10 firma** bulunana kadar devam eder.  
⚠️ Eğer Google Maps'te yeterli sonuç yoksa, bulunanları döner (örn: sadece 7 firma varsa 7 döner).

### 2. **İşlem ne kadar sürer?**
- 10 firma: ~2-3 dakika
- 20 firma: ~5-7 dakika
- Ban koruması nedeniyle her 20 firmada 60 saniye bekler

### 3. **İşlemi iptal edebilir miyim?**
🔴 Hayır, şu an iptal özelliği yok. Ancak yakında eklenecek!  
Geçici çözüm: Sayfayı kapatmayın, işlem tamamlanana kadar bekleyin.

### 4. **Aynı firmalar tekrar gelir mi?**
❌ Hayır! Duplicate kontrol var, aynı firma 2 kez eklenmez.

### 5. **Kredi iadesi var mı?**
✅ Eğer işlem başarısız olursa kredi geri yüklenir.  
❌ Başarılı işlemde kredi iadesi yok.

---

## 🎯 Örnek Kullanım Senaryoları

### Senaryo 1: Küçük Test (Hızlı)
```javascript
{
  "category": "Kafe",
  "city": "İstanbul",
  "maxResults": 5  // 5 firma, ~1-2 dakika
}
```

### Senaryo 2: Orta Boyut (Önerilen)
```javascript
{
  "category": "Restaurant",
  "city": "Ankara",
  "maxResults": 20  // 20 firma, ~5-7 dakika
}
```

### Senaryo 3: Büyük Veri
```javascript
{
  "category": "Kuaför",
  "city": "İzmir",
  "maxResults": 100  // 100 firma, ~30-35 dakika
}
```

---

## 📊 Kredi Sistemi

- Yeni kullanıcı: **5 kredi** ile başlar
- Her firma: **1 kredi** tüketir
- 10 firma çekmek: **10 kredi** gerekir
- Yetersiz kredide: **402 Payment Required** hatası

**Çözüm:** Kullanıcıdan kredi satın almasını isteyin veya paket yükseltsin.

---

**🎉 Başarılar! Frontend'inizden artık tam istediğiniz kadar firma çekebilirsiniz!**
