Kimlik Doğrulama
 Bu alan, entegrasyona başlamadan önce gerekli güvenlik ve kimlik doğrulama adımları hakkında temel bilgilere ulaşmanız için hazırlanmıştır.

MorPOS API’ları ile haberleşirken SHA256 algoritması ile kimlik doğrulama yapılması zorunludur. Aşağıdaki adımları takip ederek kimlik doğrulama mekanizmasını entegrasyonunuza dahil edebilirsiniz.


Copy
Base URL PROD: https://gateway.prd.morpara.com

Copy
Base URL Sandbox: https://finagopay-pf-api-gateway.prp.morpara.com
1) Headers
MorPOS Üye İşyeri Paneli > Key Yönetimi > Key Tanıma ekranına erişerek
ClientId ve ClientSecret bilgilerinizi temin ediniz.

Bu bilgiler, yapacağınız tüm API çağrılarının header bilgileri içerisinde gönderilmelidir.

2) Request Sign
Herhangi bir API çağrısında, request gövdesi ile birlikte gönderilmesi gereken sign parametresi zorunludur.

Sign değeri şu şekilde üretilir:

Request body’deki alanlar alınır

Alt alta yazmak yerine string olarak birleştirilir

Sonuna API Key eklenir

SHA256 ile şifrelenir

Base64 formatına çevrilip büyük harfe dönüştürülür

Üretilen bu değer sign alanında API isteği ile gönderilir


Copy
var CryptoJS = require("crypto-js");

let now = new Date();
let xTimestamp =
    now.getFullYear().toString() +
    String(now.getMonth() + 1).padStart(2, '0') +
    String(now.getDate()).padStart(2, '0') +
    String(now.getHours()).padStart(2, '0') +
    String(now.getMinutes()).padStart(2, '0') +
    String(now.getSeconds()).padStart(2, '0');

pm.environment.set("xTimestamp", xTimestamp);

const MrpApikey = "api_key_bilginiz";
const clientSecretKey = "client_secret_key_bilginiz";
const clientId = "client_id_bilginiz";
const MrpMerchantId = "merchant_id_bilginiz";

function generateRandomId(prefix, length) {
    const randomPart = Array(length)
        .fill(0)
        .map(() => Math.floor(Math.random() * 10))
        .join("");
    return `${prefix}${randomPart}`;
}

const conversationId = generateRandomId("MSD", 17); 
const conversationIdPayment = generateRandomId("YSD", 17);


console.log("Client Secret",clientSecretKey);

const decodedClientSecret = CryptoJS.enc.Base64.parse(clientSecretKey).toString(CryptoJS.enc.Utf8);

console.log("Decoded Client Secret (UTF-8): ", decodedClientSecret);


const combined = decodedClientSecret + xTimestamp;
console.log("Combined Value (Decoded Client Secret + X-Timestamp): ", combined);


const sha256Hash = CryptoJS.SHA256(combined);
console.log("SHA256 Hash (Raw): ", sha256Hash.toString(CryptoJS.enc.Hex)); // 


const utf8Hash = CryptoJS.enc.Utf8.parse(sha256Hash.toString(CryptoJS.enc.Hex));
const finalEncoded = CryptoJS.enc.Base64.stringify(utf8Hash);
console.log("Final Encoded Hash (Base64, UTF-8): ", finalEncoded);


pm.variables.set("EncodedHash", finalEncoded);
pm.variables.set("MpConversationId", conversationId);
pm.variables.set("MpConversationIdPayment", conversationIdPayment);

let requestBodyDump = JSON.parse(pm.request.body);


const requestBody = {
...
};

function isNullOrWhiteSpace(str) {
    return !str || str.trim().length === 0;
}

function calculateDynamicHash(requestBody) {
    const concatenatedString = Object.values(requestBody)
        .map((value) => `${value}`) 
        .join(";"); 

    if (isNullOrWhiteSpace(concatenatedString))
        return false;

    const hash = CryptoJS.enc.Base64.stringify(CryptoJS.SHA256(CryptoJS.enc.Utf8.parse(concatenatedString))).toUpperCase();

    return hash;
}

const hashResult = calculateDynamicHash(requestBody);
    pm.variables.set("MpSign", hashResult);



Test Kartları
Bu sayfada yer alan kartlar, API entegrasyonlarını ve ödeme akışlarını güvenli bir şekilde test etmek için hazırlanmıştır.
Test kartları sayesinde canlı sistemleri etkilemeden; Non‑3DS ve 3D Secure ödeme senaryolarını deneyebilir, doğrulama ve hata akışlarını kontrol edebilirsiniz.

Gerçek finansal işlem gerçekleşmez, sadece simülasyon amaçlıdır.

3D Secure işlemlerde doğrulama ekranında ilgili 3D şifresi girilmelidir.

Test senaryoları tamamlanmadan canlı ortama geçilmesi önerilmez.

💳 Test Kartları
Kart Numarası
Son Kullanma Tarihi
CVV
3D Şifresi
Not
5246776356903508

12/2030

792

123456

3D Secure Test Kart

4691810771946876

11/2048

858

123456

3D Secure Test Kart

4508034508034509

12/2036

000

123456

3D Secure Test Kart

5269737320050521

12/2030

000

123456

3D Secure Test Kart

5413330057004112

12/2026

312

-

Non‑3DS / Genel Test Kart

Önemli Notlar
Tüm kartlar yalnızca test ortamında geçerlidir.

3D Secure işlemlerde doğrulama ekranında tüm kartların 3D Şifresi: 123456 olarak girilmelidir

Canlı ortamda test kartları çalışmaz.

Gerçek kart bilgileri dokümantasyonda paylaşılmaz.

Previous
Sandbox / Test Ortamı


Hata Yönetimi
Bu sayfada, API servislerinden dönebilecek hata kodları ve açıklamaları listelenmiştir.
Hata kodları, ödeme, iade, iptal ve sorgulama gibi tüm servisler için ortak kullanılmaktadır.
Entegrasyon sırasında karşılaşılan hataları anlamak ve doğru aksiyon almak için bu tablo referans alınabilir.

⚠️Hata Kodları
Hata Kodu
Hata Mesajı
Hata Mesajı
B0000

Approved

Onaylandı

B0001

Refer to card issuer

Kartı veren banka ile iletişime geçiniz.

B0002

Refer to card issuer's special conditions

Kategori yok.

B0003

Invalid merchant

Üye kodu hatalı/tanımsız.

B0004

Capture card

Karte el koyunuz.

B0005

Do not honor

Red

B0006

Error

Hatalı işlem

B0007

Pick-up card, special condition

Karta el koyunuz.

B0008

Honor with ID

Kimlik kontrolü

B0009

Try Again

Tekrar deneyin.

B0010

Partial Approval

İşlem kısmen onaylandı.

B0011

Approved (VIP)

Onaylandı VIP

B0012

Invalid transaction

Hatalı işlem

B0013

Invalid amount

Hatalı miktar

B0014

Invalid card number

Hatalı kart no

B0015

Invalid issuer

Müşteri yok

B0016

Approved, update track 3

İşlem onaylandı, Track 3 verisi güncellenecek.

B0017

Customer cancellation

Müşteri iptali.

B0018

Customer dispute

Müşteri itirazı.

B0019

Re-enter transaction

İşlemi tekrar gir.

B0020

Invalid response

Geçersiz yanıt.

B0021

No action taken

İşlem yapılmadı.

B0022

Suspected malfunction

Arıza şüphesi.

B0023

Unacceptable transaction fee

İşlem ücreti uygun değil.

B0024

File update not supported by receiver

Dosya güncellemesi alıcı tarafından desteklenmiyor.

B0025

Unable to locate record on file

Dosyada kayıt bulunamadı.

B0026

Duplicate file update record, old record replaced

Dosya güncelleme kaydı yinelendi, eski kayıt değiştirildi.

B0027

File update field edit error

Dosya güncelleme alanı düzenleme hatası.

B0028

Original is declined

Orijinal teklif reddedildi.

B0029

Original not found

Orijinal teklif bulunamadı.

B0030

Format error

Format hatası.

B0031

Bank not supported by switch

Banka switch tarafından desteklenmiyor.

B0032

Completed partially

Kısmen tamamlandı.

B0033

Expired card - pick up

Süresi dolmuş kart.

B0034

Suspected fraud - pick up

Dolandırıcılık şüphesi.

B0035

Card acceptor contact acquirer - pick up

Kart kabul eden, bankayı arayın.

B0036

Restricted card - pick up

Kısıtlı kart – Kartı alın.

B0037

Card acceptor call acquirer security - pick up

Kart kabul eden, banka güvenliğini arayın – Kartı alın.

B0038

Allowable PIN tries exceeded

PIN deneme hakkı doldu.

B0039

No credit account

Kredi hesabı yok.

B0041

Lost card

Kayıp kart.

B0042

No universal account

Evrensel hesap yok.

B0043

Stolen card

Çalıntı kart.

B0044

No investment account

Yatırım hesabı yok.

B0045

Reserved for ISO use

ISO kullanımı için ayrılmıştır.

B0046

Reserved for ISO use

ISO kullanımı için ayrılmıştır.

B0047

Reserved for ISO use

ISO kullanımı için ayrılmıştır.

B0048

Reserved for ISO use

ISO kullanımı için ayrılmıştır.

B0049

Reserved for ISO use

ISO kullanımı için ayrılmıştır.

B0050

Reserved for ISO use

ISO kullanımı için ayrılmıştır.

B0051

Insufficient funds/over credit limit

Yetersiz hesap.

B0052

No checking account

Hesap noyu kontrol edin.

B0053

No savings account

Hesap yok.

B0054

Expired card

Süresi geçmiş kart.

B0055

Invalid PIN

Şifre hatalı.

B0056

No card record

Kart kaydı yok.

B0057

Transaction not permitted to issuer/cardholder

Kart sahibine/kartı veren kuruluşa işlem izni verilmedi.

B0058

Transaction not permitted to acquirer/terminal

Alıcı/terminal için işleme izin verilmiyor.

B0059

Suspected fraud

Dolandırıcılık şüphesi.

B0060

Card acceptor contact acquirer

Kart kabul eden kuruluş, ödeme işlemcisiyle iletişime geçmektedir.

B0061

Exceeds withdrawal amount limit

Para çekme tutarı limiti aşıldı.

B0062

Restricted card

Yasaklanmış kart.

B0063

Security violation

Güvenlik ihlali.

B0064

Original amount incorrect

Orijinal tutar yanlış.

B0065

Exceeds withdrawal count limit OR Identity Check Soft-Decline of EMV 3DS Authentication (merchant should resubmit authentication with 3DSv1)

Para çekme işlem adedi limitini aşmaktadır VEYA EMV 3DS kimlik doğrulamasında yumuşak ret. (üye işyeri, 3DSv1 ile kimlik doğrulamayı yeniden göndermelidir).

B0066

Card acceptor call acquirer's security department

Kart kabul eden taraf, üye işyeri bankasının güvenlik departmanını aramalıdır.

B0067

Hard capture (requires that card be picked up at ATM)

Zorla el koyma (kartın ATM’de alıkonulmasını gerektirir).

B0068

Response received too late

Yanıt çok geç alındı.

B0069

Reserved for ISO use

ISO kullanımı için ayrılmıştır.

B0070

Contact Card Issuer

Kartı veren banka ile iletişime geçiniz.

B0071

PIN Not Changed

PIN değiştirilmedi.

B0072

Reserved for ISO use

ISO kullanımı için ayrılmıştır.

B0073

Reserved for ISO use

ISO kullanımı için ayrılmıştır.

B0074

Reserved for ISO use

ISO kullanımı için ayrılmıştır.

B0075

Allowable number of PIN tries exceeded

İzin verilen PIN deneme sayısı aşıldı.

B0076

Key synchronisation error

Anahtar senkronizasyon hatası.

B0077

Decline of Request – No script available

Talep reddedildi-Tanımlı işlem akışı yok.

B0078

Unsafe PIN

Güvensiz PIN.

B0079

ARQC failed

ARQC doğrulanmadı.

B0080

Reserved for private use

Özel kullanım için ayrılmıştır.

B0081

Domestic Debit Transaction Not Allowed (Regional use only)

Yurt içi debit işlemi yapılamaz (yalnızca bölgesel kullanım).

B0082

Policy (Mastercard use only)

Politika (yalnızca Mastercard kullanımına özeldir).

B0083

Fraud/Security (Mastercard use only)

Dolandırıcılık/Güvenlik (yalnızca Mastercard kullanımına özeldir).

B0084

Invalid Authorization Life Cycle

Geçersiz yetkilendirme yaşam döngüsü.

B0085

Approval of request (for PIN management messages)

Talep onaylandı (PIN yönetimi mesajları için).

B0086

PIN Validation not possible

PIN doğrulaması yapılamıyor.

B0087

Purchase Amount Only, No Cash Back Allowed

Yalnızca alışveriş tutarı geçerlidir, nakit çekim yapılamaz.

B0088

Cryptographic failure

Kriptografik hata.

B0089

Unacceptable PIN—Transaction Declined—Retry

PIN kabul edilemez — İşlem reddedildi — Tekrar deneyin.

B0090

Cutoff is in process (switch ending a day's business and starting the next. Transaction can be sent again in a few minutes)

Gün sonu (cut-off) işlemi devam ediyor (switch gün sonunu kapatıp yeni günü başlatıyor. İşlem birkaç dakika sonra tekrar gönderilebilir).

B0091

Authorization System or issuer system inoperative

Yetkilendirme sistemi veya kartı çıkaran banka sistemi çalışmıyor.

B0092

Unable to route transaction

İşlem yönlendirilemedi.

B0093

Transaction cannot be completed. Violation of law

İşlem tamamlanamaz. Yasal ihlal.

B0095

Reconcile error

Hesap eşleşme hatası.

B0096

System error

Sistem hatası.

B0098

Duplicate transmission detected

Aynı işlem birden fazla gönderildi.

B0100

PaymentID is not found

Ödeme kimliği bulunamadı.

B0101

PaymentID is invalid

Ödeme kimliği geçersiz.

B9999

Bank Validation Error

Banka doğrulama hatası.

Üye işyerlerinin 3D doğrulama olmaksızın ödeme talebi göndermesine olanak tanır.

Endpoint

Copy
POST /v1/Payment/DoPayment
Headers
Anahtar
Tür
Açıklama
Örnek
x-ClientID

String

API istemcisi için benzersiz tanımlayıcı

sizin-istemci-id’niz

x-ClientSecret

String

API istemcisi için gizli anahtar

sizin-gizli-anahtarınız

x-GrantType

String

Kullanılan erişim token türü

client_credentials

x-Scope

String

API kapsamı

pf_write, pf_read

x-Timestamp

String

İstek zaman damgası (ISO 8601)

2024-12-17T12:34:56Z

Request Body
Alan
Zorunlu
Tür
Açıklama
Örnek
merchantId

✅

String

Üye işyeri için benzersiz kimlik

100000000000004

returnUrl

✅

URL

Başarılı işlem sonrası yönlendirme URL

https://www.ornek.com/basari

failUrl

✅

URL

Başarısız işlem sonrası yönlendirme URL

https://www.ornek.com/hata

paymentMethod

✅

String

Ödeme yöntemi türü

CARDPAYMENT

language

✅

String

İşlem dili

tr

conversationId

✅

String

Takip için benzersiz kimlik

MSD2024030500000000115

sign

✅

String

İsteğin dijital imzası

AAVWY3RZKJVVCEAD8LDQB4KWJL4QX/IQQRGQ3XIWAAG=

paymentInstrumentType

✅

String

Ödeme aracı türü

CARD

cardDetails.cardNo

✅

String

Kart numarası

4508034508034581

cardDetails.expDate

✅

String

Kart son kullanma tarihi (AAYY formatında)

1226

cardDetails.cvv

✅

String

Kart güvenlik kodu

000

transactionDetails.transactionType

✅

String

İşlem türü

SALE

transactionDetails.installmentCount

✅

Integer

Taksit sayısı

1

transactionDetails.amount

✅

Float

İşlem tutarı

60.00

transactionDetails.currencyCode

✅

Integer

Para birimi kodu (örneğin Türk Lirası=949)

949

transactionDetails.vftFlag

❌

Boolean

VFT’nin etkin olup olmadığı (opsiyonel)

false

cardHolderInfo.cardHolderName

✅

String

Kart sahibinin adı

Selim Dil

cardHolderInfo.buyerGsm

✅

String

Alıcının GSM numarası

55555555

extraParameter.pfSubMerchantId

❌

String

Alt üye işyeri kimliği (varsa)

12345

Örnek Request

Copy
{
  "merchantId": "100000000000004",
  "returnUrl": "https://www.ornek.com/basari",
  "failUrl": "https://www.ornek.com/hata",
  "paymentMethod": "CARDPAYMENT",
  "language": "tr",
  "conversationId": "MSD2024030500000000115",
  "sign": "AAVWY3RZKJVVCEAD8LDQB4KWJL4QX/IQQRGQ3XIWAAG=",
  "paymentInstrumentType": "CARD",
  "cardDetails": {
    "cardNo": "4508034508034581",
    "expDate": "1226",
    "cvv": "000"
  },
  "transactionDetails": {
    "transactionType": "SALE",
    "installmentCount": 1,
    "amount": 60.00,
    "currencyCode": 949,
    "vftFlag": false
  },
  "cardHolderInfo": {
    "cardHolderName": "Selim Dil",
    "buyerGsm": "55555555"
  },
  "extraParameter": {
    "pfSubMerchantId": "12345"
  }
}
Örnek Response

Copy
{
  "code": "B0000",
  "message": "Onaylandı",
  "resultCode": "B0000",
  "resultDescription": "Onaylandı",
  "responseDate": "05/02/2025 15:09:09",
  "conversationId": "YBS11108699401304534",
  "orderId": "5000000045202502030041",
  "paymentId": "5000000045202502030041",
  "bankUniqueReferenceNumber": "503615446559",
  "transactionDate": "05/02/2025 15:09:09",
  "currency": "949",
  "authCode": "655050",
  "paymentInstrumentType": "CARDPAYMENT",
  "sign": "AAVWY3RZKJVVCEAD8LDQB4KWJL4QX/IQQRGQ3XIWAAG=",
  "instrumentDetail": {
    "maskedCardNumber": "524677******3508",
    "cardType": "CARD"
  },
  "paymentInfo": {
    "installmentCount": 1,
    "payFacCommissionRate": 1.00,
    "amount": 25.00,
    "payFacCommissionAmount": 0.2500
  },
  "okUrl": null,
  "failUrl": null
}


3D Secure Ödeme
3D Ödeme (3D Secure), kartlı online ödemelerde kart sahibinin banka tarafından doğrulanmasını sağlayan, iki aşamalı bir ödeme sürecidir.
Bu yöntemle yapılan işlemlerde ödeme, bankanın 3D Secure altyapısı üzerinden kullanıcı doğrulaması alındıktan sonra kesinleştirilir.

3D Ödeme süreci yalnızca ödeme başlatma adımıyla tamamlanmaz.
Kullanıcının bankadaki doğrulama adımını tamamlamasının ardından, işlem sonucunun ayrıca doğrulanarak ödemenin başarılı veya başarısız olarak sonlandırılması gerekir.

3D Ödeme Süreci
3D Ödeme aşağıdaki iki ana adımdan oluşur:

3D Ödeme Başlatma (Init3d)

3D Ödeme Tamamlama (Auth3d)

Her iki adımın da eksiksiz şekilde uygulanması gerekmektedir.

1. 3D Ödeme Başlatma (Init3d)
Bu adımda üye işyeri, kart bilgileri ve işlem detayları ile birlikte 3D ödeme başlatma isteği gönderir.

API, bankaya yönlendirme için Base64 encoded 3D içeriği döner

Bu içerik decode edilerek kullanıcı tarayıcısında render edilir

Kullanıcı bankanın 3D Secure doğrulama ekranına yönlendirilir

Bu aşamada ödeme henüz tamamlanmış sayılmaz.

3D Doğrulama ve Kullanıcı Aksiyonu
Kullanıcı, bankanın 3D Secure ekranında:

SMS / mobil uygulama / banka doğrulaması gibi yöntemlerle

İşlemi onaylar veya reddeder

Bu aksiyon bankanın altyapısında gerçekleşir.

2. 3D Ödeme Tamamlama (Auth3d)
Kullanıcının 3D doğrulama adımını tamamlamasının ardından banka:

İşlem sonucunu

Başarılı veya başarısız durum bilgilerini

üye işyerinin Init3d adımında tanımladığı returnUrl veya failUrl adresine Request olarak iletir.

Üye işyeri bu gelen Request içeriğini kullanarak:

Auth3d endpoint’ine son bir çağrı yapar

İşlemi sistemsel olarak başarılı veya başarısız şekilde kesinleştirir

Bu adım tamamlanmadan ödeme süreci kapanmış sayılmaz.

Sonuç Bildirimi ve Zamanlama
Bankadan dönüş genellikle kullanıcı aksiyonuna bağlı olarak ~1 dakika içerisinde gerçekleşir

Return / Fail URL’e gelen veriler doğrulanmalı ve Auth3d çağrısında kullanılmalıdır

Nihai ödeme sonucu Auth3d response’u ile belirlenir

İlgili Endpoint’ler
3D Ödeme sürecinde kullanılan endpoint’ler:

3D Ödeme Başlatma: POST /v1/Payment/Init3d

3D Ödeme Tamamlama: POST /v1/Payment/Auth3d

Bu endpoint’lere ait request ve response detayları aşağıdaki bölümlerde açıklanmaktadır.


Gömülü Ödeme Formu
MorPOS Gömülü Ödeme  Formu (Embedded Payment Form) ile kart bilgileri MorPOS'un güvenli arayüzü üzerinden alınır.

Endpoint

Copy
POST /v1/EmbeddedPayment/CreatePaymentForm
Headers
Anahtar
Tür
Açıklama
Örnek
x-ClientID

String

API istemcisi için benzersiz tanımlayıcı

sizin-istemci-id’niz

x-ClientSecret

String

API istemcisi için gizli anahtar

sizin-gizli-anahtarınız

x-GrantType

String

Kullanılan erişim token türü

client_credentials

x-Scope

String

API kapsamı

pf_write, pf_read

x-Timestamp

String

İstek zaman damgası (ISO 8601)

2024-12-17T12:34:56Z

Request Body
Alan
Zorunlu
Tür
Açıklama
Örnek
merchantId

✅

String

Üye işyeri için benzersiz kimlik

100000000000004

returnUrl

✅

URL

Başarılı işlem sonrası yönlendirme URL

https://www.ornek.com/basari

failUrl

✅

URL

Başarısız işlem sonrası yönlendirme URL

https://www.ornek.com/hata

paymentMethod

✅

String

Ödeme yöntemi

EMBEDDEDPAYMENT

paymentInstrumentType

✅

String

Ödeme aracı türü

CARD

language

✅

String

İşlem dili

en

conversationId

✅

String

Takip için benzersiz kimlik

MP20240305001

sign

✅

String

İsteğin dijital imzası

ABC123...

transactionDetails.transactionType

✅

String

İşlem türü

SALE

transactionDetails.installmentCount

✅

Integer

Taksit sayısı (0 = tek çekim)

0

transactionDetails.amount

✅

Decimal

İşlem tutarı

1.00

transactionDetails.currencyCode

✅

Integer

Para birimi kodu (TRY = 949)

949

transactionDetails.vftFlag

❌

Boolean

VFT kullanım durumu

false

extraParameter.pFSubMerchantId

❌

String

Alt üye işyeri kimliği

12345

Örnek Request

Copy
{
  "merchantId": "100000000000004",
  "returnUrl": "https://www.ornek.com/basari",
  "failUrl": "https://www.ornek.com/hata",
  "paymentMethod": "EMBEDDEDPAYMENT",
  "paymentInstrumentType": "CARD",
  "language": "en",
  "conversationId": "MSD2024030500000000115",
  "sign": "AAVWY3RZKJVVCEAD8LDQB4KWJL4QX/IQQRGQ3XIWAAG=",
  "transactionDetails": {
    "transactionType": "SALE",
    "installmentCount": 0,
    "amount": "1.00",
    "currencyCode": "949",
    "vftFlag": false
  },
  "extraParameter": {
    "pFSubMerchantId": "12345"
  }
}
Örnek Response

Copy
{
    "code": "B0000",
    "message": "SUCCESS",
    "conversationId": "MSD28729399188493475",
    "paymentFormContent": "                    <div id='finagopay-container'>jscode</div>                    <script  src='https://finagopay-pf-ui-merchant.prp.morpara.com/embedded/finagopayPayment.js'></script>                       <script>                                                let isPaymentStarted = false;                        const paymentDateTime = new Date('2026-01-27 10:28:40');                         const now = new Date();                        const timePassed = paymentDateTime -now;                        const expireTime = Math.max(0, timePassed);                        function postRedirect(url, dataObj) {                            let form = document.createElement('form');                            form.method = 'POST';                            form.action = url;                            for (const key in dataObj) {                                if (dataObj.hasOwnProperty(key)) {                                    let input = document.createElement('input');                                    input.type = 'hidden';                                    input.name = key;                                    input.value = dataObj[key];                                    form.appendChild(input);                                }                            }                            document.body.appendChild(form);                            form.submit();                        }                        let finagopayPayment = new FinagopayPayment({                            container: '#finagopay-container',                    baseUrl:'https://finagopay-pf-api-gateway.prp.morpara.com/v1/EmbeddedPayment',                            clientSecret:'MjEyYTFmYzA4NTVhMWNiNDA1YzllNjM3Mzk3NDBjNzVmYjZmMDJlODI5NWU1ZmQ4ZTZhMmY1ZTA5NGFhYjZiYw==',                            clientId:'cx+eQAPE6JJAaJrGDDl2',                            timestamp:'20260127101840',                            merchantId:'5000000017',                            amount:'1.00',                            conversationId:'YBS28729399188493475',                            language:'en',                            onSuccess: function(payload) {                                                                isPaymentStarted = true;                                let request = {                                    RequestData: 'usZND1SEut9/Il3R2FwU/cIhDzw3NZpFfLEjfymUUj/qEnxBt0PqJk7WJmWrLT9u9GLpCnRQAzcMbPFIUu+ZM4fE9ngZxxrzp36C1tfo98plQux5NjjyV58uOIg02jeBDSxSD4borOvyanfV0+LhTy2lbhvJgVGJCVsOOw7GE+g4sWQZshwQ9piSjAgUu/zZUHkP/rHWSMP08C9XAtrgmdzrkayOZLyxI7mg3REHQlVI6Zjocz4pMbwVuduSUprt8VfvCxu8rZWeDEeG6DOga8+fUkGQL8RTVFSVgGUNwrXlApAQwtaNMteYef/0Kd7QMb0c2y+rqT9yUmQePJvQuZNXPRKXEGcMtKC/0XtSXM2D4Es4X9uTRGNP3MWDpNG502yI25mIkjcfI2UDOO2iVPZPWFnN5F0BuQSWfCV49eBTGemkrOmSLN/C3ubyLrvkAvB1WokeTi/h9cTuQnluPsVazXw7z8kWM6lyl6/kGTq+l5Ai+FgrCOIdNJABt7bs9ctD9CtUChiuLpBKIOsHT01QQ55/Uz5xgnMwqOM1uCne71BLrp1+Pc6+Xa6+y/F+7A4yEBlD1kKV98ro3e3T4MqiVL9KiTIhH1GWeGKUZ4Obeela6Wz6JNiVEHuJqW1suPXoNvQ5kp7ylmwztRZC080nffntk7w7CT+BdBv7an/IkmYyGvIkcsye38qmHi/jsKyUNrc40PGvg0uoz00+BA==æe6yK94u/Z3ye4r2frkolqQ==',                                    PaymentPageTransactionGuid:'vJGv1A+C9HJ1F6EAGdp2mEd8ltt0U7+VM565KqQptB4=æUm3LGMAvV21tZ1ySvXzjfQ==',                                    CardToken: payload.token                                };                                fetch('https://finagopay-pf-api-gateway.prp.morpara.com/v1/EmbeddedPayment/processPayment', {                                    method: 'POST',                                    headers: {                                        'Content-Type': 'application/json',                                        'X-ClientSecret': 'MjEyYTFmYzA4NTVhMWNiNDA1YzllNjM3Mzk3NDBjNzVmYjZmMDJlODI5NWU1ZmQ4ZTZhMmY1ZTA5NGFhYjZiYw==' ,                                        'X-ClientId': 'cx+eQAPE6JJAaJrGDDl2' ,                                        'X-GrantType': 'client_credentials' ,                                        'X-Scope': 'pf_write pf_read',                                         'X-Timestamp': '20260127101840',                                     },                                    body: JSON.stringify(request)                                })                               .then(response => response.json())                               .then(data => {                                    if (data.use3d)                                        {                                        if (data.htmlForm) {                                            const decodedHtmlContent = atob(data.htmlForm);                                            document.open();                                            document.write(decodedHtmlContent);                                            document.close();                                         }                                         else {                                                postRedirect(data.failUrl, {Code: 'R0099',Message: '3D Secure doğrulama için gerekli HTML içeriği alınamadı'});                                             }                                        }                                    else                                        {                                            let targetUrl = (data.code === 'B0000') ? data.returnUrl : data.failUrl;                                            postRedirect(targetUrl, data);                                        }                                                                 })                               .catch(error => {                                         postRedirect('{baseUrl}/fail-response', { error: error.message || 'Bilinmeyen hata'});                                });                            }                        });                     setTimeout(() => {                           if (!isPaymentStarted) {                                const container = document.querySelector('#finagopay-container');                                if (container) {                                    container.innerHTML = '<p>Ödeme süresi sona erdi. Lütfen tekrar deneyin.</p>';                                }                            }                        }, expireTime);                                       </script>",
    "paymentFormExpireTime": "2026-01-27T10:28:40.4442155+03:00",
    "returnUrl": "https://www.ornek.com/basari",
    "failUrl": "https://www.ornek.com/basarisiz"
}
İşleyiş Notları
Kart bilgileri merchant sistemine iletilmez

Kart verileri ödeme sağlayıcının gömülü (embedded) ödeme arayüzü üzerinden toplanır

PCI-DSS kapsamı önemli ölçüde azaltılır

İşlem sonucuna göre kullanıcı returnUrl veya failUrl adresine yönlendirilir



Ortak Ödeme Sayfası
MorPOS Yönlendirmeli Ödeme (Hosted Payment)
MorPOS Hosted Payment modeli ile kart bilgileri MorPOS’un güvenli ödeme sayfasında alınır.
Kullanıcı, ödeme başlatıldıktan sonra MorPOS tarafından sunulan ödeme ekranına yönlendirilir.

Endpoint

Copy
POST /v1/HostedPayment/HostedPaymentRedirect
Headers
Anahtar
Tür
Açıklama
Örnek
x-ClientID

String

API istemcisi için benzersiz tanımlayıcı

sizin-istemci-id’niz

x-ClientSecret

String

API istemcisi için gizli anahtar

sizin-gizli-anahtarınız

x-GrantType

String

Kullanılan erişim token türü

client_credentials

x-Scope

String

API kapsamı

pf_write, pf_read

x-Timestamp

String

İstek zaman damgası (ISO 8601)

2024-12-17T12:34:56Z

Request Body
Alan
Zorunlu
Tür
Açıklama
Örnek
merchantId

✅

String

Üye işyeri için benzersiz kimlik

100000000000004

returnUrl

✅

URL

Başarılı işlem sonrası yönlendirme URL

https://www.ornek.com/basari

failUrl

✅

URL

Başarısız işlem sonrası yönlendirme URL

https://www.ornek.com/hata

paymentMethod

✅

String

Ödeme yöntemi

EMBEDDEDPAYMENT

paymentInstrumentType

✅

String

Ödeme aracı türü

CARD

language

✅

String

İşlem dili

en

conversationId

✅

String

Takip için benzersiz kimlik

MP20240305001

sign

✅

String

İsteğin dijital imzası

ABC123...

transactionDetails.transactionType

✅

String

İşlem türü

SALE

transactionDetails.installmentCount

✅

Integer

Taksit sayısı (0 = tek çekim)

0

transactionDetails.amount

✅

Decimal

İşlem tutarı

1.00

transactionDetails.currencyCode

✅

Integer

Para birimi kodu (TRY = 949)

949

transactionDetails.vftFlag

❌

Boolean

VFT kullanım durumu

false

extraParameter.pFSubMerchantId

❌

String

Alt üye işyeri kimliği

12345

Örnek Request

Copy
{
  "merchantId": "100000000000004",
  "returnUrl": "https://www.ornek.com/basari",
  "failUrl": "https://www.ornek.com/hata",
  "paymentMethod": "HOSTEDPAYMENT",
  "paymentInstrumentType": "CARD",
  "language": "en",
  "conversationId": "MSD2024030500000000123",
  "sign": "AAVWY3RZKJVVCEAD8LDQB4KWJL4QX/IQQRGQ3XIWAAG=",
  "transactionDetails": {
    "transactionType": "SALE",
    "installmentCount": 0,
    "amount": "1.00",
    "currencyCode": "949",
    "vftFlag": false
  },
  "extraParameter": {
    "pFSubMerchantId": "12345"
  }
}
Örnek Response

Copy
{
    "returnUrl": "https://api.morpara.com/hostedpaymentpage/SdL+XGrAcMi+Z+7brD69kMasgWJIPui/nP1CFNETmXo="
}
İşleyiş Notları
Kart bilgileri merchant sistemine iletilmez

Kullanıcı MorPOS’un hosted ödeme sayfasına yönlendirilir

Kart verileri MorPOS altyapısında işlenir

PCI-DSS yükümlülüğü minimum seviyededir

Ödeme sonucuna göre kullanıcı:

Başarılı işlemde returnUrl

Başarısız işlemde failUrl
adresine yönlendirilir

Hosted Payment, 3D Secure ve non-3D Secure senaryoları destekler


 MorPOS Postman Collection
Bu sayfa, MorPOS için hazırlanmış Postman Collection'ın referansını sunar.

Tüm API servisleri (Non-3DS, 3D Secure, Ödeme Sorgulama, BIN Kontrol, İptal, İade) tek bir koleksiyonda birleştirilmiştir.

Servislerin kullanımı, gerekli header ve request parametreleri, örnek JSON request & response’lar koleksiyon içinde yer almaktadır.

Postman üzerinden doğrudan çağrı yapabilir, örnek verilerle test edebilirsiniz.

Postman Collection İndir
Koleksiyonu Postman’a import ederek tüm API servislerini kolayca kullanabilirsiniz:

📥 Postman Collection İndir

⚡ Koleksiyon, güncel endpoint ve örnek verilerle sürekli olarak güncellenmektedir.
🔒 API çağrıları için x-ClientID ve x-ClientSecret bilgilerinizi kullanmanız gerekmektedir.

Request Header (Tüm Servisler)
Alan
Zorunlu
Tür
Açıklama
Örnek
x-ClientID

✅

String

API istemcisi için benzersiz tanımlayıcı

sizin-istemci-id'niz

x-ClientSecret

✅

String

API istemcisi için gizli anahtar

sizin-gizli-anahtarınız

x-GrantType

✅

String

Kullanılan erişim tokeni türü

istemci_kimlik_bilgileri

x-Scope

✅

String

API kapsamı

pf_write, pf_read

x-Timestamp

✅

String

Doğrulama için isteğin zaman damgası

2024-12-17T12:34:56Z

Content-Type

✅

String

Gönderilen veri formatı

application/json

Önerilen Environment Değişkenleri
Değişken
Açıklama
Örnek
clientId

API istemci kimliği

sizin-istemci-id

clientSecret

API gizli anahtarı

sizin-gizli-anahtar

baseUrl

API taban URL

https://api.ornek.com

Kullanım Notları
Koleksiyon içindeki tüm servisler için gerekli header bilgilerini yukarıdaki gibi ekleyin.

Environment değişkenlerini tanımlayarak kolay test ve import yapabilirsiniz.

Servisler için zorunlu ve opsiyonel alanlar, koleksiyon açıklamalarında belirtilmiştir.

Postman ile doğrudan test yapabilir ve örnek request/response’ları inceleyebilirsiniz.