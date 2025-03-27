
using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;
using Domain.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Context;

public class TumdexDbContext : IdentityDbContext<AppUser,AppRole,string>
{
    public TumdexDbContext(DbContextOptions<TumdexDbContext> options) : base(options)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true); 
    }

    public DbSet<Endpoint> Endpoints { get; set; }
    public DbSet<ACMenu> ACMenus { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Brand> Brands { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Feature> Features { get; set; }
    public DbSet<FeatureValue> FeatureValues { get; set; }
    public DbSet<ProductFeatureValue> ProductFeatureValues { get; set; }
    public DbSet<ProductImageFile> ProductImageFiles { get; set; }
    public DbSet<CategoryImageFile> CategoryImageFiles { get; set; }
    public DbSet<BrandImageFile> BrandImageFiles { get; set; }
    public DbSet<CarouselImageFile> CarouselImageFiles { get; set; }
    public DbSet<ImageFile> ImageFiles { get; set; }
    public DbSet<ImageVersion> ImageVersions { get; set; }
    public DbSet<Cart> Carts { get; set; }
    public DbSet<CartItem> CartItems { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<CompletedOrder> CompletedOrders { get; set; }
    public DbSet<Carousel> Carousel { get; set; }
    public DbSet<ProductLike> ProductLikes { get; set; }
    public DbSet<ProductView> ProductViews { get; set; }
    public DbSet<UserAddress> UserAddresses { get; set; }
    public DbSet<PhoneNumber> PhoneNumbers { get; set; }
    public DbSet<SecurityLog> SecurityLogs { get; set; }
    public DbSet<AlertLog> AlertLogs { get; set; }
    public DbSet<Newsletter> Newsletters { get; set; }
    public DbSet<NewsletterLog> NewsletterLogs { get; set; }
    public DbSet<Contact> Contacts { get; set; }
    public DbSet<StockReservation> StockReservations { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Country> Countries { get; set; }
    public DbSet<City> Cities { get; set; }
    public DbSet<District> Districts { get; set; }
    
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Product>().HasQueryFilter(p=>!p.DeletedDate.HasValue);
        builder.Entity<Brand>().HasQueryFilter(b => !b.DeletedDate.HasValue);
        builder.Entity<Category>().HasQueryFilter(c => !c.DeletedDate.HasValue);
        builder.Entity<Feature>().HasQueryFilter(f => !f.DeletedDate.HasValue);
        builder.Entity<ImageFile>().HasQueryFilter(i => !i.DeletedDate.HasValue);
        builder.Entity<ImageVersion>().HasQueryFilter(iv => !iv.DeletedDate.HasValue);
        builder.Entity<FeatureValue>().HasQueryFilter(fv => !fv.DeletedDate.HasValue);
        builder.Entity<ProductFeatureValue>().HasQueryFilter(pfv => !pfv.Product.DeletedDate.HasValue);
        builder.Entity<Cart>().HasQueryFilter(c => !c.DeletedDate.HasValue);
        builder.Entity<CartItem>().HasQueryFilter(ci => !ci.DeletedDate.HasValue);
        builder.Entity<Order>().HasQueryFilter(o => !o.DeletedDate.HasValue);
        builder.Entity<OrderItem>().HasQueryFilter(oi => !oi.DeletedDate.HasValue);
        builder.Entity<CompletedOrder>().HasQueryFilter(co => !co.DeletedDate.HasValue);
        builder.Entity<Carousel>().HasQueryFilter(c => !c.DeletedDate.HasValue);
        builder.Entity<ProductView>().HasQueryFilter(pv => !pv.DeletedDate.HasValue);
        builder.Entity<UserAddress>().HasQueryFilter(ua => !ua.DeletedDate.HasValue);
        builder.Entity<PhoneNumber>().HasQueryFilter(pn => !pn.DeletedDate.HasValue);
        builder.Entity<ProductLike>()
            .HasQueryFilter(pl => !pl.Product.DeletedDate.HasValue);
        builder.Entity<SecurityLog>().HasQueryFilter(sl => !sl.DeletedDate.HasValue);
        builder.Entity<AlertLog>().HasQueryFilter(al => !al.DeletedDate.HasValue);
        builder.Entity<Newsletter>().HasQueryFilter(n => !n.DeletedDate.HasValue);
        builder.Entity<NewsletterLog>().HasQueryFilter(nl => !nl.DeletedDate.HasValue);
        builder.Entity<Contact>().HasQueryFilter(c => !c.DeletedDate.HasValue);
        builder.Entity<StockReservation>().HasQueryFilter(sr => !sr.DeletedDate.HasValue);
        builder.Entity<OutboxMessage>().HasQueryFilter(om => !om.DeletedDate.HasValue);
        builder.Entity<RefreshToken>().HasQueryFilter(rt => !rt.DeletedDate.HasValue);
        builder.Entity<Country>().HasQueryFilter(c => !c.DeletedDate.HasValue);
        builder.Entity<City>().HasQueryFilter(c => !c.DeletedDate.HasValue);
        builder.Entity<District>().HasQueryFilter(d => !d.DeletedDate.HasValue);
        
        builder.Entity<Country>(a =>
        {
            a.ToTable("Countries").HasKey(k => k.Id);
            a.Property(p => p.Id).HasColumnName("Id");
            a.Property(p => p.Code).HasColumnName("Code").HasMaxLength(10);
            a.Property(p => p.Name).HasColumnName("Name").HasMaxLength(100);
            a.Property(p => p.PhoneCode).HasColumnName("PhoneCode").HasMaxLength(10);
        
            a.HasMany(p => p.Cities)
                .WithOne(p => p.Country)
                .HasForeignKey(p => p.CountryId);
        });
    
        // City configuration
        builder.Entity<City>(a =>
        {
            a.ToTable("Cities").HasKey(k => k.Id);
            a.Property(p => p.Id).HasColumnName("Id");
            a.Property(p => p.CountryId).HasColumnName("CountryId");
            a.Property(p => p.Name).HasColumnName("Name").HasMaxLength(100);
            a.Property(p => p.Code).HasColumnName("Code").HasMaxLength(20).IsRequired(false);
        
            a.HasOne(p => p.Country)
                .WithMany(p => p.Cities)
                .HasForeignKey(p => p.CountryId);
            
            a.HasMany(p => p.Districts)
                .WithOne(p => p.City)
                .HasForeignKey(p => p.CityId);
        });
    
        // District configuration
        builder.Entity<District>(a =>
        {
            a.ToTable("Districts").HasKey(k => k.Id);
            a.Property(p => p.Id).HasColumnName("Id");
            a.Property(p => p.CityId).HasColumnName("CityId");
            a.Property(p => p.Name).HasColumnName("Name").HasMaxLength(100);
            a.Property(p => p.Code).HasColumnName("Code").HasMaxLength(20).IsRequired(false);
        
            a.HasOne(p => p.City)
                .WithMany(p => p.Districts)
                .HasForeignKey(p => p.CityId);
        });
        
        
        builder.Entity<ProductLike>()
            .HasOne(pl => pl.Product)
            .WithMany(p => p.ProductLikes)
            .HasForeignKey(pl => pl.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ProductLike>()
            .HasOne(pl => pl.User)
            .WithMany(u => u.ProductLikes)
            .HasForeignKey(pl => pl.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        
        builder.Entity<Order>()
            .HasIndex(o=>o.OrderCode)
            .IsUnique();
        builder.Entity<OrderItem>()
            .HasOne(oi => oi.Order)
            .WithMany(o => o.OrderItems)
            .HasForeignKey(oi => oi.OrderId);
        
        builder.Entity<Order>()
            .HasOne(o => o.CompletedOrder)
            .WithOne(c => c.Order)
            .HasForeignKey<CompletedOrder>(c => c.OrderId);
        
        builder.Entity<UserAddress>()
            .HasOne(ua => ua.User)
            .WithMany(u => u.UserAddresses)
            .HasForeignKey(ua => ua.UserId);
            
        builder.Entity<PhoneNumber>()
            .HasOne(pn => pn.User)
            .WithMany(u => u.PhoneNumbers)
            .HasForeignKey(pn => pn.UserId);
        
        builder.Entity<Product>()
            .HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.Entity<Product>()
            .HasOne(p => p.Brand)
            .WithMany(b => b.Products)
            .HasForeignKey(p => p.BrandId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.Entity<Category>()
            .HasKey(c => c.Id);
        
        builder.Entity<Category>()
            .HasMany(c => c.Features)
            .WithMany(f => f.Categories)
            .UsingEntity(j => j.ToTable("CategoryFeature"));
        
        builder.Entity<Feature>()
            .HasMany(f => f.Categories)
            .WithMany(c => c.Features)
            .UsingEntity(j => j.ToTable("CategoryFeature"));
        
        builder.Entity<Feature>()
            .HasMany(f => f.FeatureValues)
            .WithOne(v => v.Feature)
            .HasForeignKey(v => v.FeatureId);
        
        builder.Entity<ProductFeatureValue>()
            .HasKey(pfv => new { pfv.ProductId, pfv.FeatureValueId });
        
        builder.Entity<ProductFeatureValue>()
            .HasOne(pfv => pfv.Product)
            .WithMany(p => p.ProductFeatureValues)
            .HasForeignKey(pfv => pfv.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.Entity<ProductFeatureValue>()
            .HasOne(pfv => pfv.FeatureValue)
            .WithMany(fv => fv.ProductFeatureValues)
            .HasForeignKey(pfv => pfv.FeatureValueId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.Entity<SecurityLog>(entity =>
        { 
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Level);
        });
        
        builder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Type)
                .IsRequired()
                .HasMaxLength(200);
            
            entity.Property(e => e.Data)
                .IsRequired();

            entity.Property(e => e.Status)
                .HasConversion<int>();

            // Important indexes for performance
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ProcessedAt);
            entity.HasIndex(e => e.CreatedDate);
        });
        
        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(rt => rt.Id);
                
            entity.Property(rt => rt.TokenHash)
                .IsRequired()
                .HasMaxLength(64);
                
            // Token alanı veritabanında saklanmaz
            entity.Ignore(rt => rt.Token);
                
            entity.Property(rt => rt.JwtId)
                .IsRequired()
                .HasMaxLength(36);
                
            entity.Property(rt => rt.CreatedByIp)
                .HasMaxLength(45);
                
            entity.Property(rt => rt.UserAgent)
                .HasMaxLength(512);
                
            entity.HasIndex(rt => rt.TokenHash);
            entity.HasIndex(rt => rt.UserId);
            entity.HasIndex(rt => rt.FamilyId);
                
            entity.HasOne(rt => rt.User)
                .WithMany()
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        
        base.OnModelCreating(builder);
    }
    
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {   
        
        var datas = ChangeTracker.Entries<Entity<string>>();
        foreach (var data in datas)
        {
            _ = data.State switch
            {
                EntityState.Added => data.Entity.CreatedDate = DateTime.UtcNow,
                EntityState.Modified => data.Entity.UpdatedDate = DateTime.UtcNow,
                _ => DateTime.UtcNow
            };
        }
        
        return await base.SaveChangesAsync(cancellationToken);
    }
}