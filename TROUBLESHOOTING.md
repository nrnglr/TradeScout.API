# 🔧 Troubleshooting: "Firma Ara" Butonu Hata Veriyor

## ❌ Problem: Firma ara tuşuna basınca direkt login ekranına atıyor

Bu sorun **401 Unauthorized** hatası nedeniyle oluyor. Backend JWT token'ı geçersiz buluyor ve sizi login'e yönlendiriyor.

---

## 🔍 Olası Sebepler ve Çözümleri

### 1️⃣ Token Gönderilmiyor (EN MUHTEMEL)

#### Problem:
API isteğinde `Authorization` header'ı eksik veya yanlış formatta.

#### Kontrol Edin:
```typescript
// ❌ YANLIŞ - Token gönderilmiyor
fetch('http://localhost:5000/api/scraper/scrape', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
  },
  body: JSON.stringify({ searchQuery: 'test', maxResults: 10 })
})

// ✅ DOĞRU - Token gönderiliyor
const token = localStorage.getItem('token');
fetch('http://localhost:5000/api/scraper/scrape', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${token}`,  // <- BU ÇOK ÖNEMLİ!
  },
  body: JSON.stringify({ searchQuery: 'test', maxResults: 10 })
})
```

#### ✅ Çözüm:
Axios kullanıyorsanız interceptor ekleyin:

```typescript
// src/services/api.ts
import axios from 'axios';

export const apiClient = axios.create({
  baseURL: 'http://localhost:5000',
  headers: {
    'Content-Type': 'application/json',
  },
});

// HER İSTEKTE OTOMATIK TOKEN EKLE
apiClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    console.log('📤 İstek gönderiliyor:', config.url);
    console.log('🔑 Token:', token ? 'Mevcut' : 'YOK!');
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// 401 hatası gelirse otomatik logout
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    console.error('❌ API Hatası:', error.response?.status, error.response?.data);
    
    if (error.response?.status === 401) {
      console.log('🚪 Token geçersiz, logout yapılıyor...');
      localStorage.removeItem('token');
      localStorage.removeItem('user');
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);
```

---

### 2️⃣ Token Kayıt Edilmemiş

#### Problem:
Login/Register sonrası token localStorage'a kaydedilmemiş.

#### Kontrol Edin:
Browser DevTools'da (F12) → Application → Local Storage → http://localhost:3000 (veya frontend portunuz)

**Görmeniz gereken:**
```
token: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
user: {"fullName":"Test User","email":"test@example.com","credits":5}
```

#### ✅ Çözüm:
Login/Register fonksiyonunuzu kontrol edin:

```typescript
// ❌ YANLIŞ
const handleLogin = async (data) => {
  const response = await fetch('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify(data),
  });
  const result = await response.json();
  // Token kaydedilmiyor!
  navigate('/dashboard');
}

// ✅ DOĞRU
const handleLogin = async (data) => {
  const response = await fetch('http://localhost:5000/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data),
  });
  
  if (!response.ok) {
    throw new Error('Login başarısız');
  }
  
  const result = await response.json();
  
  // TOKEN'I KAYDET!
  localStorage.setItem('token', result.token);
  localStorage.setItem('user', JSON.stringify({
    fullName: result.fullName,
    email: result.email,
    credits: result.credits,
  }));
  
  console.log('✅ Token kaydedildi:', result.token);
  navigate('/dashboard');
}
```

---

### 3️⃣ Token Süresi Dolmuş

#### Problem:
Token 12 saat sonra expire oluyor. Eski bir token kullanıyorsunuz.

#### Kontrol Edin:
Token'ı decode edin: https://jwt.io

```javascript
// Browser console'da test edin:
const token = localStorage.getItem('token');
const payload = JSON.parse(atob(token.split('.')[1]));
console.log('Token expire:', new Date(payload.exp * 1000));
console.log('Şu an:', new Date());
```

#### ✅ Çözüm:
Yeniden login yapın veya token refresh mekanizması ekleyin.

---

### 4️⃣ CORS Hatası

#### Problem:
Backend CORS ayarları frontend portunu içermiyor.

#### Kontrol Edin:
Browser Console'da (F12) şöyle bir hata var mı:

```
Access to fetch at 'http://localhost:5000/api/scraper/scrape' from origin 'http://localhost:3000' 
has been blocked by CORS policy
```

#### ✅ Çözüm:
Backend'de (Program.cs) CORS ayarını kontrol edin:

```csharp
// Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",      // React
                "http://localhost:5173",      // Vite
                "http://localhost:4200",      // Angular
                "http://localhost:3001",      // Alternatif port
                "http://localhost:8080"       // Başka bir port ekleyin
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// ...

app.UseCors("AllowReactApp"); // UseAuthentication()'dan ÖNCE olmalı!
```

---

### 5️⃣ API URL'i Yanlış

#### Problem:
Frontend yanlış API URL'ine istek yapıyor.

#### Kontrol Edin:
```typescript
// ❌ YANLIŞ
const API_URL = 'http://localhost:5001'; // Backend 5000'de çalışıyor!

// ✅ DOĞRU
const API_URL = 'http://localhost:5000';
```

#### Test Edin:
```bash
# Backend çalışıyor mu?
curl http://localhost:5000/
# Beklenen: {"status":"ok","message":"TradeScout API is running",...}
```

---

## 🛠️ Debug Adımları (Sırayla Yapın)

### Adım 1: Backend Çalışıyor mu?
```bash
curl http://localhost:5000/
```
**Beklenen:** `{"status":"ok",...}`

---

### Adım 2: Token Mevcut mu?
Browser Console'da (F12):
```javascript
console.log('Token:', localStorage.getItem('token'));
```
**Beklenen:** Uzun bir string (eyJhbG...)

**Eğer `null`:** Login ekranına gidin ve yeniden giriş yapın.

---

### Adım 3: Token Geçerli mi?
```javascript
const token = localStorage.getItem('token');
const payload = JSON.parse(atob(token.split('.')[1]));
console.log('Token expire:', new Date(payload.exp * 1000));
console.log('Geçerli mi?', Date.now() < payload.exp * 1000);
```
**Beklenen:** `Geçerli mi? true`

**Eğer `false`:** Token süresi dolmuş, yeniden login yapın.

---

### Adım 4: API İsteği Token ile Gönderiliyor mu?
Browser DevTools → Network tab → Firma ara butonuna basın → İsteğe tıklayın → Headers sekmesi

**Kontrol edin:**
```
Request Headers:
  Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
  Content-Type: application/json
```

**Eğer `Authorization` yok:** Interceptor ekleyin (Çözüm 1).

---

### Adım 5: Backend Token'ı Kabul Ediyor mu?
Backend terminal çıktısına bakın veya Postman ile test edin:

```bash
# 1. Önce login yapın
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!"}'

# Gelen token'ı kopyalayın, örnek:
# TOKEN=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...

# 2. Token ile scraping deneyin
TOKEN="BURAYA_GELEN_TOKENI_YAPIŞTIRIN"

curl -X POST http://localhost:5000/api/scraper/scrape \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"searchQuery":"restaurants in Istanbul","maxResults":5}'
```

**Beklenen:** Scraping başlar, 200 OK döner.

**Eğer 401 döner:** Backend JWT ayarlarında sorun var.

---

## ✅ Hızlı Çözüm: Hazır Kod

İşte garantili çalışan tam bir örnek:

### 1. API Client (`src/services/api.ts`)
```typescript
import axios from 'axios';

const API_BASE_URL = 'http://localhost:5000';

export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
  timeout: 60000,
});

// OTOMATIK TOKEN EKLEME
apiClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
      console.log('✅ Token eklendi');
    } else {
      console.warn('⚠️ Token bulunamadı!');
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// OTOMATIK HATA YÖNETİMİ
apiClient.interceptors.response.use(
  (response) => {
    console.log('✅ API başarılı:', response.config.url);
    return response;
  },
  (error) => {
    console.error('❌ API hatası:', error.response?.status, error.response?.data);
    
    if (error.response?.status === 401) {
      console.log('🚪 Yetkisiz erişim, logout yapılıyor...');
      localStorage.removeItem('token');
      localStorage.removeItem('user');
      window.location.href = '/login';
    }
    
    return Promise.reject(error);
  }
);
```

### 2. Scraper Service (`src/services/scraper.service.ts`)
```typescript
import { apiClient } from './api';

export const scraperService = {
  async scrape(searchQuery: string, maxResults: number) {
    console.log('📤 Scraping isteği gönderiliyor...');
    console.log('🔍 Sorgu:', searchQuery);
    console.log('📊 Max sonuç:', maxResults);
    console.log('🔑 Token:', localStorage.getItem('token') ? 'Mevcut' : 'YOK!');
    
    const response = await apiClient.post('/api/scraper/scrape', {
      searchQuery,
      maxResults,
    });
    
    console.log('✅ Scraping başarılı:', response.data);
    return response.data;
  },
};
```

### 3. React Component
```typescript
import { useState } from 'react';
import { scraperService } from '../services/scraper.service';

export function ScraperForm() {
  const [searchQuery, setSearchQuery] = useState('');
  const [maxResults, setMaxResults] = useState(10);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    // DEBUG: Token kontrolü
    const token = localStorage.getItem('token');
    console.log('🔍 Token kontrolü:', token ? 'Mevcut ✅' : 'YOK ❌');
    
    if (!token) {
      alert('Token bulunamadı! Lütfen giriş yapın.');
      window.location.href = '/login';
      return;
    }
    
    setIsLoading(true);
    setError('');

    try {
      const result = await scraperService.scrape(searchQuery, maxResults);
      console.log('🎉 Sonuç:', result);
      alert(`Başarılı! ${result.businessesFound} firma bulundu.`);
    } catch (err: any) {
      console.error('❌ Hata:', err);
      const errorMsg = err.response?.data?.message || 'Bir hata oluştu';
      setError(errorMsg);
      alert(`Hata: ${errorMsg}`);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <form onSubmit={handleSubmit}>
      <h2>Firma Ara</h2>
      
      {error && <div style={{color: 'red'}}>{error}</div>}
      
      <input
        type="text"
        placeholder="Arama sorgusu"
        value={searchQuery}
        onChange={(e) => setSearchQuery(e.target.value)}
        required
      />
      
      <input
        type="number"
        placeholder="Firma sayısı"
        value={maxResults}
        onChange={(e) => setMaxResults(parseInt(e.target.value))}
        min="1"
        max="100"
        required
      />
      
      <button type="submit" disabled={isLoading}>
        {isLoading ? 'Aranıyor...' : 'Firma Ara'}
      </button>
    </form>
  );
}
```

---

## 🎯 Hızlı Test

Bu kodu **Browser Console'da** çalıştırın:

```javascript
// 1. Token var mı?
console.log('Token:', localStorage.getItem('token'));

// 2. Token geçerli mi?
const token = localStorage.getItem('token');
if (token) {
  const payload = JSON.parse(atob(token.split('.')[1]));
  console.log('Expire:', new Date(payload.exp * 1000));
  console.log('Geçerli:', Date.now() < payload.exp * 1000);
}

// 3. API test
fetch('http://localhost:5000/api/scraper/scrape', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${token}`,
  },
  body: JSON.stringify({
    searchQuery: 'test',
    maxResults: 5
  })
})
.then(r => r.json())
.then(d => console.log('✅ Başarılı:', d))
.catch(e => console.error('❌ Hata:', e));
```

---

## 📞 Hala Çalışmıyor mu?

Şunları gönderin:

1. **Browser Console çıktısı** (F12 → Console)
2. **Network tab çıktısı** (F12 → Network → İsteğin Headers ve Response'u)
3. **localStorage içeriği** (`localStorage.getItem('token')`)
4. **Frontend kodunuzda scraping isteği nasıl yapılıyor** (ilgili kod parçası)

Bu bilgilerle daha spesifik yardım edebilirim! 🚀
