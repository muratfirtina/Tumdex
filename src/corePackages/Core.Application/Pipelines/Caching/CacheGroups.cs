namespace Core.Application.Pipelines.Caching;

public static class CacheGroups
{
    // Ana Veri Grupları (Paylaşılan veriler)
    public const string Products = "Products";
    public const string Categories = "Categories";
    public const string Brands = "Brands";
    public const string Features = "Features";
    public const string FeatureValues = "FeatureValues";
    public const string Carousels = "Carousels";
    
    // Kullanıcıya Özel Gruplar (User Specific)
    public const string UserCarts = "UserCarts";
    public const string UserOrders = "UserOrders"; // Kullanıcının KENDİ siparişleri
    public const string UserAddresses = "UserAddresses";
    public const string UserPhoneNumbers = "UserPhoneNumbers";
    public const string UserFavorites = "UserFavorites";

    // Admin / Paylaşılan Gruplar (Shared)
    public const string Orders = "Orders"; // Tüm siparişler (Admin görünümü vb.)

    // İlişkili Gruplar
    public const string ProductRelated = "Products,Categories,Brands,Features,FeatureValues";
    public const string UserProfile = "UserAddresses,UserPhoneNumbers";
    // UserActivity artık hem kullanıcı hem de admin siparişlerini içerebilir.
    public const string UserActivity = "UserCarts,UserOrders,UserFavorites,Orders"; // Belki Orders'ı da eklemek gerekir? Duruma göre.

    // Geriye Uyumluluk & Kolaylık
    public const string Carts = UserCarts;
    // public const string Orders = UserOrders; // BU SATIRI YORUMA ALIN VEYA KALDIRIN. Orders artık paylaşılan grup.
    public const string CartsAndOrders = "UserCarts,UserOrders,Orders"; // Hem kullanıcı hem admin siparişleri
    public const string PhoneNumbers = UserPhoneNumbers;
    public const string UserAddress = UserAddresses;
    public const string ProductLikes = UserFavorites;
}