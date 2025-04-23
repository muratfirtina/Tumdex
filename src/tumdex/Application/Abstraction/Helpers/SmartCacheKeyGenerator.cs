// Application/Abstraction/Helpers/SmartCacheKeyGenerator.cs
using Application.Consts; // CacheGroups için
using Application.Services;
using Core.Application.Pipelines.Caching;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic; // HashSet için
using System.Linq; // Linq metodları için
using System.Threading.Tasks; // Task için

namespace Application.Abstraction.Helpers;

/// <summary>
/// Önbellek anahtarlarını oluşturan servis.
/// Kullanıcıya özel gruplar için otomatik olarak kullanıcı ID'sini ekler.
/// </summary>
public class SmartCacheKeyGenerator : ICacheKeyGenerator
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SmartCacheKeyGenerator> _logger;

    // CacheGroups sınıfındaki KULLANICIYA ÖZEL grupları tanımlar.
    // Bu listedeki gruplar için üretilen anahtarlara kullanıcı ID'si eklenir.
    // Paylaşılan gruplar (örn: Orders (admin), Products, Categories, Brands) buraya EKLENMEZ.
    private readonly HashSet<string> _userSpecificCacheGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        // Kullanıcıya Özel Basit Gruplar
        CacheGroups.UserCarts,
        CacheGroups.UserOrders,       // Kullanıcının kendi siparişleri (admin 'Orders' değil)
        CacheGroups.UserAddresses,
        CacheGroups.UserPhoneNumbers,
        CacheGroups.UserFavorites,    // Kullanıcının favorileri (ProductLikes)

        // Kullanıcıya Özel Kompozit Gruplar (Bunlar da kullanıcı ID'si gerektirir)
        // CacheRemovingBehavior bu kompozitleri çözse de,
        // CachingBehavior tarafında bu isimlerle cache'e ekleme yapılırsa diye burada tanımlı olması iyi olur.
        CacheGroups.UserProfile,      // UserAddresses ve UserPhoneNumbers içerir
        CacheGroups.UserActivity,     // UserCarts, UserOrders, UserFavorites içerir

        // Geriye Uyumluluk İsmiyle Kullanıcıya Özel Olanlar (Yeni kodlarda direkt User* olanlar tercih edilmeli)
        CacheGroups.Carts,            // -> UserCarts
        // CacheGroups.Orders,        // YORUMA ALINDI/SİLİNDİ - Artık paylaşılan admin grubunu temsil ediyor.
        CacheGroups.CartsAndOrders,   // İçinde UserCarts ve UserOrders olduğu için kullanıcıya özel kabul edilir.
        CacheGroups.PhoneNumbers,     // -> UserPhoneNumbers
        CacheGroups.UserAddress,      // -> UserAddresses
        CacheGroups.ProductLikes      // -> UserFavorites
    };

    public SmartCacheKeyGenerator(
        ICurrentUserService currentUserService,
        ILogger<SmartCacheKeyGenerator> logger)
    {
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Verilen baseKey ve groupKey'e göre önbellek anahtarını oluşturur.
    /// Eğer groupKey kullanıcıya özel olarak tanımlanmışsa, anahtara kullanıcı ID'si eklenir.
    /// </summary>
    /// <param name="baseKey">Anahtarın temel parçası (örn. "Product-123", "Products-Page1-Size10"). Boş olabilir.</param>
    /// <param name="groupKey">Önbellek grubu anahtarı (örn. "Products", "UserOrders"). Virgülle ayrılmış olabilir.</param>
    /// <returns>Oluşturulan önbellek anahtarı.</returns>
    public async Task<string> GenerateKeyAsync(string baseKey, string? groupKey)
    {
        string userIdPart = ""; // Kullanıcı ID'si için ek parça
        bool isUserSpecificGroupDetected = false; // Kullanıcıya özel bir grup bulundu mu?

        // Adım 1: Grubun kullanıcıya özel olup olmadığını belirle
        if (!string.IsNullOrEmpty(groupKey) && IsUserSpecificGroup(groupKey))
        {
            isUserSpecificGroupDetected = true;
            try
            {
                // Kullanıcı ID'sini almayı dene
                string userId = await _currentUserService.GetCurrentUserIdAsync();
                // Anahtarın sonuna ayırt edici ve okunabilir bir kullanıcı parçası ekle
                userIdPart = $"-User({userId})";
                _logger.LogTrace("User ID '{UserId}' obtained for user-specific cache key generation for group '{GroupKey}'.", userId, groupKey);
            }
            catch (Exception ex) // Kullanıcı girişi yoksa veya hata oluşursa
            {
                _logger.LogWarning(ex, "Failed to get authenticated user ID for cache key generation (Group: '{GroupKey}'). Using 'anonymous' marker.", groupKey);
                userIdPart = "-User(anonymous)"; // Anonim kullanıcı veya hata durumu
            }
        }
        else if (!string.IsNullOrEmpty(groupKey))
        {
             // Grup var ama kullanıcıya özel değilse logla (Debug seviyesi daha uygun olabilir)
             _logger.LogTrace("GroupKey '{GroupKey}' is not identified as user-specific.", groupKey);
        }

        // Adım 2: Final anahtarı oluştur
        string finalKey;

        if (!string.IsNullOrWhiteSpace(baseKey)) // baseKey varsa onu kullan
        {
            // baseKey varsa, groupKey sadece kullanıcıya özel olup olmadığını belirlemek içindir.
            // Kullanıcıya özelse userIdPart eklenir.
            finalKey = $"{baseKey.Trim()}{userIdPart}";
             _logger.LogDebug("Generated cache key from baseKey ('{BaseKey}'): {FinalKey} (IsUserSpecific: {IsUserSpecific})", baseKey, finalKey, isUserSpecificGroupDetected);
        }
        else if (!string.IsNullOrWhiteSpace(groupKey)) // baseKey yok ama groupKey varsa
        {
            // groupKey'i anahtarın temeli olarak kullan.
            // Virgülle ayrılmışsa, ilk geçerli parçayı temsilci olarak alalım.
            // Bu durum genellikle CacheRemovingBehavior'da grup anahtarını üretirken kullanılır (baseKey boş gelir).
            string representativeGroupKey = groupKey.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                  .Select(g => g.Trim())
                                                  .FirstOrDefault(g => !string.IsNullOrEmpty(g)) ?? "NoGroup"; // İlk geçerli grup adı veya varsayılan

            finalKey = $"{representativeGroupKey}{userIdPart}";
            _logger.LogDebug("Generated cache key from groupKey ('{GroupKey}'): {FinalKey} (IsUserSpecific: {IsUserSpecific})", groupKey, finalKey, isUserSpecificGroupDetected);
        }
        else // Ne baseKey ne de groupKey varsa (genellikle olmamalı)
        {
            // Genel, paylaşılan bir anahtar. Loglanmalı.
            finalKey = "Shared-Global-FallbackKey"; // Daha anlamlı bir varsayılan belirlenebilir.
            _logger.LogWarning("Generated cache key with no baseKey or groupKey specified: {FinalKey}", finalKey);
        }

        // Anahtarın çok uzamasını önlemek ve geçersiz karakterler içermemesini sağlamak için ek kontroller/temizlik yapılabilir.
        // Örneğin: finalKey = SanitizeCacheKey(finalKey);

        return finalKey;
    }

    /// <summary>
    /// Verilen grup anahtarının (veya virgülle ayrılmışsa içindeki herhangi bir parçanın)
    /// kullanıcıya özel olarak tanımlanıp tanımlanmadığını kontrol eder.
    /// </summary>
    /// <param name="groupKey">Kontrol edilecek grup anahtarı (virgülle ayrılmış olabilir).</param>
    /// <returns>Eğer grup veya parçalarından biri kullanıcıya özelse true, değilse false.</returns>
    private bool IsUserSpecificGroup(string groupKey)
    {
        // groupKey null veya sadece boşluk içeriyorsa, kullanıcıya özel değildir.
        if (string.IsNullOrWhiteSpace(groupKey))
            return false;

        // Virgülle ayrılmış grupları tek tek kontrol et
        var groups = groupKey.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var group in groups)
        {
            // Eğer herhangi bir parça _userSpecificCacheGroups içinde varsa, true dön.
            if (_userSpecificCacheGroups.Contains(group))
            {
                _logger.LogTrace("Group '{Group}' within '{OriginalGroupKey}' identified as user-specific.", group, groupKey);
                return true; // Kullanıcıya özel bir grup bulundu.
            }
        }

        // Hiçbir parça kullanıcıya özel değilse false dön.
        _logger.LogTrace("No user-specific groups found within '{OriginalGroupKey}'.", groupKey);
        return false; // Kullanıcıya özel grup bulunamadı.
    }

    // Opsiyonel: Önbellek anahtarlarını temizlemek için bir metod
    // private string SanitizeCacheKey(string key)
    // {
    //     // Geçersiz karakterleri değiştir veya kaldır
    //     // Uzunluğu sınırla (Redis gibi sistemlerde limit olabilir)
    //     return key.Replace(" ", "-").Replace(":", "_"); // Örnek basit temizlik
    // }
}