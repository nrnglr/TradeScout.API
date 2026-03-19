# Tosla Ödeme Doğrulama - Frontend Entegrasyonu

## 🎯 Amaç
Tosla callback URL'sine güvenmeden, frontend-tetiklemeli ödeme doğrulama sistemi.

## 📋 Akış

```
1. Kullanıcı ödeme yapar
2. Tosla → Success URL'e yönlendirir (örn: /payment/success?orderId=FGS...)
3. Frontend otomatik olarak Backend'e verify isteği atar
4. Backend Tosla'dan ödeme durumunu sorgular
5. Başarılıysa krediler/üyelik aktifleşir
6. Frontend kullanıcıya sonucu gösterir
```

---

## 🔧 Backend Endpoint

### POST `/api/payment/verify/{orderId}`

**Request:**
```
POST https://api.fgstrade.com/api/payment/verify/FGS26031214450000002
```

**Response (Başarılı):**
```json
{
  "success": true,
  "message": "Ödeme başarıyla doğrulandı ve krediler yüklendi",
  "orderId": "FGS26031214450000002",
  "creditsAdded": 0,
  "packageName": "Starter",
  "isAlreadyProcessed": false,
  "userId": 2,
  "membershipEnd": "2026-04-12T14:45:00Z"
}
```

**Response (Zaten İşlenmiş):**
```json
{
  "success": true,
  "message": "Ödeme başarıyla doğrulandı ve krediler yüklendi",
  "orderId": "FGS26031214450000002",
  "creditsAdded": 0,
  "packageName": "Starter",
  "isAlreadyProcessed": true
}
```

**Response (Başarısız):**
```json
{
  "success": false,
  "message": "Ödeme başarısız: Yetersiz bakiye",
  "orderId": "FGS26031214450000002"
}
```

---

## 💻 Frontend Kodu (React/TypeScript)

### 1. Payment Success Sayfası

**Dosya:** `src/pages/payment/success.tsx`

```typescript
import { useEffect, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import axios from 'axios';

export default function PaymentSuccess() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const orderId = searchParams.get('orderId');
  
  const [verifying, setVerifying] = useState(true);
  const [result, setResult] = useState<any>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!orderId) {
      setError('Order ID bulunamadı');
      setVerifying(false);
      return;
    }

    // Otomatik ödeme doğrulama
    verifyPayment(orderId);
  }, [orderId]);

  const verifyPayment = async (orderId: string) => {
    try {
      console.log('🔍 Ödeme doğrulanıyor...', orderId);
      
      const response = await axios.post(
        `${process.env.NEXT_PUBLIC_API_URL}/api/payment/verify/${orderId}`,
        {},
        {
          headers: {
            'Content-Type': 'application/json'
          },
          timeout: 30000 // 30 saniye
        }
      );

      console.log('✅ Ödeme doğrulandı:', response.data);
      setResult(response.data);
      setVerifying(false);

      // Başarılıysa 3 saniye sonra dashboard'a yönlendir
      if (response.data.success) {
        setTimeout(() => {
          router.push('/dashboard');
        }, 3000);
      }

    } catch (err: any) {
      console.error('❌ Ödeme doğrulama hatası:', err);
      setError(err.response?.data?.message || 'Ödeme doğrulanamadı');
      setVerifying(false);
    }
  };

  if (verifying) {
    return (
      <div className="flex flex-col items-center justify-center min-h-screen">
        <div className="animate-spin rounded-full h-32 w-32 border-b-2 border-blue-500"></div>
        <p className="mt-4 text-lg">Ödemeniz doğrulanıyor...</p>
        <p className="text-sm text-gray-500">Lütfen bekleyin...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex flex-col items-center justify-center min-h-screen">
        <div className="text-red-500 text-6xl mb-4">❌</div>
        <h1 className="text-2xl font-bold text-red-600 mb-2">Ödeme Başarısız</h1>
        <p className="text-gray-600 mb-6">{error}</p>
        <button
          onClick={() => router.push('/pricing')}
          className="px-6 py-3 bg-blue-500 text-white rounded hover:bg-blue-600"
        >
          Paket Seçimine Dön
        </button>
      </div>
    );
  }

  if (result?.success) {
    return (
      <div className="flex flex-col items-center justify-center min-h-screen bg-green-50">
        <div className="text-green-500 text-6xl mb-4">✅</div>
        <h1 className="text-3xl font-bold text-green-600 mb-2">Ödeme Başarılı!</h1>
        
        {result.isAlreadyProcessed ? (
          <p className="text-gray-600 mb-4">Bu ödeme daha önce işlenmiş</p>
        ) : (
          <p className="text-gray-600 mb-4">Ödemeniz başarıyla alındı</p>
        )}

        <div className="bg-white p-6 rounded-lg shadow-md mb-6">
          <p className="text-sm text-gray-500">Sipariş No</p>
          <p className="font-mono text-lg">{result.orderId}</p>

          <p className="text-sm text-gray-500 mt-4">Paket</p>
          <p className="font-semibold text-xl">{result.packageName}</p>

          {result.creditsAdded > 0 && (
            <>
              <p className="text-sm text-gray-500 mt-4">Eklenen Kredi</p>
              <p className="font-semibold text-2xl text-blue-600">+{result.creditsAdded}</p>
            </>
          )}

          {result.membershipEnd && (
            <>
              <p className="text-sm text-gray-500 mt-4">Üyelik Bitiş Tarihi</p>
              <p className="font-semibold">{new Date(result.membershipEnd).toLocaleDateString('tr-TR')}</p>
            </>
          )}
        </div>

        <p className="text-sm text-gray-500 mb-4">Dashboard'a yönlendiriliyorsunuz...</p>
        
        <button
          onClick={() => router.push('/dashboard')}
          className="px-6 py-3 bg-green-500 text-white rounded hover:bg-green-600"
        >
          Dashboard'a Git
        </button>
      </div>
    );
  }

  return null;
}
```

### 2. API Service (axios instance)

**Dosya:** `src/services/api.ts`

```typescript
import axios from 'axios';

const api = axios.create({
  baseURL: process.env.NEXT_PUBLIC_API_URL || 'https://api.fgstrade.com',
  timeout: 30000,
  headers: {
    'Content-Type': 'application/json'
  }
});

// JWT token interceptor (eğer varsa)
api.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('accessToken');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

export default api;

// Payment verification
export const verifyPayment = async (orderId: string) => {
  const response = await api.post(`/api/payment/verify/${orderId}`);
  return response.data;
};
```

---

## 🧪 Manuel Test

### cURL ile Test

```bash
# Başarılı ödeme testi
curl -X POST https://api.fgstrade.com/api/payment/verify/FGS26031214450000002 \
  -H "Content-Type: application/json" \
  -v

# Beklenen yanıt:
# {
#   "success": true,
#   "message": "Ödeme başarıyla doğrulandı ve krediler yüklendi",
#   ...
# }
```

### Postman ile Test

1. **Method:** POST
2. **URL:** `https://api.fgstrade.com/api/payment/verify/{orderId}`
3. **Headers:**
   - `Content-Type: application/json`
4. **Body:** (empty)

---

## 📊 Log Örnekleri

### Backend Başarılı İşlem

```
🔍 ÖDEME DOĞRULAMA BAŞLADI | OrderId=FGS26031214450000002
📞 Tosla'dan ödeme durumu sorgulanıyor | OrderId=FGS26031214450000002
📋 Transaction bulundu | TxId=2000000002067296 | BankCode=00 | Amount=100
✅ Ödeme başarılı doğrulandı, aktivasyon yapılıyor
🎯 ActivateMembershipAsync başladı
✅ OrderId'den UserId çıkarıldı → UserId=2
✅ Amount'dan ProductCode belirlendi | 1 TL → 1274715
👤 Kullanıcı bulundu | Email=user@example.com | Mevcut Kredi=5
📦 Paket bulundu | Starter
👑 ÜYELİK AKTİFLEŞTİRİLİYOR
💾💾💾 SaveChangesAsync ÇAĞRILIYOR...
✅✅✅ VERİTABANI GÜNCELLENDİ
🎉 ÖDEME DOĞRULAMA TAMAMLANDI | UserId=2 | Credits=0
```

---

## 🚀 Production Deployment

### 1. Backend Deploy
```bash
# Build
dotnet publish -c Release -o ./publish

# Deploy (PM2)
pm2 restart backend
pm2 logs backend --lines 50
```

### 2. Frontend Deploy
```bash
# .env.production dosyasına ekle:
NEXT_PUBLIC_API_URL=https://api.fgstrade.com

# Build ve deploy
npm run build
pm2 restart frontend
```

### 3. Test
```bash
# Success sayfasını test et
https://fgstrade.com/payment/success?orderId=FGS26031214450000002
```

---

## ✅ Avantajlar

1. ✅ **Callback'e bağımlı değil** - Frontend tetiklemeli
2. ✅ **Güvenli** - Tosla API'den direkt sorgulama
3. ✅ **Hızlı** - Kullanıcı success sayfasında sonucu hemen görür
4. ✅ **İdempotent** - Aynı ödeme birden fazla kez işlenmez
5. ✅ **Retry** - Başarısız olursa kullanıcı tekrar deneyebilir
6. ✅ **Detaylı log** - Her adım loglanıyor

---

## 🎯 Next Steps

1. Frontend payment success sayfasını yukarıdaki koda göre güncelle
2. `.env.production` dosyasına `NEXT_PUBLIC_API_URL` ekle
3. Backend'i production'a deploy et
4. Test ödemesi yap ve logları izle
5. Başarılı olduğunda dashboard'da kredilerin geldiğini kontrol et

**Başarılar! 🎉**
