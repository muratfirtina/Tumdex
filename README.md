# TUMDEX - E-Ticaret Platform Projesi

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet)
![EntityFramework](https://img.shields.io/badge/-Entity_Framework-8C3D65?logo=dotnet&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-316192?style=flat&logo=postgresql&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-DC382D?style=flat&logo=redis&logoColor=white)
![RabbitMQ](https://img.shields.io/badge/RabbitMQ-FF6600?style=flat&logo=rabbitmq&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=flat&logo=docker&logoColor=white)
![Azure](https://img.shields.io/badge/Microsoft-Azure-blue?logo=microsoftazure&logoColor=white&style=white)
![SignalR](https://img.shields.io/badge/SignalR-512BD4?style=flat&logo=signalr&logoColor=white)
![Grafana](https://img.shields.io/badge/Grafana-F2F4F9?style=for-the-badge&logo=grafana&logoColor=orange&labelColor=F2F4F9)

## 📋 İçindekiler

- [Proje Hakkında](#proje-hakkında)
- [Mimari Yapı](#mimari-yapı)
- [Kullanılan Teknolojiler](#kullanılan-teknolojiler)
- [Proje Yapısı](#proje-yapısı)
- [Özellikler](#özellikler)
- [Kurulum](#kurulum)
- [Yapılandırma](#yapılandırma)
- [Kullanım](#kullanım)
- [API Dokümantasyonu](#api-dokümantasyonu)
- [Monitoring ve Logging](#monitoring-ve-logging)
- [Güvenlik](#güvenlik)
- [Katkıda Bulunma](#katkıda-bulunma)

## 🎯 Proje Hakkında

**TUMDEX**, modern mikroservis mimarisi ve best practice'ler kullanılarak geliştirilmiş, kurumsal seviyede bir e-ticaret platformudur. Proje, Clean Architecture prensiplerine uygun olarak tasarlanmış ve ölçeklenebilir bir yapıya sahiptir.

### Temel Özellikler

- ✅ **Clean Architecture** ve **CQRS Pattern** ile geliştirilmiş modüler yapı
- ✅ **Event-Driven Architecture** ile asenkron işlem yönetimi
- ✅ **Real-time** bildirimler ve anlık veri güncelleme (SignalR)
- ✅ **Mikroservis** altyapısına uygun tasarım
- ✅ **Multi-cloud** depolama desteği (AWS S3, Google Cloud, Cloudinary)
- ✅ **Comprehensive monitoring** (Prometheus & Grafana)
- ✅ **Advanced logging** (Serilog & Seq)
- ✅ **High-performance caching** (Redis)
- ✅ **Message queue** sistemi (RabbitMQ & MassTransit)
- ✅ **JWT Authentication & Authorization**
- ✅ **GDPR uyumlu** veri yönetimi
- ✅ **Rate Limiting & DDoS koruması**
- ✅ **Health check** mekanizması
- ✅ **SEO optimizasyonu** (Sitemap, Robots.txt)
- ✅ **Google Analytics** entegrasyonu

## 🏗️ Mimari Yapı

Proje, **Clean Architecture (Onion Architecture)** prensiplerine uygun olarak 5 katmandan oluşmaktadır:

```
┌─────────────────────────────────────────┐
│           WebAPI (Presentation)         │
├─────────────────────────────────────────┤
│              SignalR Layer              │
├─────────────────────────────────────────┤
│         Infrastructure Layer            │
├─────────────────────────────────────────┤
│          Persistence Layer              │
├─────────────────────────────────────────┤
│          Application Layer              │
│      (CQRS, Business Logic)             │
├─────────────────────────────────────────┤
│            Domain Layer                 │
│         (Entities, Rules)               │
└─────────────────────────────────────────┘
            ↑
    ┌───────┴───────┐
    │ Core Packages │
    └───────────────┘
```

### Katman Sorumlulukları

#### 1. **Domain Layer** (Merkez Katman)
- Entity'ler ve Domain Model'ler
- Business kuralları ve validasyonlar
- Enum'lar ve domain-specific tipler
- Identity modelleri
- **Bağımlılık**: Hiçbir katmana bağımlı değil

#### 2. **Application Layer**
- Use case'ler (CQRS: Commands & Queries)
- Business logic ve orchestration
- DTO'lar ve mapping profilleri
- Repository interface'leri
- Service interface'leri
- Custom attributes ve exceptions
- **Bağımlılık**: Domain, Core Packages

#### 3. **Persistence Layer**
- Entity Framework Core DbContext
- Repository implementasyonları
- Database migrations
- Entity configurations
- Identity implementasyonu
- **Bağımlılık**: Application, Domain

#### 4. **Infrastructure Layer**
- External service implementasyonları
- Message broker (RabbitMQ) consumers
- Background jobs (Quartz.NET)
- File storage services (AWS, GCP, Cloudinary)
- Email services
- Caching (Redis)
- Monitoring (Prometheus)
- Middleware'ler
- **Bağımlılık**: Application, Persistence

#### 5. **SignalR Layer**
- Real-time hub'lar
- SignalR service implementasyonları
- Client-server iletişimi
- **Bağımlılık**: Application

#### 6. **WebAPI Layer** (Presentation)
- REST API Controllers
- Authentication & Authorization
- Swagger/OpenAPI
- Health checks
- CORS configuration
- **Bağımlılık**: Application, Infrastructure, SignalR

### Core Packages

Projenin yeniden kullanılabilir bileşenlerini içeren core paketler:

- **Core.Application**: MediatR pipelines, base requests/responses
- **Core.Persistence**: Generic repository pattern, dynamic LINQ, pagination
- **Core.CrossCuttingConcerns**: Exception handling, logging aspects

## 🛠️ Kullanılan Teknolojiler

### Backend Framework
- **.NET 8.0** - Modern, performanslı ve cross-platform
- **ASP.NET Core Web API** - RESTful API geliştirme

### Veritabanı & ORM
- **PostgreSQL** - Relational database
- **Entity Framework Core 8.0** - ORM
- **Entity Framework Core Dynamic LINQ** - Dynamic query building

### Caching & Performance
- **Redis** - Distributed caching
- **Microsoft.Extensions.Caching.StackExchangeRedis** - Redis client

### Message Broker
- **RabbitMQ** - Message queue
- **MassTransit** - Distributed application framework

### Real-time Communication
- **SignalR** - Real-time web functionality

### Authentication & Security
- **JWT Bearer** - Token-based authentication
- **Microsoft.AspNetCore.Identity** - User management
- **Azure Key Vault** - Secret management
- **HtmlSanitizer** - XSS protection

### Design Patterns & Architecture
- **MediatR** - CQRS implementation
- **AutoMapper** - Object-to-object mapping
- **FluentValidation** - Fluent interface for building validation rules

### File Storage
- **AWS S3** - Amazon cloud storage
- **Google Cloud Storage** - Google cloud storage
- **Cloudinary** - Image and video management

### Image Processing
- **SkiaSharp** - Cross-platform image processing

### Logging & Monitoring
- **Serilog** - Structured logging
- **Seq** - Centralized log aggregation
- **Prometheus** - Metrics collection
- **Grafana** - Metrics visualization

### Email
- **MailKit** - Email sending
- **MimeKit** - MIME message composition

### Background Jobs
- **Quartz.NET** - Job scheduling

### Health Checks
- **AspNetCore.HealthChecks.NpgSql** - PostgreSQL health check
- **AspNetCore.HealthChecks.Redis** - Redis health check
- **AspNetCore.HealthChecks.Rabbitmq** - RabbitMQ health check

### Analytics
- **Google Analytics API** - Web analytics

### Development Tools
- **Swagger/OpenAPI** - API documentation
- **dotenv.net** - Environment variable management

### DevOps
- **Docker & Docker Compose** - Containerization

### Additional Libraries
- **Newtonsoft.Json** - JSON serialization
- **UAParser** - User agent parsing
- **Sprache** - Parser combinator library

## 📁 Proje Yapısı

```
Tumdex/
│
├── src/
│   ├── corePackages/                    # Yeniden kullanılabilir core bileşenler
│   │   ├── Core.Application/            # MediatR pipelines, base classes
│   │   ├── Core.Persistence/            # Generic repository, dynamic LINQ
│   │   └── Core.CrossCuttingConcerns/   # Exception handling, logging
│   │
│   ├── tumdex/                          # Ana uygulama
│   │   ├── Domain/                      # Domain katmanı
│   │   │   ├── Entities/                # Domain entity'ler
│   │   │   ├── Enum/                    # Domain enum'lar
│   │   │   ├── Identity/                # Identity modelleri
│   │   │   └── Model/                   # Domain modeller
│   │   │
│   │   ├── Application/                 # Application katmanı
│   │   │   ├── Features/                # CQRS features
│   │   │   │   ├── Products/
│   │   │   │   │   ├── Commands/        # Create, Update, Delete
│   │   │   │   │   ├── Queries/         # GetList, GetById, Search
│   │   │   │   │   ├── Rules/           # Business rules
│   │   │   │   │   ├── Dtos/            # Data transfer objects
│   │   │   │   │   └── Profiles/        # AutoMapper profiles
│   │   │   │   ├── Orders/
│   │   │   │   ├── Carts/
│   │   │   │   ├── Categories/
│   │   │   │   ├── Brands/
│   │   │   │   ├── Users/
│   │   │   │   └── ... (25+ features)
│   │   │   ├── Repositories/            # Repository interfaces
│   │   │   ├── Services/                # Service interfaces
│   │   │   ├── Storage/                 # Storage interfaces
│   │   │   ├── Exceptions/              # Custom exceptions
│   │   │   └── Extensions/              # Extension methods
│   │   │
│   │   ├── Persistence/                 # Persistence katmanı
│   │   │   ├── Context/                 # DbContext
│   │   │   ├── Repositories/            # Repository implementations
│   │   │   ├── Services/                # Service implementations
│   │   │   ├── Migrations/              # EF Core migrations
│   │   │   └── DbConfiguration/         # Database configurations
│   │   │
│   │   ├── Infrastructure/              # Infrastructure katmanı
│   │   │   ├── Services/                # External service implementations
│   │   │   │   ├── Storage/             # AWS, GCP, Cloudinary
│   │   │   │   ├── Email/               # Email services
│   │   │   │   ├── Token/               # JWT services
│   │   │   │   ├── Security/            # Security services
│   │   │   │   └── Monitoring/          # Prometheus metrics
│   │   │   ├── Consumers/               # RabbitMQ consumers
│   │   │   ├── BackgroundJobs/          # Quartz jobs
│   │   │   ├── Middleware/              # Custom middlewares
│   │   │   └── Adapters/                # Third-party adapters
│   │   │
│   │   ├── SignalR/                     # SignalR katmanı
│   │   │   ├── Hubs/                    # SignalR hubs
│   │   │   └── HubService/              # Hub services
│   │   │
│   │   └── WebAPI/                      # Presentation katmanı
│   │       ├── Controllers/             # API controllers
│   │       ├── Extensions/              # Extension methods
│   │       ├── Attributes/              # Custom attributes
│   │       ├── wwwroot/                 # Static files
│   │       ├── Program.cs               # Application entry point
│   │       └── appsettings.json         # Configuration
│   │
│   ├── monitoring/                      # Monitoring configurations
│   │   ├── prometheus/                  # Prometheus config
│   │   │   ├── prometheus.yml
│   │   │   └── alert.rules
│   │   └── grafana/                     # Grafana dashboards
│   │       ├── dashboards/
│   │       └── datasources/
│   │
│   ├── docker-compose.yml               # Docker Compose configuration
│   └── .env                             # Environment variables
│
└── Tumdex.sln                           # Solution file
```

## ✨ Özellikler

### E-Ticaret Özellikleri

#### Ürün Yönetimi
- Ürün CRUD operasyonları
- Çoklu görsel yükleme ve yönetimi
- Ürün varyantları (renk, beden, vb.)
- Stok yönetimi (sınırsız stok desteği)
- Dinamik özellik değerleri
- Ürün beğenme ve görüntüleme istatistikleri
- Ürün filtreleme ve arama
- En çok satan, en çok beğenilen, en çok görüntülenen ürünler
- SEO dostu URL yapısı

#### Kategori & Marka Yönetimi
- Hiyerarşik kategori yapısı
- Kategori özellik tanımlama
- Marka yönetimi
- Görsel yönetimi

#### Sepet & Sipariş Yönetimi
- Sepete ürün ekleme/çıkarma
- Misafir kullanıcı için sepet
- Stok rezervasyon sistemi
- Sipariş oluşturma
- Sipariş durumu takibi
- Sipariş bildirimleri (email)
- Sipariş geçmişi

#### Kullanıcı Yönetimi
- JWT tabanlı authentication
- Role-based authorization (RBAC)
- Kullanıcı profili yönetimi
- Adres yönetimi
- Kullanıcı beğenileri
- Email doğrulama
- Şifre sıfırlama
- Çoklu oturum yönetimi
- IP ve User-Agent kontrolü

#### İletişim & Haber Bülteni
- İletişim formu
- Haber bülteni aboneliği
- Otomatik email gönderimi
- Email throttling
- Newsletter programlama (aylık otomatik gönderim)

#### GDPR & Veri Gizliliği
- Kullanıcı onay yönetimi
- Veri sahibi talepleri (Data Subject Requests)
- Veri silme/indirme istekleri
- Privacy policy yönetimi
- Cookie onayı

#### SEO & Analytics
- Dinamik sitemap oluşturma
- Robots.txt yönetimi
- Google Analytics entegrasyonu
- Visitor tracking
- Meta tag yönetimi

#### Carousel & Banner Yönetimi
- Ana sayfa carousel'ları
- Video/görsel carousel desteği
- Dinamik içerik yönetimi

### Teknik Özellikler

#### Performans & Caching
- **Redis distributed caching**
- Response caching
- Memory caching
- Cache invalidation strategies
- Sliding expiration

#### Message Queue & Event-Driven
- **RabbitMQ** ile asenkron işlem yönetimi
- Order created/updated events
- Cart updated events
- Stock updated events
- Email notification events
- Event-driven architecture
- Retry mechanism
- Dead letter queue

#### Real-time Features
- **SignalR** ile real-time bildirimler
- Anlık ziyaretçi istatistikleri
- Sipariş durumu güncelleme bildirimleri
- Stok güncellemeleri
- Admin dashboard real-time metrikleri

#### Background Jobs
- **Quartz.NET** ile zamanlanmış görevler
- Stok rezervasyon temizleme
- Outbox message processing
- Newsletter gönderimi
- Log cleanup
- Analytics data collection

#### File Storage
- **Multi-provider** depolama sistemi
  - Local storage
  - AWS S3
  - Google Cloud Storage
  - Cloudinary
- Otomatik image optimization
- Thumbnail generation (SkiaSharp)
- Çoklu dosya yükleme
- Görsel versiyonlama

#### Logging & Monitoring

**Structured Logging (Serilog)**
- Console logging
- Seq centralized logging
- PostgreSQL logging
- Log enrichment (machine name, thread ID, etc.)
- Correlation ID tracking
- Request/response logging

**Metrics (Prometheus)**
- HTTP request metrics
- Custom business metrics
- System metrics
- Database metrics
- Cache metrics
- Message queue metrics

**Visualization (Grafana)**
- Pre-configured dashboards
- Alert rules
- Custom queries
- Real-time monitoring

**Health Checks**
- Database health
- Redis health
- RabbitMQ health
- Custom health checks
- Health UI endpoint

#### Security

**Authentication & Authorization**
- JWT Bearer tokens
- Refresh token rotation
- Token family tracking
- IP address validation
- User-Agent validation
- Multi-device session management
- Email confirmation requirement

**Security Features**
- Rate limiting (IP-based)
- DDoS protection
- CORS policy
- XSS protection (HTML sanitizer)
- CSRF protection
- Content Security Policy (CSP)
- X-Frame-Options
- HTTPS enforcement
- HSTS (HTTP Strict Transport Security)

**Data Protection**
- Azure Key Vault integration
- Encrypted connection strings
- Sensitive data encryption
- Password hashing (Identity)

#### Monitoring & Alerting

**Alert Channels**
- Email alerts
- Slack webhook integration

**Alert Thresholds**
- Rate limit warnings
- DDoS detection
- Latency monitoring
- Payment failures
- Cart abandonment
- Concurrent user limits

**Security Logging**
- Failed login attempts
- Login lockout events
- Unauthorized access attempts
- IP-based suspicious activity
- Security event tracking

#### API Features

**Versioning**
- API versioning support
- Backward compatibility

**Documentation**
- Swagger/OpenAPI integration
- Detailed endpoint documentation
- Request/response examples

**Response Formats**
- JSON responses
- Consistent error responses
- Pagination support
- Dynamic filtering
- Sorting support

**Validation**
- FluentValidation
- Model validation
- Business rule validation
- Custom validators

## 🚀 Kurulum

### Gereksinimler

- **.NET 8 SDK** veya üzeri
- **Docker & Docker Compose**
- **PostgreSQL 15+** (Docker ile kurulacak)
- **Redis 7+** (Docker ile kurulacak)
- **RabbitMQ 3+** (Docker ile kurulacak)
- **Visual Studio 2022** veya **Rider** veya **VS Code**

### 1. Projeyi Klonlayın

```bash
git clone https://github.com/yourusername/Tumdex.git
cd Tumdex
```

### 2. Environment Variables

`.env` dosyasını `src` klasörü altında oluşturun ve aşağıdaki değişkenleri doldurun:

```env
# PostgreSQL
POSTGRES_USER=your_postgres_user
POSTGRES_PASSWORD=your_postgres_password
POSTGRES_HOST=localhost
POSTGRES_PORT=5433
POSTGRES_DB=TumdexDb

# Redis
REDIS_HOST=localhost
REDIS_PORT=6379
REDIS_USER=default
REDIS_PASSWORD=your_redis_password

# RabbitMQ
RABBITMQ_HOST=localhost
RABBITMQ_PORT=5672
RABBITMQ_USERNAME=your_rabbitmq_user
RABBITMQ_PASSWORD=your_rabbitmq_password
RABBITMQ_VHOST=/
RABBITMQ_MANAGEMENT_PORT=15672

# Seq
SEQ_PORT=5341
SEQ_API_KEY=your_seq_api_key
SEQ_SERVER_URL=http://localhost:5341

# JWT
JWT_SECRET=your_jwt_secret_key_min_32_characters
JWT_ISSUER=https://tumdex.com
JWT_AUDIENCE=https://www.tumdex.com

# Encryption
ENCRYPTION_KEY=your_encryption_key
ENCRYPTION_IV=your_encryption_iv

# Grafana
GRAFANA_ADMIN_USER=admin
GRAFANA_ADMIN_PASSWORD=your_grafana_password

# Storage (Optional - Choose one)
# Local Storage (Default)
LOCAL_STORAGE_PATH=/app/wwwroot

# AWS S3 (Optional)
AWS_ACCESS_KEY_ID=your_aws_key
AWS_SECRET_ACCESS_KEY=your_aws_secret
AWS_REGION=your_aws_region
AWS_BUCKET_NAME=your_bucket_name

# Google Cloud Storage (Optional)
GOOGLE_STORAGE_URL=your_gcs_url
GOOGLE_CREDENTIALS_FILE_PATH=/path/to/credentials.json
GOOGLE_VIEW_ID=your_analytics_view_id

# Cloudinary (Optional)
CLOUDINARY_CLOUD_NAME=your_cloud_name
CLOUDINARY_API_KEY=your_api_key
CLOUDINARY_API_SECRET=your_api_secret
CLOUDINARY_URL=your_cloudinary_url

# Azure Key Vault (Optional)
AZURE_KEYVAULT_NAME=your_keyvault_name
AZURE_KEYVAULT_URI=https://your-keyvault.vault.azure.net/
AZURE_TENANT_ID=your_tenant_id
AZURE_CLIENT_ID=your_client_id
AZURE_CLIENT_SECRET=your_client_secret

# Email (SMTP)
SMTP_HOST=smtp.your-domain.com
SMTP_PORT=587
SMTP_USER=your_email@domain.com
SMTP_PASSWORD=your_email_password

# Monitoring
MONITORING_EMAIL=admin@tumdex.com
MONITORING_SLACK_CHANNEL=#alerts
MONITORING_SLACK_WEBHOOK=your_slack_webhook_url
```

### 3. Docker Services

Docker Compose ile gerekli servisleri başlatın:

```bash
cd src
docker-compose up -d
```

Bu komut aşağıdaki servisleri başlatacak:
- PostgreSQL (Port: 5433)
- Redis (Port: 6379)
- RabbitMQ (Port: 5672, Management: 15672)
- Seq (Port: 5341)
- Prometheus (Port: 9090)
- Grafana (Port: 3000)

### 4. Database Migration

```bash
cd src/tumdex/WebAPI
dotnet ef database update --project ../Persistence/Persistence.csproj
```

### 5. Projeyi Çalıştırın

**Visual Studio ile:**
- Solution'ı açın
- WebAPI projesini startup project olarak ayarlayın
- F5 ile çalıştırın

**Komut satırından:**
```bash
cd src/tumdex/WebAPI
dotnet run
```

API varsayılan olarak şu adreslerde çalışacaktır:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001
- Swagger: https://localhost:5001/swagger

### 6. Health Check

Servislerinizin durumunu kontrol edin:
```
GET https://localhost:5001/health
```

## ⚙️ Yapılandırma

### appsettings.json

Uygulama yapılandırması `src/tumdex/WebAPI/appsettings.json` dosyasında bulunur.

#### Ana Konfigürasyon Bölümleri:

**ConnectionStrings**
```json
{
  "ConnectionStrings": {
    "TumdexDb": "Connection string with environment variables"
  }
}
```

**Security**
- JWT Settings
- Encryption
- Login policies
- Rate limiting
- DDoS protection
- Content Security Policy

**Email**
- Account email settings
- Contact email settings
- Order email settings
- Newsletter settings
- Monitoring email settings

**Storage**
- Active provider selection
- Multi-provider configuration
- Company assets

**Monitoring**
- Metrics configuration
- Alert settings
- Thresholds

**RabbitMQ**
- Connection settings
- Queue configurations
- Retry policies

**Serilog**
- Log levels
- Sinks (Console, Seq, PostgreSQL)
- Enrichers

**CacheSettings**
```json
{
  "CacheSettings": {
    "SlidingExpiration": 2
  }
}
```

**Newsletter**
- Send time configuration
- Throttling settings
- Template paths

**GoogleAnalytics**
- View ID
- Credentials path
- Application name

### Storage Provider Değiştirme

`appsettings.json` içinde:
```json
{
  "Storage": {
    "ActiveProvider": "localstorage" // veya "google", "cloudinary", "aws"
  }
}
```

### Rate Limiting Ayarları

```json
{
  "Security": {
    "RateLimiting": {
      "Enabled": true,
      "RequestsPerHour": 20000,
      "AuthenticatedRequestsPerHour": 40000,
      "MaxConcurrentRequests": 1000
    }
  }
}
```

## 📚 Kullanım

### API Endpoint Kategorileri

#### Authentication & Authorization
```
POST   /api/auth/login                    # Kullanıcı girişi
POST   /api/auth/register                 # Kullanıcı kaydı
POST   /api/auth/refresh-token            # Token yenileme
POST   /api/auth/logout                   # Çıkış
POST   /api/auth/forgot-password          # Şifre sıfırlama
POST   /api/auth/reset-password           # Şifre güncelleme
GET    /api/auth/confirm-email            # Email doğrulama
```

#### Products
```
GET    /api/products                      # Ürün listesi (filtreleme, pagination)
GET    /api/products/{id}                 # Ürün detayı
POST   /api/products                      # Ürün oluştur
PUT    /api/products/{id}                 # Ürün güncelle
DELETE /api/products/{id}                 # Ürün sil
GET    /api/products/best-selling         # En çok satanlar
GET    /api/products/most-liked           # En çok beğenilenler
GET    /api/products/most-viewed          # En çok görüntülenenler
POST   /api/products/{id}/like            # Ürün beğen/beğeniyi kaldır
POST   /api/products/{id}/view            # Ürün görüntüleme kaydı
```

#### Categories
```
GET    /api/categories                    # Kategori listesi
GET    /api/categories/{id}               # Kategori detayı
POST   /api/categories                    # Kategori oluştur
PUT    /api/categories/{id}               # Kategori güncelle
DELETE /api/categories/{id}               # Kategori sil
```

#### Brands
```
GET    /api/brands                        # Marka listesi
GET    /api/brands/{id}                   # Marka detayı
POST   /api/brands                        # Marka oluştur
PUT    /api/brands/{id}                   # Marka güncelle
DELETE /api/brands/{id}                   # Marka sil
```

#### Cart
```
GET    /api/carts                         # Sepet getir
POST   /api/carts/items                   # Sepete ürün ekle
PUT    /api/carts/items/{id}              # Sepet ürünü güncelle
DELETE /api/carts/items/{id}              # Sepetten ürün çıkar
DELETE /api/carts                         # Sepeti temizle
```

#### Orders
```
GET    /api/orders                        # Sipariş listesi
GET    /api/orders/{id}                   # Sipariş detayı
POST   /api/orders                        # Sipariş oluştur
PUT    /api/orders/{id}                   # Sipariş güncelle
GET    /api/orders/user                   # Kullanıcının siparişleri
```

#### Users
```
GET    /api/users                         # Kullanıcı listesi (Admin)
GET    /api/users/{id}                    # Kullanıcı detayı
PUT    /api/users/{id}                    # Kullanıcı güncelle
DELETE /api/users/{id}                    # Kullanıcı sil
GET    /api/users/profile                 # Profil bilgisi
PUT    /api/users/profile                 # Profil güncelle
```

#### User Addresses
```
GET    /api/user-addresses                # Adres listesi
GET    /api/user-addresses/{id}           # Adres detayı
POST   /api/user-addresses                # Adres ekle
PUT    /api/user-addresses/{id}           # Adres güncelle
DELETE /api/user-addresses/{id}           # Adres sil
```

#### Newsletter
```
POST   /api/newsletter/subscribe          # Abone ol
DELETE /api/newsletter/unsubscribe        # Abonelikten çık
```

#### Contact
```
POST   /api/contacts                      # İletişim formu gönder
```

#### Dashboard (Admin)
```
GET    /api/dashboard/statistics          # Dashboard istatistikleri
GET    /api/dashboard/sales               # Satış raporları
GET    /api/dashboard/visitors            # Ziyaretçi istatistikleri
```

#### SEO
```
GET    /sitemap.xml                       # Sitemap
GET    /robots.txt                        # Robots.txt
```

#### Health & Monitoring
```
GET    /health                            # Health check
GET    /api/metrics                       # Prometheus metrics
```

### Örnek API Kullanımları

#### Ürün Listesi (Filtreleme ve Pagination)

```bash
curl -X GET "https://localhost:5001/api/products?PageIndex=0&PageSize=10&CategoryId=cat-1" \
  -H "accept: application/json"
```

Response:
```json
{
  "items": [
    {
      "id": "prod-1",
      "name": "Ürün Adı",
      "price": 99.99,
      "stock": 50,
      "categoryId": "cat-1",
      "brandId": "brand-1",
      "images": [...]
    }
  ],
  "index": 0,
  "size": 10,
  "count": 100,
  "pages": 10,
  "hasPrevious": false,
  "hasNext": true
}
```

#### Sepete Ürün Ekleme

```bash
curl -X POST "https://localhost:5001/api/carts/items" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -d '{
    "productId": "prod-1",
    "quantity": 2
  }'
```

#### Sipariş Oluşturma

```bash
curl -X POST "https://localhost:5001/api/orders" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -d '{
    "userAddressId": "addr-1",
    "note": "Lütfen kapıyı çalın"
  }'
```

### SignalR Hub Kullanımı

#### JavaScript Client Örneği

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("https://localhost:5001/visitor-tracking-hub", {
        accessTokenFactory: () => yourJwtToken
    })
    .withAutomaticReconnect()
    .build();

// Ziyaretçi istatistiklerini dinle
connection.on("ReceiveVisitorStats", (stats) => {
    console.log("Current visitors:", stats.currentVisitors);
    console.log("Today's visitors:", stats.todayVisitors);
});

// Bağlantıyı başlat
connection.start()
    .then(() => console.log("SignalR Connected"))
    .catch(err => console.error(err));
```

#### .NET Client Örneği

```csharp
var connection = new HubConnectionBuilder()
    .WithUrl("https://localhost:5001/visitor-tracking-hub")
    .Build();

connection.On<VisitorStats>("ReceiveVisitorStats", (stats) =>
{
    Console.WriteLine($"Current visitors: {stats.CurrentVisitors}");
});

await connection.StartAsync();
```

## 📊 Monitoring ve Logging

### Prometheus Metrics

Prometheus metriklerine erişim:
```
http://localhost:9090
```

**Özel Metriks:**
- `http_requests_total` - Toplam HTTP istekleri
- `http_request_duration_seconds` - İstek süreleri
- `cache_hits_total` - Cache hit sayısı
- `cache_misses_total` - Cache miss sayısı
- `order_created_total` - Oluşturulan sipariş sayısı
- `product_views_total` - Ürün görüntüleme sayısı

### Grafana Dashboards

Grafana'ya erişim:
```
http://localhost:3000
Username: admin
Password: (your GRAFANA_ADMIN_PASSWORD)
```

**Pre-configured Dashboards:**
- API Performance Dashboard
- Database Metrics Dashboard
- Cache Performance Dashboard
- Business Metrics Dashboard

### Seq Logging

Seq log viewer:
```
http://localhost:5341
```

**Log Levels:**
- Verbose: Detaylı debug bilgileri
- Debug: Geliştirme aşaması bilgileri
- Information: Genel uygulama akış bilgileri
- Warning: Uyarılar
- Error: Hatalar
- Fatal: Kritik hatalar

**Query Örnekleri:**
```
# Hatalı login denemeleri
@Level = 'Error' AND @MessageTemplate LIKE '%login%'

# Yavaş sorgular
@Properties.RequestDuration > 1000

# Belirli bir kullanıcının logları
UserId = 'user-123'
```

### Health Checks

Health check endpoint'i sürekli olarak sistemin durumunu kontrol eder:

```bash
curl https://localhost:5001/health
```

Response:
```json
{
  "status": "Healthy",
  "checks": {
    "postgresql": "Healthy",
    "redis": "Healthy",
    "rabbitmq": "Healthy"
  },
  "totalDuration": "00:00:00.523"
}
```

## 🔒 Güvenlik

### Authentication Flow

1. Kullanıcı `/api/auth/login` endpoint'ine credentials gönderir
2. Sistem credentials'ı doğrular
3. Başarılı ise Access Token ve Refresh Token döner
4. Client, her istekte Authorization header'da Access Token gönderir
5. Token expire olduğunda Refresh Token ile yeni token alınır

### Token Yapısı

**Access Token:**
- Ömür: 30 dakika
- Claims: UserId, Email, Roles
- IP validation
- User-Agent validation

**Refresh Token:**
- Ömür: 14 gün
- Token family tracking
- Automatic rotation
- Maximum 5 active token per user

### Rate Limiting

**IP-based Rate Limiting:**
- Anonymous users: 40,000 request/hour
- Authenticated users: 40,000 request/hour
- Whitelisted IPs: 1000 request/minute

**Endpoint-specific limits:**
- `/api/auth/login`: 5 request/minute
- Public endpoints: Normal limits
- Admin endpoints: Increased limits

### DDoS Protection

**Protection Measures:**
- Concurrent request limiting (1000 max)
- Request per minute threshold (500)
- Automatic IP blocking
- Rate limit by IP address
- Challenge-response for suspicious traffic

### Security Headers

```http
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
X-XSS-Protection: 1; mode=block
Strict-Transport-Security: max-age=31536000; includeSubDomains
Content-Security-Policy: default-src 'self'
```

### Data Protection

- **At Rest**: Database encryption, Azure Key Vault
- **In Transit**: TLS 1.3, HTTPS enforcement
- **Sensitive Data**: Encrypted configuration values
- **PII**: GDPR compliant data handling

### Security Best Practices

1. **Never commit secrets**: Use `.env` or Azure Key Vault
2. **Rotate credentials**: Regular password and token rotation
3. **Audit logging**: All security events are logged
4. **Input validation**: All inputs are validated
5. **Output encoding**: XSS protection
6. **Parameterized queries**: SQL injection protection
7. **CORS**: Strict CORS policy
8. **Dependencies**: Regular security updates

## 🧪 Testing

### Unit Tests

```bash
cd src/tumdex
dotnet test
```

### Integration Tests

```bash
cd src/tumdex
dotnet test --filter Category=Integration
```

### API Testing (Postman/Swagger)

Swagger UI: `https://localhost:5001/swagger`

## 📈 Performance Optimization

### Caching Strategy

**Redis Distributed Cache:**
- Product listings
- Category hierarchy
- User sessions
- Frequently accessed data

**Memory Cache:**
- Configuration data
- Static resources
- Short-lived data

**Cache Invalidation:**
- Time-based expiration
- Event-based invalidation
- Manual cache clear

### Database Optimization

- **Indexes**: Strategic indexing on frequently queried columns
- **Eager Loading**: Include related entities
- **Query Optimization**: Avoid N+1 problems
- **Connection Pooling**: Efficient connection management

### Background Jobs

- **Stock Reservation Cleanup**: Every 30 seconds
- **Outbox Message Processing**: Continuous
- **Newsletter Sending**: Monthly scheduled
- **Log Cleanup**: Daily
- **Analytics Collection**: Hourly

## 🔄 CI/CD

### GitHub Actions (Örnek)

```yaml
name: .NET Build and Test

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]

jobs:
  build:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 8.0.x
        
    - name: Restore dependencies
      run: dotnet restore
      
    - name: Build
      run: dotnet build --no-restore
      
    - name: Test
      run: dotnet test --no-build --verbosity normal
```

## 📝 Database Schema

### Temel Tablolar

**Products**
- Id (PK)
- Name
- Title
- Description
- CategoryId (FK)
- BrandId (FK)
- Sku
- Price
- Stock
- Tax
- VaryantGroupID
- Timestamps

**Orders**
- Id (PK)
- OrderNumber
- UserId (FK)
- AddressId (FK)
- Status
- TotalAmount
- Note
- Timestamps

**OrderItems**
- Id (PK)
- OrderId (FK)
- ProductId (FK)
- Quantity
- UnitPrice
- TotalPrice

**Categories**
- Id (PK)
- Name
- Description
- ParentCategoryId (FK)
- DisplayOrder

**Users** (Identity)
- Id (PK)
- Email
- UserName
- EmailConfirmed
- PhoneNumber
- PhoneNumberConfirmed

### İlişkiler

```
Users 1--* Orders
Orders 1--* OrderItems
Products 1--* OrderItems
Categories 1--* Products
Brands 1--* Products
Products 1--* ProductImageFiles
Users 1--* UserAddresses
Products *--* Features (ProductFeatureValue)
```

## 🌐 Deployment

### Docker Deployment

```bash
# Build image
docker build -t tumdex-api:latest .

# Run container
docker run -d \
  -p 5000:80 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  --name tumdex-api \
  tumdex-api:latest
```

### Production Checklist

- [ ] Update production connection strings
- [ ] Configure production CORS origins
- [ ] Set up production Azure Key Vault
- [ ] Configure production email settings
- [ ] Set up production storage (AWS S3/GCP/Cloudinary)
- [ ] Configure production monitoring
- [ ] Set up SSL certificates
- [ ] Configure production logging (Seq)
- [ ] Set up automated backups
- [ ] Configure firewall rules
- [ ] Set up CDN (if needed)
- [ ] Performance testing
- [ ] Security audit
- [ ] Load testing

## 🤝 Katkıda Bulunma

Katkılarınızı memnuniyetle karşılıyoruz!

### Contribution Guidelines

1. Fork this repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

### Code Standards

- **Clean Code**: SOLID prensipleri
- **Naming**: Anlamlı ve açıklayıcı isimler
- **Comments**: Sadece gerekli yerlerde
- **Tests**: Yeni feature'lar için test yazın
- **Documentation**: README güncellemesi

## 📄 License

Bu proje özel/ticari bir projedir. Kullanım izni için lütfen iletişime geçin.

## 📞 İletişim

- **Email**: info@tumdex.com
- **Website**: https://www.tumdex.com
- **LinkedIn**: https://linkedin.com/company/tumdex
- **WhatsApp**: +90 533 803 7714

## 🙏 Teşekkürler

Bu proje aşağıdaki açık kaynak projeleri kullanmaktadır:

- ASP.NET Core
- Entity Framework Core
- MediatR
- AutoMapper
- FluentValidation
- Serilog
- Prometheus
- RabbitMQ
- Redis
- SignalR
- ve daha fazlası...

---

**© 2024 Tüm Trading Dış Ticaret Ltd. Şti. Tüm hakları saklıdır.**
