# Frontend İndirim Kodu Kullanım Kılavuzu

Bu döküman, FGS Trade uygulamasında indirim kodu sisteminin frontend tarafında nasıl kullanılacağını açıklar.

## 📋 İçindekiler
1. [Genel Bakış](#genel-bakış)
2. [API Endpoints](#api-endpoints)
3. [Kullanım Akışı](#kullanım-akışı)
4. [Örnek Kod](#örnek-kod)
5. [Hata Yönetimi](#hata-yönetimi)
6. [UI/UX Önerileri](#uiux-önerileri)

## 🎯 Genel Bakış

İndirim kodu sistemi, kullanıcıların kredi paketi satın alırken indirim kodları kullanarak daha uygun fiyatlarla alışveriş yapmalarını sağlar.

### İndirim Kodu Formatı
- **Format**: `FGS10_XXXXXX` (örnek: `FGS10_A1B2C3`)
- **İndirim Oranı**: %10
- **Kullanım Limiti**: 1000 kullanım/kod
- **Durum**: Aktif kodlar kullanılabilir

## 🔌 API Endpoints

### 1. İndirim Kodu Doğrulama
**Endpoint**: `POST /api/discountcode/validate`

**Headers**:
```json
{
  "Authorization": "Bearer {access_token}",
  "Content-Type": "application/json"
}
```

**Request Body**:
```json
{
  "code": "FGS10_A1B2C3",
  "originalPrice": 100.00
}
```

**Success Response** (200 OK):
```json
{
  "isValid": true,
  "code": "FGS10_A1B2C3",
  "discountPercentage": 10,
  "originalPrice": 100.00,
  "discountAmount": 10.00,
  "finalPrice": 90.00,
  "message": "İndirim kodu geçerli."
}
```

**Error Response** (400 Bad Request):
```json
{
  "isValid": false,
  "code": "FGS10_INVALID",
  "discountPercentage": 0,
  "originalPrice": 100.00,
  "discountAmount": 0,
  "finalPrice": 100.00,
  "message": "İndirim kodu bulunamadı veya geçersiz."
}
```

### 2. Ödeme İşlemi (İndirim Kodu ile)
**Endpoint**: `POST /api/payment/initialize`

**Request Body**:
```json
{
  "amount": 90.00,
  "creditAmount": 100,
  "successUrl": "https://yourapp.com/payment/success",
  "failUrl": "https://yourapp.com/payment/fail",
  "discountCode": "FGS10_A1B2C3"
}
```

**Not**: `amount` değeri, indirim uygulanmış final fiyat olmalıdır.

### 3. Kullanıcı Bilgisi Güncelleme
**Endpoint**: `GET /api/auth/me`

Ödeme başarılı olduktan sonra kullanıcının güncel kredi bakiyesini almak için kullanın.

**Success Response** (200 OK):
```json
{
  "id": "user-id",
  "email": "user@example.com",
  "name": "User Name",
  "credits": 150,
  "maxResultsPerSearch": 60,
  "role": "Free"
}
```

## 🔄 Kullanım Akışı

### Adım 1: Kullanıcı İndirim Kodu Girer
Ödeme sayfasında bir input alanı ekleyin:
```html
<input type="text" 
       placeholder="İndirim kodu (opsiyonel)" 
       id="discountCode" />
<button onclick="validateDiscountCode()">Uygula</button>
```

### Adım 2: İndirim Kodunu Doğrula
```javascript
async function validateDiscountCode() {
  const code = document.getElementById('discountCode').value;
  const originalPrice = 100.00; // Kredi paketi fiyatı
  
  try {
    const response = await fetch('https://api.yourapp.com/api/discountcode/validate', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${getAccessToken()}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        code: code,
        originalPrice: originalPrice
      })
    });
    
    const result = await response.json();
    
    if (result.isValid) {
      // İndirim başarılı
      updatePriceDisplay(result);
      showSuccessMessage(result.message);
    } else {
      // İndirim geçersiz
      showErrorMessage(result.message);
    }
  } catch (error) {
    showErrorMessage('İndirim kodu doğrulanırken bir hata oluştu.');
  }
}
```

### Adım 3: Fiyatı Güncelle
```javascript
function updatePriceDisplay(discountData) {
  // Orijinal fiyatı göster (üzeri çizili)
  document.getElementById('originalPrice').textContent = `${discountData.originalPrice} TL`;
  document.getElementById('originalPrice').style.textDecoration = 'line-through';
  
  // İndirim miktarını göster
  document.getElementById('discountAmount').textContent = `-${discountData.discountAmount} TL`;
  
  // Final fiyatı göster (vurgulu)
  document.getElementById('finalPrice').textContent = `${discountData.finalPrice} TL`;
  
  // İndirim kodunu sakla (ödeme için)
  sessionStorage.setItem('appliedDiscountCode', discountData.code);
  sessionStorage.setItem('finalPrice', discountData.finalPrice);
}
```

### Adım 4: Ödeme İşlemini Başlat
```javascript
async function initiatePayment() {
  const discountCode = sessionStorage.getItem('appliedDiscountCode');
  const finalPrice = sessionStorage.getItem('finalPrice') || 100.00;
  
  const paymentData = {
    amount: parseFloat(finalPrice),
    creditAmount: 100, // Satın alınacak kredi miktarı
    successUrl: `${window.location.origin}/payment/success`,
    failUrl: `${window.location.origin}/payment/fail`,
    discountCode: discountCode // İndirim kodu varsa gönder
  };
  
  try {
    const response = await fetch('https://api.yourapp.com/api/payment/initialize', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${getAccessToken()}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(paymentData)
    });
    
    const result = await response.json();
    
    if (result.paymentUrl) {
      // Kullanıcıyı ödeme sayfasına yönlendir
      window.location.href = result.paymentUrl;
    }
  } catch (error) {
    showErrorMessage('Ödeme başlatılırken bir hata oluştu.');
  }
}
```

### Adım 5: Ödeme Sonrası Kullanıcı Bilgilerini Güncelle
```javascript
async function updateUserCredits() {
  try {
    const response = await fetch('https://api.yourapp.com/api/auth/me', {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${getAccessToken()}`,
        'Content-Type': 'application/json'
      }
    });
    
    const userData = await response.json();
    
    // localStorage'ı güncelle
    localStorage.setItem('userCredits', userData.credits);
    
    // UI'ı güncelle
    updateCreditsDisplay(userData.credits);
    
    // Session'ı temizle
    sessionStorage.removeItem('appliedDiscountCode');
    sessionStorage.removeItem('finalPrice');
    
  } catch (error) {
    console.error('Kullanıcı bilgileri güncellenirken hata:', error);
  }
}

// Ödeme başarı sayfasında çağırın
if (window.location.pathname === '/payment/success') {
  updateUserCredits();
}
```

## 🔧 Örnek Kod

### React Component Örneği

```jsx
import React, { useState } from 'react';

const PaymentPage = () => {
  const [discountCode, setDiscountCode] = useState('');
  const [discountData, setDiscountData] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  
  const originalPrice = 100.00;

  const validateDiscount = async () => {
    if (!discountCode.trim()) {
      setError('Lütfen bir indirim kodu girin');
      return;
    }
    
    setLoading(true);
    setError('');
    
    try {
      const response = await fetch('/api/discountcode/validate', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${localStorage.getItem('token')}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          code: discountCode,
          originalPrice: originalPrice
        })
      });
      
      const result = await response.json();
      
      if (result.isValid) {
        setDiscountData(result);
        setError('');
      } else {
        setError(result.message);
        setDiscountData(null);
      }
    } catch (err) {
      setError('İndirim kodu doğrulanırken bir hata oluştu');
    } finally {
      setLoading(false);
    }
  };

  const handlePayment = async () => {
    const paymentData = {
      amount: discountData ? discountData.finalPrice : originalPrice,
      creditAmount: 100,
      successUrl: `${window.location.origin}/payment/success`,
      failUrl: `${window.location.origin}/payment/fail`,
      discountCode: discountData?.code
    };
    
    // Ödeme API çağrısı...
  };

  return (
    <div className="payment-container">
      <h2>Kredi Satın Al</h2>
      
      {/* Fiyat Gösterimi */}
      <div className="price-section">
        {discountData ? (
          <>
            <p className="original-price">
              <del>{discountData.originalPrice} TL</del>
            </p>
            <p className="discount">
              İndirim: -{discountData.discountAmount} TL
            </p>
            <p className="final-price">
              {discountData.finalPrice} TL
            </p>
          </>
        ) : (
          <p className="price">{originalPrice} TL</p>
        )}
      </div>
      
      {/* İndirim Kodu Input */}
      <div className="discount-section">
        <input
          type="text"
          placeholder="İndirim kodu (opsiyonel)"
          value={discountCode}
          onChange={(e) => setDiscountCode(e.target.value.toUpperCase())}
          disabled={loading}
        />
        <button 
          onClick={validateDiscount}
          disabled={loading}
        >
          {loading ? 'Kontrol ediliyor...' : 'Uygula'}
        </button>
      </div>
      
      {/* Hata Mesajı */}
      {error && <p className="error-message">{error}</p>}
      
      {/* Ödeme Butonu */}
      <button 
        className="payment-button"
        onClick={handlePayment}
      >
        Ödemeye Geç
      </button>
    </div>
  );
};

export default PaymentPage;
```

## ⚠️ Hata Yönetimi

### Yaygın Hata Durumları

| Hata | Sebep | Çözüm |
|------|-------|-------|
| "İndirim kodu bulunamadı" | Kod yanlış girilmiş | Kullanıcıdan kodu kontrol etmesini isteyin |
| "İndirim kodu kullanım limiti dolmuş" | Kod maksimum kullanıma ulaşmış | Farklı bir kod denemesini önerin |
| "İndirim kodu aktif değil" | Kod devre dışı bırakılmış | Destek ekibiyle iletişime geçmesini önerin |
| 401 Unauthorized | Token geçersiz veya eksik | Kullanıcıyı yeniden giriş yapmaya yönlendirin |

### Hata Mesajları UI

```javascript
const errorMessages = {
  'not_found': 'İndirim kodu bulunamadı. Lütfen kodunuzu kontrol edin.',
  'expired': 'Bu indirim kodunun kullanım limiti dolmuştur.',
  'inactive': 'Bu indirim kodu aktif değil.',
  'network_error': 'Bağlantı hatası. Lütfen tekrar deneyin.'
};

function showErrorMessage(errorType) {
  const message = errorMessages[errorType] || 'Bir hata oluştu.';
  // Toast, alert veya inline message göster
  toast.error(message);
}
```

## 🎨 UI/UX Önerileri

### 1. İndirim Kodu Input Alanı
- **Placeholder**: "İndirim kodu (opsiyonel)" yazısı ekleyin
- **Auto-uppercase**: Kullanıcı küçük harf girerse otomatik büyük harfe çevirin
- **Karakter limiti**: 15-20 karakter ile sınırlayın
- **Temizle butonu**: Kullanıcının kodu kolayca silebilmesi için

### 2. Fiyat Gösterimi
```css
.original-price {
  text-decoration: line-through;
  color: #999;
  font-size: 14px;
}

.discount {
  color: #27ae60;
  font-weight: bold;
  font-size: 16px;
}

.final-price {
  color: #2c3e50;
  font-weight: bold;
  font-size: 24px;
}
```

### 3. Başarı/Hata Mesajları
- ✅ **Başarılı**: Yeşil renk, check icon
- ❌ **Hata**: Kırmızı renk, error icon
- ⏳ **Yükleniyor**: Loading spinner

### 4. Animasyonlar
- İndirim uygulandığında fiyat güncelleme animasyonu
- Başarılı doğrulama için confetti efekti (opsiyonel)
- Smooth scroll to payment button

### 5. Mobile Responsive
```css
@media (max-width: 768px) {
  .discount-section {
    flex-direction: column;
    gap: 10px;
  }
  
  .discount-section input,
  .discount-section button {
    width: 100%;
  }
}
```

## 📱 Ödeme Sonrası İşlemler

### Success Page
```javascript
// /payment/success sayfasında
useEffect(() => {
  // 1. Kullanıcı bilgilerini güncelle
  fetchUserData();
  
  // 2. Başarı mesajı göster
  toast.success('Ödeme başarılı! Krediniz hesabınıza tanımlandı.');
  
  // 3. Session temizle
  sessionStorage.removeItem('appliedDiscountCode');
  sessionStorage.removeItem('finalPrice');
  
  // 4. 3 saniye sonra dashboard'a yönlendir
  setTimeout(() => {
    router.push('/dashboard');
  }, 3000);
}, []);
```

## 🔐 Güvenlik Notları

1. **Token Yönetimi**: Her API isteğinde `Authorization` header'ını ekleyin
2. **Input Validation**: İndirim kodunu frontend'de de validate edin (format kontrolü)
3. **XSS Koruması**: Kullanıcı inputlarını sanitize edin
4. **HTTPS**: Tüm API isteklerinin HTTPS üzerinden yapıldığından emin olun

## 📊 Analytics ve Tracking

Aşağıdaki olayları tracking sistemine ekleyin:

```javascript
// İndirim kodu uygulandı
trackEvent('discount_code_applied', {
  code: discountCode,
  discount_amount: discountAmount,
  original_price: originalPrice,
  final_price: finalPrice
});

// İndirim kodu hatası
trackEvent('discount_code_error', {
  code: discountCode,
  error_message: errorMessage
});

// İndirimli ödeme tamamlandı
trackEvent('payment_completed_with_discount', {
  code: discountCode,
  amount_paid: finalPrice,
  credits_purchased: creditAmount
});
```

## 📞 Destek ve Yardım

- **API Hataları**: Backend loglarını kontrol edin
- **UI Sorunları**: Browser console'u inceleyin
- **Test Kodları**: Demo ortamda `FGS10_TEST01` kodunu kullanabilirsiniz

---

## 🚀 Quick Start Checklist

- [ ] İndirim kodu input alanı eklendi
- [ ] Validate endpoint entegrasyonu yapıldı
- [ ] Fiyat güncelleme UI'ı hazırlandı
- [ ] Ödeme endpoint'ine discountCode parametresi eklendi
- [ ] Ödeme sonrası kullanıcı bilgileri güncelleme eklendi
- [ ] Hata yönetimi implemente edildi
- [ ] Mobile responsive tasarım tamamlandı
- [ ] Test senaryoları oluşturuldu

---

**Son Güncelleme**: 19 Mart 2026
**Versiyon**: 1.0
**İletişim**: Backend ekibi ile koordinasyon için Slack #dev-payment kanalını kullanın
