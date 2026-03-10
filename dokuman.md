Tosla İşim Geliştirici Merkezi
Tosla İşim’e entegre olurken her adımınızda sizlere yardımcı olabilmemiz için Geliştirici Merkezi’ne göz atabilir ya da entegrasyon kapsamında sorun yaşamanız durumunda posdestek@tosla.com mail adresinden destek alabilirsiniz.



Ortam Bilgileri
Test Ortam URL https://prepentegrasyon.tosla.com/api/Payment/ 

ClientId 1000000494

ApiUser POS_ENT_Test_001

ApiPass POS_ENT_Test_001!*!*



Production (Üretim) Ortam URL https://entegrasyon.tosla.com/api/Payment/

* Production (Üretim) ortamı için clientId, apiUser, apiPass bilgileriniz entegrasyonunuz tamamlandıktan sonra iletilecektir.





Zorunlu Parametreler
Tüm API çağrılarında aşağıdaki parametrelerin gönderilmesi gerekmektedir.



Alan Adı	Tipi	Açıklama	Örnek Değer
clientId	long	API müşteri numaranız	1000000494
apiUser	string	API kullanıcı adınız	POS_ENT_Test_001
rnd	string (max 24)	İşlem için üretilmiş random değer. Hash içerisinde kullanılan değer ile aynı olmalıdır.	123456ABC
timeSpan	string	İşlem tarihi (yyyyMMddHHmmss). Hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GMT+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.	20221012010436
Hash	string	Güvenlik kontrolünde kullanılacak hash stringi, her hash sadece bir kez kullanılacaktır.	Hash üretim adımını inceleyiniz




Hash Üretimi
ApiPass, ClientId, ApiUser, RandomString, TimeSpan değerleri sırasıyla uc uca eklenerek string oluşturulur.
Oluşturulan string'in SHA512 algoritması ile Byte hash'i alınır.
Oluşturulan byte hash base64 encode edilerek hash değeri oluşturulur.


.Net Örnek Hash Fonksiyonu

                            private string CreateHash()
                            {
                                var randomGenerator = new Random();
                                var ApiPass = "POS_ENT_Test_001!*!*";
                                var ClientId = "1000000494";
                                var ApiUser = "POS_ENT_Test_001";
                                var Rnd = randomGenerator.Next(1, 1000000).ToString();
                                var TimeSpan = DateTime.Now.ToString("yyyyMMddHHmmss");
                                var hashString =ApiPass + ClientId + ApiUser + Rnd + TimeSpan;
                                System.Security.Cryptography.SHA512 sha = new System.Security.Cryptography.SHA512CryptoServiceProvider();
                                byte[] bytes = Encoding.UTF8.GetBytes(hashString);
                                byte[] hashingbytes = sha.ComputeHash(bytes);
                                var hash = Convert.ToBase64String(hashingbytes);
                                return hash;
                            }
                        
PHP Örnek Hash Fonksiyonu
                            public function generateHash()
                            {
                                    $apiPass = "POS_ENT_Test_001!*!*";
                                    $clientId = "1000000494";
                                    $apiUser = "POS_ENT_Test_001";
                                    $rnd = rand(1, 10000);
                                    $timeSpan = time();
                            
                                    $hashString = $apiPass . $clientId . $apiUser . $rnd . $timeSpan;
                                    $hashingbytes = hash("sha512", ($hashString), true);
                                    $hash = base64_encode($hashingbytes);
                                    return $hash;
                            }
                        




CallBack URL Hash Doğrulama Mekanizması
CallbackURL’e dönen HashParameters içinde yer alan parametrelerin başına apiPass eklenerek Hash generate edilerek Hash validasyonu yapılabilir.


.Net Örnek Hash Fonksiyonu

                            static void Main(string[]args)
                            {
                                //var hashString = apiPass + threeDsession.ClientId + threeDsession.ApiUser +;
                                threeDsession.OrderId + threeDsession.MdStatus + threeDsession.BankResponseCode + 
                                threeDsession.BankResponseMessage + threeDsession.RequestStatus;
                                var hashString = "DGSApi123.123" + "1000000061" + "DGS_Api" + "P-2" + "1" + "00"+
                                "Onaylandı" + "1";
                                var hash = CalculateHash(string inputData)
                                }
                                public static string CalculateHash(string inputData)
                                {
                                string hash = "";
                                System.Security.Cryptography.SHA512 sha = new
                                System.Security.Cryptography.SHA512CryptoServiceProvider(); byte[]
                                bytes = Encoding.UTF8.GetBytes(inputData);
                                byte[] hashingbytes = sha.ComputeHash(bytes);
                                hash = Convert.ToBase64String(hashingbytes);
                                return hash;
                            }
                      

Ödeme İşlem Başlatma
Ödeme formu ve Ortak Ödeme Sayfası ile ödeme işlemi başlatmak için ThreeDSessionId değeri üretilmelidir. Bu servis 3D secure başlatılması için session açar ve sessionId bilgisini döner.

Bu servisten dönen ThreeDSessionId değeri ödeme formunda veya ortak ödeme sayfa çağırma işleminde kullanılır.



ThreeDSessionId 3D doğrulama işleminde kullanılan benzersiz değerdir. Her başarılı/başarısız işlem için tekrar üretilmelidir.

Method Name : threeDPayment

Http Method : Post

Content-Type : application/json

Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
CallbackUrl	string	1024	Evet	3D sonucunun dönüleceği url. Url’e dönülecek veri hashlenecektir. CallBack URL Hash Doğrulama Mekanizması başlığından incelenmelidir.
isCommission	Int	1	Opsiyonel	Taksitli işlemlerde Komisyonlu Ödeme alınmak istendiği durumlarda 1 olarak gönderilmelidir. Dİğer durumlarda 0 veya gönderilmez.
OrderId	string	20	Opsiyonel	Sipariş Numarasıdır. Belirlenmediği takdirde sistem otomatik üretecektir.
Amount	long	18	Evet	İşlem Tutarı, son iki hane kuruştur. 1522 = 15 TL 22 Kuruş
Currency	int	3	Evet	İşlem Para birimi 949
InstallmentCount	int	2	Opsiyonel	Default “0”
Description	string	256	Opsiyonel	İşleme ait açıklama
Echo	string	256	Opsiyonel	İstek sonucunda geri gönderilecek bilgi alanı
ExtraParameters	string	4000	Opsiyonel	Ekstra bilgilerin gönderildiği alan. Inquiry servisi cevabında döner


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
ThreeDSessionId	string	1024	3D işlem için üretilen Unqiue numara
TransactionId	string
20	İşlem ID'si


Örnekler - Komisyon Yansıtılmasının Kullanılmadığı Durum
Request	Response
{
                            "clientId": 1,
    "apiUser": "testUser",
    "rnd": "***",
    "timeSpan": "20191121160000",
    "hash": "***",
    "orderId": "12345678901234567890",
    "callbackUrl": "***merchantCallbackUrl***",
    "description": "açıklama",
    "echo": "string",
    "extraParameters": "string",
    "amount": 100,
    "currency": 949,
    "installmentCount": 0
                           }
{
                             
    "Code": 0,
    "Message": "Başarılı",
    "ThreeDSessionId": "***",
    "TransactionId": "12345"
}


Örnekler - Komisyon Yansıtıldığı Durum
Request	Response
{
                            "clientId": 1,
    "apiUser": "testUser",
    "rnd": "***",
    "timeSpan": "20191121160000",
    "hash": "***",
    "orderId": "12345678901234567890",
    "callbackUrl": "***merchantCallbackUrl***",
    "description": "açıklama",
    "echo": "string",
    "extraParameters": "string",
    "isCommission ":1,
    "amount": 100,
    "totalAmount "102,
    "currency": 949,
    "installmentCount": 0
}
{
                             
   "Code": 0,
    "Message": "Başarılı",
    "ThreeDSessionId": "***",
    "TransactionId": "12345"
}


CODE	MESSAGE	Validation Errors	AÇIKLAMA
0			
202	Üye İşyeri Kullanıcısı Bulunamadı		
250	Üye İşyeri Api Kullanıcısı için Yetkisiz İşlem		
510	IsComission true iken TotalAmount 0 dan büyük olmalıdır.		
511	Komisyonlu tutar yanlış hesaplanmıştır.		
997	hash Hatası		
998	Validasyon Hatası	Time Span' alanı yyyyMMddHHmmss formatında olmalıdır.	
'Time Span' alanı 60 dakidan büyük olamaz, mevcut dakika fark: 240.	

An undefined token is not a valid System.Nullable`1[System.Int64]. Path 'amount', line 3, position 12."	Zorunlu alan boş gönderildiğinde alınan hata




3D ile Ödeme
Kredi kartı bilgileri girildikten sonra kullanıcıyı 3D Ödeme akışına yönlendirir. İşlem sonucunu callbackUrl parametresinde belirttiğiniz adrese POST eder.



Kurallar & Uyarılar
API entegrasyonunda üye işyeri kendi ödeme formunu kullanır.
Form görsel tasarımını üye iş yeri istediği gibi tasarlayabilir.
Kredi kartı numarası (PAN) luhn algoritmasına tabi tutularak kontrol edilmeli, doğruluğu teyit edildikten sonra API'ye gönderilmelidir.
Kartın hamilinin adı ve soyadı alınmalıdır.
Son kullanma tarihi geçmiş kartlar ile API'ye sorgu yapılmamalıdır.
Ödeme formu American Express (AMEX) kartları ile işlem alabilir şekilde tasarlanmalıdır.
Formunuzdaki input name değerleri aşağıdaki parametreler ile aynı olmalıdır.
Method Name : ProcessCardForm
Http Method : Post
Content-Type : multipart/form-data


Request Parametreleri
Input Name	Zorunlu	Açıklama
ThreeDSessionId	Evet	Ödeme işlem başlatma adımında oluşturduğunuz ThreeDSessionId değeri
CardHolderName	Evet	Kart hamilinin adı ve soyadı
CardNo	Evet	Kredi kart numarası
ExpireDate	Evet	Kart son kullanım tarihi (AA/YY)
Cvv	Evet	Kart güvenlik kodu


Response
'Ödeme İşlem Başlatma' adımındaki callbackUrl parametresinde belirttiğiniz adrese 3D işlem sonucunu POST eder.



3D İşlem Sonucu Response POST Parametreleri
Eğer BankResponseCode 00 ise ödeme işlem başarılıdır. Bunun dışındaki tüm işlemler başarısız olarak kabul edilir.



Alan Adı	Açıklama
Code	0 ise İşlem servis isteği başarılıdır. Diğer numaralar hatalıdır.
Message	İşleme ait mesaj
OrderId	İşlemin sipariş numarası
BankResponseCode	Banka tarafındaki işlem statü kodu. 00 ise ödeme başarılı diğer tüm durumlar başarısız olarak kabul edilir.
BankResponseMessage	Banka tarafındaki işlem statü mesajı
AuthCode	İşlemin bankadaki otorizasyon kodu
HostReferenceNumber	Host Reference Number
TransactionId	İşlemin sistemdeki numarası
CardHolderName	İşlemde kullanılan kartın hamili



CODE	MESSAGE	Validation Errors	AÇIKLAMA
0	Başarılı		
1	AlreadyBankResponse		İşlem ekranında kayıtlı işleme ait BankResponseCode is not null
1	Tutar Sıfır Olamaz		İşlemler ekranında kayıtlı işlemde Amount=0 ise
998	Validasyon Hatası	After parsing a value an unexpected character was encountered: \". Path 'additionalProp3', line 5, position 1.	
0824' is not a valid number. Path 'additionalProp4', line 5, position 25.	
999	Genel Hata		






3D Otorizasyon (PreAuth - PostAuth)
Müşteriden tahsilat yapmadan önce kredi kartında otorizasyon yapmak için kullanılır.

Otorizasyon Adımları
ThreeDPreAuth servisi ile ön otorizasyon işlemi başlatılır. Servis ThreeDSessionId değeri döner. ThreeDPreAuth servisini inceleyiniz.
ThreeDSessionId değeri ile ProcessCardForm servisine kredi kartı bilgileri form POST edilerek otorizasyon alınır. Bu aşamada tutar, müşteri kredi kartından bloke edilir. Bu işlem için "3D ile Ödeme" adımını inceleyiniz.
Otorizasyon işleminin tamamlanıp tutarın tahsil edilmesi için postAuth servisi çağrılır. postAuth servisi çağrıldıktan sonra işlem finansallaşır ve tahsilat gerçekleşmiş olur. PostAuth servisini inceleyiniz.


Notlar
ThreeDPreAuth, ProcessCardForm ve PostAuth servisleri farklı zamanlarda çağrılabilir.
İstenilen bir zamanda "İptal" adımı takip edilerek. Otorizasyon işlemi iptal edilip, kart üzerindeki tutar blokesi kaldırılabilir.


ThreeDPreAuth
Müşteriden bir ön otorizasyon alma işlemi başlatır.

Method Name : threeDPreAuth
Http Method : Post
Content-Type : application/json




Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
CallbackUrl	string	1024	Evet	3D sonucunun dönüleceği url. Url’e dönülecek veri hashlenecektir. CallBack URL Hash Doğrulama Mekanizması başlığından incelenmelidir.
OrderId	string	20	Opsiyonel	Ön Otorizasyon (PreAuth) işleminde gönderilen Sipariş numarasıdır.
Amount	long	18	Evet	İşlem tutarı
Currency	int	3	Evet	İşlem Para birimi TL için 949
Extraparameters	string	30	Opsiyonel	Application channel bilgisi


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
ThreeDSessionId	string	256	3D işlem için üretilen Unqiue numara
TransactionId	string	256	3D işlem için üretilen Unqiue numara


Örnekler
Request	Response
{
                          "clientId": 1,
                          "apiUser": "***",
                          "rnd": "***",
                          "timeSpan": "20191121160000",
                          "hash": "***",
                          "orderId": "12345678901234567890",
                          "callbackUrl": "***merchantCallbackUrl***",
                          "amount": 100,
                          "currency": 949,
                          "extraparameters": "abc"
                          }
{
                          "code": 0,
                          "message": "Başarılı",
                          "threeDSessionId": "***"
                          }




PostAuth
Müşteriden bir ön otorizasyon alma işlemi başlatır.

Method Name : postAuth
Http Method : Post
Content-Type : application/json



Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
OrderId	string	20	Opsiyonel	Ön Otorizasyon (PreAuth) işleminde gönderilen Sipariş numarasıdır.
Amount	long	18	Evet	İşlem tutarı
Currency	int	3	Evet	İşlem Para birimi TL için 949
Extraparameters	string	30	Opsiyonel	Application channel bilgisi


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
OrderId	string	20	Sipariş Numarasıdır. Belirlenmediği takdirde sistem otomatik üretecektir.


Örnekler
Request	Response
{
                                                  "clientId": 1,
                                                  "apiUser": "testUser",
                                                  "rnd": "***",
                                                  "timeSpan": "20191121160000",
                                                  "hash": "***",
                                                  "orderId": "12345678901234567890",
                                                  "amount": 2211,
                                                  "extraparameters": "abc"
                                                  }
{
  "code": 0,
  "message": "Başarılı",
  "orderId": "12345678901234567890"
}
                                                  "code": 0,
                                                  "message": "Başarılı",
                                                  "orderId": "12345678901234567890"
                                                  }








Non3D ile Ödeme
Kredi kartı bilgileri girildikten sonra kullanıcıyı Non3D Ödeme akışına yönlendirir. 3D ile ÖDEME adımında girilen Kurallar ve Uyarılar burada da geçerli olmalıdır.

Kurallar & Uyarılar
API entegrasyonunda üye işyeri kendi ödeme formunu kullanır.
Form görsel tasarımını üye iş yeri istediği gibi tasarlayabilir.
Kredi kartı numarası (PAN) luhn algoritmasına tabi tutularak kontrol edilmeli, doğruluğu teyit edildikten sonra API'ye gönderilmelidir.
Kartın hamilinin adı ve soyadı alınmalıdır.
Son kullanma tarihi geçmiş kartlar ile API'ye sorgu yapılmamalıdır.
Ödeme formu American Express (AMEX) kartları ile işlem alabilir şekilde tasarlanmalıdır.
Formunuzdaki input name değerleri aşağıdaki parametreler ile aynı olmalıdır.
Method Name : Payment
Http Method : Post
Content-Type : application/json



Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
cardHolderName	string	100	Evet	Kart hamilinin adı ve soyadı
cardNo	string	16	Evet	Kredi kartı numarası
expireDate	string	4	Evet	Kart son kullanım tarihi (AAYY)
cvv	string	4	Evet	Kart güvenlik kodu
clientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
apiUser	string	100	Evet	Api kullanıcı adı
rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
timeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
orderId	string	20	Opsiyonel	Sipariş Numarasıdır. Belirlenmediği takdirde sistem otomatik üretecektir.
isCommission	long	18	Opsiyonel	isCommission değeri 1 gönderildiği durumlarda zorunludur. totalAmount değeri GetInstallmentOptions’ta dönen taksite karşılık gelen değer gönderilmelidir. isCommission değeri 1 gönderildiğinde bankaya gönderilen tutardır, son iki hane kuruştur. 1528 = 15 TL 28 Kuruş
amount	long	18	Evet	İşlem Tutarı, son iki hane kuruştur. 1522 = 15 TL 22 Kuruş
totalAmount	long	18	Opsiyonel	isCommission değeri 1 gönderildiği durumlarda zorunludur. totalAmount değeri GetInstallmentOptions’ta dönen taksite karşılık gelen değer gönderilmelidir. isCommission değeri 1 gönderildiğinde bankaya gönderilen tutardır, son iki hane kuruştur. 1528 = 15 TL 28 Kuruş
currency	int	3	Evet	İşlem Para birimi 949
installmentCount	int	2	Opsiyonel	Default “0”
description	string	256	Opsiyonel	İşleme ait açıklama
echo	string	256	Opsiyonel	İstek sonucunda geri gönderilecek bilgi alanı
extraParameters	string	4000	Opsiyonel	Ekstra bilgilerin gönderildiği alan. Inquiry servisi cevabında döner


Response
Alan Adı	Açıklama
Code	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	İşleme ait mesaj
OrderId	İşlemin sipariş numarası
BankResponseCode	Banka tarafındaki işlem statü kodu. 00 ise başarılı diğer tüm durumlar başarısız olarak kabul edilir.
BankResponseMessage	Banka tarafındaki işlem statü mesajı
AuthCode	İşlemin bankadaki otorizasyon kodu
HostReferenceNumber	Host Reference Number
TransactionId	İşlemin sistemdeki numarası
CardHolderName	İşlemde kullanılan kartın hamili


Eğer BankResponseCode 00 ise işlem başarılıdır. Bunun dışındaki tüm işlemler başarısız olarak kabul edilir.



Örnekler - Komisyon Yansıtılmasının Kullanılmadığı Durum
Request	Response
{
                            "clientId": 1,
    "apiUser": "testUser",
    "rnd": "***",
    "timeSpan": "20191121160000",
    "hash": "***",
    "orderId": "12345678901234567890",
    "callbackUrl": "***merchantCallbackUrl***",
    "description": "açıklama",
    "echo": "string",
    "extraParameters": "string",
    "amount": 100,
    "currency": 949,
    "installmentCount": 0
                           }
{
                             
    "Code": 0,
    "Message": "Başarılı",
    "ThreeDSessionId": "***",
    "TransactionId": "12345"
}


Örnekler - Komisyon Yansıtıldığı Durum
Request	Response
{
                            "clientId": 1,
    "apiUser": "testUser",
    "rnd": "***",
    "timeSpan": "20191121160000",
    "hash": "***",
    "orderId": "12345678901234567890",
    "callbackUrl": "***merchantCallbackUrl***",
    "description": "açıklama",
    "echo": "string",
    "extraParameters": "string",
    "isCommission ":1,
    "amount": 100,
    "totalAmount "102,
    "currency": 949,
    "installmentCount": 0
}
{
                             
   "Code": 0,
    "Message": "Başarılı",
    "ThreeDSessionId": "***",
    "TransactionId": "12345"
}



CODE	MESSAGE	Validation Errors
0	Başarılı	
100	Ödeme Hatalı	
202	Üye İşyeri Kullanıcısı Bulunamadı	
250	Üye İşyeri Api Kullanıcısı için Yetkisiz İşlem	
997	Hash hatası	
998	Validasyon Hatası	'Time Span' alanı yyyyMMddHHmmss formatında olmalıdır.
'Time Span' alanı 60 dakidan büyük olamaz, mevcut dakika fark: xxx.
999	Genel Hata	İstenmeyen bir durumda alınan hatadır.








Ortak Ödeme Sayfası
Kendi özel ödeme formunu kullanmak istemeyen üye işyerleri için uygundur. 'Ödeme İşlem Başlatma' adımında oluşturulan threeDSessionId değeri ile Ortak Ödeme Sayfası iframe içinde gösterilebilir ya da kullanıcı doğrudan bu linke yönlendirilebilir. Ödeme işlem sonucu CallbackUrl adresine sistem tarafından POST edilir.

Not: Kullanıcının bankanın 3D sayfasına yönlendirme yapılabilmesi için browser desteği gerekmektedir.

Method Name : threeDSecure
Http Method : get
Content-Type : html/text


Request Parametreleri

Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ThreeDSessionId	string	256	Evet	3D işlem için üretilen Unqiue numara


<iframe src="https://[Ortam URL]/api/Payment/threeDSecure/[threeDSessionId]" height="500" width="100%"></iframe>

Ortam URL bilgisinin "Üretim Ortamında" değiştirilmesi gerekmektedir. Link formatı [Ortam URL] /threeDSecure/ [threeDSessionId]'dır.







Taksit ve Komisyon Bilgisi
Bin numarasına göre kartın bağlı olduğu banka bilgileri ile birlikte sunulabilecek taksit ve komisyon oranlarını gönderir.

Method Name :GetCommissionAndInstallmentInfo
Http Method : Post
Content-Type : application/json


Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
Bin	int	6	Evet	Kredi kartının ilk 6 hanesidir.


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
CardPrefix	int	6	Bin numarası
BankId	int	3	Sistemdeki banka ID değeri
BankCode	string	512	Banka kodu
BankName	string	512	Banka adı
CardName	string	512	Kart adı
CardClass	string	512	Kart sınıfı
CardType	string	512	Kart tipi
Country	string	200	Kart ülkesi
BankCommission	int	6	Banka komisyonu
InstallmentInfo	string		Taksit listesi ve komisyon oranı


Örnekler
Request	Response
{
 "bin": 407814,
 "clientId": 1000000000,
 "apiUser": "XXXXXXXX",
 "rnd": "string",
 "timeSpan": "string",
 "hash": "XXXXXXXX"
 }
{
 "CardPrefix": 407814,
 "BankId": 9,
 "BankCode": "ZiraatBankası",
 "BankName": "Ziraat Bankası",
 "CardName": "Bankkart",
 "CardClass": "Banka Kartı",
 "CardType": "Visa",
 "Country": "TR",
 "BankCommission": 0,
 "InstallmentInfo": {
 "T2": {
 "Rate": 2.99,
 "Constant": 2
 },
 "T3": {
 "Rate": 3.99,
 "Constant": 2
 },
 "T4": {
 "Rate": 4.99,
 "Constant": 2
 },
 "T5": {
 "Rate": 5.99,
 "Constant": 2
 },
 "T6": {
 "Rate": 6.99,
 "Constant": 0
 },
 "T7": {
 "Rate": 7.99,
 "Constant": 0
 },
 "T8": {
 "Rate": 8.99,
 "Constant": 0
 },
 "T9": {
 "Rate": 9.99,
 "Constant": 0
 },
 "T10": {
 "Rate": 10.99,
 "Constant": 0
 },
 "T11": {
 "Rate": 11.99,
 "Constant": 0
 },
 "T12": {
 "Rate": 12.99,
 "Constant": 0
 }
 },
 "Code": 0,
 "Message": ""
 }




Taksit ve Taksitlere Karşılık Gelen Tutar Bilgisi
isCommission 1 gönderildiği durumda amount değerine karşılık gelen taksitlerdeki totalAmount değerinde gönderilecek değerler döner.

Method Name :GetInstallmentOptions
Http Method : Post
Content-Type : application/json


Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
amount	long	18	Evet	İşlem tutarı


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
InstallmentOptions	int	6	Taksit listesi ve tutar

Örnekler
Request	Response
{
  "amount": 10000,
  "clientId": 1000000000,
  "apiUser": "XXXXXXXX",
  "rnd": "string",
  "timeSpan": "string",
  "hash": "XXXXXXXX"
}
{
  "installmentOptions": [
    {
      "installment": 1,
      "title": "Tek Çekim",
      "amount": 10000,
      "currency": 949
    },
    {
      "installment": 2,
      "title": "2 Taksit",
      "amount": 10089,
      "currency": 0
    }
    {
      "installment": 3,
      "title": "3 Taksit",
      "amount": 10115,
      "currency": 949
    }
   ],
  "Code": 0,
  "Message":""
 }




Ödeme Sorgulama
Tosla İşim de kaydı oluşturulan bir işlemin detayları sorgulanabilir. İşlem sorgulama servisidir. OrderID ile işlem sorgulama yapılabilmektedir.

Method Name : inquiry
Http Method : Post
Content-Type : application/json



Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
OrderId	string	20	Evet	Ön Otorizasyon (PreAuth) işleminde gönderilen Sipariş numarasıdır.
TransactionId	string	20	Opsiyonel	Sorgulanacak işlemin Id’si


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
TransactionType	int	65	3D işlem için Üretilen Unqiue numara
CreateDate	string	23	Oluşturma tarihi
OrderId	String	20	Sipariş numarasıdır
BankResponseCode	String	7	Bankadan alınan cevap kodu
BankResponseMessage	String	512	Bankadan alınan cevap mesajı
AuthCode	String	200	Otorizasyonda kullanılan kodu
HostReferenceNumber	string	200	
Amount	long	18	İşlem tutarı
Currency	int	3	İşlem Para birimi 949
InstallmentCount	int	2	Taksit sayısı
ClientId	long	20	Mağaza İçin belirlenmiş benzersiz numara
CardNo	string	19	Kart Numarası
RequestStatus	int	2	İşlem durumunu gösterir
RefundedAmount	Long	18	İade edilen miktar
PostAuthedAmount	Long	18	Ön Otorizasyon işlemindeki tutar
TransactionId	Long	20	İşlem İD’si
CommissionStatus	Int	1	Komisyon durumu
NetAmount	Long	18	Net tutar
MerchantCommissionAmount	Long	18	Üye iş yerinin komisyon miktarı
MerchantCommissionRate	decimal	18	Üye iş yerinin komisyon oranı
CardBankId	Long	20	Kullanılan banka id’si
CardTypeId	long	20	Kullanılan kart tipi id’si
ValorDate	int	8	Valör günü
TransactionDate	int	8	İşlem yapılan tarihtir. yyyyMMdd
BankValorDate	int	8	 
ExtraParameters	string	4000	 


Örnekler
Request	Response
{
 "clientId": 1,
 "apiUser": "***",
 "rnd": "***",
 "timeSpan": "20221011235931",
 "hash": "***",
 "orderId": "20221011999",
 "transactionId": ""
 } 
{
 "Count": 36,
 "Transactions": [
 {
 "TransactionType": 1,
 "CreateDate": "20221011152449",
 "OrderId": "20221011999",
 "BankResponseCode": "00",
 "BankResponseMessage": "Onaylandı",
 "AuthCode": "S90037",
 "HostReferenceNumber": "228415127919",
 "Amount": 1090,
 "Currency": 949,
 "InstallmentCount": 0,
 "ClientId": 1000000494,
 "CardNo": "41595600****7732",
 "RequestStatus": 1,
 "RefundedAmount": 0,
 "PostAuthedAmount": 0,
 "TransactionId": 2000000000029968,
 "CommissionStatus": 1,
 "NetAmount": 1090,
 "MerchantCommissionAmount": 0,
 "MerchantCommissionRate": 0,
 "CardBankId": 13,
 "CardTypeId": 2,
 "ValorDate": 0,
 "TransactionDate": 20221011,
 "BankValorDate": 0,
 "ExtraParameters": null,
 "Code": 0,
 "Message": "Başarılı"
 }
 ],
 "Code": 0,
 "Message": "Başarılı"
 }


CODE	MESSAGE	Validation Errors	AÇIKLAMA
0			
202	Üye İşyeri Kullanıcısı Bulunamadı		
250	Üye İşyeri Api Kullanıcısı için Yetkisiz İşlem		
997	Hash Hatası		
998	Validasyon Hatası	Time Span' alanı yyyyMMddHHmmss formatında olmalıdır.	
Time Span' alanı 60 dakidan büyük olamaz, mevcut dakika fark: 1050626	
Unexpected character encountered while parsing value: ,. Path 'orderId', line 2, position 14.	Zorunlu alan boş gönderildiğinde alınan hata
An undefined token is not a valid System.Int64. Path 'clientId', line 3, position 14.	Zorunlu alan boş gönderildiğinde alınan hata
999	Genel Hata		




İşlem Listeleme
Tarihe göre işlemleri listeler. Tarih gönderilerek iletilen tarihteki tüm işlemlerin listelenmesini sağlar.

Method Name : history
Http Method : Post
Content-Type : application/json


Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
OrderId	string	20	Opsiyonel	Sipariş numarası
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
TransactionDate	int	8	Evet	İşlem yapılan tarihtir. yyyyMMdd
Page	int	int max	Evet	Paging yapısı gereği kullanılan sayfa numarası ilk sayfa için 1, ikinci sayfa için 2...
PageSize	int	3	Evet	Paging yapısı gereği kullanılan sayfa başı kayıt sayısı.


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
Count	int	int max	Tarih filtresine uygun toplam kayıt sayısını döner.
Transactions	list		İşleme ait ödeme listesini döner.
#	Değer	Anlam
TransactionType	1	Satış
TransactionType	4	İptal
TransactionType	5	İade
TransactionType	6	Harcama İtirazı
#	Değer	Anlam
RequestStatus	0	Hatalı
RequestStatus	1	Başarılı
RequestStatus	2	İptal Edildi
RequestStatus	3	Parçalı İade Edildi
RequestStatus	4	İptal Edildi //Tamamı İade Edildi
RequestStatus	5	Ön Otorizasyon Kapandı
RequestStatus	6	Parçalı Harcama İtirazı
RequestStatus	7	Tam Harcama İtirazı
RequestStatus	10	3D Bekleniyor
RequestStatus	11	3D ye Gönderildi
RequestStatus	12	3D den Cevap Geldi
RequestStatus	14	İade Bekleniyor
RequestStatus	15	İptal Edildi
RequestStatus	16	İleri Vadeli Alacaktan İade


Örnekler
Request	Response
{
 "OrderId": " A1B2C3D4",
 "ClientId": 1,
 "ApiUser": "apiuser",
 "Rnd": "string",
 "TimeSpan": "20220119111007",
 "Hash": " A1B2C3D4",
 "Page": 1,
 "PageSize": 10,
 "TransactionDate": 20220412
 }
 
{
 "Code": 101,
 "Message": "string",
 "Count": 123,
 "Transactions": [
 {
 "TransactionType": 1,
 "CreateDate": "20220119111007",
 "OrderId": " A1B2C3D4",
 "BankResponseCode": "MD99",
 "BankResponseMessage": "mesaj",
 "AuthCode": null,
 "HostReferenceNumber": null,
 "Amount": 100,
 "Currency": 949,
 "InstallmentCount": 1,
 "ClientId": 1,
 "CardNo": "123456******1234",
 "RequestStatus": 0,
 "RefundedAmount": 0,
 "PostAuthedAmount": 0,
 "TransactionId": 0,
 "CommissionStatus": null,
 "NetAmount": 0,
 "MerchantCommissionAmount": 0,
 "MerchantCommissionRate": null,
 "CardBankId": 0,
 "CardTypeId": 0,
 "Code": 0,
 "Message": ""
 }
 ]
 }


CODE	MESSAGE	Validation Errors	AÇIKLAMA
0	Başarılı		
250	Üye İşyeri Api Kullanıcısı için Yetkisiz İşlem		
998	Validasyon Hatası	Time Span' alanı yyyyMMddHHmmss formatında olmalıdır.	
Time Span' alanı 60 dakidan büyük olamaz, mevcut dakika fark: 1050626	
Unexpected character encountered while parsing value: ,. Path 'orderId', line 2, position 14.	Zorunlu alan boş gönderildiğinde alınan hata
An undefined token is not a valid System.Int64. Path 'clientId', line 3, position 14.	Zorunlu alan boş gönderildiğinde alınan hata
999	Genel Hata		




İptal
Bankalar gün sonu almadan önce işlemin iptal edilmesidir. OrderId ile ödeme işleminin iptal edilmesini sağlar.

Method Name : void
Http Method : Post
Content-Type : application/json



Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
OrderId	string	20	Evet	Ön Otorizasyon (PreAuth) işleminde gönderilen Sipariş numarasıdır.
Echo	string	256	Opsiyonel	İstek sonucunda geri gönderilecek bilgi alanı


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
OrderId	string	20	Sipariş Numarasıdır.
BankResponseCode	string	7	Bankadan alınan cevap kodu
BankResponseMessage	string	512	Bankadan alınan cevap mesajı
AuthCode	string	200	Otorizasyonda kullanılan kodu
HostReferenceNumber	string	200	Ödeme kanalın referans numarası
TransactionId	long	20	İşlem İD’si


Örnekler
Request	Response
{
 "OrderId": " A1B2C3D4",
 "ClientId": 1,
 "ApiUser": "****",
 "Rnd": "****",
 "TimeSpan": "20220119111007",
 "Hash": "****",
 "echo": "string"
 }
 
{
 "OrderId": " A1B2C3D4",
 "BankResponseCode": null,
 "BankResponseMessage": null,
 "AuthCode": null,
 "HostReferenceNumber": null,
 "TransactionId": null,
 "Code": 101,
 "Message": "string"
 }


CODE	MESSAGE	Validation Errors	AÇIKLAMA
0	Başarılı		
1	Iptal etmek istediğiniz tutar, hakkediş tutarınızdan fazladır. 10,00 tutarından daha fazla iptal/iade yapamazsınız.		
1	Başarılı bir postauth olduğu için ön otorizasyon işlemini iptal edemezsiniz.		
101	Orjinal Kayıt Bulunamadı		
202	Üye İşyeri Kullanıcısı Bulunamadı		
250	Üye İşyeri Api Kullanıcısı için Yetkisiz İşlem		
997	Hash Hatası		
998	Validasyon Hatası	Time Span' alanı yyyyMMddHHmmss formatında olmalıdır.	
'Time Span' alanı 60 dakidan büyük olamaz, mevcut dakika fark: 240.	
Unexpected character encountered while parsing value: ,. Path 'orderId', line 2, position 14.	Zorunlu alan boş gönderildiğinde alınan hata
An undefined token is not a valid System.Int64. Path 'clientId', line 3, position 14.	Zorunlu alan boş gönderildiğinde alınan hata
999	Genel Hata		




İade
Bankaların gün sonu aldıktan sonra işlemin kısmi veya tam olarak iade edilmesidir. Ödeme işlemini iade eder. Tam iade ve kısmi iade yapılabilmesini sağlar.

Method Name : refund
Http Method : Post
Content-Type : application/json


Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long19		Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
OrderId	string	20	Evet	Ön Otorizasyon (PreAuth) işleminde gönderilen Sipariş numarasıdır.
Amount	long	18	Evet	İade İşlem Tutarı, son iki hane kuruştur. 1522 = 15 TL 22 Kuruş
Echo	string	256	Opsiyonel	İstek sonucunda geri gönderilecek bilgi alanı


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
OrderId	string	20	Sipariş Numarasıdır.
BankResponseCode	string	7	Bankadan alınan cevap kodu
BankResponseMessage	string	512	Bankadan alınan cevap mesajı
AuthCode	string	200	Otorizasyonda kullanılan kodu
HostReferenceNumber	string	200	Ödeme kanalın referans numarası
TransactionId	long	20	İşlem İD’si


Örnekler
Request	Response
{
 "OrderId": " A1B2C3D4",
 "Amount": "125",
 "ClientId": 1,
 "ApiUser": "***",
 "Rnd": "***",
 "TimeSpan": "20220119111007",
 "Hash": "***",
 "echo": "string"
 }
{
 "OrderId": " A1B2C3D4",
 "BankResponseCode": null,
 "BankResponseMessage": null,
 "AuthCode": null,
 "HostReferenceNumber": null,
 "TransactionId": null,
 "Code": 101,
 "Message": "string"
 }


CODE	MESSAGE	Validation Errors	AÇIKLAMA
0	Başarılı		
1	İade etmek istediğiniz tutar, hakkediş tutarınızdan fazladır. 10,00 tutarından daha fazla iade yapamazsınız.		
100	Ödeme Hatalı		
101	Orjinal Kayıt Bulunamadı		
103	İade Tutarı Satış Tutarından Büyük Olamaz		
104	RefundedAmountError		iade edilen tutar ve işlemde daha önce iade edilmiş olan tutarların toplamından işlemde yapılan ödeme miktarından büyük ise
200	Üye İşyeri Bulunamadı		
250	Üye İşyeri Api Kullanıcısı için Yetkisiz İşlem		
400	Cvv Format Hatası.		
401	Expire Date Format Hatası.		
402	Card No Format Hatası.		
997	Hash Hatası		
998	Validasyon Hatası	Time Span' alanı yyyyMMddHHmmss formatında olmalıdır.	
'Time Span' alanı 60 dakidan büyük olamaz, mevcut dakika fark: 240.	
Unexpected character encountered while parsing value: ,. Path 'orderId', line 2, position 14. Zorunlu alan boş gönderildiğinde alınan hata	Zorunlu alan boş gönderildiğinde alınan hata
An undefined token is not a valid System.Int64. Path 'clientId', line 3, position 14.	Zorunlu alan boş gönderildiğinde alınan hata
999	Genel Hata		


Test Kart Bilgileri
Kart No	Son Kullanım Tarihi	CVV	3D Secure Şifre
4546711234567894	12/26	000	-
4531444531442283	12/26	001	-
5406675406675403	12/26	000	-






Eklentiler
WOOCOMMERCE Tosla İşim Kurulum Adımları
Adım 1. https://tosla.com/isim-icin/gelistirici-merkezi sayfasının en alt kısmında yer alan Woocommerce v8.2 altı veya v8.2 üstü versiyondan uygun olan Sanal POS WooCommerce modülüne tıklayarak modülü bilgisayarına indirmelisin.

Adım 2. Wordpress yönetim panelinize giriş yaptıktan sonra "Eklentiler" bölümünden "Yeni Ekle" butonuna tıkladıktan sonra indirdiğin modülü seçmelisin.

Adım 3. Karşına çıkan Tosla İşim eklentisinde "Yükle" butonuna basmalısın. Böylelikle Tosla İşim eklentisi sistemine yüklenmiş olacaktır.

Adım 4. Wordpress yönetim panelinizde Eklentiler > Yüklü Eklentiler menüsüne ulaştıktan sonra Tosla İşim WooCommerce eklentisini bulup aktif hale getirmelisin.

Adım 5. Woocommerce >Ayarlar >Ödeme menüsüne gidin ve "Tosla İşim" linkine tıkla. "Sanal POS 3D Ödeme Modülü Aktif" kutucuğunu işaretle.




Adım 6. Bu aşamadan sonra senden Tosla İşim Api ve güvenlik anahtarlarınızı girmen istenecektir. Tosla İşim Üye İşyeri Panelinize giriş yapın. Sol menü içerisinde İşyeri Bilgileri başlığına, ardından API Bilgileri başlığına tıklamalısın. Sanal POS API Bilgileri kutucuğunu tikleyerek SMS ile gönder’e tıkladığında Tosla İşim’e üye olurken bizlere belirttiğin iletişim cep numarasına API KEY değerlerin iletilecektir. Bilgilerin SMS ile iletilmesinde sorun yaşıyorsan desteğe ihtiyaç duyduğun anda posdestek@tosla.com adresi üzerinden bize ulaşabileceğini unutma!

Adım 7. Bu değerleri WooCommerce'teki yönetim sayfanızda ilgili alanlara kopyalayın. Sıralama için ise ödeme seçenekleri arasında hangi sırada görülmesini istiyorsan o sıra numarasını girmelisin.

Adım 8. Kaydet butonuna bastıktan sonra Tosla İşim Sanal POS’u kullanmaya başlayabilirsin.

PrestaShop Tosla İşim Kurulum Adımları
Adım 1. https://tosla.com/isim-icin/gelistirici-merkezi sayfasının en alt kısmında yer alan Presta v1.6x - v1.7x veya v8.x arasından sana uygun olan Sanal POS Presta modülüne tıklayarak modülü indirmelisin. İndirdiğin zip dosyasını açmalı/çıkarmalısın.

Adım 2. PrestaShop admin panelinize giriş yap.

Adım 3. Sol menüden Modüller -> Modül Manager seç.

Adım 4. “Bir modül yükle” butonuna bas. Bastıktan sonra açılan pop-up üzerine indirdiğin dosyayı sürükle veya bir dosya seçin alanına tıklayarak indirdiğin modül dosyasını seç.

Adım 5. Yükleme işlemi tamamlandıktan sonra “Yapılandır” butonuna basarak modül ayar sayfasına gitmelisin.

Adım 6. Tosla İşim Üye İşyeri Paneline giriş yap. Sol menü içerisinde İşyeri Bilgileri başlığına, ardından API Bilgileri başlığına tıklamalısın. Sanal POS API Bilgileri kutucuğunu tikleyerek SMS ile gönder’e tıkladığında Tosla İşim’e üye olurken bizlere belirttiğin iletişim cep numarasına API KEY değerlerin iletilecektir. Bilgilerin SMS ile iletilmesinde sorun yaşıyorsan desteğe ihtiyaç duyduğun anda posdestek@tosla.com adresi üzerinden bize ulaşabileceğini unutma!

Tosla İşim Geliştirici Merkezi
Tosla İşim’e entegre olurken her adımınızda sizlere yardımcı olabilmemiz için Geliştirici Merkezi’ne göz atabilir ya da entegrasyon kapsamında sorun yaşamanız durumunda posdestek@tosla.com mail adresinden destek alabilirsiniz.



Ortam Bilgileri
Test Ortam URL https://prepentegrasyon.tosla.com/api/Payment/ 

ClientId 1000000494

ApiUser POS_ENT_Test_001

ApiPass POS_ENT_Test_001!*!*



Production (Üretim) Ortam URL https://entegrasyon.tosla.com/api/Payment/

* Production (Üretim) ortamı için clientId, apiUser, apiPass bilgileriniz entegrasyonunuz tamamlandıktan sonra iletilecektir.





Zorunlu Parametreler
Tüm API çağrılarında aşağıdaki parametrelerin gönderilmesi gerekmektedir.



Alan Adı	Tipi	Açıklama	Örnek Değer
clientId	long	API müşteri numaranız	1000000494
apiUser	string	API kullanıcı adınız	POS_ENT_Test_001
rnd	string (max 24)	İşlem için üretilmiş random değer. Hash içerisinde kullanılan değer ile aynı olmalıdır.	123456ABC
timeSpan	string	İşlem tarihi (yyyyMMddHHmmss). Hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GMT+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.	20221012010436
Hash	string	Güvenlik kontrolünde kullanılacak hash stringi, her hash sadece bir kez kullanılacaktır.	Hash üretim adımını inceleyiniz




Hash Üretimi
ApiPass, ClientId, ApiUser, RandomString, TimeSpan değerleri sırasıyla uc uca eklenerek string oluşturulur.
Oluşturulan string'in SHA512 algoritması ile Byte hash'i alınır.
Oluşturulan byte hash base64 encode edilerek hash değeri oluşturulur.


.Net Örnek Hash Fonksiyonu

                            private string CreateHash()
                            {
                                var randomGenerator = new Random();
                                var ApiPass = "POS_ENT_Test_001!*!*";
                                var ClientId = "1000000494";
                                var ApiUser = "POS_ENT_Test_001";
                                var Rnd = randomGenerator.Next(1, 1000000).ToString();
                                var TimeSpan = DateTime.Now.ToString("yyyyMMddHHmmss");
                                var hashString =ApiPass + ClientId + ApiUser + Rnd + TimeSpan;
                                System.Security.Cryptography.SHA512 sha = new System.Security.Cryptography.SHA512CryptoServiceProvider();
                                byte[] bytes = Encoding.UTF8.GetBytes(hashString);
                                byte[] hashingbytes = sha.ComputeHash(bytes);
                                var hash = Convert.ToBase64String(hashingbytes);
                                return hash;
                            }
                        
PHP Örnek Hash Fonksiyonu
                            public function generateHash()
                            {
                                    $apiPass = "POS_ENT_Test_001!*!*";
                                    $clientId = "1000000494";
                                    $apiUser = "POS_ENT_Test_001";
                                    $rnd = rand(1, 10000);
                                    $timeSpan = time();
                            
                                    $hashString = $apiPass . $clientId . $apiUser . $rnd . $timeSpan;
                                    $hashingbytes = hash("sha512", ($hashString), true);
                                    $hash = base64_encode($hashingbytes);
                                    return $hash;
                            }
                        




CallBack URL Hash Doğrulama Mekanizması
CallbackURL’e dönen HashParameters içinde yer alan parametrelerin başına apiPass eklenerek Hash generate edilerek Hash validasyonu yapılabilir.


.Net Örnek Hash Fonksiyonu

                            static void Main(string[]args)
                            {
                                //var hashString = apiPass + threeDsession.ClientId + threeDsession.ApiUser +;
                                threeDsession.OrderId + threeDsession.MdStatus + threeDsession.BankResponseCode + 
                                threeDsession.BankResponseMessage + threeDsession.RequestStatus;
                                var hashString = "DGSApi123.123" + "1000000061" + "DGS_Api" + "P-2" + "1" + "00"+
                                "Onaylandı" + "1";
                                var hash = CalculateHash(string inputData)
                                }
                                public static string CalculateHash(string inputData)
                                {
                                string hash = "";
                                System.Security.Cryptography.SHA512 sha = new
                                System.Security.Cryptography.SHA512CryptoServiceProvider(); byte[]
                                bytes = Encoding.UTF8.GetBytes(inputData);
                                byte[] hashingbytes = sha.ComputeHash(bytes);
                                hash = Convert.ToBase64String(hashingbytes);
                                return hash;
                            }
                      

Ödeme İşlem Başlatma
Ödeme formu ve Ortak Ödeme Sayfası ile ödeme işlemi başlatmak için ThreeDSessionId değeri üretilmelidir. Bu servis 3D secure başlatılması için session açar ve sessionId bilgisini döner.

Bu servisten dönen ThreeDSessionId değeri ödeme formunda veya ortak ödeme sayfa çağırma işleminde kullanılır.



ThreeDSessionId 3D doğrulama işleminde kullanılan benzersiz değerdir. Her başarılı/başarısız işlem için tekrar üretilmelidir.

Method Name : threeDPayment

Http Method : Post

Content-Type : application/json

Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
CallbackUrl	string	1024	Evet	3D sonucunun dönüleceği url. Url’e dönülecek veri hashlenecektir. CallBack URL Hash Doğrulama Mekanizması başlığından incelenmelidir.
isCommission	Int	1	Opsiyonel	Taksitli işlemlerde Komisyonlu Ödeme alınmak istendiği durumlarda 1 olarak gönderilmelidir. Dİğer durumlarda 0 veya gönderilmez.
OrderId	string	20	Opsiyonel	Sipariş Numarasıdır. Belirlenmediği takdirde sistem otomatik üretecektir.
Amount	long	18	Evet	İşlem Tutarı, son iki hane kuruştur. 1522 = 15 TL 22 Kuruş
Currency	int	3	Evet	İşlem Para birimi 949
InstallmentCount	int	2	Opsiyonel	Default “0”
Description	string	256	Opsiyonel	İşleme ait açıklama
Echo	string	256	Opsiyonel	İstek sonucunda geri gönderilecek bilgi alanı
ExtraParameters	string	4000	Opsiyonel	Ekstra bilgilerin gönderildiği alan. Inquiry servisi cevabında döner


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
ThreeDSessionId	string	1024	3D işlem için üretilen Unqiue numara
TransactionId	string
20	İşlem ID'si


Örnekler - Komisyon Yansıtılmasının Kullanılmadığı Durum
Request	Response
{
                            "clientId": 1,
    "apiUser": "testUser",
    "rnd": "***",
    "timeSpan": "20191121160000",
    "hash": "***",
    "orderId": "12345678901234567890",
    "callbackUrl": "***merchantCallbackUrl***",
    "description": "açıklama",
    "echo": "string",
    "extraParameters": "string",
    "amount": 100,
    "currency": 949,
    "installmentCount": 0
                           }
{
                             
    "Code": 0,
    "Message": "Başarılı",
    "ThreeDSessionId": "***",
    "TransactionId": "12345"
}


Örnekler - Komisyon Yansıtıldığı Durum
Request	Response
{
                            "clientId": 1,
    "apiUser": "testUser",
    "rnd": "***",
    "timeSpan": "20191121160000",
    "hash": "***",
    "orderId": "12345678901234567890",
    "callbackUrl": "***merchantCallbackUrl***",
    "description": "açıklama",
    "echo": "string",
    "extraParameters": "string",
    "isCommission ":1,
    "amount": 100,
    "totalAmount "102,
    "currency": 949,
    "installmentCount": 0
}
{
                             
   "Code": 0,
    "Message": "Başarılı",
    "ThreeDSessionId": "***",
    "TransactionId": "12345"
}


CODE	MESSAGE	Validation Errors	AÇIKLAMA
0			
202	Üye İşyeri Kullanıcısı Bulunamadı		
250	Üye İşyeri Api Kullanıcısı için Yetkisiz İşlem		
510	IsComission true iken TotalAmount 0 dan büyük olmalıdır.		
511	Komisyonlu tutar yanlış hesaplanmıştır.		
997	hash Hatası		
998	Validasyon Hatası	Time Span' alanı yyyyMMddHHmmss formatında olmalıdır.	
'Time Span' alanı 60 dakidan büyük olamaz, mevcut dakika fark: 240.	

An undefined token is not a valid System.Nullable`1[System.Int64]. Path 'amount', line 3, position 12."	Zorunlu alan boş gönderildiğinde alınan hata




3D ile Ödeme
Kredi kartı bilgileri girildikten sonra kullanıcıyı 3D Ödeme akışına yönlendirir. İşlem sonucunu callbackUrl parametresinde belirttiğiniz adrese POST eder.



Kurallar & Uyarılar
API entegrasyonunda üye işyeri kendi ödeme formunu kullanır.
Form görsel tasarımını üye iş yeri istediği gibi tasarlayabilir.
Kredi kartı numarası (PAN) luhn algoritmasına tabi tutularak kontrol edilmeli, doğruluğu teyit edildikten sonra API'ye gönderilmelidir.
Kartın hamilinin adı ve soyadı alınmalıdır.
Son kullanma tarihi geçmiş kartlar ile API'ye sorgu yapılmamalıdır.
Ödeme formu American Express (AMEX) kartları ile işlem alabilir şekilde tasarlanmalıdır.
Formunuzdaki input name değerleri aşağıdaki parametreler ile aynı olmalıdır.
Method Name : ProcessCardForm
Http Method : Post
Content-Type : multipart/form-data


Request Parametreleri
Input Name	Zorunlu	Açıklama
ThreeDSessionId	Evet	Ödeme işlem başlatma adımında oluşturduğunuz ThreeDSessionId değeri
CardHolderName	Evet	Kart hamilinin adı ve soyadı
CardNo	Evet	Kredi kart numarası
ExpireDate	Evet	Kart son kullanım tarihi (AA/YY)
Cvv	Evet	Kart güvenlik kodu


Response
'Ödeme İşlem Başlatma' adımındaki callbackUrl parametresinde belirttiğiniz adrese 3D işlem sonucunu POST eder.



3D İşlem Sonucu Response POST Parametreleri
Eğer BankResponseCode 00 ise ödeme işlem başarılıdır. Bunun dışındaki tüm işlemler başarısız olarak kabul edilir.



Alan Adı	Açıklama
Code	0 ise İşlem servis isteği başarılıdır. Diğer numaralar hatalıdır.
Message	İşleme ait mesaj
OrderId	İşlemin sipariş numarası
BankResponseCode	Banka tarafındaki işlem statü kodu. 00 ise ödeme başarılı diğer tüm durumlar başarısız olarak kabul edilir.
BankResponseMessage	Banka tarafındaki işlem statü mesajı
AuthCode	İşlemin bankadaki otorizasyon kodu
HostReferenceNumber	Host Reference Number
TransactionId	İşlemin sistemdeki numarası
CardHolderName	İşlemde kullanılan kartın hamili



CODE	MESSAGE	Validation Errors	AÇIKLAMA
0	Başarılı		
1	AlreadyBankResponse		İşlem ekranında kayıtlı işleme ait BankResponseCode is not null
1	Tutar Sıfır Olamaz		İşlemler ekranında kayıtlı işlemde Amount=0 ise
998	Validasyon Hatası	After parsing a value an unexpected character was encountered: \". Path 'additionalProp3', line 5, position 1.	
0824' is not a valid number. Path 'additionalProp4', line 5, position 25.	
999	Genel Hata		






3D Otorizasyon (PreAuth - PostAuth)
Müşteriden tahsilat yapmadan önce kredi kartında otorizasyon yapmak için kullanılır.

Otorizasyon Adımları
ThreeDPreAuth servisi ile ön otorizasyon işlemi başlatılır. Servis ThreeDSessionId değeri döner. ThreeDPreAuth servisini inceleyiniz.
ThreeDSessionId değeri ile ProcessCardForm servisine kredi kartı bilgileri form POST edilerek otorizasyon alınır. Bu aşamada tutar, müşteri kredi kartından bloke edilir. Bu işlem için "3D ile Ödeme" adımını inceleyiniz.
Otorizasyon işleminin tamamlanıp tutarın tahsil edilmesi için postAuth servisi çağrılır. postAuth servisi çağrıldıktan sonra işlem finansallaşır ve tahsilat gerçekleşmiş olur. PostAuth servisini inceleyiniz.


Notlar
ThreeDPreAuth, ProcessCardForm ve PostAuth servisleri farklı zamanlarda çağrılabilir.
İstenilen bir zamanda "İptal" adımı takip edilerek. Otorizasyon işlemi iptal edilip, kart üzerindeki tutar blokesi kaldırılabilir.


ThreeDPreAuth
Müşteriden bir ön otorizasyon alma işlemi başlatır.

Method Name : threeDPreAuth
Http Method : Post
Content-Type : application/json




Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
CallbackUrl	string	1024	Evet	3D sonucunun dönüleceği url. Url’e dönülecek veri hashlenecektir. CallBack URL Hash Doğrulama Mekanizması başlığından incelenmelidir.
OrderId	string	20	Opsiyonel	Ön Otorizasyon (PreAuth) işleminde gönderilen Sipariş numarasıdır.
Amount	long	18	Evet	İşlem tutarı
Currency	int	3	Evet	İşlem Para birimi TL için 949
Extraparameters	string	30	Opsiyonel	Application channel bilgisi


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
ThreeDSessionId	string	256	3D işlem için üretilen Unqiue numara
TransactionId	string	256	3D işlem için üretilen Unqiue numara


Örnekler
Request	Response
{
                          "clientId": 1,
                          "apiUser": "***",
                          "rnd": "***",
                          "timeSpan": "20191121160000",
                          "hash": "***",
                          "orderId": "12345678901234567890",
                          "callbackUrl": "***merchantCallbackUrl***",
                          "amount": 100,
                          "currency": 949,
                          "extraparameters": "abc"
                          }
{
                          "code": 0,
                          "message": "Başarılı",
                          "threeDSessionId": "***"
                          }




PostAuth
Müşteriden bir ön otorizasyon alma işlemi başlatır.

Method Name : postAuth
Http Method : Post
Content-Type : application/json



Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
OrderId	string	20	Opsiyonel	Ön Otorizasyon (PreAuth) işleminde gönderilen Sipariş numarasıdır.
Amount	long	18	Evet	İşlem tutarı
Currency	int	3	Evet	İşlem Para birimi TL için 949
Extraparameters	string	30	Opsiyonel	Application channel bilgisi


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
OrderId	string	20	Sipariş Numarasıdır. Belirlenmediği takdirde sistem otomatik üretecektir.


Örnekler
Request	Response
{
                                                  "clientId": 1,
                                                  "apiUser": "testUser",
                                                  "rnd": "***",
                                                  "timeSpan": "20191121160000",
                                                  "hash": "***",
                                                  "orderId": "12345678901234567890",
                                                  "amount": 2211,
                                                  "extraparameters": "abc"
                                                  }
{
  "code": 0,
  "message": "Başarılı",
  "orderId": "12345678901234567890"
}
                                                  "code": 0,
                                                  "message": "Başarılı",
                                                  "orderId": "12345678901234567890"
                                                  }








Non3D ile Ödeme
Kredi kartı bilgileri girildikten sonra kullanıcıyı Non3D Ödeme akışına yönlendirir. 3D ile ÖDEME adımında girilen Kurallar ve Uyarılar burada da geçerli olmalıdır.

Kurallar & Uyarılar
API entegrasyonunda üye işyeri kendi ödeme formunu kullanır.
Form görsel tasarımını üye iş yeri istediği gibi tasarlayabilir.
Kredi kartı numarası (PAN) luhn algoritmasına tabi tutularak kontrol edilmeli, doğruluğu teyit edildikten sonra API'ye gönderilmelidir.
Kartın hamilinin adı ve soyadı alınmalıdır.
Son kullanma tarihi geçmiş kartlar ile API'ye sorgu yapılmamalıdır.
Ödeme formu American Express (AMEX) kartları ile işlem alabilir şekilde tasarlanmalıdır.
Formunuzdaki input name değerleri aşağıdaki parametreler ile aynı olmalıdır.
Method Name : Payment
Http Method : Post
Content-Type : application/json



Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
cardHolderName	string	100	Evet	Kart hamilinin adı ve soyadı
cardNo	string	16	Evet	Kredi kartı numarası
expireDate	string	4	Evet	Kart son kullanım tarihi (AAYY)
cvv	string	4	Evet	Kart güvenlik kodu
clientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
apiUser	string	100	Evet	Api kullanıcı adı
rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
timeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
orderId	string	20	Opsiyonel	Sipariş Numarasıdır. Belirlenmediği takdirde sistem otomatik üretecektir.
isCommission	long	18	Opsiyonel	isCommission değeri 1 gönderildiği durumlarda zorunludur. totalAmount değeri GetInstallmentOptions’ta dönen taksite karşılık gelen değer gönderilmelidir. isCommission değeri 1 gönderildiğinde bankaya gönderilen tutardır, son iki hane kuruştur. 1528 = 15 TL 28 Kuruş
amount	long	18	Evet	İşlem Tutarı, son iki hane kuruştur. 1522 = 15 TL 22 Kuruş
totalAmount	long	18	Opsiyonel	isCommission değeri 1 gönderildiği durumlarda zorunludur. totalAmount değeri GetInstallmentOptions’ta dönen taksite karşılık gelen değer gönderilmelidir. isCommission değeri 1 gönderildiğinde bankaya gönderilen tutardır, son iki hane kuruştur. 1528 = 15 TL 28 Kuruş
currency	int	3	Evet	İşlem Para birimi 949
installmentCount	int	2	Opsiyonel	Default “0”
description	string	256	Opsiyonel	İşleme ait açıklama
echo	string	256	Opsiyonel	İstek sonucunda geri gönderilecek bilgi alanı
extraParameters	string	4000	Opsiyonel	Ekstra bilgilerin gönderildiği alan. Inquiry servisi cevabında döner


Response
Alan Adı	Açıklama
Code	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	İşleme ait mesaj
OrderId	İşlemin sipariş numarası
BankResponseCode	Banka tarafındaki işlem statü kodu. 00 ise başarılı diğer tüm durumlar başarısız olarak kabul edilir.
BankResponseMessage	Banka tarafındaki işlem statü mesajı
AuthCode	İşlemin bankadaki otorizasyon kodu
HostReferenceNumber	Host Reference Number
TransactionId	İşlemin sistemdeki numarası
CardHolderName	İşlemde kullanılan kartın hamili


Eğer BankResponseCode 00 ise işlem başarılıdır. Bunun dışındaki tüm işlemler başarısız olarak kabul edilir.



Örnekler - Komisyon Yansıtılmasının Kullanılmadığı Durum
Request	Response
{
                            "clientId": 1,
    "apiUser": "testUser",
    "rnd": "***",
    "timeSpan": "20191121160000",
    "hash": "***",
    "orderId": "12345678901234567890",
    "callbackUrl": "***merchantCallbackUrl***",
    "description": "açıklama",
    "echo": "string",
    "extraParameters": "string",
    "amount": 100,
    "currency": 949,
    "installmentCount": 0
                           }
{
                             
    "Code": 0,
    "Message": "Başarılı",
    "ThreeDSessionId": "***",
    "TransactionId": "12345"
}


Örnekler - Komisyon Yansıtıldığı Durum
Request	Response
{
                            "clientId": 1,
    "apiUser": "testUser",
    "rnd": "***",
    "timeSpan": "20191121160000",
    "hash": "***",
    "orderId": "12345678901234567890",
    "callbackUrl": "***merchantCallbackUrl***",
    "description": "açıklama",
    "echo": "string",
    "extraParameters": "string",
    "isCommission ":1,
    "amount": 100,
    "totalAmount "102,
    "currency": 949,
    "installmentCount": 0
}
{
                             
   "Code": 0,
    "Message": "Başarılı",
    "ThreeDSessionId": "***",
    "TransactionId": "12345"
}



CODE	MESSAGE	Validation Errors
0	Başarılı	
100	Ödeme Hatalı	
202	Üye İşyeri Kullanıcısı Bulunamadı	
250	Üye İşyeri Api Kullanıcısı için Yetkisiz İşlem	
997	Hash hatası	
998	Validasyon Hatası	'Time Span' alanı yyyyMMddHHmmss formatında olmalıdır.
'Time Span' alanı 60 dakidan büyük olamaz, mevcut dakika fark: xxx.
999	Genel Hata	İstenmeyen bir durumda alınan hatadır.








Ortak Ödeme Sayfası
Kendi özel ödeme formunu kullanmak istemeyen üye işyerleri için uygundur. 'Ödeme İşlem Başlatma' adımında oluşturulan threeDSessionId değeri ile Ortak Ödeme Sayfası iframe içinde gösterilebilir ya da kullanıcı doğrudan bu linke yönlendirilebilir. Ödeme işlem sonucu CallbackUrl adresine sistem tarafından POST edilir.

Not: Kullanıcının bankanın 3D sayfasına yönlendirme yapılabilmesi için browser desteği gerekmektedir.

Method Name : threeDSecure
Http Method : get
Content-Type : html/text


Request Parametreleri

Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ThreeDSessionId	string	256	Evet	3D işlem için üretilen Unqiue numara


<iframe src="https://[Ortam URL]/api/Payment/threeDSecure/[threeDSessionId]" height="500" width="100%"></iframe>

Ortam URL bilgisinin "Üretim Ortamında" değiştirilmesi gerekmektedir. Link formatı [Ortam URL] /threeDSecure/ [threeDSessionId]'dır.







Taksit ve Komisyon Bilgisi
Bin numarasına göre kartın bağlı olduğu banka bilgileri ile birlikte sunulabilecek taksit ve komisyon oranlarını gönderir.

Method Name :GetCommissionAndInstallmentInfo
Http Method : Post
Content-Type : application/json


Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
Bin	int	6	Evet	Kredi kartının ilk 6 hanesidir.


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
CardPrefix	int	6	Bin numarası
BankId	int	3	Sistemdeki banka ID değeri
BankCode	string	512	Banka kodu
BankName	string	512	Banka adı
CardName	string	512	Kart adı
CardClass	string	512	Kart sınıfı
CardType	string	512	Kart tipi
Country	string	200	Kart ülkesi
BankCommission	int	6	Banka komisyonu
InstallmentInfo	string		Taksit listesi ve komisyon oranı


Örnekler
Request	Response
{
 "bin": 407814,
 "clientId": 1000000000,
 "apiUser": "XXXXXXXX",
 "rnd": "string",
 "timeSpan": "string",
 "hash": "XXXXXXXX"
 }
{
 "CardPrefix": 407814,
 "BankId": 9,
 "BankCode": "ZiraatBankası",
 "BankName": "Ziraat Bankası",
 "CardName": "Bankkart",
 "CardClass": "Banka Kartı",
 "CardType": "Visa",
 "Country": "TR",
 "BankCommission": 0,
 "InstallmentInfo": {
 "T2": {
 "Rate": 2.99,
 "Constant": 2
 },
 "T3": {
 "Rate": 3.99,
 "Constant": 2
 },
 "T4": {
 "Rate": 4.99,
 "Constant": 2
 },
 "T5": {
 "Rate": 5.99,
 "Constant": 2
 },
 "T6": {
 "Rate": 6.99,
 "Constant": 0
 },
 "T7": {
 "Rate": 7.99,
 "Constant": 0
 },
 "T8": {
 "Rate": 8.99,
 "Constant": 0
 },
 "T9": {
 "Rate": 9.99,
 "Constant": 0
 },
 "T10": {
 "Rate": 10.99,
 "Constant": 0
 },
 "T11": {
 "Rate": 11.99,
 "Constant": 0
 },
 "T12": {
 "Rate": 12.99,
 "Constant": 0
 }
 },
 "Code": 0,
 "Message": ""
 }




Taksit ve Taksitlere Karşılık Gelen Tutar Bilgisi
isCommission 1 gönderildiği durumda amount değerine karşılık gelen taksitlerdeki totalAmount değerinde gönderilecek değerler döner.

Method Name :GetInstallmentOptions
Http Method : Post
Content-Type : application/json


Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
amount	long	18	Evet	İşlem tutarı


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
InstallmentOptions	int	6	Taksit listesi ve tutar

Örnekler
Request	Response
{
  "amount": 10000,
  "clientId": 1000000000,
  "apiUser": "XXXXXXXX",
  "rnd": "string",
  "timeSpan": "string",
  "hash": "XXXXXXXX"
}
{
  "installmentOptions": [
    {
      "installment": 1,
      "title": "Tek Çekim",
      "amount": 10000,
      "currency": 949
    },
    {
      "installment": 2,
      "title": "2 Taksit",
      "amount": 10089,
      "currency": 0
    }
    {
      "installment": 3,
      "title": "3 Taksit",
      "amount": 10115,
      "currency": 949
    }
   ],
  "Code": 0,
  "Message":""
 }




Ödeme Sorgulama
Tosla İşim de kaydı oluşturulan bir işlemin detayları sorgulanabilir. İşlem sorgulama servisidir. OrderID ile işlem sorgulama yapılabilmektedir.

Method Name : inquiry
Http Method : Post
Content-Type : application/json



Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
OrderId	string	20	Evet	Ön Otorizasyon (PreAuth) işleminde gönderilen Sipariş numarasıdır.
TransactionId	string	20	Opsiyonel	Sorgulanacak işlemin Id’si


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
TransactionType	int	65	3D işlem için Üretilen Unqiue numara
CreateDate	string	23	Oluşturma tarihi
OrderId	String	20	Sipariş numarasıdır
BankResponseCode	String	7	Bankadan alınan cevap kodu
BankResponseMessage	String	512	Bankadan alınan cevap mesajı
AuthCode	String	200	Otorizasyonda kullanılan kodu
HostReferenceNumber	string	200	
Amount	long	18	İşlem tutarı
Currency	int	3	İşlem Para birimi 949
InstallmentCount	int	2	Taksit sayısı
ClientId	long	20	Mağaza İçin belirlenmiş benzersiz numara
CardNo	string	19	Kart Numarası
RequestStatus	int	2	İşlem durumunu gösterir
RefundedAmount	Long	18	İade edilen miktar
PostAuthedAmount	Long	18	Ön Otorizasyon işlemindeki tutar
TransactionId	Long	20	İşlem İD’si
CommissionStatus	Int	1	Komisyon durumu
NetAmount	Long	18	Net tutar
MerchantCommissionAmount	Long	18	Üye iş yerinin komisyon miktarı
MerchantCommissionRate	decimal	18	Üye iş yerinin komisyon oranı
CardBankId	Long	20	Kullanılan banka id’si
CardTypeId	long	20	Kullanılan kart tipi id’si
ValorDate	int	8	Valör günü
TransactionDate	int	8	İşlem yapılan tarihtir. yyyyMMdd
BankValorDate	int	8	 
ExtraParameters	string	4000	 


Örnekler
Request	Response
{
 "clientId": 1,
 "apiUser": "***",
 "rnd": "***",
 "timeSpan": "20221011235931",
 "hash": "***",
 "orderId": "20221011999",
 "transactionId": ""
 } 
{
 "Count": 36,
 "Transactions": [
 {
 "TransactionType": 1,
 "CreateDate": "20221011152449",
 "OrderId": "20221011999",
 "BankResponseCode": "00",
 "BankResponseMessage": "Onaylandı",
 "AuthCode": "S90037",
 "HostReferenceNumber": "228415127919",
 "Amount": 1090,
 "Currency": 949,
 "InstallmentCount": 0,
 "ClientId": 1000000494,
 "CardNo": "41595600****7732",
 "RequestStatus": 1,
 "RefundedAmount": 0,
 "PostAuthedAmount": 0,
 "TransactionId": 2000000000029968,
 "CommissionStatus": 1,
 "NetAmount": 1090,
 "MerchantCommissionAmount": 0,
 "MerchantCommissionRate": 0,
 "CardBankId": 13,
 "CardTypeId": 2,
 "ValorDate": 0,
 "TransactionDate": 20221011,
 "BankValorDate": 0,
 "ExtraParameters": null,
 "Code": 0,
 "Message": "Başarılı"
 }
 ],
 "Code": 0,
 "Message": "Başarılı"
 }


CODE	MESSAGE	Validation Errors	AÇIKLAMA
0			
202	Üye İşyeri Kullanıcısı Bulunamadı		
250	Üye İşyeri Api Kullanıcısı için Yetkisiz İşlem		
997	Hash Hatası		
998	Validasyon Hatası	Time Span' alanı yyyyMMddHHmmss formatında olmalıdır.	
Time Span' alanı 60 dakidan büyük olamaz, mevcut dakika fark: 1050626	
Unexpected character encountered while parsing value: ,. Path 'orderId', line 2, position 14.	Zorunlu alan boş gönderildiğinde alınan hata
An undefined token is not a valid System.Int64. Path 'clientId', line 3, position 14.	Zorunlu alan boş gönderildiğinde alınan hata
999	Genel Hata		




İşlem Listeleme
Tarihe göre işlemleri listeler. Tarih gönderilerek iletilen tarihteki tüm işlemlerin listelenmesini sağlar.

Method Name : history
Http Method : Post
Content-Type : application/json


Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
OrderId	string	20	Opsiyonel	Sipariş numarası
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
TransactionDate	int	8	Evet	İşlem yapılan tarihtir. yyyyMMdd
Page	int	int max	Evet	Paging yapısı gereği kullanılan sayfa numarası ilk sayfa için 1, ikinci sayfa için 2...
PageSize	int	3	Evet	Paging yapısı gereği kullanılan sayfa başı kayıt sayısı.


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
Count	int	int max	Tarih filtresine uygun toplam kayıt sayısını döner.
Transactions	list		İşleme ait ödeme listesini döner.
#	Değer	Anlam
TransactionType	1	Satış
TransactionType	4	İptal
TransactionType	5	İade
TransactionType	6	Harcama İtirazı
#	Değer	Anlam
RequestStatus	0	Hatalı
RequestStatus	1	Başarılı
RequestStatus	2	İptal Edildi
RequestStatus	3	Parçalı İade Edildi
RequestStatus	4	İptal Edildi //Tamamı İade Edildi
RequestStatus	5	Ön Otorizasyon Kapandı
RequestStatus	6	Parçalı Harcama İtirazı
RequestStatus	7	Tam Harcama İtirazı
RequestStatus	10	3D Bekleniyor
RequestStatus	11	3D ye Gönderildi
RequestStatus	12	3D den Cevap Geldi
RequestStatus	14	İade Bekleniyor
RequestStatus	15	İptal Edildi
RequestStatus	16	İleri Vadeli Alacaktan İade


Örnekler
Request	Response
{
 "OrderId": " A1B2C3D4",
 "ClientId": 1,
 "ApiUser": "apiuser",
 "Rnd": "string",
 "TimeSpan": "20220119111007",
 "Hash": " A1B2C3D4",
 "Page": 1,
 "PageSize": 10,
 "TransactionDate": 20220412
 }
 
{
 "Code": 101,
 "Message": "string",
 "Count": 123,
 "Transactions": [
 {
 "TransactionType": 1,
 "CreateDate": "20220119111007",
 "OrderId": " A1B2C3D4",
 "BankResponseCode": "MD99",
 "BankResponseMessage": "mesaj",
 "AuthCode": null,
 "HostReferenceNumber": null,
 "Amount": 100,
 "Currency": 949,
 "InstallmentCount": 1,
 "ClientId": 1,
 "CardNo": "123456******1234",
 "RequestStatus": 0,
 "RefundedAmount": 0,
 "PostAuthedAmount": 0,
 "TransactionId": 0,
 "CommissionStatus": null,
 "NetAmount": 0,
 "MerchantCommissionAmount": 0,
 "MerchantCommissionRate": null,
 "CardBankId": 0,
 "CardTypeId": 0,
 "Code": 0,
 "Message": ""
 }
 ]
 }


CODE	MESSAGE	Validation Errors	AÇIKLAMA
0	Başarılı		
250	Üye İşyeri Api Kullanıcısı için Yetkisiz İşlem		
998	Validasyon Hatası	Time Span' alanı yyyyMMddHHmmss formatında olmalıdır.	
Time Span' alanı 60 dakidan büyük olamaz, mevcut dakika fark: 1050626	
Unexpected character encountered while parsing value: ,. Path 'orderId', line 2, position 14.	Zorunlu alan boş gönderildiğinde alınan hata
An undefined token is not a valid System.Int64. Path 'clientId', line 3, position 14.	Zorunlu alan boş gönderildiğinde alınan hata
999	Genel Hata		




İptal
Bankalar gün sonu almadan önce işlemin iptal edilmesidir. OrderId ile ödeme işleminin iptal edilmesini sağlar.

Method Name : void
Http Method : Post
Content-Type : application/json



Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
OrderId	string	20	Evet	Ön Otorizasyon (PreAuth) işleminde gönderilen Sipariş numarasıdır.
Echo	string	256	Opsiyonel	İstek sonucunda geri gönderilecek bilgi alanı


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
OrderId	string	20	Sipariş Numarasıdır.
BankResponseCode	string	7	Bankadan alınan cevap kodu
BankResponseMessage	string	512	Bankadan alınan cevap mesajı
AuthCode	string	200	Otorizasyonda kullanılan kodu
HostReferenceNumber	string	200	Ödeme kanalın referans numarası
TransactionId	long	20	İşlem İD’si


Örnekler
Request	Response
{
 "OrderId": " A1B2C3D4",
 "ClientId": 1,
 "ApiUser": "****",
 "Rnd": "****",
 "TimeSpan": "20220119111007",
 "Hash": "****",
 "echo": "string"
 }
 
{
 "OrderId": " A1B2C3D4",
 "BankResponseCode": null,
 "BankResponseMessage": null,
 "AuthCode": null,
 "HostReferenceNumber": null,
 "TransactionId": null,
 "Code": 101,
 "Message": "string"
 }


CODE	MESSAGE	Validation Errors	AÇIKLAMA
0	Başarılı		
1	Iptal etmek istediğiniz tutar, hakkediş tutarınızdan fazladır. 10,00 tutarından daha fazla iptal/iade yapamazsınız.		
1	Başarılı bir postauth olduğu için ön otorizasyon işlemini iptal edemezsiniz.		
101	Orjinal Kayıt Bulunamadı		
202	Üye İşyeri Kullanıcısı Bulunamadı		
250	Üye İşyeri Api Kullanıcısı için Yetkisiz İşlem		
997	Hash Hatası		
998	Validasyon Hatası	Time Span' alanı yyyyMMddHHmmss formatında olmalıdır.	
'Time Span' alanı 60 dakidan büyük olamaz, mevcut dakika fark: 240.	
Unexpected character encountered while parsing value: ,. Path 'orderId', line 2, position 14.	Zorunlu alan boş gönderildiğinde alınan hata
An undefined token is not a valid System.Int64. Path 'clientId', line 3, position 14.	Zorunlu alan boş gönderildiğinde alınan hata
999	Genel Hata		




İade
Bankaların gün sonu aldıktan sonra işlemin kısmi veya tam olarak iade edilmesidir. Ödeme işlemini iade eder. Tam iade ve kısmi iade yapılabilmesini sağlar.

Method Name : refund
Http Method : Post
Content-Type : application/json


Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long19		Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
OrderId	string	20	Evet	Ön Otorizasyon (PreAuth) işleminde gönderilen Sipariş numarasıdır.
Amount	long	18	Evet	İade İşlem Tutarı, son iki hane kuruştur. 1522 = 15 TL 22 Kuruş
Echo	string	256	Opsiyonel	İstek sonucunda geri gönderilecek bilgi alanı


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
OrderId	string	20	Sipariş Numarasıdır.
BankResponseCode	string	7	Bankadan alınan cevap kodu
BankResponseMessage	string	512	Bankadan alınan cevap mesajı
AuthCode	string	200	Otorizasyonda kullanılan kodu
HostReferenceNumber	string	200	Ödeme kanalın referans numarası
TransactionId	long	20	İşlem İD’si


Örnekler
Request	Response
{
 "OrderId": " A1B2C3D4",
 "Amount": "125",
 "ClientId": 1,
 "ApiUser": "***",
 "Rnd": "***",
 "TimeSpan": "20220119111007",
 "Hash": "***",
 "echo": "string"
 }
{
 "OrderId": " A1B2C3D4",
 "BankResponseCode": null,
 "BankResponseMessage": null,
 "AuthCode": null,
 "HostReferenceNumber": null,
 "TransactionId": null,
 "Code": 101,
 "Message": "string"
 }


CODE	MESSAGE	Validation Errors	AÇIKLAMA
0	Başarılı		
1	İade etmek istediğiniz tutar, hakkediş tutarınızdan fazladır. 10,00 tutarından daha fazla iade yapamazsınız.		
100	Ödeme Hatalı		
101	Orjinal Kayıt Bulunamadı		
103	İade Tutarı Satış Tutarından Büyük Olamaz		
104	RefundedAmountError		iade edilen tutar ve işlemde daha önce iade edilmiş olan tutarların toplamından işlemde yapılan ödeme miktarından büyük ise
200	Üye İşyeri Bulunamadı		
250	Üye İşyeri Api Kullanıcısı için Yetkisiz İşlem		
400	Cvv Format Hatası.		
401	Expire Date Format Hatası.		
402	Card No Format Hatası.		
997	Hash Hatası		
998	Validasyon Hatası	Time Span' alanı yyyyMMddHHmmss formatında olmalıdır.	
'Time Span' alanı 60 dakidan büyük olamaz, mevcut dakika fark: 240.	
Unexpected character encountered while parsing value: ,. Path 'orderId', line 2, position 14. Zorunlu alan boş gönderildiğinde alınan hata	Zorunlu alan boş gönderildiğinde alınan hata
An undefined token is not a valid System.Int64. Path 'clientId', line 3, position 14.	Zorunlu alan boş gönderildiğinde alınan hata
999	Genel Hata		


Test Kart Bilgileri
Kart No	Son Kullanım Tarihi	CVV	3D Secure Şifre
4546711234567894	12/26	000	-
4531444531442283	12/26	001	-
5406675406675403	12/26	000	-






Eklentiler
WOOCOMMERCE Tosla İşim Kurulum Adımları
Adım 1. https://tosla.com/isim-icin/gelistirici-merkezi sayfasının en alt kısmında yer alan Woocommerce v8.2 altı veya v8.2 üstü versiyondan uygun olan Sanal POS WooCommerce modülüne tıklayarak modülü bilgisayarına indirmelisin.

Adım 2. Wordpress yönetim panelinize giriş yaptıktan sonra "Eklentiler" bölümünden "Yeni Ekle" butonuna tıkladıktan sonra indirdiğin modülü seçmelisin.

Adım 3. Karşına çıkan Tosla İşim eklentisinde "Yükle" butonuna basmalısın. Böylelikle Tosla İşim eklentisi sistemine yüklenmiş olacaktır.

Adım 4. Wordpress yönetim panelinizde Eklentiler > Yüklü Eklentiler menüsüne ulaştıktan sonra Tosla İşim WooCommerce eklentisini bulup aktif hale getirmelisin.

Adım 5. Woocommerce >Ayarlar >Ödeme menüsüne gidin ve "Tosla İşim" linkine tıkla. "Sanal POS 3D Ödeme Modülü Aktif" kutucuğunu işaretle.




Adım 6. Bu aşamadan sonra senden Tosla İşim Api ve güvenlik anahtarlarınızı girmen istenecektir. Tosla İşim Üye İşyeri Panelinize giriş yapın. Sol menü içerisinde İşyeri Bilgileri başlığına, ardından API Bilgileri başlığına tıklamalısın. Sanal POS API Bilgileri kutucuğunu tikleyerek SMS ile gönder’e tıkladığında Tosla İşim’e üye olurken bizlere belirttiğin iletişim cep numarasına API KEY değerlerin iletilecektir. Bilgilerin SMS ile iletilmesinde sorun yaşıyorsan desteğe ihtiyaç duyduğun anda posdestek@tosla.com adresi üzerinden bize ulaşabileceğini unutma!

Adım 7. Bu değerleri WooCommerce'teki yönetim sayfanızda ilgili alanlara kopyalayın. Sıralama için ise ödeme seçenekleri arasında hangi sırada görülmesini istiyorsan o sıra numarasını girmelisin.

Adım 8. Kaydet butonuna bastıktan sonra Tosla İşim Sanal POS’u kullanmaya başlayabilirsin.

PrestaShop Tosla İşim Kurulum Adımları
Adım 1. https://tosla.com/isim-icin/gelistirici-merkezi sayfasının en alt kısmında yer alan Presta v1.6x - v1.7x veya v8.x arasından sana uygun olan Sanal POS Presta modülüne tıklayarak modülü indirmelisin. İndirdiğin zip dosyasını açmalı/çıkarmalısın.

Adım 2. PrestaShop admin panelinize giriş yap.

Adım 3. Sol menüden Modüller -> Modül Manager seç.

Adım 4. “Bir modül yükle” butonuna bas. Bastıktan sonra açılan pop-up üzerine indirdiğin dosyayı sürükle veya bir dosya seçin alanına tıklayarak indirdiğin modül dosyasını seç.

Adım 5. Yükleme işlemi tamamlandıktan sonra “Yapılandır” butonuna basarak modül ayar sayfasına gitmelisin.

Adım 6. Tosla İşim Üye İşyeri Paneline giriş yap. Sol menü içerisinde İşyeri Bilgileri başlığına, ardından API Bilgileri başlığına tıklamalısın. Sanal POS API Bilgileri kutucuğunu tikleyerek SMS ile gönder’e tıkladığında Tosla İşim’e üye olurken bizlere belirttiğin iletişim cep numarasına API KEY değerlerin iletilecektir. Bilgilerin SMS ile iletilmesinde sorun yaşıyorsan desteğe ihtiyaç duyduğun anda posdestek@tosla.com adresi üzerinden bize ulaşabileceğini unutma!

Tosla İşim Geliştirici Merkezi
Tosla İşim’e entegre olurken her adımınızda sizlere yardımcı olabilmemiz için Geliştirici Merkezi’ne göz atabilir ya da entegrasyon kapsamında sorun yaşamanız durumunda posdestek@tosla.com mail adresinden destek alabilirsiniz.



Ortam Bilgileri
Test Ortam URL https://prepentegrasyon.tosla.com/api/Payment/ 

ClientId 1000000494

ApiUser POS_ENT_Test_001

ApiPass POS_ENT_Test_001!*!*



Production (Üretim) Ortam URL https://entegrasyon.tosla.com/api/Payment/

* Production (Üretim) ortamı için clientId, apiUser, apiPass bilgileriniz entegrasyonunuz tamamlandıktan sonra iletilecektir.





Zorunlu Parametreler
Tüm API çağrılarında aşağıdaki parametrelerin gönderilmesi gerekmektedir.



Alan Adı	Tipi	Açıklama	Örnek Değer
clientId	long	API müşteri numaranız	1000000494
apiUser	string	API kullanıcı adınız	POS_ENT_Test_001
rnd	string (max 24)	İşlem için üretilmiş random değer. Hash içerisinde kullanılan değer ile aynı olmalıdır.	123456ABC
timeSpan	string	İşlem tarihi (yyyyMMddHHmmss). Hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GMT+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.	20221012010436
Hash	string	Güvenlik kontrolünde kullanılacak hash stringi, her hash sadece bir kez kullanılacaktır.	Hash üretim adımını inceleyiniz




Hash Üretimi
ApiPass, ClientId, ApiUser, RandomString, TimeSpan değerleri sırasıyla uc uca eklenerek string oluşturulur.
Oluşturulan string'in SHA512 algoritması ile Byte hash'i alınır.
Oluşturulan byte hash base64 encode edilerek hash değeri oluşturulur.


.Net Örnek Hash Fonksiyonu

                            private string CreateHash()
                            {
                                var randomGenerator = new Random();
                                var ApiPass = "POS_ENT_Test_001!*!*";
                                var ClientId = "1000000494";
                                var ApiUser = "POS_ENT_Test_001";
                                var Rnd = randomGenerator.Next(1, 1000000).ToString();
                                var TimeSpan = DateTime.Now.ToString("yyyyMMddHHmmss");
                                var hashString =ApiPass + ClientId + ApiUser + Rnd + TimeSpan;
                                System.Security.Cryptography.SHA512 sha = new System.Security.Cryptography.SHA512CryptoServiceProvider();
                                byte[] bytes = Encoding.UTF8.GetBytes(hashString);
                                byte[] hashingbytes = sha.ComputeHash(bytes);
                                var hash = Convert.ToBase64String(hashingbytes);
                                return hash;
                            }
                        
PHP Örnek Hash Fonksiyonu
                            public function generateHash()
                            {
                                    $apiPass = "POS_ENT_Test_001!*!*";
                                    $clientId = "1000000494";
                                    $apiUser = "POS_ENT_Test_001";
                                    $rnd = rand(1, 10000);
                                    $timeSpan = time();
                            
                                    $hashString = $apiPass . $clientId . $apiUser . $rnd . $timeSpan;
                                    $hashingbytes = hash("sha512", ($hashString), true);
                                    $hash = base64_encode($hashingbytes);
                                    return $hash;
                            }
                        




CallBack URL Hash Doğrulama Mekanizması
CallbackURL’e dönen HashParameters içinde yer alan parametrelerin başına apiPass eklenerek Hash generate edilerek Hash validasyonu yapılabilir.


.Net Örnek Hash Fonksiyonu

                            static void Main(string[]args)
                            {
                                //var hashString = apiPass + threeDsession.ClientId + threeDsession.ApiUser +;
                                threeDsession.OrderId + threeDsession.MdStatus + threeDsession.BankResponseCode + 
                                threeDsession.BankResponseMessage + threeDsession.RequestStatus;
                                var hashString = "DGSApi123.123" + "1000000061" + "DGS_Api" + "P-2" + "1" + "00"+
                                "Onaylandı" + "1";
                                var hash = CalculateHash(string inputData)
                                }
                                public static string CalculateHash(string inputData)
                                {
                                string hash = "";
                                System.Security.Cryptography.SHA512 sha = new
                                System.Security.Cryptography.SHA512CryptoServiceProvider(); byte[]
                                bytes = Encoding.UTF8.GetBytes(inputData);
                                byte[] hashingbytes = sha.ComputeHash(bytes);
                                hash = Convert.ToBase64String(hashingbytes);
                                return hash;
                            }
                      

Ödeme İşlem Başlatma
Ödeme formu ve Ortak Ödeme Sayfası ile ödeme işlemi başlatmak için ThreeDSessionId değeri üretilmelidir. Bu servis 3D secure başlatılması için session açar ve sessionId bilgisini döner.

Bu servisten dönen ThreeDSessionId değeri ödeme formunda veya ortak ödeme sayfa çağırma işleminde kullanılır.



ThreeDSessionId 3D doğrulama işleminde kullanılan benzersiz değerdir. Her başarılı/başarısız işlem için tekrar üretilmelidir.

Method Name : threeDPayment

Http Method : Post

Content-Type : application/json

Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
CallbackUrl	string	1024	Evet	3D sonucunun dönüleceği url. Url’e dönülecek veri hashlenecektir. CallBack URL Hash Doğrulama Mekanizması başlığından incelenmelidir.
isCommission	Int	1	Opsiyonel	Taksitli işlemlerde Komisyonlu Ödeme alınmak istendiği durumlarda 1 olarak gönderilmelidir. Dİğer durumlarda 0 veya gönderilmez.
OrderId	string	20	Opsiyonel	Sipariş Numarasıdır. Belirlenmediği takdirde sistem otomatik üretecektir.
Amount	long	18	Evet	İşlem Tutarı, son iki hane kuruştur. 1522 = 15 TL 22 Kuruş
Currency	int	3	Evet	İşlem Para birimi 949
InstallmentCount	int	2	Opsiyonel	Default “0”
Description	string	256	Opsiyonel	İşleme ait açıklama
Echo	string	256	Opsiyonel	İstek sonucunda geri gönderilecek bilgi alanı
ExtraParameters	string	4000	Opsiyonel	Ekstra bilgilerin gönderildiği alan. Inquiry servisi cevabında döner


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
ThreeDSessionId	string	1024	3D işlem için üretilen Unqiue numara
TransactionId	string
20	İşlem ID'si


Örnekler - Komisyon Yansıtılmasının Kullanılmadığı Durum
Request	Response
{
                            "clientId": 1,
    "apiUser": "testUser",
    "rnd": "***",
    "timeSpan": "20191121160000",
    "hash": "***",
    "orderId": "12345678901234567890",
    "callbackUrl": "***merchantCallbackUrl***",
    "description": "açıklama",
    "echo": "string",
    "extraParameters": "string",
    "amount": 100,
    "currency": 949,
    "installmentCount": 0
                           }
{
                             
    "Code": 0,
    "Message": "Başarılı",
    "ThreeDSessionId": "***",
    "TransactionId": "12345"
}


Örnekler - Komisyon Yansıtıldığı Durum
Request	Response
{
                            "clientId": 1,
    "apiUser": "testUser",
    "rnd": "***",
    "timeSpan": "20191121160000",
    "hash": "***",
    "orderId": "12345678901234567890",
    "callbackUrl": "***merchantCallbackUrl***",
    "description": "açıklama",
    "echo": "string",
    "extraParameters": "string",
    "isCommission ":1,
    "amount": 100,
    "totalAmount "102,
    "currency": 949,
    "installmentCount": 0
}
{
                             
   "Code": 0,
    "Message": "Başarılı",
    "ThreeDSessionId": "***",
    "TransactionId": "12345"
}


CODE	MESSAGE	Validation Errors	AÇIKLAMA
0			
202	Üye İşyeri Kullanıcısı Bulunamadı		
250	Üye İşyeri Api Kullanıcısı için Yetkisiz İşlem		
510	IsComission true iken TotalAmount 0 dan büyük olmalıdır.		
511	Komisyonlu tutar yanlış hesaplanmıştır.		
997	hash Hatası		
998	Validasyon Hatası	Time Span' alanı yyyyMMddHHmmss formatında olmalıdır.	
'Time Span' alanı 60 dakidan büyük olamaz, mevcut dakika fark: 240.	

An undefined token is not a valid System.Nullable`1[System.Int64]. Path 'amount', line 3, position 12."	Zorunlu alan boş gönderildiğinde alınan hata




3D ile Ödeme
Kredi kartı bilgileri girildikten sonra kullanıcıyı 3D Ödeme akışına yönlendirir. İşlem sonucunu callbackUrl parametresinde belirttiğiniz adrese POST eder.



Kurallar & Uyarılar
API entegrasyonunda üye işyeri kendi ödeme formunu kullanır.
Form görsel tasarımını üye iş yeri istediği gibi tasarlayabilir.
Kredi kartı numarası (PAN) luhn algoritmasına tabi tutularak kontrol edilmeli, doğruluğu teyit edildikten sonra API'ye gönderilmelidir.
Kartın hamilinin adı ve soyadı alınmalıdır.
Son kullanma tarihi geçmiş kartlar ile API'ye sorgu yapılmamalıdır.
Ödeme formu American Express (AMEX) kartları ile işlem alabilir şekilde tasarlanmalıdır.
Formunuzdaki input name değerleri aşağıdaki parametreler ile aynı olmalıdır.
Method Name : ProcessCardForm
Http Method : Post
Content-Type : multipart/form-data


Request Parametreleri
Input Name	Zorunlu	Açıklama
ThreeDSessionId	Evet	Ödeme işlem başlatma adımında oluşturduğunuz ThreeDSessionId değeri
CardHolderName	Evet	Kart hamilinin adı ve soyadı
CardNo	Evet	Kredi kart numarası
ExpireDate	Evet	Kart son kullanım tarihi (AA/YY)
Cvv	Evet	Kart güvenlik kodu


Response
'Ödeme İşlem Başlatma' adımındaki callbackUrl parametresinde belirttiğiniz adrese 3D işlem sonucunu POST eder.



3D İşlem Sonucu Response POST Parametreleri
Eğer BankResponseCode 00 ise ödeme işlem başarılıdır. Bunun dışındaki tüm işlemler başarısız olarak kabul edilir.



Alan Adı	Açıklama
Code	0 ise İşlem servis isteği başarılıdır. Diğer numaralar hatalıdır.
Message	İşleme ait mesaj
OrderId	İşlemin sipariş numarası
BankResponseCode	Banka tarafındaki işlem statü kodu. 00 ise ödeme başarılı diğer tüm durumlar başarısız olarak kabul edilir.
BankResponseMessage	Banka tarafındaki işlem statü mesajı
AuthCode	İşlemin bankadaki otorizasyon kodu
HostReferenceNumber	Host Reference Number
TransactionId	İşlemin sistemdeki numarası
CardHolderName	İşlemde kullanılan kartın hamili



CODE	MESSAGE	Validation Errors	AÇIKLAMA
0	Başarılı		
1	AlreadyBankResponse		İşlem ekranında kayıtlı işleme ait BankResponseCode is not null
1	Tutar Sıfır Olamaz		İşlemler ekranında kayıtlı işlemde Amount=0 ise
998	Validasyon Hatası	After parsing a value an unexpected character was encountered: \". Path 'additionalProp3', line 5, position 1.	
0824' is not a valid number. Path 'additionalProp4', line 5, position 25.	
999	Genel Hata		






3D Otorizasyon (PreAuth - PostAuth)
Müşteriden tahsilat yapmadan önce kredi kartında otorizasyon yapmak için kullanılır.

Otorizasyon Adımları
ThreeDPreAuth servisi ile ön otorizasyon işlemi başlatılır. Servis ThreeDSessionId değeri döner. ThreeDPreAuth servisini inceleyiniz.
ThreeDSessionId değeri ile ProcessCardForm servisine kredi kartı bilgileri form POST edilerek otorizasyon alınır. Bu aşamada tutar, müşteri kredi kartından bloke edilir. Bu işlem için "3D ile Ödeme" adımını inceleyiniz.
Otorizasyon işleminin tamamlanıp tutarın tahsil edilmesi için postAuth servisi çağrılır. postAuth servisi çağrıldıktan sonra işlem finansallaşır ve tahsilat gerçekleşmiş olur. PostAuth servisini inceleyiniz.


Notlar
ThreeDPreAuth, ProcessCardForm ve PostAuth servisleri farklı zamanlarda çağrılabilir.
İstenilen bir zamanda "İptal" adımı takip edilerek. Otorizasyon işlemi iptal edilip, kart üzerindeki tutar blokesi kaldırılabilir.


ThreeDPreAuth
Müşteriden bir ön otorizasyon alma işlemi başlatır.

Method Name : threeDPreAuth
Http Method : Post
Content-Type : application/json




Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
CallbackUrl	string	1024	Evet	3D sonucunun dönüleceği url. Url’e dönülecek veri hashlenecektir. CallBack URL Hash Doğrulama Mekanizması başlığından incelenmelidir.
OrderId	string	20	Opsiyonel	Ön Otorizasyon (PreAuth) işleminde gönderilen Sipariş numarasıdır.
Amount	long	18	Evet	İşlem tutarı
Currency	int	3	Evet	İşlem Para birimi TL için 949
Extraparameters	string	30	Opsiyonel	Application channel bilgisi


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
ThreeDSessionId	string	256	3D işlem için üretilen Unqiue numara
TransactionId	string	256	3D işlem için üretilen Unqiue numara


Örnekler
Request	Response
{
                          "clientId": 1,
                          "apiUser": "***",
                          "rnd": "***",
                          "timeSpan": "20191121160000",
                          "hash": "***",
                          "orderId": "12345678901234567890",
                          "callbackUrl": "***merchantCallbackUrl***",
                          "amount": 100,
                          "currency": 949,
                          "extraparameters": "abc"
                          }
{
                          "code": 0,
                          "message": "Başarılı",
                          "threeDSessionId": "***"
                          }




PostAuth
Müşteriden bir ön otorizasyon alma işlemi başlatır.

Method Name : postAuth
Http Method : Post
Content-Type : application/json



Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
OrderId	string	20	Opsiyonel	Ön Otorizasyon (PreAuth) işleminde gönderilen Sipariş numarasıdır.
Amount	long	18	Evet	İşlem tutarı
Currency	int	3	Evet	İşlem Para birimi TL için 949
Extraparameters	string	30	Opsiyonel	Application channel bilgisi


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
OrderId	string	20	Sipariş Numarasıdır. Belirlenmediği takdirde sistem otomatik üretecektir.


Örnekler
Request	Response
{
                                                  "clientId": 1,
                                                  "apiUser": "testUser",
                                                  "rnd": "***",
                                                  "timeSpan": "20191121160000",
                                                  "hash": "***",
                                                  "orderId": "12345678901234567890",
                                                  "amount": 2211,
                                                  "extraparameters": "abc"
                                                  }
{
  "code": 0,
  "message": "Başarılı",
  "orderId": "12345678901234567890"
}
                                                  "code": 0,
                                                  "message": "Başarılı",
                                                  "orderId": "12345678901234567890"
                                                  }








Non3D ile Ödeme
Kredi kartı bilgileri girildikten sonra kullanıcıyı Non3D Ödeme akışına yönlendirir. 3D ile ÖDEME adımında girilen Kurallar ve Uyarılar burada da geçerli olmalıdır.

Kurallar & Uyarılar
API entegrasyonunda üye işyeri kendi ödeme formunu kullanır.
Form görsel tasarımını üye iş yeri istediği gibi tasarlayabilir.
Kredi kartı numarası (PAN) luhn algoritmasına tabi tutularak kontrol edilmeli, doğruluğu teyit edildikten sonra API'ye gönderilmelidir.
Kartın hamilinin adı ve soyadı alınmalıdır.
Son kullanma tarihi geçmiş kartlar ile API'ye sorgu yapılmamalıdır.
Ödeme formu American Express (AMEX) kartları ile işlem alabilir şekilde tasarlanmalıdır.
Formunuzdaki input name değerleri aşağıdaki parametreler ile aynı olmalıdır.
Method Name : Payment
Http Method : Post
Content-Type : application/json



Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
cardHolderName	string	100	Evet	Kart hamilinin adı ve soyadı
cardNo	string	16	Evet	Kredi kartı numarası
expireDate	string	4	Evet	Kart son kullanım tarihi (AAYY)
cvv	string	4	Evet	Kart güvenlik kodu
clientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
apiUser	string	100	Evet	Api kullanıcı adı
rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
timeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
orderId	string	20	Opsiyonel	Sipariş Numarasıdır. Belirlenmediği takdirde sistem otomatik üretecektir.
isCommission	long	18	Opsiyonel	isCommission değeri 1 gönderildiği durumlarda zorunludur. totalAmount değeri GetInstallmentOptions’ta dönen taksite karşılık gelen değer gönderilmelidir. isCommission değeri 1 gönderildiğinde bankaya gönderilen tutardır, son iki hane kuruştur. 1528 = 15 TL 28 Kuruş
amount	long	18	Evet	İşlem Tutarı, son iki hane kuruştur. 1522 = 15 TL 22 Kuruş
totalAmount	long	18	Opsiyonel	isCommission değeri 1 gönderildiği durumlarda zorunludur. totalAmount değeri GetInstallmentOptions’ta dönen taksite karşılık gelen değer gönderilmelidir. isCommission değeri 1 gönderildiğinde bankaya gönderilen tutardır, son iki hane kuruştur. 1528 = 15 TL 28 Kuruş
currency	int	3	Evet	İşlem Para birimi 949
installmentCount	int	2	Opsiyonel	Default “0”
description	string	256	Opsiyonel	İşleme ait açıklama
echo	string	256	Opsiyonel	İstek sonucunda geri gönderilecek bilgi alanı
extraParameters	string	4000	Opsiyonel	Ekstra bilgilerin gönderildiği alan. Inquiry servisi cevabında döner


Response
Alan Adı	Açıklama
Code	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	İşleme ait mesaj
OrderId	İşlemin sipariş numarası
BankResponseCode	Banka tarafındaki işlem statü kodu. 00 ise başarılı diğer tüm durumlar başarısız olarak kabul edilir.
BankResponseMessage	Banka tarafındaki işlem statü mesajı
AuthCode	İşlemin bankadaki otorizasyon kodu
HostReferenceNumber	Host Reference Number
TransactionId	İşlemin sistemdeki numarası
CardHolderName	İşlemde kullanılan kartın hamili


Eğer BankResponseCode 00 ise işlem başarılıdır. Bunun dışındaki tüm işlemler başarısız olarak kabul edilir.



Örnekler - Komisyon Yansıtılmasının Kullanılmadığı Durum
Request	Response
{
                            "clientId": 1,
    "apiUser": "testUser",
    "rnd": "***",
    "timeSpan": "20191121160000",
    "hash": "***",
    "orderId": "12345678901234567890",
    "callbackUrl": "***merchantCallbackUrl***",
    "description": "açıklama",
    "echo": "string",
    "extraParameters": "string",
    "amount": 100,
    "currency": 949,
    "installmentCount": 0
                           }
{
                             
    "Code": 0,
    "Message": "Başarılı",
    "ThreeDSessionId": "***",
    "TransactionId": "12345"
}


Örnekler - Komisyon Yansıtıldığı Durum
Request	Response
{
                            "clientId": 1,
    "apiUser": "testUser",
    "rnd": "***",
    "timeSpan": "20191121160000",
    "hash": "***",
    "orderId": "12345678901234567890",
    "callbackUrl": "***merchantCallbackUrl***",
    "description": "açıklama",
    "echo": "string",
    "extraParameters": "string",
    "isCommission ":1,
    "amount": 100,
    "totalAmount "102,
    "currency": 949,
    "installmentCount": 0
}
{
                             
   "Code": 0,
    "Message": "Başarılı",
    "ThreeDSessionId": "***",
    "TransactionId": "12345"
}



CODE	MESSAGE	Validation Errors
0	Başarılı	
100	Ödeme Hatalı	
202	Üye İşyeri Kullanıcısı Bulunamadı	
250	Üye İşyeri Api Kullanıcısı için Yetkisiz İşlem	
997	Hash hatası	
998	Validasyon Hatası	'Time Span' alanı yyyyMMddHHmmss formatında olmalıdır.
'Time Span' alanı 60 dakidan büyük olamaz, mevcut dakika fark: xxx.
999	Genel Hata	İstenmeyen bir durumda alınan hatadır.








Ortak Ödeme Sayfası
Kendi özel ödeme formunu kullanmak istemeyen üye işyerleri için uygundur. 'Ödeme İşlem Başlatma' adımında oluşturulan threeDSessionId değeri ile Ortak Ödeme Sayfası iframe içinde gösterilebilir ya da kullanıcı doğrudan bu linke yönlendirilebilir. Ödeme işlem sonucu CallbackUrl adresine sistem tarafından POST edilir.

Not: Kullanıcının bankanın 3D sayfasına yönlendirme yapılabilmesi için browser desteği gerekmektedir.

Method Name : threeDSecure
Http Method : get
Content-Type : html/text


Request Parametreleri

Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ThreeDSessionId	string	256	Evet	3D işlem için üretilen Unqiue numara


<iframe src="https://[Ortam URL]/api/Payment/threeDSecure/[threeDSessionId]" height="500" width="100%"></iframe>

Ortam URL bilgisinin "Üretim Ortamında" değiştirilmesi gerekmektedir. Link formatı [Ortam URL] /threeDSecure/ [threeDSessionId]'dır.







Taksit ve Komisyon Bilgisi
Bin numarasına göre kartın bağlı olduğu banka bilgileri ile birlikte sunulabilecek taksit ve komisyon oranlarını gönderir.

Method Name :GetCommissionAndInstallmentInfo
Http Method : Post
Content-Type : application/json


Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
Bin	int	6	Evet	Kredi kartının ilk 6 hanesidir.


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
CardPrefix	int	6	Bin numarası
BankId	int	3	Sistemdeki banka ID değeri
BankCode	string	512	Banka kodu
BankName	string	512	Banka adı
CardName	string	512	Kart adı
CardClass	string	512	Kart sınıfı
CardType	string	512	Kart tipi
Country	string	200	Kart ülkesi
BankCommission	int	6	Banka komisyonu
InstallmentInfo	string		Taksit listesi ve komisyon oranı


Örnekler
Request	Response
{
 "bin": 407814,
 "clientId": 1000000000,
 "apiUser": "XXXXXXXX",
 "rnd": "string",
 "timeSpan": "string",
 "hash": "XXXXXXXX"
 }
{
 "CardPrefix": 407814,
 "BankId": 9,
 "BankCode": "ZiraatBankası",
 "BankName": "Ziraat Bankası",
 "CardName": "Bankkart",
 "CardClass": "Banka Kartı",
 "CardType": "Visa",
 "Country": "TR",
 "BankCommission": 0,
 "InstallmentInfo": {
 "T2": {
 "Rate": 2.99,
 "Constant": 2
 },
 "T3": {
 "Rate": 3.99,
 "Constant": 2
 },
 "T4": {
 "Rate": 4.99,
 "Constant": 2
 },
 "T5": {
 "Rate": 5.99,
 "Constant": 2
 },
 "T6": {
 "Rate": 6.99,
 "Constant": 0
 },
 "T7": {
 "Rate": 7.99,
 "Constant": 0
 },
 "T8": {
 "Rate": 8.99,
 "Constant": 0
 },
 "T9": {
 "Rate": 9.99,
 "Constant": 0
 },
 "T10": {
 "Rate": 10.99,
 "Constant": 0
 },
 "T11": {
 "Rate": 11.99,
 "Constant": 0
 },
 "T12": {
 "Rate": 12.99,
 "Constant": 0
 }
 },
 "Code": 0,
 "Message": ""
 }




Taksit ve Taksitlere Karşılık Gelen Tutar Bilgisi
isCommission 1 gönderildiği durumda amount değerine karşılık gelen taksitlerdeki totalAmount değerinde gönderilecek değerler döner.

Method Name :GetInstallmentOptions
Http Method : Post
Content-Type : application/json


Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
amount	long	18	Evet	İşlem tutarı


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
InstallmentOptions	int	6	Taksit listesi ve tutar

Örnekler
Request	Response
{
  "amount": 10000,
  "clientId": 1000000000,
  "apiUser": "XXXXXXXX",
  "rnd": "string",
  "timeSpan": "string",
  "hash": "XXXXXXXX"
}
{
  "installmentOptions": [
    {
      "installment": 1,
      "title": "Tek Çekim",
      "amount": 10000,
      "currency": 949
    },
    {
      "installment": 2,
      "title": "2 Taksit",
      "amount": 10089,
      "currency": 0
    }
    {
      "installment": 3,
      "title": "3 Taksit",
      "amount": 10115,
      "currency": 949
    }
   ],
  "Code": 0,
  "Message":""
 }




Ödeme Sorgulama
Tosla İşim de kaydı oluşturulan bir işlemin detayları sorgulanabilir. İşlem sorgulama servisidir. OrderID ile işlem sorgulama yapılabilmektedir.

Method Name : inquiry
Http Method : Post
Content-Type : application/json



Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
OrderId	string	20	Evet	Ön Otorizasyon (PreAuth) işleminde gönderilen Sipariş numarasıdır.
TransactionId	string	20	Opsiyonel	Sorgulanacak işlemin Id’si


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
TransactionType	int	65	3D işlem için Üretilen Unqiue numara
CreateDate	string	23	Oluşturma tarihi
OrderId	String	20	Sipariş numarasıdır
BankResponseCode	String	7	Bankadan alınan cevap kodu
BankResponseMessage	String	512	Bankadan alınan cevap mesajı
AuthCode	String	200	Otorizasyonda kullanılan kodu
HostReferenceNumber	string	200	
Amount	long	18	İşlem tutarı
Currency	int	3	İşlem Para birimi 949
InstallmentCount	int	2	Taksit sayısı
ClientId	long	20	Mağaza İçin belirlenmiş benzersiz numara
CardNo	string	19	Kart Numarası
RequestStatus	int	2	İşlem durumunu gösterir
RefundedAmount	Long	18	İade edilen miktar
PostAuthedAmount	Long	18	Ön Otorizasyon işlemindeki tutar
TransactionId	Long	20	İşlem İD’si
CommissionStatus	Int	1	Komisyon durumu
NetAmount	Long	18	Net tutar
MerchantCommissionAmount	Long	18	Üye iş yerinin komisyon miktarı
MerchantCommissionRate	decimal	18	Üye iş yerinin komisyon oranı
CardBankId	Long	20	Kullanılan banka id’si
CardTypeId	long	20	Kullanılan kart tipi id’si
ValorDate	int	8	Valör günü
TransactionDate	int	8	İşlem yapılan tarihtir. yyyyMMdd
BankValorDate	int	8	 
ExtraParameters	string	4000	 


Örnekler
Request	Response
{
 "clientId": 1,
 "apiUser": "***",
 "rnd": "***",
 "timeSpan": "20221011235931",
 "hash": "***",
 "orderId": "20221011999",
 "transactionId": ""
 } 
{
 "Count": 36,
 "Transactions": [
 {
 "TransactionType": 1,
 "CreateDate": "20221011152449",
 "OrderId": "20221011999",
 "BankResponseCode": "00",
 "BankResponseMessage": "Onaylandı",
 "AuthCode": "S90037",
 "HostReferenceNumber": "228415127919",
 "Amount": 1090,
 "Currency": 949,
 "InstallmentCount": 0,
 "ClientId": 1000000494,
 "CardNo": "41595600****7732",
 "RequestStatus": 1,
 "RefundedAmount": 0,
 "PostAuthedAmount": 0,
 "TransactionId": 2000000000029968,
 "CommissionStatus": 1,
 "NetAmount": 1090,
 "MerchantCommissionAmount": 0,
 "MerchantCommissionRate": 0,
 "CardBankId": 13,
 "CardTypeId": 2,
 "ValorDate": 0,
 "TransactionDate": 20221011,
 "BankValorDate": 0,
 "ExtraParameters": null,
 "Code": 0,
 "Message": "Başarılı"
 }
 ],
 "Code": 0,
 "Message": "Başarılı"
 }


CODE	MESSAGE	Validation Errors	AÇIKLAMA
0			
202	Üye İşyeri Kullanıcısı Bulunamadı		
250	Üye İşyeri Api Kullanıcısı için Yetkisiz İşlem		
997	Hash Hatası		
998	Validasyon Hatası	Time Span' alanı yyyyMMddHHmmss formatında olmalıdır.	
Time Span' alanı 60 dakidan büyük olamaz, mevcut dakika fark: 1050626	
Unexpected character encountered while parsing value: ,. Path 'orderId', line 2, position 14.	Zorunlu alan boş gönderildiğinde alınan hata
An undefined token is not a valid System.Int64. Path 'clientId', line 3, position 14.	Zorunlu alan boş gönderildiğinde alınan hata
999	Genel Hata		




İşlem Listeleme
Tarihe göre işlemleri listeler. Tarih gönderilerek iletilen tarihteki tüm işlemlerin listelenmesini sağlar.

Method Name : history
Http Method : Post
Content-Type : application/json


Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
OrderId	string	20	Opsiyonel	Sipariş numarası
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
TransactionDate	int	8	Evet	İşlem yapılan tarihtir. yyyyMMdd
Page	int	int max	Evet	Paging yapısı gereği kullanılan sayfa numarası ilk sayfa için 1, ikinci sayfa için 2...
PageSize	int	3	Evet	Paging yapısı gereği kullanılan sayfa başı kayıt sayısı.


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
Count	int	int max	Tarih filtresine uygun toplam kayıt sayısını döner.
Transactions	list		İşleme ait ödeme listesini döner.
#	Değer	Anlam
TransactionType	1	Satış
TransactionType	4	İptal
TransactionType	5	İade
TransactionType	6	Harcama İtirazı
#	Değer	Anlam
RequestStatus	0	Hatalı
RequestStatus	1	Başarılı
RequestStatus	2	İptal Edildi
RequestStatus	3	Parçalı İade Edildi
RequestStatus	4	İptal Edildi //Tamamı İade Edildi
RequestStatus	5	Ön Otorizasyon Kapandı
RequestStatus	6	Parçalı Harcama İtirazı
RequestStatus	7	Tam Harcama İtirazı
RequestStatus	10	3D Bekleniyor
RequestStatus	11	3D ye Gönderildi
RequestStatus	12	3D den Cevap Geldi
RequestStatus	14	İade Bekleniyor
RequestStatus	15	İptal Edildi
RequestStatus	16	İleri Vadeli Alacaktan İade


Örnekler
Request	Response
{
 "OrderId": " A1B2C3D4",
 "ClientId": 1,
 "ApiUser": "apiuser",
 "Rnd": "string",
 "TimeSpan": "20220119111007",
 "Hash": " A1B2C3D4",
 "Page": 1,
 "PageSize": 10,
 "TransactionDate": 20220412
 }
 
{
 "Code": 101,
 "Message": "string",
 "Count": 123,
 "Transactions": [
 {
 "TransactionType": 1,
 "CreateDate": "20220119111007",
 "OrderId": " A1B2C3D4",
 "BankResponseCode": "MD99",
 "BankResponseMessage": "mesaj",
 "AuthCode": null,
 "HostReferenceNumber": null,
 "Amount": 100,
 "Currency": 949,
 "InstallmentCount": 1,
 "ClientId": 1,
 "CardNo": "123456******1234",
 "RequestStatus": 0,
 "RefundedAmount": 0,
 "PostAuthedAmount": 0,
 "TransactionId": 0,
 "CommissionStatus": null,
 "NetAmount": 0,
 "MerchantCommissionAmount": 0,
 "MerchantCommissionRate": null,
 "CardBankId": 0,
 "CardTypeId": 0,
 "Code": 0,
 "Message": ""
 }
 ]
 }


CODE	MESSAGE	Validation Errors	AÇIKLAMA
0	Başarılı		
250	Üye İşyeri Api Kullanıcısı için Yetkisiz İşlem		
998	Validasyon Hatası	Time Span' alanı yyyyMMddHHmmss formatında olmalıdır.	
Time Span' alanı 60 dakidan büyük olamaz, mevcut dakika fark: 1050626	
Unexpected character encountered while parsing value: ,. Path 'orderId', line 2, position 14.	Zorunlu alan boş gönderildiğinde alınan hata
An undefined token is not a valid System.Int64. Path 'clientId', line 3, position 14.	Zorunlu alan boş gönderildiğinde alınan hata
999	Genel Hata		




İptal
Bankalar gün sonu almadan önce işlemin iptal edilmesidir. OrderId ile ödeme işleminin iptal edilmesini sağlar.

Method Name : void
Http Method : Post
Content-Type : application/json



Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long	19	Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
OrderId	string	20	Evet	Ön Otorizasyon (PreAuth) işleminde gönderilen Sipariş numarasıdır.
Echo	string	256	Opsiyonel	İstek sonucunda geri gönderilecek bilgi alanı


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
OrderId	string	20	Sipariş Numarasıdır.
BankResponseCode	string	7	Bankadan alınan cevap kodu
BankResponseMessage	string	512	Bankadan alınan cevap mesajı
AuthCode	string	200	Otorizasyonda kullanılan kodu
HostReferenceNumber	string	200	Ödeme kanalın referans numarası
TransactionId	long	20	İşlem İD’si


Örnekler
Request	Response
{
 "OrderId": " A1B2C3D4",
 "ClientId": 1,
 "ApiUser": "****",
 "Rnd": "****",
 "TimeSpan": "20220119111007",
 "Hash": "****",
 "echo": "string"
 }
 
{
 "OrderId": " A1B2C3D4",
 "BankResponseCode": null,
 "BankResponseMessage": null,
 "AuthCode": null,
 "HostReferenceNumber": null,
 "TransactionId": null,
 "Code": 101,
 "Message": "string"
 }


CODE	MESSAGE	Validation Errors	AÇIKLAMA
0	Başarılı		
1	Iptal etmek istediğiniz tutar, hakkediş tutarınızdan fazladır. 10,00 tutarından daha fazla iptal/iade yapamazsınız.		
1	Başarılı bir postauth olduğu için ön otorizasyon işlemini iptal edemezsiniz.		
101	Orjinal Kayıt Bulunamadı		
202	Üye İşyeri Kullanıcısı Bulunamadı		
250	Üye İşyeri Api Kullanıcısı için Yetkisiz İşlem		
997	Hash Hatası		
998	Validasyon Hatası	Time Span' alanı yyyyMMddHHmmss formatında olmalıdır.	
'Time Span' alanı 60 dakidan büyük olamaz, mevcut dakika fark: 240.	
Unexpected character encountered while parsing value: ,. Path 'orderId', line 2, position 14.	Zorunlu alan boş gönderildiğinde alınan hata
An undefined token is not a valid System.Int64. Path 'clientId', line 3, position 14.	Zorunlu alan boş gönderildiğinde alınan hata
999	Genel Hata		




İade
Bankaların gün sonu aldıktan sonra işlemin kısmi veya tam olarak iade edilmesidir. Ödeme işlemini iade eder. Tam iade ve kısmi iade yapılabilmesini sağlar.

Method Name : refund
Http Method : Post
Content-Type : application/json


Request Parametreleri
Alan Adı	Tipi	Max Uzunluk	Zorunlu	Açıklama
ClientId	long19		Evet	Mağaza İçin belirlenmiş benzersiz numara
ApiUser	string	100	Evet	Api kullanıcı adı
Rnd	string	24	Evet	İşlem için üretilmiş random değer. hash içerisinde kullanılan değer ile aynı olmalıdır.
TimeSpan	string	14	Evet	İşlem tarihi (yyyyMMddHHmmss). hash içerisinde kullanılan değer ile aynı olmalıdır. İşlem anında verilen tarih ve saat bilgisi olmalıdır. GTM+3 zaman diliminde ve max 1 saat farka izin verilmektedir. Diğer durumlarda hash hatası alınır.
Hash	string	512	Evet	Güvenlik kontrolünde kullanılacak hash stringi, bir hash bir kez kullanılacaktır.
OrderId	string	20	Evet	Ön Otorizasyon (PreAuth) işleminde gönderilen Sipariş numarasıdır.
Amount	long	18	Evet	İade İşlem Tutarı, son iki hane kuruştur. 1522 = 15 TL 22 Kuruş
Echo	string	256	Opsiyonel	İstek sonucunda geri gönderilecek bilgi alanı


Response Parametreleri
Alan Adı	Tipi	Max Uzunluk	Açıklama
Code	int	3	0 ise İşlem başarılıdır. Diğer numaralar hatalıdır.
Message	string	256	İşleme ait mesaj
OrderId	string	20	Sipariş Numarasıdır.
BankResponseCode	string	7	Bankadan alınan cevap kodu
BankResponseMessage	string	512	Bankadan alınan cevap mesajı
AuthCode	string	200	Otorizasyonda kullanılan kodu
HostReferenceNumber	string	200	Ödeme kanalın referans numarası
TransactionId	long	20	İşlem İD’si


Örnekler
Request	Response
{
 "OrderId": " A1B2C3D4",
 "Amount": "125",
 "ClientId": 1,
 "ApiUser": "***",
 "Rnd": "***",
 "TimeSpan": "20220119111007",
 "Hash": "***",
 "echo": "string"
 }
{
 "OrderId": " A1B2C3D4",
 "BankResponseCode": null,
 "BankResponseMessage": null,
 "AuthCode": null,
 "HostReferenceNumber": null,
 "TransactionId": null,
 "Code": 101,
 "Message": "string"
 }


CODE	MESSAGE	Validation Errors	AÇIKLAMA
0	Başarılı		
1	İade etmek istediğiniz tutar, hakkediş tutarınızdan fazladır. 10,00 tutarından daha fazla iade yapamazsınız.		
100	Ödeme Hatalı		
101	Orjinal Kayıt Bulunamadı		
103	İade Tutarı Satış Tutarından Büyük Olamaz		
104	RefundedAmountError		iade edilen tutar ve işlemde daha önce iade edilmiş olan tutarların toplamından işlemde yapılan ödeme miktarından büyük ise
200	Üye İşyeri Bulunamadı		
250	Üye İşyeri Api Kullanıcısı için Yetkisiz İşlem		
400	Cvv Format Hatası.		
401	Expire Date Format Hatası.		
402	Card No Format Hatası.		
997	Hash Hatası		
998	Validasyon Hatası	Time Span' alanı yyyyMMddHHmmss formatında olmalıdır.	
'Time Span' alanı 60 dakidan büyük olamaz, mevcut dakika fark: 240.	
Unexpected character encountered while parsing value: ,. Path 'orderId', line 2, position 14. Zorunlu alan boş gönderildiğinde alınan hata	Zorunlu alan boş gönderildiğinde alınan hata
An undefined token is not a valid System.Int64. Path 'clientId', line 3, position 14.	Zorunlu alan boş gönderildiğinde alınan hata
999	Genel Hata		


Test Kart Bilgileri
Kart No	Son Kullanım Tarihi	CVV	3D Secure Şifre
4546711234567894	12/26	000	-
4531444531442283	12/26	001	-
5406675406675403	12/26	000	-






Eklentiler
WOOCOMMERCE Tosla İşim Kurulum Adımları
Adım 1. https://tosla.com/isim-icin/gelistirici-merkezi sayfasının en alt kısmında yer alan Woocommerce v8.2 altı veya v8.2 üstü versiyondan uygun olan Sanal POS WooCommerce modülüne tıklayarak modülü bilgisayarına indirmelisin.

Adım 2. Wordpress yönetim panelinize giriş yaptıktan sonra "Eklentiler" bölümünden "Yeni Ekle" butonuna tıkladıktan sonra indirdiğin modülü seçmelisin.

Adım 3. Karşına çıkan Tosla İşim eklentisinde "Yükle" butonuna basmalısın. Böylelikle Tosla İşim eklentisi sistemine yüklenmiş olacaktır.

Adım 4. Wordpress yönetim panelinizde Eklentiler > Yüklü Eklentiler menüsüne ulaştıktan sonra Tosla İşim WooCommerce eklentisini bulup aktif hale getirmelisin.

Adım 5. Woocommerce >Ayarlar >Ödeme menüsüne gidin ve "Tosla İşim" linkine tıkla. "Sanal POS 3D Ödeme Modülü Aktif" kutucuğunu işaretle.




Adım 6. Bu aşamadan sonra senden Tosla İşim Api ve güvenlik anahtarlarınızı girmen istenecektir. Tosla İşim Üye İşyeri Panelinize giriş yapın. Sol menü içerisinde İşyeri Bilgileri başlığına, ardından API Bilgileri başlığına tıklamalısın. Sanal POS API Bilgileri kutucuğunu tikleyerek SMS ile gönder’e tıkladığında Tosla İşim’e üye olurken bizlere belirttiğin iletişim cep numarasına API KEY değerlerin iletilecektir. Bilgilerin SMS ile iletilmesinde sorun yaşıyorsan desteğe ihtiyaç duyduğun anda posdestek@tosla.com adresi üzerinden bize ulaşabileceğini unutma!

Adım 7. Bu değerleri WooCommerce'teki yönetim sayfanızda ilgili alanlara kopyalayın. Sıralama için ise ödeme seçenekleri arasında hangi sırada görülmesini istiyorsan o sıra numarasını girmelisin.

Adım 8. Kaydet butonuna bastıktan sonra Tosla İşim Sanal POS’u kullanmaya başlayabilirsin.

PrestaShop Tosla İşim Kurulum Adımları
Adım 1. https://tosla.com/isim-icin/gelistirici-merkezi sayfasının en alt kısmında yer alan Presta v1.6x - v1.7x veya v8.x arasından sana uygun olan Sanal POS Presta modülüne tıklayarak modülü indirmelisin. İndirdiğin zip dosyasını açmalı/çıkarmalısın.

Adım 2. PrestaShop admin panelinize giriş yap.

Adım 3. Sol menüden Modüller -> Modül Manager seç.

Adım 4. “Bir modül yükle” butonuna bas. Bastıktan sonra açılan pop-up üzerine indirdiğin dosyayı sürükle veya bir dosya seçin alanına tıklayarak indirdiğin modül dosyasını seç.

Adım 5. Yükleme işlemi tamamlandıktan sonra “Yapılandır” butonuna basarak modül ayar sayfasına gitmelisin.

Adım 6. Tosla İşim Üye İşyeri Paneline giriş yap. Sol menü içerisinde İşyeri Bilgileri başlığına, ardından API Bilgileri başlığına tıklamalısın. Sanal POS API Bilgileri kutucuğunu tikleyerek SMS ile gönder’e tıkladığında Tosla İşim’e üye olurken bizlere belirttiğin iletişim cep numarasına API KEY değerlerin iletilecektir. Bilgilerin SMS ile iletilmesinde sorun yaşıyorsan desteğe ihtiyaç duyduğun anda posdestek@tosla.com adresi üzerinden bize ulaşabileceğini unutma!

