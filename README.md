# TUMDEX - E-Commerce Platform Project

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet)
![EntityFramework](https://img.shields.io/badge/-Entity_Framework-8C3D65?logo=dotnet&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-316192?style=flat&logo=postgresql&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-DC382D?style=flat&logo=redis&logoColor=white)
![RabbitMQ](https://img.shields.io/badge/RabbitMQ-FF6600?style=flat&logo=rabbitmq&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=flat&logo=docker&logoColor=white)
![Azure](https://img.shields.io/badge/Microsoft-Azure-blue?logo=microsoftazure&logoColor=white&style=white)
![SignalR](https://img.shields.io/badge/SignalR-512BD4?style=flat&logo=signalr&logoColor=white)
![Grafana](https://img.shields.io/badge/Grafana-F2F4F9?style=for-the-badge&logo=grafana&logoColor=orange&labelColor=F2F4F9)

## 📋 Table of Contents

- [About The Project](#about-the-project)
- [Architecture](#architecture)
- [Technologies Used](#technologies-used)
- [Project Structure](#project-structure)
- [Features](#features)
- [Installation](#installation)
- [Configuration](#configuration)
- [Usage](#usage)
- [API Documentation](#api-documentation)
- [Monitoring and Logging](#monitoring-and-logging)
- [Security](#security)
- [Contributing](#contributing)

## 🎯 About The Project

**TUMDEX** is an enterprise-level e-commerce platform developed using modern microservices architecture and best practices. The project is designed according to Clean Architecture principles and has a scalable structure.

### Key Features

- ✅ Modular structure developed with **Clean Architecture** and **CQRS Pattern**
- ✅ Asynchronous process management with **Event-Driven Architecture**
- ✅ **Real-time** notifications and instant data updates (SignalR)
- ✅ Design suitable for **Microservice** infrastructure
- ✅ **Multi-cloud** storage support (AWS S3, Google Cloud, Cloudinary)
- ✅ **Comprehensive monitoring** (Prometheus & Grafana)
- ✅ **Advanced logging** (Serilog & Seq)
- ✅ **High-performance caching** (Redis)
- ✅ **Message queue** system (RabbitMQ & MassTransit)
- ✅ **JWT Authentication & Authorization**
- ✅ **GDPR compliant** data management
- ✅ **Rate Limiting & DDoS protection**
- ✅ **Health check** mechanism
- ✅ **SEO optimization** (Sitemap, Robots.txt)
- ✅ **Google Analytics** integration

## 🏗️ Architecture

The project consists of 5 layers according to **Clean Architecture (Onion Architecture)** principles:

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

### Layer Responsibilities

#### 1. **Domain Layer** (Core Layer)
- Entities and Domain Models
- Business rules and validations
- Enums and domain-specific types
- Identity models
- **Dependencies**: No dependencies on other layers

#### 2. **Application Layer**
- Use cases (CQRS: Commands & Queries)
- Business logic and orchestration
- DTOs and mapping profiles
- Repository interfaces
- Service interfaces
- Custom attributes and exceptions
- **Dependencies**: Domain, Core Packages

#### 3. **Persistence Layer**
- Entity Framework Core DbContext
- Repository implementations
- Database migrations
- Entity configurations
- Identity implementation
- **Dependencies**: Application, Domain

#### 4. **Infrastructure Layer**
- External service implementations
- Message broker (RabbitMQ) consumers
- Background jobs (Quartz.NET)
- File storage services (AWS, GCP, Cloudinary)
- Email services
- Caching (Redis)
- Monitoring (Prometheus)
- Middlewares
- **Dependencies**: Application, Persistence

#### 5. **SignalR Layer**
- Real-time hubs
- SignalR service implementations
- Client-server communication
- **Dependencies**: Application

#### 6. **WebAPI Layer** (Presentation)
- REST API Controllers
- Authentication & Authorization
- Swagger/OpenAPI
- Health checks
- CORS configuration
- **Dependencies**: Application, Infrastructure, SignalR

### Core Packages

Core packages containing reusable components of the project:

- **Core.Application**: MediatR pipelines, base requests/responses
- **Core.Persistence**: Generic repository pattern, dynamic LINQ, pagination
- **Core.CrossCuttingConcerns**: Exception handling, logging aspects

## 🛠️ Technologies Used

### Backend Framework
- **.NET 8.0** - Modern, performant and cross-platform
- **ASP.NET Core Web API** - RESTful API development

### Database & ORM
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

## 📁 Project Structure

```
Tumdex/
│
├── src/
│   ├── corePackages/                    # Reusable core components
│   │   ├── Core.Application/            # MediatR pipelines, base classes
│   │   ├── Core.Persistence/            # Generic repository, dynamic LINQ
│   │   └── Core.CrossCuttingConcerns/   # Exception handling, logging
│   │
│   ├── tumdex/                          # Main application
│   │   ├── Domain/                      # Domain layer
│   │   │   ├── Entities/                # Domain entities
│   │   │   ├── Enum/                    # Domain enums
│   │   │   ├── Identity/                # Identity models
│   │   │   └── Model/                   # Domain models
│   │   │
│   │   ├── Application/                 # Application layer
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
│   │   ├── Persistence/                 # Persistence layer
│   │   │   ├── Context/                 # DbContext
│   │   │   ├── Repositories/            # Repository implementations
│   │   │   ├── Services/                # Service implementations
│   │   │   ├── Migrations/              # EF Core migrations
│   │   │   └── DbConfiguration/         # Database configurations
│   │   │
│   │   ├── Infrastructure/              # Infrastructure layer
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
│   │   ├── SignalR/                     # SignalR layer
│   │   │   ├── Hubs/                    # SignalR hubs
│   │   │   └── HubService/              # Hub services
│   │   │
│   │   └── WebAPI/                      # Presentation layer
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

## ✨ Features

### E-Commerce Features

#### Product Management
- Product CRUD operations
- Multiple image upload and management
- Product variants (color, size, etc.)
- Stock management (unlimited stock support)
- Dynamic attribute values
- Product like and view statistics
- Product filtering and search
- Best-selling, most-liked, most-viewed products
- SEO-friendly URL structure

#### Category & Brand Management
- Hierarchical category structure
- Category attribute definition
- Brand management
- Image management

#### Cart & Order Management
- Add/remove products to cart
- Guest user cart
- Stock reservation system
- Order creation
- Order status tracking
- Order notifications (email)
- Order history

#### User Management
- JWT-based authentication
- Role-based authorization (RBAC)
- User profile management
- Address management
- User favorites
- Email verification
- Password reset
- Multi-session management
- IP and User-Agent verification

#### Contact & Newsletter
- Contact form
- Newsletter subscription
- Automatic email sending
- Email throttling
- Newsletter scheduling (monthly automatic sending)

#### GDPR & Data Privacy
- User consent management
- Data subject requests (DSR)
- Data deletion/download requests
- Privacy policy management
- Cookie consent

#### SEO & Analytics
- Dynamic sitemap generation
- Robots.txt management
- Google Analytics integration
- Visitor tracking
- Meta tag management

#### Carousel & Banner Management
- Homepage carousels
- Video/image carousel support
- Dynamic content management

### Technical Features

#### Performance & Caching
- **Redis distributed caching**
- Response caching
- Memory caching
- Cache invalidation strategies
- Sliding expiration

#### Message Queue & Event-Driven
- **RabbitMQ** for asynchronous process management
- Order created/updated events
- Cart updated events
- Stock updated events
- Email notification events
- Event-driven architecture
- Retry mechanism
- Dead letter queue

#### Real-time Features
- **SignalR** for real-time notifications
- Real-time visitor statistics
- Order status update notifications
- Stock updates
- Admin dashboard real-time metrics

#### Background Jobs
- **Quartz.NET** for scheduled tasks
- Stock reservation cleanup
- Outbox message processing
- Newsletter sending
- Log cleanup
- Analytics data collection

#### File Storage
- **Multi-provider** storage system
  - Local storage
  - AWS S3
  - Google Cloud Storage
  - Cloudinary
- Automatic image optimization
- Thumbnail generation (SkiaSharp)
- Multiple file upload
- Image versioning

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

## 🚀 Installation

### Prerequisites

- **.NET 8 SDK** or higher
- **Docker & Docker Compose**
- **PostgreSQL 15+** (will be installed via Docker)
- **Redis 7+** (will be installed via Docker)
- **RabbitMQ 3+** (will be installed via Docker)
- **Visual Studio 2022** or **Rider** or **VS Code**

### 1. Clone the Project

```bash
git clone https://github.com/yourusername/Tumdex.git
cd Tumdex
```

### 2. Environment Variables

Create a `.env` file under the `src` folder and fill in the following variables:

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

Start the required services with Docker Compose:

```bash
cd src
docker-compose up -d
```

This command will start the following services:
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

### 5. Run the Project

**With Visual Studio:**
- Open the Solution
- Set WebAPI project as startup project
- Run with F5

**From Command Line:**
```bash
cd src/tumdex/WebAPI
dotnet run
```

The API will run at the following addresses by default:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001
- Swagger: https://localhost:5001/swagger

### 6. Health Check

Check the status of your services:
```
GET https://localhost:5001/health
```

## ⚙️ Configuration

### appsettings.json

Application configuration is located in the `src/tumdex/WebAPI/appsettings.json` file.

#### Main Configuration Sections:

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

### Changing Storage Provider

In `appsettings.json`:
```json
{
  "Storage": {
    "ActiveProvider": "localstorage" // or "google", "cloudinary", "aws"
  }
}
```

### Rate Limiting Settings

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

## 📚 Usage

### API Endpoint Categories

#### Authentication & Authorization
```
POST   /api/auth/login                    # User login
POST   /api/auth/register                 # User registration
POST   /api/auth/refresh-token            # Refresh token
POST   /api/auth/logout                   # Logout
POST   /api/auth/forgot-password          # Password reset
POST   /api/auth/reset-password           # Password update
GET    /api/auth/confirm-email            # Email verification
```

#### Products
```
GET    /api/products                      # Product list (filtering, pagination)
GET    /api/products/{id}                 # Product detail
POST   /api/products                      # Create product
PUT    /api/products/{id}                 # Update product
DELETE /api/products/{id}                 # Delete product
GET    /api/products/best-selling         # Best selling products
GET    /api/products/most-liked           # Most liked products
GET    /api/products/most-viewed          # Most viewed products
POST   /api/products/{id}/like            # Like/Unlike product
POST   /api/products/{id}/view            # Record product view
```

#### Categories
```
GET    /api/categories                    # Category list
GET    /api/categories/{id}               # Category detail
POST   /api/categories                    # Create category
PUT    /api/categories/{id}               # Update category
DELETE /api/categories/{id}               # Delete category
```

#### Brands
```
GET    /api/brands                        # Brand list
GET    /api/brands/{id}                   # Brand detail
POST   /api/brands                        # Create brand
PUT    /api/brands/{id}                   # Update brand
DELETE /api/brands/{id}                   # Delete brand
```

#### Cart
```
GET    /api/carts                         # Get cart
POST   /api/carts/items                   # Add item to cart
PUT    /api/carts/items/{id}              # Update cart item
DELETE /api/carts/items/{id}              # Remove item from cart
DELETE /api/carts                         # Clear cart
```

#### Orders
```
GET    /api/orders                        # Order list
GET    /api/orders/{id}                   # Order detail
POST   /api/orders                        # Create order
PUT    /api/orders/{id}                   # Update order
GET    /api/orders/user                   # User's orders
```

#### Users
```
GET    /api/users                         # User list (Admin)
GET    /api/users/{id}                    # User detail
PUT    /api/users/{id}                    # Update user
DELETE /api/users/{id}                    # Delete user
GET    /api/users/profile                 # Profile information
PUT    /api/users/profile                 # Update profile
```

#### User Addresses
```
GET    /api/user-addresses                # Address list
GET    /api/user-addresses/{id}           # Address detail
POST   /api/user-addresses                # Add address
PUT    /api/user-addresses/{id}           # Update address
DELETE /api/user-addresses/{id}           # Delete address
```

#### Newsletter
```
POST   /api/newsletter/subscribe          # Subscribe
DELETE /api/newsletter/unsubscribe        # Unsubscribe
```

#### Contact
```
POST   /api/contacts                      # Send contact form
```

#### Dashboard (Admin)
```
GET    /api/dashboard/statistics          # Dashboard statistics
GET    /api/dashboard/sales               # Sales reports
GET    /api/dashboard/visitors            # Visitor statistics
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

### Sample API Usages

#### Product List (Filtering and Pagination)

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
      "name": "Product Name",
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

#### Add Item to Cart

```bash
curl -X POST "https://localhost:5001/api/carts/items" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -d '{
    "productId": "prod-1",
    "quantity": 2
  }'
```

#### Create Order

```bash
curl -X POST "https://localhost:5001/api/orders" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -d '{
    "userAddressId": "addr-1",
    "note": "Please ring the doorbell"
  }'
```

### SignalR Hub Usage

#### JavaScript Client Example

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("https://localhost:5001/visitor-tracking-hub", {
        accessTokenFactory: () => yourJwtToken
    })
    .withAutomaticReconnect()
    .build();

// Listen to visitor statistics
connection.on("ReceiveVisitorStats", (stats) => {
    console.log("Current visitors:", stats.currentVisitors);
    console.log("Today's visitors:", stats.todayVisitors);
});

// Start connection
connection.start()
    .then(() => console.log("SignalR Connected"))
    .catch(err => console.error(err));
```

#### .NET Client Example

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

## 📊 Monitoring and Logging

### Prometheus Metrics

Access Prometheus metrics:
```
http://localhost:9090
```

**Custom Metrics:**
- `http_requests_total` - Total HTTP requests
- `http_request_duration_seconds` - Request durations
- `cache_hits_total` - Cache hit count
- `cache_misses_total` - Cache miss count
- `order_created_total` - Created order count
- `product_views_total` - Product view count

### Grafana Dashboards

Access Grafana:
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
- Verbose: Detailed debug information
- Debug: Development phase information
- Information: General application flow information
- Warning: Warnings
- Error: Errors
- Fatal: Critical errors

**Query Examples:**
```
# Failed login attempts
@Level = 'Error' AND @MessageTemplate LIKE '%login%'

# Slow queries
@Properties.RequestDuration > 1000

# Logs for a specific user
UserId = 'user-123'
```

### Health Checks

The health check endpoint continuously monitors the system status:

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

## 🔒 Security

### Authentication Flow

1. User sends credentials to `/api/auth/login` endpoint
2. System validates credentials
3. If successful, returns Access Token and Refresh Token
4. Client sends Access Token in Authorization header with each request
5. When token expires, new token is obtained with Refresh Token

### Token Structure

**Access Token:**
- Lifetime: 30 minutes
- Claims: UserId, Email, Roles
- IP validation
- User-Agent validation

**Refresh Token:**
- Lifetime: 14 days
- Token family tracking
- Automatic rotation
- Maximum 5 active tokens per user

### Rate Limiting

**IP-based Rate Limiting:**
- Anonymous users: 40,000 requests/hour
- Authenticated users: 40,000 requests/hour
- Whitelisted IPs: 1000 requests/minute

**Endpoint-specific limits:**
- `/api/auth/login`: 5 requests/minute
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

### GitHub Actions (Example)

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

### Main Tables

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

### Relationships

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

## 🤝 Contributing

Contributions are welcome!

### Contribution Guidelines

1. Fork this repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

### Code Standards

- **Clean Code**: SOLID principles
- **Naming**: Meaningful and descriptive names
- **Comments**: Only where necessary
- **Tests**: Write tests for new features
- **Documentation**: Update README

## 📄 License

Please contact for usage permission.

## 📞 Contact

- **Email**: muratfirtina@hotmail.com
- **Website**: 
- **LinkedIn**: 
- **WhatsApp**: 

## 🙏 Acknowledgments

This project uses the following open source projects:

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
- and more...

---

**This project is written as open source by me.**
