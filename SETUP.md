# 🚀 TradeScout API - Setup Talimatları

## 📋 Gereksinimler

- .NET 9.0 SDK
- PostgreSQL 14+
- Git

## 🛠️ Kurulum Adımları

### 1. Repository'yi Klonlayın

```bash
git clone <YOUR_REPO_URL>
cd TradeScout.API
```

### 2. Bağımlılıkları Yükleyin

```bash
dotnet restore
```

### 3. Appsettings Dosyasını Oluşturun

`appsettings.Development.json.example` dosyasını kopyalayıp `appsettings.Development.json` oluşturun:

```bash
cp appsettings.Development.json.example appsettings.Development.json
```

### 4. Konfigürasyonu Düzenleyin

`appsettings.Development.json` dosyasını açın ve şunları güncelleyin:

- **PostgreSQL Şifresi**: `Password=` kısmına kendi şifrenizi yazın
- **JWT Secret Key**: En az 32 karakter uzunluğunda güçlü bir key girin

### 5. Veritabanını Oluşturun

PostgreSQL'de veritabanını oluşturun:

```sql
CREATE TABLE "Users" (
    "Id" SERIAL PRIMARY KEY,
    "FullName" VARCHAR(100) NOT NULL,
    "Email" VARCHAR(150) UNIQUE NOT NULL,
    "PasswordHash" TEXT NOT NULL,
    "CompanyName" VARCHAR(150),
    "Credits" INTEGER DEFAULT 5,
    "PackageType" VARCHAR(50) DEFAULT 'Free',
    "Role" VARCHAR(20) DEFAULT 'User',
    "IsActive" BOOLEAN DEFAULT TRUE,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "LastLogin" TIMESTAMP
);
```

### 6. Uygulamayı Çalıştırın

```bash
dotnet run
```

API şu adreste çalışacak: **http://localhost:5000**

## 📡 API Endpoints

### Register
```bash
POST /api/auth/register
Content-Type: application/json

{
  "fullName": "John Doe",
  "email": "john@example.com",
  "password": "Password123!",
  "companyName": "ACME Corp"
}
```

### Login
```bash
POST /api/auth/login
Content-Type: application/json

{
  "email": "john@example.com",
  "password": "Password123!"
}
```

## 🔐 Güvenlik Notları

⚠️ **Hassas bilgileri asla commit etmeyin!**

- `appsettings.Development.json` - Git'e eklenmez (.gitignore'da)
- `appsettings.Production.json` - Git'e eklenmez (.gitignore'da)
- JWT Secret Key'leri environment variable olarak saklanmalı

## 📄 Lisans

Bu proje TradeScout tarafından geliştirilmiştir.
