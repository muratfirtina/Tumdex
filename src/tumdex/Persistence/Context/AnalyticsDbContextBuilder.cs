using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Context;

public static class AnalyticsDbContextBuilder
{
    public static void ConfigureTumdexDbContextForAnalytics(this ModelBuilder builder)
    {
        // VisitorTrackingEvent entity konfigürasyonu
        builder.Entity<VisitorTrackingEvent>(entity =>
        {
            entity.ToTable("VisitorTrackingEvents");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(450);
            entity.Property(e => e.SessionId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(1024);
            entity.Property(e => e.Page).HasMaxLength(2048);
            entity.Property(e => e.Username).HasMaxLength(256);
            entity.Property(e => e.VisitTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Referrer).HasMaxLength(2048).HasDefaultValue(string.Empty);

            // ReferrerDomain alanını boş string default değeri ile yapılandır
            entity.Property(e => e.ReferrerDomain).HasMaxLength(256).HasDefaultValue(string.Empty).IsRequired();

            entity.Property(e => e.ReferrerType).HasMaxLength(50).HasDefaultValue("Doğrudan");
            entity.Property(e => e.UTMSource).HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(e => e.UTMMedium).HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(e => e.UTMCampaign).HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(e => e.Country).HasMaxLength(50).IsRequired(false); // Nullable olarak işaretle
            entity.Property(e => e.City).HasMaxLength(100).IsRequired(false); // Nullable olarak işaretle
            entity.Property(e => e.DeviceType).HasMaxLength(20).HasDefaultValue("Bilinmiyor");
            entity.Property(e => e.BrowserName).HasMaxLength(50).HasDefaultValue("Bilinmiyor");

            // Kullanıcı ilişkisi (opsiyonel)
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            // İndeksler
            entity.HasIndex(e => e.VisitTime);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.ReferrerDomain);
            entity.HasIndex(e => e.ReferrerType);
            entity.HasIndex(e => e.DeviceType);
            entity.HasIndex(e => e.IsAuthenticated);
            entity.HasIndex(e => e.IsNewVisitor);
        });
    }
}