using Application.Abstraction.Services;
using Application.Abstraction.Services.Authentication;
using Application.Repositories;
using Application.Services;
using Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Persistence.BackgroundJob;
using Persistence.Context;
using Persistence.DbConfiguration;
using Persistence.Repositories;
using Persistence.Services;

namespace Persistence;

public static class ServiceRegistration
{
    public static IServiceCollection AddPersistenceServices(
        this IServiceCollection services,
        string connectionString,
        bool isDevelopment)
    {
        // 1. Veritabanı Bağlamı
        services.AddDbContext<TumdexDbContext>(options =>
            DatabaseSettings.ConfigureDatabase(options, connectionString, isDevelopment));

        // 2. Identity Sistemi
        ConfigureIdentity(services);

        // 3. Background Servisler
        ConfigureBackgroundServices(services);

        // 4. Repository'ler
        RegisterRepositories(services);

        return services;
    }

    private static void ConfigureIdentity(IServiceCollection services)
    {
        services.AddIdentityCore<AppUser>(options =>
        {
            // Şifre politikası
            options.Password.RequiredLength = 3;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireDigit = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            
            // Kullanıcı politikası
            options.User.RequireUniqueEmail = true;
            
            // Hesap kilitleme politikası
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.MaxFailedAccessAttempts = 5;
        })
        .AddRoles<AppRole>()
        .AddEntityFrameworkStores<TumdexDbContext>()
        .AddDefaultTokenProviders()
        .AddTokenProvider<DataProtectorTokenProvider<AppUser>>(TokenOptions.DefaultProvider)
        .AddTokenProvider<EmailTokenProvider<AppUser>>("Email")
        .AddTokenProvider<PhoneNumberTokenProvider<AppUser>>("Phone")
        .AddSignInManager<SignInManager<AppUser>>();

        // Authentication şeması yapılandırması
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
            options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        })
        .AddIdentityCookies();
    }

    private static void ConfigureBackgroundServices(IServiceCollection services)
    {
        services.Configure<HostOptions>(options =>
        {
            options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
            options.ShutdownTimeout = TimeSpan.FromSeconds(30);
        });
        // Veritabanı Migration Servisi
        services.AddHostedService<DatabaseMigrationService>();
        
        // İş Yönetimi Servisleri
        services.AddHostedService<StockReservationCleanupService>();
        services.AddHostedService<OutboxProcessor>();
        services.AddHostedService<OutboxCleanupService>();
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IBrandRepository, BrandRepository>();
        services.AddScoped<IEndpointRepository, EndpointRepository>();
        services.AddScoped<IACMenuRepository, ACMenuRepository>();
        services.AddScoped<IImageFileRepository, ImageFileRepository>();
        services.AddScoped<IEndpointRepository, EndpointRepository>();
        services.AddScoped<IFeatureRepository, FeatureRepository>();
        services.AddScoped<IFeatureValueRepository, FeatureValueRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<ICartItemRepository, CartItemRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderItemRepository, OrderItemRepository>();
        services.AddScoped<ICompletedOrderRepository, CompletedOrderRepository>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IInternalAuthentication, AuthService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IAuthorizationEndpointService, AuthorizationEndpointService>();
        services.AddScoped<ICarouselRepository, CarouselRepository>();
        services.AddScoped<IProductLikeRepository, ProductLikeRepository>();
        services.AddScoped<IProductViewRepository, ProductViewRepository>();
        services.AddScoped<IUserAddressRepository, UserAddressRepository>();
        services.AddScoped<IPhoneNumberRepository, PhoneNumberRepository>();
        services.AddScoped<INewsletterRepository, NewsletterRepository>();
        services.AddScoped<INewsletterLogRepository, NewsletterLogRepository>();
        services.AddScoped<IImageVersionRepository, ImageVersionRepository>();
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IStockReservationService, StockReservationService>();
        services.AddScoped<IStockReservationRepository, StockReservationRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
    }
}